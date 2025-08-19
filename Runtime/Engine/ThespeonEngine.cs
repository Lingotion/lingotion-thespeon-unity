// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Inference;
using Lingotion.Thespeon.Inputs;
using System;
using System.Collections.Generic;
using System.Collections;

namespace Lingotion.Thespeon.Engine
{

    /// <summary>
    /// The ThespeonEngine class is responsible for managing the Thespeon synthesis and acts as an API endpoint to the Thespeon user.
    /// It provides methods to synthesize audio from ThespeonInput, preload and unload actors.
    /// The engine supports various inference configurations which combine to control Thespeons performance and resource allocation.
    /// </summary>
    public class ThespeonEngine : MonoBehaviour
    {
        
        /// <summary>
        /// The internal inference session used to perform synthesis computations.
        /// </summary>
        private ThespeonInference inferenceSession;

        /// <summary>
        /// Event triggered when audio data is received. Action takes the current audio packet as a float array and its associated metadata as arguments.
        /// </summary>
        public Action<float[], PacketMetadata> OnAudioReceived;

        /// <summary>
        /// Event triggered when synthesis is complete. Action takes the final packet Metadata as an argument. This means OnAudioReceived has been called with the final packet.
        /// </summary>
        public Action<PacketMetadata> OnSynthesisComplete;

        /// <summary>
        /// Event triggered when TryPreloadCoroutine is complete. Action takes bool result of preload as an argument.
        /// </summary>
        public Action<bool> OnPreloadComplete;


        private Queue<float[]> dataQueue = new();
        private int currentDataLength = 0;
        private float bufferSeconds = 0.1f;
        private bool isRunningSynth = false;
        private Queue<SynthRequest> synthQueue = new();
        private Queue<SynthRequest> warmupQueue = new();
        void Update()
        {
            if (!isRunningSynth && synthQueue.Count > 0)
            {
                LingotionLogger.Info("Processing queued synthesis request...");
                SynthRequest nextRequest = synthQueue.Dequeue();
                Synthesize(nextRequest.input, nextRequest.sessionID, nextRequest.configOverride);
            }
            if (!isRunningSynth && warmupQueue.Count > 0)
            {
                LingotionLogger.Debug("Processing queued Warmup...");
                SynthRequest nextRequest = warmupQueue.Dequeue();
                InferenceConfig config = nextRequest.configOverride == null ? new InferenceConfig() : nextRequest.configOverride.GenerateConfig();
                inferenceSession?.Dispose();
                inferenceSession = new ThespeonInference();
                StartCoroutine(RunSynthCoroutine(nextRequest.input, config , nextRequest.sessionID, WarmupPacketHandler));

            }
        }

        /// <summary>
        /// Synthesize audio from the provided ThespeonInput using the specified inference configuration. If a synthesis is already running, the request will be queued and executed when the current synthesis is complete.
        /// </summary>
        /// <param name="input">The ThespeonInput containing the text to synthesize.</param>
        /// <param name="sessionID">An optional session ID for tracking the synthesis session.</param>
        /// <param name="configOverride">An optional InferenceConfigOverride where each provided property overrides the existing default. </param>
        public void Synthesize(ThespeonInput input, string sessionID = "", InferenceConfigOverride configOverride = null)
        {
            InferenceConfig config = configOverride == null ? new InferenceConfig() : configOverride.GenerateConfig();
            if (config.PreferredBackendType == Unity.InferenceEngine.BackendType.GPUPixel)
            {
                LingotionLogger.Error("GPUPixel backend is not supported yet. Please use a different backend.");
                return;
            }
            if (isRunningSynth)
            {
                synthQueue.Enqueue(new SynthRequest(input, sessionID, configOverride));
                LingotionLogger.Info("Synthesis is already running. Request has been queued.");
                return;
            }
            bufferSeconds = config.BufferSeconds;
            inferenceSession?.Dispose();
            inferenceSession = new ThespeonInference();
            StartCoroutine(RunSynthCoroutine(input, config, sessionID));
        }

