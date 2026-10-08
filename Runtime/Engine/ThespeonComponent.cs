// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEngine;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Inference;
using Lingotion.Thespeon.Inputs;
using System;
using System.Collections.Generic;
using System.Collections;
using Unity.InferenceEngine;

namespace Lingotion.Thespeon.Engine
{

    /// <summary>
    /// The ThespeonComponent class is responsible for managing the Thespeon synthesis and acts as an API endpoint to the Thespeon user.
    /// It provides methods to synthesize audio from ThespeonInput, preload and unload characters.
    /// The engine supports various inference configurations which combine to control Thespeons performance and resource allocation.
    /// </summary>
    public class ThespeonComponent : MonoBehaviour
    {
        
        /// <summary>
        /// The internal inference session used to perform synthesis computations.
        /// </summary>
        private ThespeonInference inferenceSession;

        /// <summary>
        /// Event triggered when audio data is received. Action takes synth ID string and the current audio packet as a float array.
        /// </summary>
        public Action<string, float[]> OnAudioReceived;

        /// <summary>
        /// Event triggered when the requested audio sample indices are ready. Action takes synth ID string and the requested sample indices as a long array.
        /// This is guaranteed to fire before the first OnAudioReceived event for a given synthesis session, allowing the user to prepare for the incoming audio data.
        /// </summary>
        public Action<string, long[]> OnAudioSampleRequestReceived;

        /// <summary>
        /// Event triggered when synthesis is complete. Action takes a string corresponding to the SessionID that was completed.
        /// </summary>
        public Action<string> OnSynthesisComplete;

        /// <summary>
        /// Event triggered when TryPreloadCoroutine is complete. Action takes bool result of preload as an argument along with the characterName, moduleType and backend for which the preload was attempted.
        /// </summary>
        public Action<bool, string, ModuleType, BackendType> OnPreloadComplete;

        /// <summary>
        /// Event triggered when synthesis has failed.
        /// </summary>
        public Action<string> OnSynthesisFailed;


        private bool isRunningSynth = false;
        private Queue<SynthRequest> synthQueue = new();
        private Queue<SynthRequest> warmupQueue = new();

        private const int OutputSampleRate = 44100;

        public const string WARMUP_SESSION_ID = "WarmupSession";
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
                config.UseAdaptiveScheduling = false;
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
            sessionID ??= string.Empty;
            InferenceConfig config = configOverride == null ? new InferenceConfig() : configOverride.GenerateConfig();
            VerbosityLevel previousVerbosity = LingotionLogger.CurrentLevel;
            LingotionLogger.CurrentLevel = config.Verbosity;
            LingotionLogger.Debug($"Running Synthesis with config: PreferredBackendType={config.PreferredBackendType}, Verbosity={config.Verbosity}, BufferSeconds={config.BufferSeconds}, UseAdaptiveScheduling={config.UseAdaptiveScheduling}");
            if (config.PreferredBackendType == Unity.InferenceEngine.BackendType.GPUPixel)
            {
                LingotionLogger.Error("GPUPixel backend is not supported yet. Please use a different backend.");
                LingotionLogger.CurrentLevel = previousVerbosity;
                OnSynthesisFailed?.Invoke(sessionID);
                return;
            }
            if (isRunningSynth)
            {
                synthQueue.Enqueue(new SynthRequest(input, sessionID, configOverride));
                LingotionLogger.Info("Synthesis is already running. Request has been queued.");
                LingotionLogger.CurrentLevel = previousVerbosity;
                return;
            }
            StartCoroutine(RunSynthCoroutine(input, config, sessionID, CreatePacketHandler(config)));
        }

        /// <summary>
        /// Preload a character module for inference.
        /// </summary> 
        /// <param name="characterName">The name of the character to preload.</param>
        /// <param name="moduleType">The type of module to preload.</param>
        /// <param name="configOverride">An optional InferenceConfigOverride to customize the inference behavior.</param>
        /// <param name="runWarmup">Whether to run a warmup synthesis after preloading.</param>
        /// <returns>True if the character was successfully preloaded, false otherwise.</returns>
        public bool TryPreloadCharacter(string characterName, ModuleType moduleType, InferenceConfigOverride configOverride = null, bool runWarmup = true)
        {
            InferenceConfig config = configOverride == null ? new InferenceConfig() : configOverride.GenerateConfig();
            config.UseAdaptiveScheduling = !runWarmup;
            if (config.PreferredBackendType == Unity.InferenceEngine.BackendType.GPUPixel)
            {
                LingotionLogger.Error("GPUPixel backend is not supported yet. Please use a different backend.");
                return false;
            }
            LingotionLogger.CurrentLevel = config.Verbosity;
            bool loadSuccess = ThespeonInference.TrySetupModules(characterName, moduleType, config);
            if (loadSuccess && runWarmup)
            {
                const string gibberish = "helloz 100";
                LingotionLogger.Debug($"Running warmup for character {characterName} with module type {moduleType}");

                Dictionary<string, string> langs = ManifestHandler.Instance.GetAllSupportedLanguageCodes(characterName, moduleType);
                List<ThespeonInputSegment> mockSegments = new();
                foreach (string code in langs.Values)
                {
                    LingotionLogger.Debug($"Warmup for language: {code}");
                    mockSegments.Add(new(gibberish, code));
                }
                if (mockSegments.Count == 0) return loadSuccess;
                ThespeonInput mockInput = new(mockSegments, characterName, moduleType);
                if (isRunningSynth)
                {
                    warmupQueue.Enqueue(new SynthRequest(mockInput, WARMUP_SESSION_ID, configOverride));
                    LingotionLogger.Debug("Synth is running. Warmup has been queued.");
                }
                else
                {
                    LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
                    StartCoroutine(RunSynthCoroutine(mockInput, config, WARMUP_SESSION_ID, WarmupPacketHandler));
                }
            }
            LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
            return loadSuccess;
        }

