// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using Lingotion.Thespeon.API;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using UnityEngine.Profiling;
using Lingotion.Thespeon.Utils;

namespace Lingotion.Thespeon.ThespeonRunscripts
{
    /// <summary>
    /// ThespeonEngine is the main game component for interfacing with the Thespeon API from your scene. It is responsible for loading and managing actors and modules, and for running inference jobs.
    /// </summary>
    public class ThespeonEngine : MonoBehaviour
    {
        public float targetFrameTimeMs{ get; set; } = 0.005f;
        public Action<float[]> defaultCallback;
        
        private Queue<LingotionDataPacket<float>> outputPackets = new Queue<LingotionDataPacket<float>>();
        public int jitterPacketSize = 1024;
        public int jitterDataLimit = 2;
        public float jitterSecondsWaitTime = 0.5f;
        bool start = false;
        private int currentDataLength = 0;
        List<int>[] customSkipIndices;
        // Assumes first packet has been returned
        private Queue<LingotionSynthRequest> synthQueue = new Queue<LingotionSynthRequest>();
        private bool isRunningSynth = false;


        void Start()
        {
            List<(string, ActorTags)> availableActors=ThespeonAPI.GetActorsAvailabeOnDisk();

            foreach((string, ActorTags) actor in availableActors)
            {
                if(!ThespeonAPI.GetRegisteredActorPacks().ContainsKey(actor.Item1))
                {
                    ThespeonAPI.RegisterActorPacks(actor.Item1);
                }
                ThespeonAPI.PreloadActorPackModule(actor.Item1, actor.Item2);           //preloads it all now. Should change for TUNI-28 to instead let the user select which modules to load.
            }

        }

        void Update()
        {
            if(!isRunningSynth && synthQueue.Count > 0)
            {
                LingotionSynthRequest nextRequest = synthQueue.Dequeue();
                StartCoroutine(RunSynthCoroutine(nextRequest));
            }
        }

        /// <summary>
        /// Creates a new UserModelInput object with the specified actor name and text segments.
        /// </summary>
        /// <param name="actorName">The name of the actor to use for inference.</param>
        /// <param name="textSegments">A list of UserSegment objects representing the text to synthesize.</param>
        /// <returns>A new UserModelInput object.</returns>
        public UserModelInput CreateModelInput(string actorName, List<UserSegment> textSegments)
        {
            return new UserModelInput(actorName, textSegments);
        }

        /// <summary>
        /// Sets the custom skip indices for the decoder.
        /// </summary>
        /// <param name="customSkipIndices">A list of lists of integers representing the custom skip indices for each layer of the decoder.</param>
        /// <remarks>
        /// The custom skip indices are used to manually set model layers where the engine should allow the coroutine to break for the next frame. 
        /// </remarks>
        public void SetCustomSkipIndices(List<int>[] customSkipIndices)
        {
            this.customSkipIndices = customSkipIndices;
        }

        /// <summary>
        /// Starts a Thespeon inference job with the specified input. An Action callback can be otionally provided to receive audio data as it is synthesized. A PackageConfig object can also be optionally provided to override the global configuration.
        /// </summary>
        /// <param name="input">The UserModelInput object containing the actor name and text segments.</param>
        /// <param name="audioStreamCallback">An Action callback to receive audio data as it is synthesized.</param>
        /// <param name="config">A PackageConfig object to override the global configuration.</param>
        /// <returns>A LingotionSynthRequest object representing the synthesis request.</returns>
        public LingotionSynthRequest Synthesize(UserModelInput input, Action<float[]> audioStreamCallback = null, PackageConfig config = null)
        {
            if(audioStreamCallback != null)
                defaultCallback = audioStreamCallback;
            LingotionSynthRequest synthRequest = ThespeonAPI.Synthesize(input, defaultCallback, config);
            if(synthRequest == null)
            {
                return null;
            }
            if(isRunningSynth){
                synthQueue.Enqueue(synthRequest);
            } else 
            {
                StartCoroutine(RunSynthCoroutine(synthRequest));
            }
            return synthRequest;
        }
        /// <summary>
        /// Enqueues a finished synthesized audio chunk.
        /// </summary>
        /// <param name="dataPacket"></param>
        private void QueueSynthAudio(LingotionDataPacket<float> dataPacket, string synthID, Action<float[]> userCallback)
        {
            Profiler.BeginSample("QueueSynth");
            if(dataPacket.Type == "Error")       //make data type enum in TUNI-123
            {
                Debug.LogError("Error in synthesis with ID: " + synthID);
                isRunningSynth = false;
            }
            if(dataPacket.Type != "Audio") Debug.LogError("Wrong packet type for audio queue");
            outputPackets.Enqueue(dataPacket);
            
            currentDataLength += dataPacket.Data.Length;

            if(currentDataLength >= jitterSecondsWaitTime * 44100)
            {
                LingotionDataPacket<float> currentPacket = null;
                bool receivedLast = false;

                while(outputPackets.TryDequeue(out currentPacket))
                {

                    userCallback?.Invoke(currentPacket.Data);
                    receivedLast = (bool) currentPacket.Metadata["finalDataPackage"];
                }
                if(receivedLast)
                {
                    currentDataLength = 0;
                }
            }


            Profiler.EndSample();
        }


        private IEnumerator RunSynthCoroutine(LingotionSynthRequest request)
        {
            isRunningSynth = true;
            yield return StartCoroutine(ThespeonInferenceHandler.RunModelCoroutine(request, QueueSynthAudio, customSkipIndices));      
            isRunningSynth = false;
        }

    }
}