// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.


using System;
using System.Collections;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Unity.InferenceEngine;
using UnityEngine;

namespace Lingotion.Thespeon.Inference
{
    /// <summary>
    /// Represents a workload for performing inference using a model runtime binding.
    /// This class manages the inputs and outputs of the model and handles the inference process.
    /// </summary>
    /// <remarks>
    /// It implements IDisposable to ensure proper resource management.
    /// </remarks>
    public class InferenceWorkload : IDisposable
    {
        private Worker _worker;
        public List<Model.Input> Inputs;
        public List<Model.Output> Outputs;
        private List<int> heavyLayers = new();
        private string DebugName = "Workload";

        /// <summary>
        /// Initializes a new instance of the <see cref="InferenceWorkload"/> class with the specified model runtime binding.
        /// </summary>
        /// <param name="runtimeBinding">The model runtime binding containing the worker and model information.</param>
        public InferenceWorkload(ModelRuntimeBinding runtimeBinding)
        {
            _worker = runtimeBinding.worker;
            Inputs = runtimeBinding.model.inputs;
            Outputs = runtimeBinding.model.outputs;
        }

        /// <summary>
        /// Disposes of the worker and releases any resources it holds.
        /// </summary>
        public void Dispose()
        {
            _worker.Dispose();
        }

        /// <summary>
        /// Runs this workload autoregressively until meeting a completion condition.
        /// </summary>
        /// <param name="tensorPool">The session tensor pool containing the tensors for inference.</param>
        /// <param name="config">The inference configuration containing settings for the inference process.</param>
        /// <param name="doneCondition">A function that returns true when the autoregressive process is done.</param>
        /// <param name="workloadmd5">The MD5 hash of the workload, used for cleanup on fail.</param>
        /// <param name="skipFrames">Whether to let the coroutine yield during inference or run in a single frame.</param>  
        /// <param name="debugName">Optional debug name for the workload, used for profiling.</param>
        /// <param name="budgetAdjustment">Adjustment factor for the budget, used for adaptive scheduling and yielding logic.</param>
        public IEnumerator InferAutoregressive(SessionTensorPool tensorPool, InferenceConfig config, Func<bool> doneCondition, string workloadmd5, bool skipFrames = true, string debugName = null, float budgetAdjustment = 1f)
        {
            IEnumerator schedule;
            bool autoregNotDone = true;
            int autoregCount = 0;
            double budgetConsumed = 0;
            double startTime = Time.realtimeSinceStartupAsDouble;
            while (autoregNotDone)
            {
                int frame = 1;
                UnityEngine.Profiling.Profiler.BeginSample($"Thespeon {debugName} {++autoregCount} autoregressive {frame}");

                schedule = Infer(tensorPool, config, (consumedSoFar) => budgetConsumed = consumedSoFar, skipFrames, debugName, true, budgetConsumed, budgetAdjustment);
                bool scheduleNotDone = true;

                while (scheduleNotDone)
                {
                    try
                    {
                        startTime = Time.realtimeSinceStartupAsDouble;
                        scheduleNotDone = schedule.MoveNext();
                    }
                    catch (Exception e)
                    {
                        LingotionLogger.Error($"Error during inference: {e.Message}");
                        tensorPool.Dispose();
                        UnityEngine.Profiling.Profiler.EndSample();
                        yield break;
                    }
                    if (skipFrames && scheduleNotDone)
                    {
                        if (schedule.Current is null)
                        {
                            UnityEngine.Profiling.Profiler.EndSample();
                            yield return null;
                            yield return new WaitForEndOfFrame();
                            budgetConsumed = 0;
                            UnityEngine.Profiling.Profiler.BeginSample($"Thespeon {debugName} {autoregCount} autoregressive {++frame}");
                        }
                    }
                    if (tensorPool.IsDisposed())
                    {
                        if (workloadmd5 != null)
                        {
                            LingotionLogger.Error($"Tensor pool was disposed in model {workloadmd5}, aborting.");
                            InferenceWorkloadManager.Instance.ReleaseWorkload(workloadmd5);
                        }
                        else
                        {
                            LingotionLogger.Error($"Tensor pool was disposed, aborting.");
                        }
                        yield break;
                    }
                }
                autoregNotDone = doneCondition();
                UnityEngine.Profiling.Profiler.EndSample();
            }
            yield return null;
            yield return new WaitForEndOfFrame();
        }