        /// <summary>
        /// Preload a character module for inference in a coroutine.
        /// </summary>
        /// <param name="characterName">The name of the character to preload.</param>
        /// <param name="moduleType">The type of module to preload.</param>
        /// <param name="configOverride">An optional InferenceConfigOverride to specify details such as which backend to load to.</param>
        /// <param name="runWarmup">Whether to run a warmup synthesis after preloading.</param>
        /// <returns>An IEnumerator for coroutine execution.</returns>
        public IEnumerator TryPreloadCharacterCoroutine(string characterName, ModuleType moduleType, InferenceConfigOverride configOverride = null, bool runWarmup = true)
        {
            InferenceConfig config = configOverride == null ? new InferenceConfig() : configOverride.GenerateConfig();
            config.UseAdaptiveScheduling = !runWarmup;
            LingotionLogger.CurrentLevel = config.Verbosity;
            if (config.PreferredBackendType == Unity.InferenceEngine.BackendType.GPUPixel)
            {
                LingotionLogger.Error("GPUPixel backend is not supported yet. Please use a different backend.");
                OnPreloadComplete?.Invoke(false, characterName, moduleType, config.PreferredBackendType);
                yield break;
            }
            var setupModules = ThespeonInference.SetupModulesCoroutine(characterName, moduleType, config);
            while (setupModules.MoveNext()) { yield return setupModules.Current; }
            if (runWarmup)
            {
                const string gibberish = "qz 100";
                LingotionLogger.Info($"Running warmup for character {characterName} with module type {moduleType}");

                Dictionary<string, string> langs = ManifestHandler.Instance.GetAllSupportedLanguageCodes(characterName, moduleType);
                List<ThespeonInputSegment> mockSegments = new();
                foreach (string code in langs.Values)
                {
                    LingotionLogger.Debug($"Warmup for language: {code}");
                    mockSegments.Add(new(gibberish, code));
                }
                if (mockSegments.Count == 0)
                {
                    OnPreloadComplete?.Invoke(true, characterName, moduleType, config.PreferredBackendType);
                    yield break;
                }
                ThespeonInput mockInput = new(mockSegments, characterName, moduleType);
                if (isRunningSynth)
                {
                    warmupQueue.Enqueue(new SynthRequest(mockInput, WARMUP_SESSION_ID, configOverride));
                    LingotionLogger.Debug("Synth is running. Warmup has been queued.");
                }
                else
                {
                    LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
                    var warmupSynth = RunSynthCoroutine(mockInput, config, WARMUP_SESSION_ID, WarmupPacketHandler);
                    while (warmupSynth.MoveNext()) { yield return warmupSynth.Current; }
                }
            }
            OnPreloadComplete?.Invoke(true, characterName, moduleType, config.PreferredBackendType);
            LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
        }

        /// <summary>
        /// Unload a character module from inference.
        /// </summary>
        /// <param name="characterName">The name of the character to unload.</param>
        /// <param name="moduleType">The type of module to unload.</param>
        /// <param name="configOverride">An optional InferenceConfigOverride to customize unload behavior. Setting preferredBackendType unload module on only that backend. Otherwise module will be unloaded on all backends.</param>
        /// <returns>True if the character was successfully unloaded, false otherwise.</returns>
        public bool TryUnloadCharacter(string characterName, ModuleType moduleType, InferenceConfigOverride configOverride = null)
        {
            Unity.InferenceEngine.BackendType? backend = configOverride?.PreferredBackendType;
            LingotionLogger.CurrentLevel = configOverride == null ? new InferenceConfig().Verbosity : configOverride.GenerateConfig().Verbosity;
            return ThespeonInference.TryUnloadCharacter(characterName, moduleType, backend);
        }

