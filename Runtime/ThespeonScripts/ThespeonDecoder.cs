// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lingotion.Thespeon.API;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Profiling;

namespace Lingotion.Thespeon.ThespeonRunscripts
{
    public class ThespeonDecoder: ThespeonInferenceStep<Tensor<float>[], DecoderInput>
    {
        Tensor<float>[] outputs = new Tensor<float>[2];

        public ThespeonDecoder(Worker[] workers, double targetFrameTime, bool useAdaptiveScheduling)
        {

            _workers = workers;
            UseAdaptiveScheduling = useAdaptiveScheduling;
            TargetFrameTime = targetFrameTime;
            HeavyLayers.Add(new List<int>());
        }

        // Inputs: {Tensor<float> encoderMel, Tensor<float> encoderMelMask}
        public Tensor<float>[] DecoderPreprocess(Tensor<float>[] inputs)
        {
            _workers[0].Schedule(inputs[0], inputs[1]);
            Tensor<float>[] outputs = new Tensor<float>[5];
            for (int i = 0; i < 4; i++)
            {
                outputs[i] = _workers[0].PeekOutput(i) as Tensor<float>;
            }

            outputs[4] = inputs[2];
            return outputs;
        }

        // Inputs: {Tensor<int> currentChunkIndex, Tensor<int> chunkLength, Tensor<float> z, Tensor<float> enc, Tensor<float> mask, Tensor<int> boundaryCloneAlpha, Tensor<int> actors,  Tensor<float> dec}

        public void AddCustomSkipIndices(List<int> customSkipIndices)
        {
            foreach (int layer in customSkipIndices)
            {
                if(!HeavyLayers[0].Contains(layer))
                    HeavyLayers[0].Add(layer);
            }
        }

        public override IEnumerator Infer(DecoderInput decoderInput)
        {

            // List<double> speed = Enumerable.Repeat(0.5, phonemeIDs.Count).ToList();                                                 //Unused
            // List<double> expressionamplitude = Enumerable.Repeat(0.5, phonemeIDs.Count).ToList();                                   //Unused
    
            double decoderstarTime = Time.realtimeSinceStartup;
            IEnumerator decoderSchedule = _workers[1].ScheduleIterable(decoderInput.InputTensors);
            bool hasLayersLeft = true;
            int counter = 0;
            float startTime = 0f;
            float currentElapsedTime = 0f;
            while(hasLayersLeft)
            {
                // move one frame and wait for the end of it.
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    startTime = Time.realtimeSinceStartup;
                    // start a new "block" of deferred calls:
                    while(true)
                    {
                        // schedule a call
                        hasLayersLeft = decoderSchedule.MoveNext();
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

            outputs[0] = _workers[1].PeekOutput(0) as Tensor<float>;

            Tensor next_overlap = null;
            _workers[1].CopyOutput(2, ref next_overlap);
            outputs[1] = next_overlap as Tensor<float>;

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
            
            decoderInput.TaskCompletion.SetResult(outputs);

            yield return null;
            yield return new WaitForEndOfFrame();
        }

        protected override void DestroyInstance()
        {
            // next_overlap?.Dispose();
            foreach (var output in outputs)
            {
                output?.Dispose();
            }
        }
    }

    public class DecoderInput : InferenceInputs<Tensor<float>[]>
    {
        public DecoderInput(Tensor[] inputs, TaskCompletionSource<Tensor<float>[]> tcs)
            : base(inputs, tcs)
        {
        }

    }
}