// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Profiling;

namespace Lingotion.Thespeon.ThespeonRunscripts
{
    public class ThespeonVocoder: ThespeonInferenceStep<float[], VocoderInput>
    {
        private Tensor<float>[] chunk_rests;
        private Tensor[] copy_outs;
        private Tensor<float> inference_result = null;

        public ThespeonVocoder(Worker[] workers, double targetFrameTime, bool useAdaptiveScheduling, int chunk_rests_size, float overshootMargin) : base(overshootMargin)
        {
            _workers = workers;
            UseAdaptiveScheduling = useAdaptiveScheduling;

            chunk_rests = new Tensor<float>[chunk_rests_size];
            copy_outs = new Tensor[chunk_rests_size];
            TargetFrameTime = targetFrameTime;


            // Initialize lists for each chunk type
            foreach (var chunkType in Enum.GetNames(typeof(ChunkType)))
            {
                HeavyLayers.Add(new List<int>());
            }

        }

        public void AddCustomSkipIndices(List<int>[] customSkipIndices) 
        {
            if(customSkipIndices != null && customSkipIndices.Length == 3) 
            {
                foreach (int layer in customSkipIndices[0])
                {
                    
                    if(!HeavyLayers[0].Contains(layer))
                        HeavyLayers[0].Add(layer);
                }

                foreach (int layer in customSkipIndices[1])
                {
                    if(!HeavyLayers[1].Contains(layer))
                        HeavyLayers[1].Add(layer);
                }

                foreach (int layer in customSkipIndices[2])
                {
                    if(!HeavyLayers[2].Contains(layer))
                        HeavyLayers[2].Add(layer);
                }
            }
        }

        protected override void DestroyInstance()
        {
            foreach (Tensor tensor in chunk_rests)
            {
                tensor?.Dispose();
            }
            inference_result?.Dispose();
        }

