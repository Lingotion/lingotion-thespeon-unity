// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Sentis;
using UnityEngine;

namespace Lingotion.Thespeon.ThespeonRunscripts
{
    public abstract class InferenceStep
    {
        protected Worker[] _workers;
        protected virtual float OvershootMargin { get; set; } = 1.4f;
        protected virtual int MaxSkipLayers { get; set; } = 4;
        protected bool UseAdaptiveScheduling { get; set; } = false;
        public double TargetFrameTime { get; set; }
        // Vocoder needs more than one list
        protected readonly List<List<int>> HeavyLayers = new();

        protected void AddHeavyLayer(int listIndex, int layerIndex)
        {
            if (HeavyLayers.Count >= MaxSkipLayers)
            {
                int rand = Mathf.RoundToInt(UnityEngine.Random.Range(0, MaxSkipLayers));
                HeavyLayers[listIndex][rand] = layerIndex;
            }
            else
            {
                HeavyLayers[listIndex].Add(layerIndex);
            }
        }

        public void Destroy()
        {
            foreach (var worker in _workers)
            {   
                worker?.Dispose();
            }
            DestroyInstance();
        }
        public int GetFirstWorkerHash()
        {
            if(_workers.Length == 0)
            {
                Debug.LogWarning("No workers to hash");
                return 0;
            }
            return _workers[0].GetHashCode();
        }

        protected abstract void DestroyInstance();
    }
    public abstract class ThespeonInferenceStep<T, TInput>: InferenceStep where TInput : InferenceInputs<T> 
    {
    
        public abstract IEnumerator Infer(TInput inputs);
    }


    public abstract class InferenceInputs<T>
    {
        public Tensor[] InputTensors { get; }
        public TaskCompletionSource<T> TaskCompletion { get; }

        protected InferenceInputs(Tensor[] inputTensors, TaskCompletionSource<T> tcs)
        {
            InputTensors = inputTensors;
            TaskCompletion = tcs;
        }

        public void Dispose()
        {
            foreach (var tensor in InputTensors)
            {
                tensor?.Dispose();
            }
        }

    }

}