// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.


using UnityEngine;
using Unity.InferenceEngine;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.Profiling;
using System.Collections.Generic;

namespace Lingotion.Thespeon.ThespeonRunscripts
{
    public class ThespeonEncoder: ThespeonInferenceStep<Tensor[], EncoderInput>
    {

        public ThespeonEncoder(Worker worker, double targetFrameTime, bool useAdaptiveScheduling, float overshootMargin) : base(overshootMargin)
        {
            _workers = new[]{worker};
            // Encoder is finicky - do not let it schedule too much
            TargetFrameTime = targetFrameTime * 0.7;
            HeavyLayers.Add(new List<int>());
            UseAdaptiveScheduling = true;

        }

        public void AddCustomSkipIndices(List<int> customSkipIndices)
        {
            foreach (int layer in customSkipIndices)
            {      
                if(!HeavyLayers[0].Contains(layer))
                    HeavyLayers[0].Add(layer);
            }
        }

        protected override void DestroyInstance()
        {
            return;
        }

        public override IEnumerator Infer(EncoderInput encoderInputs)
        {
            // List<double> speed = Enumerable.Repeat(0.5, phonemeIDs.Count).ToList();                                                 //Unused
            // List<double> expressionamplitude = Enumerable.Repeat(0.5, phonemeIDs.Count).ToList();    
            double encoderStartTime = Time.realtimeSinceStartup;
            Profiler.BeginSample("Force to GPU");
            // foreach (Tensor tensor in encoderInputs.InputTensors)
            // {
                // ComputeTensorData data = ComputeTensorData.Pin(tensor);
            // }
            Profiler.EndSample();
            IEnumerator encoderSchedule = _workers[0].ScheduleIterable(encoderInputs.InputTensors);
            bool hasLayersLeft = true;
            int counter = 0;
            float startTime = 0f;
            float currentElapsedTime = 0f;
            Profiler.BeginSample("Encoder");
            while(hasLayersLeft)
            {
                Profiler.EndSample();
                // move one frame and wait for the end of it.
                    yield return null;
                    yield return new WaitForEndOfFrame();                    
                    startTime = Time.realtimeSinceStartup;
                    
                    Profiler.BeginSample("Encoder");
                    // start a new "block" of deferred calls:
                    while(true)
                    {
                        // schedule a call
                        hasLayersLeft = encoderSchedule.MoveNext();
                        counter++;
                        // have we exceeded the budget?
                        currentElapsedTime = Time.realtimeSinceStartup - startTime;
                        if(!hasLayersLeft || currentElapsedTime > TargetFrameTime || HeavyLayers[0].Contains(counter))//|| counter == 271 || counter == 270)
                        {
                            if (UseAdaptiveScheduling && currentElapsedTime > TargetFrameTime * OvershootMargin)
                            {
                                // If layer still is too heavy, add another before it
                                if(HeavyLayers[0].Contains(counter - 1))
                                {
                                    AddHeavyLayer(0, counter - 2);
                                } else 
                                {
                                    AddHeavyLayer(0, counter - 1);
                                }
                                
                            } 
                                    
                            break;
                        }
                    }
            }

            Tensor[] encoderOutput = new Tensor[6] {
                _workers[0].PeekOutput(0),
                _workers[0].PeekOutput(1),
                _workers[0].PeekOutput(2),
                _workers[0].PeekOutput(3),
                _workers[0].PeekOutput(4),
                _workers[0].PeekOutput(5),
            };
            
            // Check if the output copying needed to wait on jobs to be completed
            float completeJobElapsedTime = Time.realtimeSinceStartup - startTime + currentElapsedTime;
            if (UseAdaptiveScheduling && completeJobElapsedTime > TargetFrameTime * OvershootMargin)
            {
                // If layer still is too heavy, add another before it
                if(HeavyLayers[0].Contains(counter - 1))
                {
                    AddHeavyLayer(0, counter - 2);
                } else 
                {
                    AddHeavyLayer(0, counter - 1);
                }
                
            } 
            
            encoderInputs.TaskCompletion.SetResult(encoderOutput);
            Profiler.EndSample();
        }

    }

    public class EncoderInput : InferenceInputs<Tensor[]>
    {
        public EncoderInput(Tensor[] inputs, TaskCompletionSource<Tensor[]> tcs)
            : base(inputs, tcs)
        {
        }

    }
    

}