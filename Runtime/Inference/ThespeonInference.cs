// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using Lingotion.Thespeon.Core;
using System.Collections;
using Lingotion.Thespeon.ActorPack;
using Lingotion.Thespeon.LanguagePack;
using System;
using Unity.InferenceEngine;
using Lingotion.Thespeon.Inputs;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using UnityEngine.Profiling;
using System.IO;
using Newtonsoft.Json;

namespace Lingotion.Thespeon.Inference
{
    /// <summary>
    ///  The ThespeonInference class handles the inference process for ThespeonInput, managing the setup of modules, input processing, and synthesized data distribution.
    /// </summary>
    public class ThespeonInference : InferenceSession<ThespeonInput, ThespeonInputSegment>
    {
        /// <summary>
        /// Sets up the modules required for inference based on the actor name and module type.
        /// </summary>
        /// <param name="actorName">The name of the actor to set up modules for.</param>
        /// <param name="moduleType">The type of module to set up.</param>
        /// <param name="config">The inference configuration to use.</param>
        public static bool TrySetupModules(string actorName, ModuleType moduleType, InferenceConfig config)
        {
            try
            {
                SetupModules(actorName, moduleType, config);
                return true;
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Failed to preload actor {actorName} ({moduleType}): {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unloads the specified module for the given actor.
        /// </summary>
        /// <param name="actorName">The name of the actor whose module should be unloaded.</param>
        /// <param name="moduleType">The type of module to unload.</param>
        public static bool TryUnloadModule(string actorName, ModuleType moduleType)
        {
            if (string.IsNullOrEmpty(actorName) || moduleType == ModuleType.None)
            {
                LingotionLogger.Warning("Actor name or module type is invalid. Cannot unload module.");
                return false;
            }
            try
            {
                ModuleEntry targetModuleInfo = PackManifestHandler.Instance.GetActorPackModuleEntry(actorName, moduleType);
                if (targetModuleInfo.IsEmpty())
                {
                    LingotionLogger.Warning($"Could not find module for actor {actorName} of type {moduleType}. Cannot unload.");
                    return false;
                }

                ActorModule actorModule = ModuleHandler.Instance.DeregisterModule<ActorModule>(targetModuleInfo);
                if (actorModule == default)
                {
                    LingotionLogger.Warning($"Actor module for {actorName} of type {moduleType} is not registered or already deregistered.");
                    return false;
                }
                if (!InferenceWorkloadManager.Instance.TryDeregisterModuleWorkloads(actorModule))
                {
                    LingotionLogger.Warning($"Failed to deregister module {actorName} of type {moduleType} as it is still in use.");

                    ModuleHandler.Instance.RegisterModule<ActorModule>(targetModuleInfo);
                    return false;
                }
                HashSet<string> langModsSafeToRemove = ModuleHandler.Instance.GetNonOverlappingLangModules(actorModule);
                foreach (string id in langModsSafeToRemove)
                {
                    targetModuleInfo = PackManifestHandler.Instance.GetLanguagePackModuleEntry(id);
                    if (targetModuleInfo.IsEmpty())
                    {
                        continue;
                    }
                    LanguageModule langModule = ModuleHandler.Instance.DeregisterModule<LanguageModule>(targetModuleInfo);
                    if (langModule == default)
                    {
                        LingotionLogger.Info($"Language module {id} is not registered or already deregistered.");
                        continue;
                    }
                    if (!InferenceWorkloadManager.Instance.TryDeregisterModuleWorkloads(langModule))
                    {
                        LingotionLogger.Warning($"Failed to deregister language module {id} as it is still in use.");
                        ModuleHandler.Instance.RegisterModule<LanguageModule>(targetModuleInfo);
                        continue;
                    }
                    LookupTableHandler.Instance.DeregisterTable(langModule);
                    LingotionLogger.Info($"Successfully unloaded language module {langModule.moduleLanguage.Iso639_2}.");
                }
                LingotionLogger.Info($"Successfully unloaded character module {actorName}-{moduleType}.");
                return true;
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Failed to unload module {actorName} of type {moduleType}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Performs inference on the given ThespeonInput, processing the input and invoking the callback with the result.
        /// </summary>
        /// <typeparam name="T">The type of data to return in the callback.</typeparam>
        /// <param name="input">The ThespeonInput to process.</param>
        /// <param name="config">The InferenceConfig to use for inference.</param>
        /// <param name="callback">The callback to invoke with the result of the inference.</param>
        /// <param name="sessionID">The session ID for the inference.</param>
        /// <param name="asyncDownload">Whether to download tensors asynchronously.</param>
        /// <returns>An IEnumerator for coroutine execution.</returns>
        public override IEnumerator Infer<T>(ThespeonInput input, InferenceConfig config, Action<ThespeonDataPacket<T>> callback, string sessionID, bool asyncDownload = true)
        {

            LingotionLogger.CurrentLevel = config.Verbosity;
            double timeSinceFrameStart = Time.realtimeSinceStartupAsDouble - Time.unscaledTimeAsDouble;
            double timeLeftOfFrame = config.TargetFrameTime - timeSinceFrameStart - config.TargetFrameTime / 10d;
            yield return new WaitForEndOfFrame();
            if (timeLeftOfFrame < 0)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
            }
            Profiler.BeginSample("Thespeon Inference preparation");
            ActorModule actorModule;
            Dictionary<string, LanguageModule> languageModules;
            ThespeonInput processedInput;
            Dictionary<string, List<string>> unknownWordsByLanguage;
            List<List<float>> markerPositionsBySegment;
            int nbrWordsNotInLookup;
            try
            {
                Profiler.BeginSample("Thespeon Setup modules");
                (actorModule, languageModules) = SetupModules(input.ActorName, input.ModuleType, config);
                Profiler.EndSample();
                input.DefaultLanguage ??= config.FallbackLanguage;
                input.DefaultEmotion = input.DefaultEmotion == Emotion.None ? config.FallbackEmotion : input.DefaultEmotion;
                Profiler.BeginSample("Thespeon Text preprocessing");
                processedInput = TextPreprocessor.PreprocessInput(input);
                Profiler.EndSample();
                Profiler.BeginSample("Thespeon Find unkown words");
                (unknownWordsByLanguage, markerPositionsBySegment) = FindUnknownWordsAndMarkerPositions(processedInput, actorModule, languageModules);
                nbrWordsNotInLookup = unknownWordsByLanguage.Values.Sum(x => x.Count);
                Profiler.EndSample();
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error during inference preparation: {e.Message}");
                tensorPool.Dispose();
                Profiler.EndSample();
                callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                yield break;
            }
            Profiler.EndSample();
            if (nbrWordsNotInLookup > 0)
            {
                // Run phonemizer
                foreach ((string languageAsJson, List<string> uniqueWords) in unknownWordsByLanguage)
                {
                    ModuleLanguage language = JsonConvert.DeserializeObject<ModuleLanguage>(languageAsJson);
                    Profiler.BeginSample("Thespeon Phonemizer preparation " + language.Iso639_2);
                    LanguageModule currentLanguageModule = languageModules[actorModule.languageModuleIDs[languageAsJson]];
                    int maxInLength = 0;
                    List<List<int>> phonemizerInputs = uniqueWords.Select(word => currentLanguageModule.EncodeGraphemes(word)).ToList();

                    phonemizerInputs.ForEach(wordIDs =>
                    {
                        currentLanguageModule.InsertStringBoundaries(wordIDs);
                        maxInLength = Math.Max(maxInLength, wordIDs.Count);
                    });

                    int batchSize = BuildPhonemizerTensors(maxInLength, phonemizerInputs, currentLanguageModule.EncodePhonemes("<sos>")[0]);
                    string phonemizerMD5 = currentLanguageModule.GetInternalModelID("phonemizer");
                    InferenceWorkload phonemizerWorkLoad = null;

                    Profiler.EndSample();
                    if (!InferenceWorkloadManager.Instance.AcquireWorkload(phonemizerMD5, ref phonemizerWorkLoad))
                    {
                        yield return new WaitUntil(() => InferenceWorkloadManager.Instance.AcquireWorkload(phonemizerMD5, ref phonemizerWorkLoad));
                        yield return new WaitForEndOfFrame();
                    }

                    bool PhonemizerDoneCondition(int currentIteration)
                    {

                        Tensor<int> srcTensor = tensorPool.GetTensor("src") as Tensor<int>;
                        TensorShape graphemesShape = srcTensor.shape;

                        int phonemizedLimit = graphemesShape[1] * 5;

                        if (currentIteration >= phonemizedLimit || currentIteration >= 200)
                        {
                            LingotionLogger.Warning($"Phonemizer reached max number of iterations {currentIteration}, forcing completion.");

                            Tensor<int> finished_indicesTensor = tensorPool.GetTensor("finished_indices") as Tensor<int>;
                            int[] finished_indices = finished_indicesTensor.DownloadToArray();
                            List<int> new_finished_indices = new();

                            foreach (int index in finished_indices)
                            {
                                if (index <= 0)
                                {
                                    new_finished_indices.Add(currentIteration);
                                }
                                else
                                {
                                    new_finished_indices.Add(index);
                                }

                            }

                            tensorPool.SetTensor("finished_indices", new Tensor<int>(finished_indicesTensor.shape, new_finished_indices.ToArray()));
                            return true;
                        }

                        Tensor<int> num_finishedTensor = tensorPool.GetTensor("num_finished") as Tensor<int>;
                        int[] numFinished = num_finishedTensor.DownloadToArray();

                        return numFinished.Last() >= batchSize;
                    }

                    yield return null;
                    yield return new WaitForEndOfFrame();
                    yield return phonemizerWorkLoad.InferAutoregressive(tensorPool, config, PhonemizerDoneCondition, phonemizerMD5, debugName: "phonemizer", budgetAdjustment: 1f);

                    InferenceWorkloadManager.Instance.ReleaseWorkload(phonemizerMD5);
                    if (CheckInferenceAbort(phonemizerMD5))
                    {
                        callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status: DataPacketStatus.FAILED));
                        yield break;
                    }
                    try
                    {
                        Profiler.BeginSample("Thespeon Phonemizer resolution");
                        ResolvePhonemizerResult(currentLanguageModule, uniqueWords);
                        Profiler.EndSample();
                    }
                    catch (Exception e)
                    {
                        LingotionLogger.Error($"Error resolving phonemizer result: {e.Message} {e.StackTrace}");
                        tensorPool.Dispose();
                        Profiler.EndSample();
                        callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status: DataPacketStatus.FAILED));
                        yield break;
                    }
                }
            }
            Profiler.BeginSample("Thespeon Encoder preparation");
            List<int> markerTensorPositions;
            try
            {
                List<string> originalTexts = processedInput.Segments.Select(segment => segment.Text).ToList();
                (Dictionary<string, Dictionary<string, int>> lengthChangesByLanguage, List<int> globalMarkerPositions) = PhonemizeInput(ref processedInput, actorModule, languageModules, markerPositionsBySegment);

                markerTensorPositions = globalMarkerPositions.Select(val => (val + 1) * 2).ToList();
                LingotionLogger.Debug($"Phonemized input: {processedInput.ToJson()}");
                LingotionLogger.Debug($"Marker tensor positions: {string.Join(", ", markerTensorPositions)}");
                SetEncoderTensors(actorModule, processedInput, lengthChangesByLanguage, originalTexts);
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error preparing tensors for encoder inference: {e.Message}");
                tensorPool.Dispose();
                Profiler.EndSample();
                callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                yield break;
            }
            Profiler.EndSample();
            yield return null;
            yield return new WaitForEndOfFrame();
            string targetModel = actorModule.GetInternalModelID("encoder");
            LingotionLogger.Debug($"Starting inference for {targetModel} with input: {processedInput.ToJson()}");
            InferenceWorkload encoderStep = null;
            if (!InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref encoderStep))
            {
                yield return new WaitUntil(() => InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref encoderStep));
                yield return new WaitForEndOfFrame();
            }
            double budgetConsumed = 0d;
            yield return encoderStep.Infer(tensorPool, config, (consumedSoFar) => budgetConsumed = consumedSoFar, debugName: "Encoder", budgetConsumed: budgetConsumed, budgetAdjustment: 0.7f);
            if (CheckInferenceAbort(targetModel))
            {
                callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                yield break;
            } 
            Tensor<float> alignmentTensor = tensorPool.GetTensor("alignment") as Tensor<float>;
            alignmentTensor.ReadbackRequest();