        /// <summary>
        /// Unload all character modules from inference.
        /// </summary>
        /// <returns>True if all characters were successfully unloaded, false otherwise.</returns>
        public bool TryUnloadAll()
        {
            bool success = true;
            foreach (var (characterName, moduleType) in ThespeonCharacterHelper.GetAllCharactersAndModules())
            {
                if (!ThespeonInference.TryUnloadCharacter(characterName, moduleType))
                {
                    success = false;
                }
            }
            return success;
        }

        /// <summary>
        /// Builds the packet handler for a single synthesis, bound to the configuration that synthesis was started with.
        /// </summary>
        /// <param name="config">The InferenceConfig the synthesis runs under.</param>
        /// <returns>A packet handler owning its own buffering state.</returns>
        private Action<ThespeonDataPacket> CreatePacketHandler(InferenceConfig config)
        {
            int bufferSamples = Mathf.Max(0, Mathf.CeilToInt(config.BufferSeconds * OutputSampleRate));
            Queue<float[]> dataQueue = new();
            int currentDataLength = 0;

            return packet =>
            {
                string packetSessionID = null;
                if (!packet.Metadata.TryGetValue(CommonMetadataKeys.SessionID, out PacketMetadataValue metadataSessionID) ||
                    !metadataSessionID.TryGet(out packetSessionID) ||
                    packetSessionID == null
                )
                {
                    LingotionLogger.Error($"Packet received from unknown session, ignoring.");
                    return;
                }
                switch (packet.CallbackType)
                {
                    case SynthCallbackType.CB_AUDIO:
                        bool isFinalPacket = false;
                        // Check for "is_final" in metadata
                        if (packet.Metadata.TryGetValue("is_final", out PacketMetadataValue isFinalVal))
                        {
                            isFinalVal.TryGet(out isFinalPacket);
                        }
                        if (!packet.Payload.TryGet(out float[] audioSamples))
                        {
                            LingotionLogger.Error($"Faulty audio packet payload received from session {packetSessionID}, ignoring.");
                            return;
                        }
                        currentDataLength += audioSamples.Length;

                        dataQueue.Enqueue(audioSamples);
                        if (isFinalPacket || currentDataLength >= bufferSamples)
                        {
                            while (dataQueue.TryDequeue(out float[] currentPacket))
                            {
                                OnAudioReceived ??= DefaultFloatHandler;
                                OnAudioReceived?.Invoke(packetSessionID, currentPacket);
                            }
                            if (isFinalPacket)
                            {
                                currentDataLength = 0;
                                OnSynthesisComplete?.Invoke(packetSessionID);
                            }
                        }
                        break;

                    case SynthCallbackType.CB_TRIGGERSAMPLE:
                        if (packet.Payload.TryGet(out long[] requestedIndices))
                        {
                            LingotionLogger.Debug($"Audio Sample Request received: {string.Join(", ", requestedIndices)}");
                            OnAudioSampleRequestReceived?.Invoke(packetSessionID, requestedIndices);
                        }
                        break;

                    case SynthCallbackType.CB_ERROR:
                        if (packet.Payload.TryGet(out string errorMsgValue))
                        {
                            LingotionLogger.Error($"Error packet received from session {packetSessionID} with message:\n {errorMsgValue}");
                        }
                        else
                        {
                            LingotionLogger.Error($"Error packet received from session {packetSessionID}.");
                        }
                        dataQueue.Clear();
                        currentDataLength = 0;
                        OnSynthesisFailed?.Invoke(packetSessionID);
                        break;

                    default:
                        LingotionLogger.Warning("ThespeonComponent received unknown callback type!");
                        break;
                }
            };
        }

        private static void WarmupPacketHandler(ThespeonDataPacket packet)
        {

        }

        private void DefaultFloatHandler(string sessionID, float[] data)
        {
            if (data != null)
            {
                if (LingotionLogger.CurrentLevel >= VerbosityLevel.Debug)
                {
                    LingotionLogger.Debug($"Default packet receiver received data {string.Join(' ', data)}!");
                }
            }
            else
            {
                LingotionLogger.Error("Default packet receiver: Data received was null.");
            }
        }

        private IEnumerator RunSynthCoroutine(ThespeonInput input, InferenceConfig config, string sessionID, Action<ThespeonDataPacket> packetHandler)
        {
            isRunningSynth = true;
            inferenceSession?.Dispose();
            inferenceSession = new ThespeonInference(sessionID, packetHandler);
            LingotionLogger.CurrentLevel = config.Verbosity;
            LingotionLogger.Info("Starting synthesis coroutine...");
            if (sessionID == WARMUP_SESSION_ID)
                yield return null;
            yield return StartCoroutine(inferenceSession.Infer(input, config));
            LingotionLogger.Info("Synthesis coroutine completed.");
            LingotionLogger.CurrentLevel = new InferenceConfig().Verbosity;
            isRunningSynth = false;
        }

        private void OnDestroy()
        {
            LingotionLogger.Info("ThespeonComponent is being destroyed. Cleaning up resources...");
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