        /// <summary>
        /// Preload an actor module for inference.
        /// </summary> 
        /// <param name="actorName">The name of the actor to preload.</param>
        /// <param name="moduleType">The type of module to preload.</param>
        /// <param name="configOverride">An optional InferenceConfigOverride to customize the inference behavior.</param>
        /// <param name="runWarmup">Whether to run a warmup synthesis after preloading.</param>
        /// <returns>True if the actor was successfully preloaded, false otherwise.</returns>
        public bool TryPreloadActor(string actorName, ModuleType moduleType, InferenceConfigOverride configOverride = null, bool runWarmup = true)
        {
            InferenceConfig config = configOverride == null ? new InferenceConfig() : configOverride.GenerateConfig();
            config.UseAdaptiveScheduling = !runWarmup;
            if (config.PreferredBackendType == Unity.InferenceEngine.BackendType.GPUPixel)
            {
                LingotionLogger.Error("GPUPixel backend is not supported yet. Please use a different backend.");
                return false;
            }
            LingotionLogger.CurrentLevel = config.Verbosity;
            bool loadSuccess = ThespeonInference.TrySetupModules(actorName, moduleType, config);
            if (loadSuccess && runWarmup)
            {
                const string gibberish = "qz 100";
                LingotionLogger.Debug($"Running warmup for actor {actorName} with module type {moduleType}");

                Dictionary<string, string> langs = PackManifestHandler.Instance.GetAllSupportedLanguageCodes(actorName, moduleType);
                List<ThespeonInputSegment> mockSegments = new();
                foreach (string code in langs.Values)
                {
                    LingotionLogger.Debug($"Warmup for language: {code}");
                    mockSegments.Add(new(gibberish, code));
                }
                if (mockSegments.Count == 0) return loadSuccess;
                ThespeonInput mockInput = new(mockSegments, actorName, moduleType);
                if (isRunningSynth)
                {
                    warmupQueue.Enqueue(new SynthRequest(mockInput, "WarmupSession", configOverride));
                    LingotionLogger.Debug("Synth is running. Warmup has been queued.");
                }
                else
                {
                    inferenceSession?.Dispose();
                    inferenceSession = new ThespeonInference();
                    LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
                    StartCoroutine(RunSynthCoroutine(mockInput, config, "WarmupSession", WarmupPacketHandler));
                }
            }
            LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
            return loadSuccess;
        }

        /// <summary>
        /// Preload an actor module for inference in a coroutine.
        /// </summary>
        /// <param name="actorName">The name of the actor to preload.</param>
        /// <param name="moduleType">The type of module to preload.</param>
        /// <param name="configOverride">An optional InferenceConfigOverride to customize preload behavior.</param>
        /// <param name="runWarmup">Whether to run a warmup synthesis after preloading.</param>
        /// <returns>An IEnumerator for coroutine execution.</returns>
        public IEnumerator TryPreloadActorCoroutine(string actorName, ModuleType moduleType, InferenceConfigOverride configOverride = null, bool runWarmup = true)
        {
            InferenceConfig config = configOverride == null ? new InferenceConfig() : configOverride.GenerateConfig();
            config.UseAdaptiveScheduling = !runWarmup;
            LingotionLogger.CurrentLevel = config.Verbosity;
            if (config.PreferredBackendType == Unity.InferenceEngine.BackendType.GPUPixel)
            {
                LingotionLogger.Error("GPUPixel backend is not supported yet. Please use a different backend.");
                OnPreloadComplete?.Invoke(false);
                yield break;
            }
            yield return ThespeonInference.SetupModulesCoroutine(actorName, moduleType, config);
            if (runWarmup)
            {
                const string gibberish = "qz 100";
                LingotionLogger.Info($"Running warmup for actor {actorName} with module type {moduleType}");

                Dictionary<string, string> langs = PackManifestHandler.Instance.GetAllSupportedLanguageCodes(actorName, moduleType);
                List<ThespeonInputSegment> mockSegments = new();
                foreach (string code in langs.Values)
                {
                    LingotionLogger.Debug($"Warmup for language: {code}");
                    mockSegments.Add(new(gibberish, code));
                }
                if (mockSegments.Count == 0)
                {
                    OnPreloadComplete?.Invoke(true);
                    yield break;
                }
                ThespeonInput mockInput = new(mockSegments, actorName, moduleType);
                if (isRunningSynth)
                {
                    warmupQueue.Enqueue(new SynthRequest(mockInput, "WarmupSession", configOverride));
                    LingotionLogger.Debug("Synth is running. Warmup has been queued.");
                }
                else
                {
                    inferenceSession?.Dispose();
                    inferenceSession = new ThespeonInference();
                    yield return RunSynthCoroutine(mockInput, config, "WarmupSession", WarmupPacketHandler);
                }
            }
            OnPreloadComplete?.Invoke(true);
            LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
        }