            InferenceWorkloadManager.Instance.ReleaseWorkload(targetModel);
            targetModel = actorModule.GetInternalModelID("decoder_preprocess");
            InferenceWorkload decoderPreprocessStep = null;
            if (!InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref decoderPreprocessStep))
            {
                yield return new WaitUntil(() => InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref decoderPreprocessStep));
                yield return new WaitForEndOfFrame();
            }
            yield return decoderPreprocessStep.Infer(tensorPool, config, (consumedSoFar) => budgetConsumed = consumedSoFar, debugName: "Decoder preprocess", budgetConsumed: budgetConsumed);
            if (CheckInferenceAbort(targetModel))
            {
                callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                yield break;
            } 

            InferenceWorkloadManager.Instance.ReleaseWorkload(targetModel);


            Tensor<int> nbrChunksTensor = tensorPool.GetTensor("nbr_of_chunks") as Tensor<int>;
            if (asyncDownload)
            {
                Profiler.BeginSample("Thespeon readback request chunk number");
                nbrChunksTensor.ReadbackRequest();
                Profiler.EndSample();
                yield return new WaitUntil(nbrChunksTensor.IsReadbackRequestDone);
                yield return new WaitForEndOfFrame();
            }
            Profiler.BeginSample("Thespeon download chunk number");
            int nbrChunks = nbrChunksTensor.DownloadToArray()[0];
            Profiler.EndSample();

            Tensor<int> chunkLength = new(new TensorShape(1), new[] { actorModule.chunk_length });
            tensorPool.SetTensor("chunk_length", chunkLength);
            Tensor<float> boundaryAlpha = new(new TensorShape(1), new[] { 0.0f });
            tensorPool.SetTensor("boundary_clone_alpha", boundaryAlpha);

            string decoderChunkedID = actorModule.GetInternalModelID("decoder_chunked");
            int chunkIdx = 0;
            const int melFrameLength = 512;
            while (chunkIdx < nbrChunks)
            {
                Tensor<int> currentChunkTensor = new(new TensorShape(1), new[] { chunkIdx });
                tensorPool.SetTensor("chunk_index", currentChunkTensor);
                
                targetModel = actorModule.GetInternalModelID("decoder_chunked");
                InferenceWorkload decoderChunked = null;
                if (!InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref decoderChunked))
                {
                    yield return new WaitUntil(() => InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref decoderChunked));
                    yield return new WaitForEndOfFrame();
                }
                if (chunkIdx == 0)
                {
                    if (chunkIdx == nbrChunks - 1)
                    {
                        LingotionLogger.Error("A synthesis this short is not supported yet. Please use longer input text, such as adding trailing pause characters, or lower speed.");
                        break;
                    }

                    yield return decoderChunked.Infer(tensorPool, config, (consumedSoFar) => budgetConsumed = consumedSoFar, debugName: "Decoder Chunked", budgetConsumed: budgetConsumed);
                    if (CheckInferenceAbort(targetModel))
                    {
                        callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                        yield break;
                    } 
                    InferenceWorkloadManager.Instance.ReleaseWorkload(decoderChunkedID);
                    yield return new WaitForEndOfFrame();
                    targetModel = actorModule.GetInternalModelID("vocoder_first");
                    InferenceWorkload vocoderFirstChunk = null;
                    if (!InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref vocoderFirstChunk))
                    {
                        yield return new WaitUntil(() => InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref vocoderFirstChunk));
                        yield return new WaitForEndOfFrame();
                    }
                    yield return vocoderFirstChunk.Infer(tensorPool, config, (consumedSoFar) => budgetConsumed = consumedSoFar, debugName: "Vocoder first", budgetConsumed: budgetConsumed);
                    if (CheckInferenceAbort(targetModel))
                    {
                        callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                        yield break;
                    } 
                    InferenceWorkloadManager.Instance.ReleaseWorkload(targetModel);

                    Tensor<float> tensor = tensorPool.GetTensor("vocoder_audio") as Tensor<float>;
                    if (asyncDownload)
                    {
                        Profiler.BeginSample("Thespeon readback request first audio");
                        tensor.ReadbackRequest();
                        Profiler.EndSample();
                        yield return new WaitUntil(tensor.IsReadbackRequestDone);
                        yield return new WaitForEndOfFrame();
                    }
                    Profiler.BeginSample("Thespeon download first audio");
                    float[] vocoderData = tensor.DownloadToArray();
                    Profiler.EndSample();
                    if (!alignmentTensor.IsReadbackRequestDone())
                    {
                        yield return new WaitUntil(alignmentTensor.IsReadbackRequestDone);
                        yield return new WaitForEndOfFrame();
                    }
                    int[] alignmentArray = CumulativeRoundSum(alignmentTensor.DownloadToArray()).ToArray();
                    Queue<int> triggerAudioIndices = markerTensorPositions.Count > 0 ? new() : null;
                    foreach (int idx in markerTensorPositions)
                    {
                        triggerAudioIndices.Enqueue(alignmentArray[idx] * melFrameLength);
                    }
                    callback?.Invoke(new ThespeonDataPacket<T>(vocoderData as T[], sessionID, characterName: input.ActorName, moduleType: input.ModuleType, requestedAudioIndices: triggerAudioIndices));

                    boundaryAlpha = new(new TensorShape(1), new[] { 1.0f });
                    tensorPool.SetTensor("boundary_clone_alpha", boundaryAlpha);

                }
                else if (chunkIdx == nbrChunks - 1)
                {

                    yield return decoderChunked.Infer(tensorPool, config, (consumedSoFar) => budgetConsumed = consumedSoFar, debugName: "Decoder Chunked", budgetConsumed: budgetConsumed);
                    if (CheckInferenceAbort(targetModel))
                    {
                        callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                        yield break;
                    } 
                    InferenceWorkloadManager.Instance.ReleaseWorkload(decoderChunkedID);
                    yield return new WaitForEndOfFrame();
                    targetModel = actorModule.GetInternalModelID("decoder_postprocess");
                    InferenceWorkload decoderPostProcess = null;
                    if (!InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref decoderPostProcess))
                    {
                        yield return new WaitUntil(() => InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref decoderPostProcess));
                        yield return new WaitForEndOfFrame();
                    }
                    yield return decoderPostProcess.Infer(tensorPool, config, (consumedSoFar) => budgetConsumed = consumedSoFar, skipFrames: false, debugName: "Decoder Postprocess", budgetConsumed: budgetConsumed);
                    if (CheckInferenceAbort(targetModel))
                    {
                        callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                        yield break;
                    } 
                    InferenceWorkloadManager.Instance.ReleaseWorkload(targetModel);

                    targetModel = actorModule.GetInternalModelID("vocoder_last");
                    InferenceWorkload vocoderLastChunk = null;
                    if (!InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref vocoderLastChunk))
                    {
                        yield return new WaitUntil(() => InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref vocoderLastChunk));
                        yield return new WaitForEndOfFrame();
                    }
                    yield return vocoderLastChunk.Infer(tensorPool, config, (consumedSoFar) => budgetConsumed = consumedSoFar, debugName: "Vocoder last", budgetConsumed: budgetConsumed);
                    if (CheckInferenceAbort(targetModel))
                    {
                        callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                        yield break;
                    } 
                    InferenceWorkloadManager.Instance.ReleaseWorkload(targetModel);

                    Tensor<float> tensor = tensorPool.GetTensor("vocoder_audio") as Tensor<float>;
                    if (asyncDownload)
                    {
                        Profiler.BeginSample("Thespeon readback request last audio");
                        tensor.ReadbackRequest();
                        Profiler.EndSample();
                        yield return new WaitUntil(tensor.IsReadbackRequestDone);
                        yield return new WaitForEndOfFrame();
                    }
                    Profiler.BeginSample("Thespeon download last audio");
                    float[] vocoderData = tensor.DownloadToArray();
                    Profiler.EndSample();
                    callback?.Invoke(new ThespeonDataPacket<T>(vocoderData as T[], sessionID, isFinalPacket: true, characterName: input.ActorName, moduleType: input.ModuleType));

                }
                else
                {

                    yield return decoderChunked.Infer(tensorPool, config, (consumedSoFar) => budgetConsumed = consumedSoFar, debugName: "Decoder Chunked", budgetConsumed: budgetConsumed);
                    if (CheckInferenceAbort(targetModel))
                    {
                        callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                        yield break;
                    } 

                    InferenceWorkloadManager.Instance.ReleaseWorkload(decoderChunkedID);
                    yield return new WaitForEndOfFrame();
                    targetModel = actorModule.GetInternalModelID("vocoder_middle");
                    InferenceWorkload vocoderMiddleChunk = null;
                    if (!InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref vocoderMiddleChunk))
                    {
                        yield return new WaitUntil(() => InferenceWorkloadManager.Instance.AcquireWorkload(targetModel, ref vocoderMiddleChunk));
                        yield return new WaitForEndOfFrame();
                    }
                    yield return vocoderMiddleChunk.Infer(tensorPool, config, (consumedSoFar) => budgetConsumed = consumedSoFar, debugName: "Vocoder middle", budgetConsumed: budgetConsumed);
                    if (CheckInferenceAbort(targetModel))
                    {
                        callback?.Invoke(new ThespeonDataPacket<T>(null, sessionID, status:DataPacketStatus.FAILED));
                        yield break;
                    } 
                    InferenceWorkloadManager.Instance.ReleaseWorkload(targetModel);

                    Tensor<float> tensor = tensorPool.GetTensor("vocoder_audio") as Tensor<float>;
                    if (asyncDownload)
                    {
                        Profiler.BeginSample("Thespeon readback request audio");
                        tensor.ReadbackRequest();
                        Profiler.EndSample();
                        yield return new WaitUntil(tensor.IsReadbackRequestDone);
                        yield return new WaitForEndOfFrame();
                    }
                    Profiler.BeginSample("Thespeon download audio");
                    float[] vocoderData = tensor.DownloadToArray();
                    Profiler.EndSample();
                    callback?.Invoke(new ThespeonDataPacket<T>(vocoderData as T[], sessionID, characterName: input.ActorName, moduleType: input.ModuleType));
                }
                chunkIdx++;
            }
            tensorPool.Dispose();
        }

        /// <summary>
        /// Sets up the actor and language modules for inference.
        /// </summary>
        private static (ActorModule, Dictionary<string, LanguageModule>) SetupModules(string actorName, ModuleType moduleType, InferenceConfig config)
        {
            Profiler.BeginSample($"Thespeon Loading {actorName} {moduleType}");

            Profiler.BeginSample("Thespeon Get actor module entry");
            ModuleEntry targetModuleInfo = PackManifestHandler.Instance.GetActorPackModuleEntry(actorName, moduleType);
            Profiler.EndSample();
            Profiler.BeginSample("Thespeon Acquire module");
            ActorModule actorModule = ModuleHandler.Instance.AcquireModule<ActorModule>(targetModuleInfo);
            Profiler.EndSample();
            Dictionary<string, LanguageModule> languageModules = new();

            foreach (var kvp in actorModule.languageModuleIDs)
            {
                Profiler.BeginSample($"Thespeon Get language module entry {kvp.Key}");
                string id = kvp.Value;
                targetModuleInfo = PackManifestHandler.Instance.GetLanguagePackModuleEntry(id);
                Profiler.EndSample();
                if (targetModuleInfo.IsEmpty())
                {
                    continue;
                }
                Profiler.BeginSample($"Thespeon Acquire language module {kvp.Key}");
                LanguageModule langModule = ModuleHandler.Instance.AcquireModule<LanguageModule>(targetModuleInfo);
                languageModules.Add(id, langModule);
                Profiler.EndSample();
                Profiler.BeginSample($"Thespeon Register language module {langModule.moduleLanguage.Iso639_2}");
                InferenceWorkloadManager.Instance.RegisterModule(langModule, config);
                Profiler.EndSample();
                Profiler.BeginSample($"Thespeon Register lookup table {langModule.moduleLanguage.Iso639_2}");
                LookupTableHandler.Instance.RegisterLookupTable(langModule);
                Profiler.EndSample();
            }

            Profiler.BeginSample("Thespeon Register actor module");
            InferenceWorkloadManager.Instance.RegisterModule(actorModule, config);
            Profiler.EndSample();
            Profiler.EndSample();

            return (actorModule, languageModules);
        }

        /// <summary>
        /// Coroutine to set up modules for inference in a coroutine.
        /// </summary>
        public static IEnumerator SetupModulesCoroutine(string actorName, ModuleType moduleType, InferenceConfig config)
        {
            yield return new WaitForEndOfFrame();
            double startTime = Time.realtimeSinceStartupAsDouble;
            Profiler.BeginSample($"Thespeon Loading {actorName} {moduleType}");

            Profiler.BeginSample("Thespeon Get actor module entry");
            ModuleEntry targetModuleInfo = PackManifestHandler.Instance.GetActorPackModuleEntry(actorName, moduleType);
            Profiler.EndSample();
            if (CheckFrameBreak(startTime, config))
            {
                Profiler.EndSample();
                yield return null;
                yield return new WaitForEndOfFrame();
                Profiler.BeginSample($"Thespeon Loading {actorName} {moduleType}");

                startTime = Time.realtimeSinceStartupAsDouble;
            }
            Profiler.BeginSample("Thespeon Acquire module");
            ActorModule actorModule = ModuleHandler.Instance.AcquireModule<ActorModule>(targetModuleInfo);
            Profiler.EndSample();
            Dictionary<string, LanguageModule> languageModules = new();

            foreach (var kvp in actorModule.languageModuleIDs)
            {
                if (CheckFrameBreak(startTime, config))
                {
                    Profiler.EndSample();
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Profiler.BeginSample($"Thespeon Loading {actorName} {moduleType}");
                    startTime = Time.realtimeSinceStartupAsDouble;
                }
                Profiler.BeginSample($"Thespeon Get language module entry {kvp.Key}");
                string id = kvp.Value;
                targetModuleInfo = PackManifestHandler.Instance.GetLanguagePackModuleEntry(id);
                Profiler.EndSample();
                if (targetModuleInfo.IsEmpty())
                {
                    continue;
                }
                if (CheckFrameBreak(startTime, config))
                {
                    Profiler.EndSample();
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Profiler.BeginSample($"Thespeon Loading {actorName} {moduleType}");
                    startTime = Time.realtimeSinceStartupAsDouble;
                }
                Profiler.BeginSample($"Thespeon Acquire language module {kvp.Key}");
                LanguageModule langModule = ModuleHandler.Instance.AcquireModule<LanguageModule>(targetModuleInfo);
                languageModules.Add(id, langModule);
                Profiler.EndSample();
                Profiler.EndSample();
                if (CheckFrameBreak(startTime, config))
                {
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    startTime = Time.realtimeSinceStartupAsDouble;
                }
                yield return InferenceWorkloadManager.Instance.RegisterModuleCoroutine(langModule, config);
                if (CheckFrameBreak(startTime, config))
                {
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    startTime = Time.realtimeSinceStartupAsDouble;
                }
                yield return LookupTableHandler.Instance.RegisterLookupTableCoroutine(langModule,
                    () => CheckFrameBreak(startTime, config),
                    () => startTime = Time.realtimeSinceStartupAsDouble);
                Profiler.BeginSample($"Thespeon Loading {actorName} {moduleType}");
            }

            if (CheckFrameBreak(startTime, config))
            {
                Profiler.EndSample();
                yield return null;
                yield return new WaitForEndOfFrame();
                Profiler.BeginSample($"Thespeon Loading {actorName} {moduleType}");
            }
            Profiler.EndSample();
            yield return InferenceWorkloadManager.Instance.RegisterModuleCoroutine(actorModule, config);
        }
        private static bool CheckFrameBreak(double startTime, InferenceConfig config)
        {
            double elapsedTime = Time.realtimeSinceStartupAsDouble - startTime;
            double timeSinceFrameStart = Time.realtimeSinceStartupAsDouble - Time.unscaledTimeAsDouble;
            double timeLeftOfFrame = config.TargetFrameTime - timeSinceFrameStart - config.TargetFrameTime / 10d;
            double timeLeftOfBudget = config.TargetBudgetTime - elapsedTime;

            return timeLeftOfFrame < 0 || timeLeftOfBudget < 0;
        }

        private (Dictionary<string, Dictionary<string, int>>, List<int>) PhonemizeInput(ref ThespeonInput processedInput, ActorModule actorModule, Dictionary<string, LanguageModule> languageModules, List<List<float>> markerPositionsBySegment)
        {
            Dictionary<string, Dictionary<string, int>> lengthChangesByLanguage = new();
            int segIdx = 0;
            List<int> globalMarkerPositions = new();
            int globalLengthCount = 0;
            foreach (ThespeonInputSegment segment in processedInput.Segments)
            {
                List<float> markerPositions = markerPositionsBySegment[segIdx++];
                if (segment.IsCustomPronounced)
                {

                    globalMarkerPositions.AddRange(markerPositions.Select(pos => Mathf.RoundToInt(globalLengthCount + pos)));
                    globalLengthCount += segment.Text.Length;
                    continue;
                }
                ModuleLanguage segmentLanguage = segment.Language ?? processedInput.DefaultLanguage;
                if (!lengthChangesByLanguage.ContainsKey(segmentLanguage.ToJson()))
                {
                    lengthChangesByLanguage[segmentLanguage.ToJson()] = new();
                }
                ModuleLanguage langPackLanguage = ModuleLanguage.BestMatch(PackManifestHandler.Instance.GetAllLanguageModuleLanguages(), segmentLanguage.Iso639_2, null);
                if (!languageModules.ContainsKey(actorModule.languageModuleIDs[langPackLanguage.ToJson()]))
                {
                    throw new FileNotFoundException($"Language pack for Language '{langPackLanguage.ToJson()}' was never imported. Please import a language pack for each language you intend to use.");
                }

                RuntimeLookupTable lookupTable = LookupTableHandler.Instance.GetLookupTable(languageModules[actorModule.languageModuleIDs[langPackLanguage.ToJson()]].GetLookupTableID());

                MatchCollection matches = TextPreprocessor.WordRegex.Matches(segment.Text);
                StringBuilder sb = new(segment.Text);
                int offset = 0;
                int wordCount = 0;
                int markerIdx = 0;
                foreach (Match match in matches)
                {
                    string word = match.Value;
                    int index = match.Index + offset;
                    if (lookupTable.TryGetValue(word, out string phonemizedWord))
                    {
                        sb.Remove(index, word.Length);
                        sb.Insert(index, phonemizedWord);
                        offset += phonemizedWord.Length - word.Length;
                        if (!lengthChangesByLanguage[segmentLanguage.ToJson()].ContainsKey(word))
                        {
                            lengthChangesByLanguage[segmentLanguage.ToJson()][word] = phonemizedWord.Length;
                        }
                        wordCount++;
                        if (markerIdx < markerPositions.Count && wordCount > markerPositions[markerIdx])
                        {
                            int globalPosition = globalLengthCount + index + Mathf.RoundToInt(phonemizedWord.Length * (markerPositions[markerIdx] - (float)Math.Truncate(markerPositions[markerIdx])));
                            LingotionLogger.Debug($"Adding audio sample request marker at global position {globalPosition} for segment {segIdx - 1}, word '{phonemizedWord}'");
                            globalMarkerPositions.Add(globalPosition);
                            markerIdx++;
                        }
                    }
                }

                segment.Text = sb.ToString();
                globalLengthCount += segment.Text.Length;
                while (markerIdx != markerPositions.Count)
                {
                    globalMarkerPositions.Add(globalLengthCount);
                    markerIdx++;
                }
            }
            return (lengthChangesByLanguage, globalMarkerPositions);
        }
        private (Dictionary<string, List<string>>, List<List<float>>) FindUnknownWordsAndMarkerPositions(ThespeonInput input, ActorModule actorModule, Dictionary<string, LanguageModule> languageModules)
        {
            Dictionary<string, List<string>> unknownWordsByLanguage = new();
            List<List<float>> markerPositionsBySegment = new();
            foreach (ThespeonInputSegment segment in input.Segments)
            {
                if (segment.IsCustomPronounced)
                {
                    (string cleanedPhonemizedText, List<int> markerPhonemizedIdx) = StripMarkers(segment.Text);
                    segment.Text = cleanedPhonemizedText;

                    markerPositionsBySegment.Add(markerPhonemizedIdx.Select(i => (float)i).ToList());
                    continue;
                }
                ModuleLanguage segmentLanguage = segment.Language ?? input.DefaultLanguage;
                ModuleLanguage langPackLanguage = ModuleLanguage.BestMatch(PackManifestHandler.Instance.GetAllLanguageModuleLanguages(), segmentLanguage.Iso639_2, null);
                if (!languageModules.ContainsKey(actorModule.languageModuleIDs[langPackLanguage.ToJson()]))
                {
                    throw new FileNotFoundException($"Language pack for Language '{langPackLanguage.ToJson()}' was never imported. Please import a language pack for each language you intend to use.");
                }
                RuntimeLookupTable lookupTable = LookupTableHandler.Instance.GetLookupTable(languageModules[actorModule.languageModuleIDs[langPackLanguage.ToJson()]].GetLookupTableID());
                (string cleanedText, List<int> markerCleanIdx) = StripMarkers(segment.Text);
                MatchCollection matches = TextPreprocessor.WordRegex.Matches(cleanedText);
                foreach (Match match in matches)
                {
                    string word = match.Value;
                    if (!lookupTable.ContainsKey(word))
                    {
                        if (!unknownWordsByLanguage.ContainsKey(langPackLanguage.ToJson()))
                            unknownWordsByLanguage[langPackLanguage.ToJson()] = new();
                        if (unknownWordsByLanguage[langPackLanguage.ToJson()].Contains(word))
                        {
                            continue;
                        }
                        unknownWordsByLanguage[langPackLanguage.ToJson()].Add(word);
                    }
                }
                segment.Text = cleanedText;
                markerPositionsBySegment.Add(ComputeMarkerPositions(matches, markerCleanIdx));
            }
            return (unknownWordsByLanguage, markerPositionsBySegment);
        }

        private int BuildPhonemizerTensors(int maxInLength, List<List<int>> phonemizerInputs, int sosID)
        {
            try
            {
                int batchSize = phonemizerInputs.Count;
                TensorShape batchShape = new(batchSize, maxInLength);
                Tensor<int> inputTensor = new(batchShape, true);

                for (int r = 0; r < batchSize; r++)
                {
                    for (int c = 0; c < maxInLength; c++)
                    {
                        inputTensor[r, c] = (c < phonemizerInputs[r].Count) ? phonemizerInputs[r][c] : 0;
                    }
                }

                Tensor<int> tgtIndices = new(
                    new TensorShape(batchSize, 1), new int[batchSize].Select(x => sosID).ToArray()
                );

                Tensor<int> eos_mask = new(new TensorShape(batchSize, 1), Enumerable.Repeat(1, batchSize).ToArray());
                Tensor<int> finished_indices = new(new TensorShape(batchSize));


                tensorPool.SetTensor("src", inputTensor);
                tensorPool.SetTensor("tgt", tgtIndices);
                tensorPool.SetTensor("mask", eos_mask);
                tensorPool.SetTensor("finished_indices", finished_indices);
                return batchSize;
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error building phonemizer tensors: {e.Message}");
                tensorPool.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Adds the phonemized words to the dynamic lookup table of the target language module.
        /// </summary>
        /// <param name="targetLanguageModule"></param>
        /// <param name="uniqueWords"></param>
        private void ResolvePhonemizerResult(LanguageModule targetLanguageModule, List<string> uniqueWords)
        {
            Tensor<int> castedTensor = tensorPool.GetTensor("tgt") as Tensor<int>;
            int[] phonemeIndices = castedTensor.DownloadToArray();
            Tensor<int> finished_indicesTensor = tensorPool.GetTensor("finished_indices") as Tensor<int>;
            int[] finished_indices = finished_indicesTensor.DownloadToArray();
            RuntimeLookupTable lookupTable = LookupTableHandler.Instance.GetLookupTable(targetLanguageModule.GetLookupTableID());
            int batchSize = uniqueWords.Count;
            for (int j = 0; j < batchSize; j++)
            {
                int start = j * phonemeIndices.Length / batchSize;
                int end = start + finished_indices[j];
                List<int> wordIDs = phonemeIndices[(start + 1)..end].ToList();
                string phonemizedWord = targetLanguageModule.DecodePhonemes(wordIDs);
                lookupTable.AddOrUpdateDynamicEntry(uniqueWords[j], phonemizedWord);
            }
        }
        private void SetEncoderTensors(ActorModule actorModule, ThespeonInput input, Dictionary<string, Dictionary<string, int>> lengthChangesByLanguage, List<string> originalSegmentTexts)
        {
            List<int> textkeys = new() { actorModule.EncodePhonemes("⏩").Item1[0] };
            List<int> emotionkeys = new();
            List<int> languagekeys = new();
            int actorkey = actorModule.GetActorKey();
            int i = 0;
            int runningLength = 0;
            List<int> indecesFiltered = new();
            foreach (ThespeonInputSegment segment in input.Segments)
            {
                ModuleLanguage segmentLanguage = segment.Language ?? input.DefaultLanguage;
                Emotion segmentEmotion = segment.Emotion != Emotion.None ? segment.Emotion : input.DefaultEmotion;
                if (segmentEmotion == Emotion.None)
                    throw new ArgumentException("Segment emotion should never be None.");
                int soseosAdjust = (i == 0 ? 1 : 0) + (i == input.Segments.Count - 1 ? 1 : 0);
                (List<int> segmentPhonemes, List<int> filteredIndeces) = actorModule.EncodePhonemes(segment.Text);
                textkeys.AddRange(segmentPhonemes);
                emotionkeys.AddRange(Enumerable.Repeat((int)segmentEmotion, segmentPhonemes.Count + soseosAdjust));
                languagekeys.AddRange(Enumerable.Repeat(actorModule.GetLanguageKey(segmentLanguage), segmentPhonemes.Count + soseosAdjust));
                indecesFiltered.AddRange(filteredIndeces.Select(index => index + runningLength + (i == 0 ? 1 : 0)).ToList());
                i++;
                runningLength += segment.Text.Length + soseosAdjust;
            }

            textkeys.Add(actorModule.EncodePhonemes("⏪").Item1[0]);

            (List<float> speedkeys, List<float> loudnesskeys) = BuildRLECurves(input, lengthChangesByLanguage, originalSegmentTexts);
            indecesFiltered.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in indecesFiltered)
            {
                if (idx < 0 || idx >= speedkeys.Count)
                {
                    LingotionLogger.Error($"Index {idx} is out of bounds for textkeys with length {textkeys.Count}. This should never happen.");
                    continue;
                }
                speedkeys.RemoveAt(idx);
                loudnesskeys.RemoveAt(idx);
            }
            if (textkeys.Count != emotionkeys.Count ||
                textkeys.Count != languagekeys.Count ||
                textkeys.Count != speedkeys.Count ||
                textkeys.Count != loudnesskeys.Count)
            {
                throw new ArgumentException("Mismatch in tensor lengths: " +
                    $"textkeys: {textkeys.Count}, " +
                    $"emotionkeys: {emotionkeys.Count}, " +
                    $"languagekeys: {languagekeys.Count}, " +
                    $"speedkeys: {speedkeys.Count}, " +
                    $"loudnesskeys: {loudnesskeys.Count} ");
            }
            LingotionLogger.Debug($"Text keys: {string.Join(", ", textkeys)}");
            LingotionLogger.Debug($"Emotion keys: {string.Join(", ", emotionkeys)}");
            LingotionLogger.Debug($"Actor key: {actorkey}");
            LingotionLogger.Debug($"Language keys: {string.Join(", ", languagekeys)}");

            int textLength = textkeys.Count;
            Tensor<int> txt = new(
                new TensorShape(1, textLength), textkeys.ToArray()
            );
            Tensor<int> emotions = new(
                new TensorShape(1, textLength), emotionkeys.ToArray()
            );
            Tensor<int> actors = new(
                new TensorShape(1, 1), new int[] { actorkey }
            );
            Tensor<int> languages = new(
                new TensorShape(1, textLength), languagekeys.ToArray()
            );
            Tensor<float> speed = new(
                new TensorShape(1, textLength), speedkeys.ToArray()
            );
            Tensor<float> loudness = new(
                new TensorShape(1, textLength), loudnesskeys.ToArray()
            );

            tensorPool.SetTensor("txt", txt);
            tensorPool.SetTensor("emotions", emotions);
            tensorPool.SetTensor("actors.1", actors);
            tensorPool.SetTensor("languages.1", languages);
            tensorPool.SetTensor("speed", speed);
            tensorPool.SetTensor("loudness", loudness);
        }
        private (List<float> speed, List<float> loudness) BuildRLECurves(ThespeonInput input, Dictionary<string, Dictionary<string, int>> lengthChangesByLanguage, List<string> originalSegmentTexts)
        {

            List<float> speed = new() { input.Speed.Evaluate(0) };

            List<float> loudness = new() { input.Loudness.Evaluate(0) };

            int totalLength = originalSegmentTexts.Sum(text => text.Length);
            int segmentStart = 0;
            for (int i = 0; i < input.Segments.Count; i++)
            {
                ThespeonInputSegment segment = input.Segments[i];
                if (segment.IsCustomPronounced)
                {
                    speed.AddRange(ResampleCurveRange(input.Speed, (float)segmentStart / totalLength, (float)(segmentStart + originalSegmentTexts[i].Length) / totalLength, originalSegmentTexts[i].Length, "speed"));
                    loudness.AddRange(ResampleCurveRange(input.Loudness, (float)segmentStart / totalLength, (float)(segmentStart + originalSegmentTexts[i].Length) / totalLength, originalSegmentTexts[i].Length, "loudness"));
                    segmentStart += segment.Text.Length;
                    continue;
                }
                ModuleLanguage segmentLanguage = segment.Language ?? input.DefaultLanguage;

                MatchCollection matches = TextPreprocessor.WordRegex.Matches(originalSegmentTexts[i]);
                int lastEnd = segmentStart;
                foreach (Match match in matches)
                {
                    string word = match.Value;
                    int oldLength = word.Length;
                    int globalStart = match.Index + segmentStart;
                    speed.AddRange(ResampleCurveRange(input.Speed, (float)lastEnd / totalLength, (float)globalStart / totalLength, globalStart - lastEnd, "speed"));
                    loudness.AddRange(ResampleCurveRange(input.Loudness, (float)lastEnd / totalLength, (float)globalStart / totalLength, globalStart - lastEnd, "loudness"));
                    if (lengthChangesByLanguage[segmentLanguage.ToJson()].TryGetValue(word, out int newLength))
                    {
                        speed.AddRange(ResampleCurveRange(input.Speed, (float)globalStart / totalLength, (float)(globalStart + oldLength) / totalLength, newLength, "speed"));
                        loudness.AddRange(ResampleCurveRange(input.Loudness, (float)globalStart / totalLength, (float)(globalStart + oldLength) / totalLength, newLength, "loudness"));
                    }
                    else
                    {
                        throw new InvalidOperationException($"Word '{word}' not found in lengthChangesByLanguage dictionary for language {segmentLanguage.ToJson()}. This should never happen. Languages: {string.Join(", ", lengthChangesByLanguage.Keys)} \n for this language:{string.Join(", ", lengthChangesByLanguage[segmentLanguage.ToJson()].Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
                    }
                    lastEnd = globalStart + oldLength;
                }
                if (lastEnd < segmentStart + originalSegmentTexts[i].Length)
                {
                    speed.AddRange(ResampleCurveRange(input.Speed, (float)lastEnd / totalLength, (float)(segmentStart + originalSegmentTexts[i].Length) / totalLength, segmentStart + originalSegmentTexts[i].Length - lastEnd, "speed"));
                    loudness.AddRange(ResampleCurveRange(input.Loudness, (float)lastEnd / totalLength, (float)(segmentStart + originalSegmentTexts[i].Length) / totalLength, segmentStart + originalSegmentTexts[i].Length - lastEnd, "loudness"));
                }
                segmentStart += originalSegmentTexts[i].Length;
            }

            speed.Add(input.Speed.Evaluate(1));

            loudness.Add(input.Loudness.Evaluate(1));
            return (speed, loudness);
        }
        /// <summary>
        /// Resamples the given curve in the range from 'from' to 'to' (non inclusive) with the specified sample count.
        /// </summary>
        private List<float> ResampleCurveRange(AnimationCurve curve, float from, float to, int sampleCount, string curveName)
        {
            if (sampleCount == 0) return new();
            List<float> samples = new(sampleCount);
            float step = (to - from) / sampleCount;
            float lowerBound = curveName == "speed" ? 0.5f : 0.1f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = from + i * step;
                float val = curve.Evaluate(t);
                val = Math.Clamp(val, lowerBound, 2.0f);
                samples.Add(val);
            }
            return samples;
        }
        private bool CheckInferenceAbort(string modelID = null)
        {
            if (tensorPool.IsDisposed())
            {
                if (modelID != null)
                {
                    LingotionLogger.Error($"Tensor pool was disposed in model {modelID}, aborting.");
                    InferenceWorkloadManager.Instance.ReleaseWorkload(modelID);
                }
                else
                {
                    LingotionLogger.Error($"Tensor pool was disposed, aborting.");
                }
                return true;
            }
            return false;
        }

        private static IEnumerable<int> CumulativeRoundSum(float[] sequence)
        {
            int sum = 0;
            foreach (var item in sequence)
            {
                sum += Mathf.RoundToInt(item);
                yield return sum;
            }
        }
        private static (string cleaned, List<int> markerCleanIdx) StripMarkers(string text)
        {
            var sb = new StringBuilder(text.Length);
            var idxs = new List<int>();
            int cleanIndex = 0;

            foreach (char ch in text)
            {
                if (ch == ControlCharacters.AudioSampleRequest) { idxs.Add(cleanIndex); continue; }
                sb.Append(ch);
                cleanIndex++;
            }
            return (sb.ToString(), idxs);
        }

        private static List<float> ComputeMarkerPositions(MatchCollection matches, List<int> markerIdx)
        {
            List<float> positions = new(markerIdx.Count);
            int mi = 0;
            int wordCountBefore = 0;

            foreach (int idx in markerIdx)
            {
                while (mi < matches.Count &&
                    (matches[mi].Index + matches[mi].Length) < idx)
                {
                    wordCountBefore++;
                    mi++;
                }
                if (mi < matches.Count)
                {
                    var m = matches[mi];
                    int start = m.Index;
                    int end   = m.Index + m.Length;

                    if (start <= idx && idx <= end)
                    {
                        float frac = (idx - start) / (float)m.Length;
                        positions.Add(wordCountBefore + frac);
                        continue;
                    }
                }
                positions.Add(wordCountBefore);
            }

            return positions;
        }

    }
}