        /// <summary>
        /// Runs this workload.
        /// </summary>
        /// <param name="tensorPool">The session tensor pool containing the tensors for inference.</param>
        /// <param name="config">The inference configuration containing settings for the inference process.</param>
        /// <param name="skipFrames">Whether to let the coroutine yield during inference or run in a single frame.</param>
        /// <param name="debugName">Optional debug name for the workload, used for profiling.</param> 
        /// <param name="fromAutoregessive">Indicates if this inference is part of an autoregressive process.</param>
        /// <param name="budgetAdjustment">Adjustment factor for the budget, used for adaptive scheduling and yielding logic.</param>
        /// <param name="OnFinished">Callback to invoke when the inference process is finished, providing the total time taken.</param>
        /// <exception cref="Exception">Thrown if an error occurs during the inference process.</exception>
        public IEnumerator Infer(SessionTensorPool tensorPool, InferenceConfig config, Action<double> OnFinished, bool skipFrames = true, string debugName = null, bool fromAutoregessive = false, double budgetConsumed = 0d, float budgetAdjustment = 1f)
        {
            if (debugName != null)
                DebugName = debugName;

            if (!fromAutoregessive) UnityEngine.Profiling.Profiler.BeginSample($"Thespeon {DebugName} inference 1");
            try
            {
                foreach (var input in Inputs)
                {
                    _worker.SetInput(input.name, tensorPool.GetTensor(input.name));
                }
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error during input tensor processing in {DebugName}: {e.Message}");
                tensorPool.Dispose();
                if (!fromAutoregessive) UnityEngine.Profiling.Profiler.EndSample();
                yield break;
            }
            IEnumerator schedule = _worker.ScheduleIterable();

            int layerCounter = 0;
            double startTime = 0d;
            double currentElapsedTime = 0d;
            bool hasLayersLeft = true;
            int frameCount = 1;
            bool breakFrame = false;
            double inferSpecificBudget = config.TargetBudgetTime * budgetAdjustment;
            while (hasLayersLeft)
            {
                if (skipFrames && breakFrame)
                {
                    breakFrame = false;
                    if (!fromAutoregessive) UnityEngine.Profiling.Profiler.EndSample();
                    yield return null;
                    if (!fromAutoregessive) yield return new WaitForEndOfFrame();
                    budgetConsumed = 0;
                    if (!fromAutoregessive) UnityEngine.Profiling.Profiler.BeginSample($"Thespeon {DebugName} inference {++frameCount}");
                }
                startTime = Time.realtimeSinceStartupAsDouble;
                try
                {
                    while (true)
                    {
                        hasLayersLeft = schedule.MoveNext();
                        layerCounter++;
                        currentElapsedTime = Time.realtimeSinceStartupAsDouble - startTime;
                        #if UNITY_EDITOR
                            // [DevComment] frame time calculation is unreliable in editor, so ignore it.
                            double timeSinceFrameStart = 0d;
                        #else
                            double timeSinceFrameStart = Time.realtimeSinceStartupAsDouble - Time.unscaledTimeAsDouble;
                        #endif
                        double timeLeftOfFrame = config.TargetFrameTime - timeSinceFrameStart - config.TargetFrameTime / 10d;
                        double timeLeftOfBudget = inferSpecificBudget - budgetConsumed - currentElapsedTime;
                        if (!hasLayersLeft || timeLeftOfBudget < 0 || timeLeftOfFrame < 0 || (config.UseAdaptiveScheduling && heavyLayers.Contains(layerCounter)))
                        {
                            if (config.UseAdaptiveScheduling && budgetConsumed + currentElapsedTime > inferSpecificBudget * config.OvershootMargin)
                            {
                                if (heavyLayers.Contains(layerCounter - 1))
                                {
                                    AddHeavyLayer(layerCounter - 2, config.MaxSkipLayers);
                                }
                                else
                                {
                                    AddHeavyLayer(layerCounter - 1, config.MaxSkipLayers);
                                }
                            }
                            // /*[DevComment]*/if (hasLayersLeft) UnityEngine.Profiling.Profiler.BeginSample($"autoregressive break \n Elapsed {currentElapsedTime}, Consumed {budgetConsumed + currentElapsedTime}\nBudget {inferSpecificBudget}, left {timeLeftOfBudget} \nFrame {timeSinceFrameStart}, left {timeLeftOfFrame} || {heavyLayers.Contains(layerCounter)}");
                            // /*[DevComment]*/if (hasLayersLeft) UnityEngine.Profiling.Profiler.EndSample();
                            breakFrame = true;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!fromAutoregessive) UnityEngine.Profiling.Profiler.EndSample();
                    LingotionLogger.Error($"Error during layer processing: {e.Message}");
                    tensorPool.Dispose();
                    throw e;
                }
            }
            try
            {
                foreach (var output in Outputs)
                {
                    Tensor currentOutput = null;
                    _worker.CopyOutput(output.name, ref currentOutput);
                    double completeJobElapsedTime = Time.realtimeSinceStartupAsDouble - startTime + budgetConsumed;
                    if (config.UseAdaptiveScheduling && completeJobElapsedTime > inferSpecificBudget * config.OvershootMargin)
                    {
                        if (heavyLayers.Contains(layerCounter - 1))
                        {
                            AddHeavyLayer(layerCounter - 2, config.MaxSkipLayers);
                        }
                        else
                        {
                            AddHeavyLayer(layerCounter - 1, config.MaxSkipLayers);
                        }
                    }
                    tensorPool.SetTensor(output.name, currentOutput);
                }
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error during output tensor processing: {e.Message}");
                tensorPool.Dispose();
                if (!fromAutoregessive) UnityEngine.Profiling.Profiler.EndSample();
                yield break;
            }
            if (!fromAutoregessive) UnityEngine.Profiling.Profiler.EndSample();
            OnFinished?.Invoke((float)Math.Max(0, Time.realtimeSinceStartupAsDouble - startTime + budgetConsumed));
        }
        
        private void AddHeavyLayer(int layerIndex, int maxSkipLayers)
        {       
            if (heavyLayers.Count >= maxSkipLayers)
            {
                int rand = Mathf.RoundToInt(UnityEngine.Random.Range(0, maxSkipLayers));
                heavyLayers[rand] = layerIndex;
            }
            else
            {
                heavyLayers.Add(layerIndex);
            }
        }


    }

}