        /// <summary>
        /// Unload an actor module from inference.
        /// </summary>
        /// <param name="actorName">The name of the actor to unload.</param>
        /// <param name="moduleType">The type of module to unload.</param>
        /// <returns>True if the actor was successfully unloaded, false otherwise.</returns>
        public bool TryUnloadActor(string actorName, ModuleType moduleType)
        {
            return ThespeonInference.TryUnloadModule(actorName, moduleType);
        }

        /// <summary>
        /// Unload all actor modules from inference.
        /// </summary>
        /// <returns>True if all actors were successfully unloaded, false otherwise.</returns>
        public bool TryUnloadAll()
        {
            bool success = true;
            foreach (var (characterName, moduleType) in ThespeonCharacterHelper.GetAllCharactersAndModules())
            {
                if (!ThespeonInference.TryUnloadModule(characterName, moduleType))
                {
                    success = false;
                }
            }
            return success;
        }

        private void PacketHandler<T>(ThespeonDataPacket<T> packet) where T : unmanaged
        {
            if (typeof(T) != typeof(float))
            {
                LingotionLogger.Error($"Packet data type {typeof(T)} is not supported. Expected float.");
                return;
            }
            float[] data = packet.data as float[];
            if (data == null)
            {
                LingotionLogger.Error("Packet data could not be cast to float[].");
                return;
            }
            bool isFinalPacket = packet.isFinalPacket;
            PacketMetadata metadata = packet.metadata;
            currentDataLength += data.Length;

            dataQueue.Enqueue(data);
            if (isFinalPacket || currentDataLength >= bufferSeconds * 44100)
            {
                while (dataQueue.TryDequeue(out float[] currentPacket))
                {
                    OnAudioReceived ??= DefaultFloatHandler;
                    OnAudioReceived?.Invoke(currentPacket, metadata);
                }
                if (isFinalPacket)
                {
                    currentDataLength = 0;
                    OnSynthesisComplete?.Invoke(metadata);
                }
            }
        }

        private static void WarmupPacketHandler(ThespeonDataPacket<float> packet)
        {
            if (packet.isFinalPacket)
            {
                LingotionLogger.Info("Warmup synthesis complete.");
            }
        }

        private void DefaultFloatHandler(float[] data, PacketMetadata metadata)
        {
            if (data != null)
            {
                LingotionLogger.Debug($"Default packet receiver received data {string.Join(' ', data)}!");
            }
            else
            {
                LingotionLogger.Error("Default packet receiver: Data received was null.");
            }
        }

        private IEnumerator RunSynthCoroutine(ThespeonInput input, InferenceConfig config, string sessionID, Action<ThespeonDataPacket<float>> packetHandler = null)
        {
            packetHandler ??= PacketHandler;
            isRunningSynth = true;
            LingotionLogger.CurrentLevel = config.Verbosity;
            LingotionLogger.Info("Starting synthesis coroutine...");
            if (sessionID == "WarmupSession")
                yield return null;
            yield return StartCoroutine(inferenceSession.Infer<float>(input, config, packetHandler, sessionID));
            LingotionLogger.Info("Synthesis coroutine completed.");
            LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
            isRunningSynth = false;
        }

        private void OnDestroy()
        {
            LingotionLogger.Info("ThespeonEngine is being destroyed. Cleaning up resources...");
            inferenceSession?.Dispose();
            inferenceSession = null;
        }


        private struct SynthRequest
        {
            public ThespeonInput input;
            public InferenceConfigOverride configOverride;
            public string sessionID;

            public SynthRequest(ThespeonInput input, string sessionID, InferenceConfigOverride configOverride = null)
            {
                this.input = input;
                this.sessionID = sessionID;
                this.configOverride = configOverride;
            }
        }
    }
}