        public override IEnumerator Infer(VocoderInput vocoderInput)
        {
        



            bool hasLayersLeft=true;

            IEnumerator vocoderSchedule;
            float startTime = Time.realtimeSinceStartup;
            float currentElapsedTime = 0f;
            
            int counter = 0;
            switch(vocoderInput.chunkType)
            {
                case ChunkType.First:
                    Profiler.BeginSample("vocoder first");
                    //_workers[1].Schedule(chunk_rests);
                    vocoderSchedule = _workers[0].ScheduleIterable(vocoderInput.InputTensors);
                    while(hasLayersLeft)
                    {
                           
                       // move one frame and wait for the end of it.
                           Profiler.EndSample();
                           yield return null;
                           yield return new WaitForEndOfFrame();
                           Profiler.BeginSample("vocoder first");
                           startTime = Time.realtimeSinceStartup;
                           // start a new "block" of deferred calls:
                           while(true)
                           {
                               // schedule a call
                               hasLayersLeft = vocoderSchedule.MoveNext();
                               counter++;
                               // have we exceeded the budget?
                               currentElapsedTime = Time.realtimeSinceStartup - startTime;
                               if(!hasLayersLeft || currentElapsedTime > TargetFrameTime || HeavyLayers[(int) vocoderInput.chunkType].Contains(counter))//|| counter == 271 || counter == 270)
                               {
                                   if (UseAdaptiveScheduling && currentElapsedTime > TargetFrameTime * OvershootMargin)
                                   {
                                       // If layer still is too heavy, add another before it
                                       if(HeavyLayers[(int) vocoderInput.chunkType].Contains(counter - 1))
                                       {
                                           AddHeavyLayer((int) vocoderInput.chunkType, counter - 2);
                                       } else 
                                       {
                                           AddHeavyLayer((int) vocoderInput.chunkType, counter - 1);
                                       }

                                   } 

                                   break;
                               }
                           }
                    }
                    
                    startTime = Time.realtimeSinceStartup;
                    for (int i = 0; i < chunk_rests.Length; i++)
                    {
                        copy_outs[i]?.Dispose();
                        _workers[0].CopyOutput(i, ref copy_outs[i]);
                        chunk_rests[i] = copy_outs[i] as Tensor<float>;
                    }
                    inference_result = chunk_rests[0];
                                        
                    break;

                case ChunkType.Middle:
                    
                    Profiler.BeginSample("vocoder mid");
                    chunk_rests[0] = vocoderInput.InputTensors[0] as Tensor<float>;
                    //_workers[1].Schedule(chunk_rests);
                    vocoderSchedule = _workers[1].ScheduleIterable(chunk_rests);
                    while(hasLayersLeft)
                    {
                           
                       // move one frame and wait for the end of it.
                            Profiler.EndSample();
                            yield return null;
                            yield return new WaitForEndOfFrame();
                            Profiler.BeginSample("vocoder mid");
                            startTime = Time.realtimeSinceStartup;
                            // start a new "block" of deferred calls:
                            while(true)
                            {
                                // schedule a call
                                hasLayersLeft = vocoderSchedule.MoveNext();
                                counter++;
                                // have we exceeded the budget?
                                currentElapsedTime = Time.realtimeSinceStartup - startTime;
                                if(!hasLayersLeft || currentElapsedTime > TargetFrameTime || HeavyLayers[(int) vocoderInput.chunkType].Contains(counter))//|| counter == 271 || counter == 270)
                                {
                                    if (UseAdaptiveScheduling && currentElapsedTime > TargetFrameTime * OvershootMargin)
                                    {
                                        // If layer still is too heavy, add another before it
                                        if(HeavyLayers[(int) vocoderInput.chunkType].Contains(counter - 1))
                                        {
                                            AddHeavyLayer((int) vocoderInput.chunkType, counter - 2);
                                        } else 
                                        {
                                            AddHeavyLayer((int) vocoderInput.chunkType, counter - 1);
                                        }

                                    } 

                                    break;
                                }
                            }
                    }
                    
                    startTime = Time.realtimeSinceStartup;
                    for (int i = 0; i < chunk_rests.Length; i++)
                    {
                        copy_outs[i]?.Dispose();
                        _workers[1].CopyOutput(i, ref copy_outs[i]);
                        chunk_rests[i] = copy_outs[i] as Tensor<float>;
                    }
                    inference_result = chunk_rests[0];
                                        
                    break;

                case ChunkType.Last:
                    Profiler.BeginSample("vocoder last");
                    chunk_rests[0] = vocoderInput.InputTensors[0] as Tensor<float>;
                    //_workers[1].Schedule(chunk_rests);
                    vocoderSchedule = _workers[2].ScheduleIterable(chunk_rests);
                    while(hasLayersLeft)
                    {
                           
                       // move one frame and wait for the end of it.
                           Profiler.EndSample();
                           yield return null;
                           yield return new WaitForEndOfFrame();
                           Profiler.BeginSample("vocoder last");
                           startTime = Time.realtimeSinceStartup;
                           // start a new "block" of deferred calls:
                           while(true)
                           {
                               // schedule a call
                               hasLayersLeft = vocoderSchedule.MoveNext();
                               counter++;
                               // have we exceeded the budget?
                               currentElapsedTime = Time.realtimeSinceStartup - startTime;
                               if(!hasLayersLeft || currentElapsedTime > TargetFrameTime || HeavyLayers[(int) vocoderInput.chunkType].Contains(counter))//|| counter == 271 || counter == 270)
                               {
                                   if (UseAdaptiveScheduling && currentElapsedTime > TargetFrameTime * OvershootMargin)
                                   {
                                       // If layer still is too heavy, add another before it
                                       if(HeavyLayers[(int) vocoderInput.chunkType].Contains(counter - 1))
                                       {
                                           AddHeavyLayer((int) vocoderInput.chunkType, counter - 2);
                                       } else 
                                       {
                                           AddHeavyLayer((int) vocoderInput.chunkType, counter - 1);
                                       }

                                   } 

                                   break;
                               }
                           }
                    }
                    
                    startTime = Time.realtimeSinceStartup;
                    
                    chunk_rests[0] = _workers[2].PeekOutput(0) as Tensor<float>;
                    for (int i = 1; i < chunk_rests.Length; i++)
                    {
                        copy_outs[i]?.Dispose();
                    }
                    inference_result = chunk_rests[0];
                                        
                    break;

                default:
                    throw new Exception("Invalid chunk type!");
            }

            yield return null;
            yield return new WaitForEndOfFrame();
            Profiler.BeginSample("Vocoder downlaod");

            float[] array = inference_result.DownloadToArray();
            inference_result.Dispose();
            Profiler.EndSample();
            vocoderInput.Dispose();
            // Check if the output copying needed to wait on jobs to be completed
            float completeJobElapsedTime = Time.realtimeSinceStartup - startTime + currentElapsedTime;
            if (UseAdaptiveScheduling && completeJobElapsedTime > TargetFrameTime * OvershootMargin)
            {
                // If layer still is too heavy, add another before it
                if(HeavyLayers[(int) vocoderInput.chunkType].Contains(counter - 1))
                {
                    AddHeavyLayer((int) vocoderInput.chunkType, counter - 2);
                } else 
                {
                    AddHeavyLayer((int) vocoderInput.chunkType, counter - 1);
                }
            } 

            
            vocoderInput.TaskCompletion.SetResult(array);
            yield break;
        }
    }


    public class VocoderInput : InferenceInputs<float[]>
    {
        public ChunkType chunkType { get; }
        public VocoderInput(Tensor[] inputs, TaskCompletionSource<float[]> tcs, ChunkType chunkType)
            : base(inputs, tcs)
        {
            this.chunkType = chunkType;
        }

    }
    
    public enum ChunkType{First, Middle, Last};
}