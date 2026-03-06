// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEngine;
using Lingotion.Thespeon.Core;
using System.Collections;
using Lingotion.Thespeon.Character;
using Lingotion.Thespeon.Language;
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

        public ThespeonInference(string sessionID, Action<ThespeonDataPacket> packetCallback)
        : base(sessionID, packetCallback)
        {
        }

        /// <summary>
        /// Sets up the modules required for inference based on the character name and module type.
        /// </summary>
        /// <param name="characterName">The name of the character to set up modules for.</param>
        /// <param name="moduleType">The type of module to set up.</param>
        /// <param name="config">The inference configuration to use.</param>
        public static bool TrySetupModules(string characterName, ModuleType moduleType, InferenceConfig config)
        {
            try
            {
                SetupModules(characterName, moduleType, config);
                return true;
            }
            catch (Exception e)
            {
                string errorMessage = e.InnerException?.Message ?? e.Message;
                LingotionLogger.Error($"Failed to preload character {characterName} ({moduleType}): {errorMessage}");
                return false;
            }
        }

        /// <summary>
        /// Unloads the specified module for the given character.
        /// </summary>
        /// <param name="characterName">The name of the character whose module should be unloaded.</param>
        /// <param name="moduleType">The type of module to unload.</param>
        /// <param name="backend">Optional backend type to consider when unloading the module. Null will unload all.</param>
        public static bool TryUnloadCharacter(string characterName, ModuleType moduleType, BackendType? backend = null)
        {
            if (string.IsNullOrEmpty(characterName) || moduleType == ModuleType.None)
            {
                LingotionLogger.Error("Character name or module type is invalid. Cannot unload module.");
                return false;
            }
            string backendStr = backend.HasValue ? $"backend {backend.Value}" : "all backends";
            try
            {
                ModuleEntry characterModuleInfo = ManifestHandler.Instance.GetCharacterModuleEntry(characterName, moduleType);
                if (characterModuleInfo.IsEmpty())
                {
                    LingotionLogger.Error($"Module for character {characterName} of type {moduleType} has not been imported. Cannot unload. Please see the Thespeon Info Window for more details.");
                    return false;
                }

                CharacterModule characterModule = ModuleHandler.Instance.AcquireModule<CharacterModule>(characterModuleInfo, false);
                if (characterModule == default)
                {
                    LingotionLogger.Debug($"Character module for {characterName} of type {moduleType} is not registered or already deregistered.");

                    return true;
                }
                HashSet<string> langModsSafeToRemove = ModuleHandler.Instance.GetNonOverlappingLangModules(characterModule);
                foreach (string id in langModsSafeToRemove)
                {
                    ModuleEntry languageModuleInfo = ManifestHandler.Instance.GetLanguageModuleEntry(id);
                    if (languageModuleInfo.IsEmpty())
                    {
                        continue;
                    }
                    LanguageModule langModule = ModuleHandler.Instance.AcquireModule<LanguageModule>(languageModuleInfo, false);
                    if (langModule == default)
                    {
                        LingotionLogger.Debug($"Language module {id} is not registered or already deregistered.");
                        continue;
                    }
                    if(backend.HasValue && characterModule.GetLoadedBackends().Count == 1 && !characterModule.GetLoadedBackends().Contains(backend.Value))
                    {
                        LingotionLogger.Debug($"Language module {id} is still used by character on other backends, skipping unload on {backendStr}.");
                        continue;
                    }

                    BackendType langModForcedBackend = BackendType.CPU;
                    if (!InferenceWorkloadManager.Instance.TryDeregisterModuleWorkloads(langModule, langModForcedBackend))
                    {
                        LingotionLogger.Error($"Failed to deregister language module {id} on {backendStr} as it is still in use.");
                        continue;
                    }
                    LookupTableHandler.Instance.DeregisterTable(langModule);
                    ModuleHandler.Instance.DeregisterModule(id);
                }
                if (!InferenceWorkloadManager.Instance.TryDeregisterModuleWorkloads(characterModule, backend))
                {
                    LingotionLogger.Error($"Failed to deregister module {characterName} of type {moduleType} on {backendStr} as it is still in use.");
                    return false;
                }
                ModuleHandler.Instance.DeregisterModule(characterModule.ModuleID);
                return true;
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Failed to unload module {characterName} of type {moduleType} on {backendStr}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Performs inference on the given ThespeonInput, processing the input and invoking the callback with the result.
        /// </summary>
        /// <param name="input">The ThespeonInput to process.</param>
        /// <param name="config">The InferenceConfig to use for inference.</param>
        /// <param name="asyncDownload">Whether to download tensors asynchronously.</param>
        /// <returns>An IEnumerator for coroutine execution.</returns>
        public override IEnumerator Infer(ThespeonInput input, InferenceConfig config, bool asyncDownload = true)
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
            CharacterModule characterModule;
            Dictionary<string, LanguageModule> languageModules;
            ThespeonInput processedInput;
            Dictionary<string, List<string>> unknownWordsByLanguage;
            List<List<float>> markerPositionsBySegment;
            try
            {
                Profiler.BeginSample("Thespeon Setup modules");
                (characterModule, languageModules) = SetupModules(input.CharacterName, input.ModuleType, config);
                Profiler.EndSample();
                input.DefaultLanguage ??= config.FallbackLanguage;
                input.DefaultEmotion = input.DefaultEmotion == Emotion.None ? config.FallbackEmotion : input.DefaultEmotion;
                Profiler.BeginSample("Thespeon Text preprocessing");
                processedInput = TextPreprocessor.PreprocessInput(input);
                Profiler.EndSample();
                Profiler.BeginSample("Thespeon Find unknown words");
                (unknownWordsByLanguage, markerPositionsBySegment) = FindUnknownWordsAndMarkerPositions(processedInput, characterModule, languageModules);
                Profiler.EndSample();
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error during inference preparation: {e.Message}");
                TensorPool.Dispose();
                Profiler.EndSample();
                SendPacketCallback(ThespeonDataPacket.CreateErrorPacket(e.Message));
                yield break;
            }
            Profiler.EndSample();

            int nbrWordsNotInLookup = unknownWordsByLanguage.Values.Sum(x => x.Count);

            // Run phonemizer if needed
            if (nbrWordsNotInLookup > 0)
            {
                foreach ((string languageAsJson, List<string> uniqueWords) in unknownWordsByLanguage)
                {
                    ModuleLanguage language = JsonConvert.DeserializeObject<ModuleLanguage>(languageAsJson);
                    Profiler.BeginSample("Thespeon Phonemizer preparation " + language.Iso639_2);
                    LanguageModule currentLanguageModule = languageModules[characterModule.languageModuleIDs[languageAsJson]];
                    int maxInLength = 0;
                    List<List<int>> phonemizerInputs = uniqueWords.Select(word => currentLanguageModule.EncodeGraphemes(word)).ToList();

                    phonemizerInputs.ForEach(wordIDs =>
                    {
                        currentLanguageModule.InsertStringBoundaries(wordIDs);
                        maxInLength = Math.Max(maxInLength, wordIDs.Count);
                    });

                    int batchSize = BuildPhonemizerTensors(maxInLength, phonemizerInputs, currentLanguageModule.EncodePhonemes("<sos>")[0]);
                    string phonemizerMD5 = currentLanguageModule.GetInternalModelID("phonemizer");
                    string phonemizerWorkloadID = Module.GetWorkloadID(phonemizerMD5, BackendType.CPU);
                    InferenceWorkload phonemizerWorkLoad = null;

                    Profiler.EndSample();
                    if (!InferenceWorkloadManager.Instance.AcquireWorkload(phonemizerWorkloadID, ref phonemizerWorkLoad))
                    {
                        yield return new WaitUntil(() => InferenceWorkloadManager.Instance.AcquireWorkload(phonemizerWorkloadID, ref phonemizerWorkLoad));
                        yield return new WaitForEndOfFrame();
                    }

                    bool PhonemizerDoneCondition(int currentIteration)
                    {
                        if(
                            !TensorPool.TryRenameTensor("new_tgt", "tgt") ||
                            !TensorPool.TryRenameTensor("new_finished_indices", "finished_indices") ||
                            !TensorPool.TryRenameTensor("new_mask", "mask_tensor")
                        )
                        {
                            throw new InvalidOperationException("Error renaming phonemizer output tensors. This likely means the phonemizer workload produced unexpected tensor names.");
                        }
                        Tensor<int> srcTensor = TensorPool.GetTensor("src") as Tensor<int>;
                        TensorShape graphemesShape = srcTensor.shape;
                        int phonemizedLimit = graphemesShape[1] * 5;
                        if (currentIteration >= phonemizedLimit || currentIteration >= 200)
                        {
                            LingotionLogger.Warning($"Phonemizer reached max number of iterations {currentIteration}, forcing completion.");

                            Tensor<int> finished_indicesTensor = TensorPool.GetTensor("finished_indices") as Tensor<int>;
                            int[] finished_indices = finished_indicesTensor.DownloadToArray();
                            List<int> new_finished_indices = new();
                            foreach (int index in finished_indices)
                            {
                                if (index <= 0)
                                {
                                    new_finished_indices.Add(-1);
                                }
                                else
                                {
                                    new_finished_indices.Add(index);
                                }
                            }
                            TensorPool.SetTensor("finished_indices", new Tensor<int>(finished_indicesTensor.shape, new_finished_indices.ToArray()));
                            return true;
                        }

                        Tensor<int> num_finishedTensor = TensorPool.GetTensor("num_finished") as Tensor<int>;
                        int[] numFinished = num_finishedTensor.DownloadToArray();
                        return numFinished.Last() >= batchSize;
                    }

                    yield return null;
                    yield return new WaitForEndOfFrame();
                    LingotionLogger.Info($"Starting phonemizer inference for language {language.Iso639_2} with {uniqueWords.Count} unknown words.");
                    var phonemizerInfer = phonemizerWorkLoad.InferAutoregressive(TensorPool, config, PhonemizerDoneCondition, phonemizerWorkloadID, debugName: "phonemizer", budgetAdjustment: 1f);
                    while (phonemizerInfer.MoveNext()) { yield return phonemizerInfer.Current; }

                    InferenceWorkloadManager.Instance.ReleaseWorkload(phonemizerWorkloadID);
                    if (CheckInferenceAbort(phonemizerWorkloadID))
                    {
                        SendPacketCallback(ThespeonDataPacket.CreateErrorPacket("Inference aborted during synthesis"));
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
                        TensorPool.Dispose();
                        Profiler.EndSample();
                        SendPacketCallback(ThespeonDataPacket.CreateErrorPacket(e.Message));
                        yield break;
                    }
                }
            }

            // Prepare input tensors for MetaGraph
            Profiler.BeginSample("Thespeon MetaGraph preparation");
            try
            {
                List<string> originalTexts = processedInput.Segments.Select(segment => segment.Text).ToList();
                (Dictionary<string, Dictionary<string, int>> lengthChangesByLanguage, List<int> globalMarkerPositions) = PhonemizeInput(ref processedInput, characterModule, languageModules, markerPositionsBySegment);
                LingotionLogger.Debug($"Phonemized input for MetaGraph: {processedInput.ToJson()}");
                SetMetaGraphInputTensors(characterModule, processedInput, lengthChangesByLanguage, originalTexts, globalMarkerPositions);
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error preparing tensors for MetaGraph inference: {e.Message} {e.StackTrace}");
                TensorPool.Dispose();
                Profiler.EndSample();
                SendPacketCallback(ThespeonDataPacket.CreateErrorPacket(e.Message));
                yield break;
            }
            Profiler.EndSample();

            yield return null;
            yield return new WaitForEndOfFrame();

            // Create and run the MetaGraphRunner
            LingotionLogger.Info($"Starting MetaGraph execution for character {processedInput.CharacterName}");

            Func<bool> shouldStop = () => TensorPool.IsDisposed();

            var runner = new MetaGraphRunner(
                TensorPool,
                characterModule,
                config,
                SendPacketCallback,
                shouldStop
            );

            if (characterModule.MetaGraph == null)
            {
                string errorMessage = $"Character module {characterModule.ModuleID} does not contain a MetaGraph. Cannot run inference.";
                LingotionLogger.Error(errorMessage);
                SendPacketCallback(ThespeonDataPacket.CreateErrorPacket(errorMessage));
                yield break;
            }
            var runMetaGraph = runner.Run(characterModule.MetaGraph, config.Verbosity == VerbosityLevel.Debug);
            while (runMetaGraph.MoveNext()) { yield return runMetaGraph.Current; }

            if (TensorPool.IsDisposed())
            {
                string errorMessage = "MetaGraph execution failed - tensor pool disposed";
                LingotionLogger.Error(errorMessage);
                SendPacketCallback(ThespeonDataPacket.CreateErrorPacket(errorMessage));
                yield break;
            }

            TensorPool.Dispose();
        
        }

        /// <summary>
        /// Sets up the character and language modules for inference.
        /// </summary>
        private static (CharacterModule, Dictionary<string, LanguageModule>) SetupModules(string characterName, ModuleType moduleType, InferenceConfig config)
        {
            LingotionLogger.Info($"Setting up modules for character {characterName} with module type {moduleType}.");
            Profiler.BeginSample($"Thespeon Loading {characterName} {moduleType}");

            Profiler.BeginSample("Thespeon Get character module entry");
            ModuleEntry targetModuleInfo = ManifestHandler.Instance.GetCharacterModuleEntry(characterName, moduleType);
            Profiler.EndSample();
            Profiler.BeginSample("Thespeon Acquire module");
            CharacterModule characterModule = ModuleHandler.Instance.AcquireModule<CharacterModule>(targetModuleInfo);
            Profiler.EndSample();
            Dictionary<string, LanguageModule> languageModules = new();

            foreach (var kvp in characterModule.languageModuleIDs)
            {
                Profiler.BeginSample($"Thespeon Get language module entry {kvp.Key}");
                string id = kvp.Value;
                targetModuleInfo = ManifestHandler.Instance.GetLanguageModuleEntry(id);
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
                InferenceConfig langConfig = new InferenceConfig
                {

                    PreferredBackendType = BackendType.CPU,
                };
                InferenceWorkloadManager.Instance.RegisterModule(langModule, langConfig);
                Profiler.EndSample();
                Profiler.BeginSample($"Thespeon Register lookup table {langModule.moduleLanguage.Iso639_2}");
                LookupTableHandler.Instance.RegisterLookupTable(langModule);
                Profiler.EndSample();
            }

            Profiler.BeginSample("Thespeon Register character module");
            InferenceWorkloadManager.Instance.RegisterModule(characterModule, config);
            Profiler.EndSample();
            Profiler.EndSample();

            return (characterModule, languageModules);
        }

        /// <summary>
        /// Coroutine to set up modules for inference in a coroutine.
        /// </summary>
        public static IEnumerator SetupModulesCoroutine(string characterName, ModuleType moduleType, InferenceConfig config)
        {
            LingotionLogger.Info($"Setting up modules for character {characterName} with module type {moduleType}.");
            yield return new WaitForEndOfFrame();
            double startTime = Time.realtimeSinceStartupAsDouble;
            Profiler.BeginSample($"Thespeon Loading {characterName} {moduleType}");

            Profiler.BeginSample("Thespeon Get character module entry");
            ModuleEntry targetModuleInfo = ManifestHandler.Instance.GetCharacterModuleEntry(characterName, moduleType);
            Profiler.EndSample();
            if (CheckFrameBreak(startTime, config))
            {
                Profiler.EndSample();
                yield return null;
                yield return new WaitForEndOfFrame();
                Profiler.BeginSample($"Thespeon Loading {characterName} {moduleType}");

                startTime = Time.realtimeSinceStartupAsDouble;
            }
            Profiler.BeginSample("Thespeon Acquire module");
            CharacterModule characterModule = ModuleHandler.Instance.AcquireModule<CharacterModule>(targetModuleInfo);
            Profiler.EndSample();
            Dictionary<string, LanguageModule> languageModules = new();

            foreach (var kvp in characterModule.languageModuleIDs)
            {
                if (CheckFrameBreak(startTime, config))
                {
                    Profiler.EndSample();
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Profiler.BeginSample($"Thespeon Loading {characterName} {moduleType}");
                    startTime = Time.realtimeSinceStartupAsDouble;
                }
                Profiler.BeginSample($"Thespeon Get language module entry {kvp.Key}");
                string id = kvp.Value;
                targetModuleInfo = ManifestHandler.Instance.GetLanguageModuleEntry(id);
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
                    Profiler.BeginSample($"Thespeon Loading {characterName} {moduleType}");
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
                InferenceConfig langConfig = new InferenceConfig
                {

                    PreferredBackendType = BackendType.CPU,
                };
                var registerLang = InferenceWorkloadManager.Instance.RegisterModuleCoroutine(langModule, langConfig);
                while (registerLang.MoveNext()) { yield return registerLang.Current; }
                if (CheckFrameBreak(startTime, config))
                {
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    startTime = Time.realtimeSinceStartupAsDouble;
                }
                var registerLookup = LookupTableHandler.Instance.RegisterLookupTableCoroutine(langModule,
                    () => CheckFrameBreak(startTime, config),
                    () => startTime = Time.realtimeSinceStartupAsDouble);
                while (registerLookup.MoveNext()) { yield return registerLookup.Current; }
                Profiler.BeginSample($"Thespeon Loading {characterName} {moduleType}");
            }

            if (CheckFrameBreak(startTime, config))
            {
                Profiler.EndSample();
                yield return null;
                yield return new WaitForEndOfFrame();
                Profiler.BeginSample($"Thespeon Loading {characterName} {moduleType}");
            }
            Profiler.EndSample();
            var registerChar = InferenceWorkloadManager.Instance.RegisterModuleCoroutine(characterModule, config);
            while (registerChar.MoveNext()) { yield return registerChar.Current; }
        }
        private static bool CheckFrameBreak(double startTime, InferenceConfig config)
        {
            double elapsedTime = Time.realtimeSinceStartupAsDouble - startTime;
            double timeSinceFrameStart = Time.realtimeSinceStartupAsDouble - Time.unscaledTimeAsDouble;
            double timeLeftOfFrame = config.TargetFrameTime - timeSinceFrameStart - config.TargetFrameTime / 10d;
            double timeLeftOfBudget = config.TargetBudgetTime - elapsedTime;

            return timeLeftOfFrame < 0 || timeLeftOfBudget < 0;
        }

        private (Dictionary<string, Dictionary<string, int>>, List<int>) PhonemizeInput(ref ThespeonInput processedInput, CharacterModule characterModule, Dictionary<string, LanguageModule> languageModules, List<List<float>> markerPositionsBySegment)
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
                ModuleLanguage language = ModuleLanguage.BestMatch(ManifestHandler.Instance.GetAllLanguageModuleLanguages(), segmentLanguage.Iso639_2, null);
                if (!languageModules.ContainsKey(characterModule.languageModuleIDs[language.ToJson()]))
                {
                    throw new FileNotFoundException($"Language '{language.ToJson()}' was never imported. Please import a language.");
                }

                RuntimeLookupTable lookupTable = LookupTableHandler.Instance.GetLookupTable(languageModules[characterModule.languageModuleIDs[language.ToJson()]].GetLookupTableID());

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
        private (Dictionary<string, List<string>>, List<List<float>>) FindUnknownWordsAndMarkerPositions(ThespeonInput input, CharacterModule characterModule, Dictionary<string, LanguageModule> languageModules)
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
                ModuleLanguage language = ModuleLanguage.BestMatch(ManifestHandler.Instance.GetAllLanguageModuleLanguages(), segmentLanguage.Iso639_2, null);
                if (!languageModules.ContainsKey(characterModule.languageModuleIDs[language.ToJson()]))
                {
                    throw new FileNotFoundException($"Language '{language.ToJson()}' was never imported. Please import a language.");
                }
                RuntimeLookupTable lookupTable = LookupTableHandler.Instance.GetLookupTable(languageModules[characterModule.languageModuleIDs[language.ToJson()]].GetLookupTableID());
                (string cleanedText, List<int> markerCleanIdx) = StripMarkers(segment.Text);
                MatchCollection matches = TextPreprocessor.WordRegex.Matches(cleanedText);
                foreach (Match match in matches)
                {
                    string word = match.Value;
                    if (!lookupTable.ContainsKey(word))
                    {
                        if (!unknownWordsByLanguage.ContainsKey(language.ToJson()))
                            unknownWordsByLanguage[language.ToJson()] = new();
                        if (unknownWordsByLanguage[language.ToJson()].Contains(word))
                        {
                            continue;
                        }
                        unknownWordsByLanguage[language.ToJson()].Add(word);
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


                TensorPool.SetTensor("src", inputTensor);
                TensorPool.SetTensor("tgt", tgtIndices);
                TensorPool.SetTensor("mask_tensor", eos_mask);
                TensorPool.SetTensor("finished_indices", finished_indices);
                return batchSize;
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error building phonemizer tensors: {e.Message}");
                TensorPool.Dispose();
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
            Tensor<int> castedTensor = TensorPool.GetTensor("tgt") as Tensor<int>;
            int[] phonemeIndices = castedTensor.DownloadToArray();
            Tensor<int> finished_indicesTensor = TensorPool.GetTensor("finished_indices") as Tensor<int>;
            int[] finished_indices = finished_indicesTensor.DownloadToArray();
            RuntimeLookupTable lookupTable = LookupTableHandler.Instance.GetLookupTable(targetLanguageModule.GetLookupTableID());
            int batchSize = uniqueWords.Count;
            for (int j = 0; j < batchSize; j++)
            {
                if (finished_indices[j] <= 0)
                {
                    LingotionLogger.Warning($"Phonemizer did not produce result for word '{uniqueWords[j]}'. Using word as-is.");
                    // Add the word itself as fallback to prevent downstream errors in lengthChangesByLanguage
                    lookupTable.AddOrUpdateDynamicEntry(uniqueWords[j], uniqueWords[j]);
                    continue;
                }
                int start = j * phonemeIndices.Length / batchSize;
                int end = start + finished_indices[j];

                // Bounds check to prevent invalid range access
                if (end <= start + 1 || end > phonemeIndices.Length)
                {
                    LingotionLogger.Warning($"Word was phonemized but with the incorrect range for '{uniqueWords[j]}' (start={start}, end={end}, length={phonemeIndices.Length}). Using word as-is.");
                    // Add the word itself as fallback to prevent downstream errors in lengthChangesByLanguage
                    lookupTable.AddOrUpdateDynamicEntry(uniqueWords[j], uniqueWords[j]);
                    continue;
                }

                List<int> wordIDs = phonemeIndices[(start + 1)..end].ToList();
                string phonemizedWord = targetLanguageModule.DecodePhonemes(wordIDs);
                lookupTable.AddOrUpdateDynamicEntry(uniqueWords[j], phonemizedWord);
            }
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
            if (TensorPool.IsDisposed())
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

        /// <summary>
        /// Sets up input tensors for MetaGraph inference.
        /// Uses different tensor names than the language model: phoneme_keys instead of txt, with text_lengths.
        /// </summary>
        private void SetMetaGraphInputTensors(
            CharacterModule characterModule,
            ThespeonInput input,
            Dictionary<string, Dictionary<string, int>> lengthChangesByLanguage,
            List<string> originalSegmentTexts,
            List<int> requestedMarkerIndices)
        {
            List<int> phonemeKeys = new() { characterModule.EncodePhonemes("⏩").Item1[0] };
            List<int> emotionKeys = new();
            List<int> languageKeys = new();
            int characterKey = characterModule.GetCharacterKey();
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
                (List<int> segmentPhonemes, List<int> filteredIndeces) = characterModule.EncodePhonemes(segment.Text);
                phonemeKeys.AddRange(segmentPhonemes);
                emotionKeys.AddRange(Enumerable.Repeat((int)segmentEmotion, segmentPhonemes.Count + soseosAdjust));
                languageKeys.AddRange(Enumerable.Repeat(characterModule.GetLanguageKey(segmentLanguage), segmentPhonemes.Count + soseosAdjust));
                indecesFiltered.AddRange(filteredIndeces.Select(index => index + runningLength + (i == 0 ? 1 : 0)).ToList());
                i++;
                runningLength += segment.Text.Length + soseosAdjust;
            }

            phonemeKeys.Add(characterModule.EncodePhonemes("⏪").Item1[0]);

            (List<float> speedKeys, List<float> loudnessKeys) = BuildRLECurves(input, lengthChangesByLanguage, originalSegmentTexts);
            indecesFiltered.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in indecesFiltered)
            {
                if (idx < 0 || idx >= speedKeys.Count)
                {
                    LingotionLogger.Error($"Index {idx} is out of bounds for phonemeKeys with length {phonemeKeys.Count}. This should never happen.");
                    continue;
                }
                speedKeys.RemoveAt(idx);
                loudnessKeys.RemoveAt(idx);
            }

            if (phonemeKeys.Count != emotionKeys.Count ||
                phonemeKeys.Count != languageKeys.Count ||
                phonemeKeys.Count != speedKeys.Count ||
                phonemeKeys.Count != loudnessKeys.Count)
            {
                throw new ArgumentException("Mismatch in tensor lengths: " +
                    $"phonemeKeys: {phonemeKeys.Count}, " +
                    $"emotionKeys: {emotionKeys.Count}, " +
                    $"languageKeys: {languageKeys.Count}, " +
                    $"speedKeys: {speedKeys.Count}, " +
                    $"loudnessKeys: {loudnessKeys.Count} ");
            }

            LingotionLogger.Debug($"Phoneme keys: {string.Join(", ", phonemeKeys)}");
            LingotionLogger.Debug($"Emotion keys: {string.Join(", ", emotionKeys)}");
            LingotionLogger.Debug($"Character key: {characterKey}");
            LingotionLogger.Debug($"Language keys: {string.Join(", ", languageKeys)}");

            int textLength = phonemeKeys.Count;

            Tensor<int> phonemeKeysTensor = new(
                new TensorShape(1, textLength), phonemeKeys.ToArray()
            );
            Tensor<int> textLengthsTensor = new(
                new TensorShape(1), new int[] { textLength }
            );
            Tensor<int> emotions = new(
                new TensorShape(1, textLength), emotionKeys.ToArray()
            );
            Tensor<int> characters = new(
                new TensorShape(1, 1), new int[] { characterKey }
            );
            Tensor<int> languages = new(
                new TensorShape(1, textLength), languageKeys.ToArray()
            );
            Tensor<float> speed = new(
                new TensorShape(1, textLength), speedKeys.ToArray()
            );
            Tensor<float> loudness = new(
                new TensorShape(1, textLength), loudnessKeys.ToArray()
            );

            // Create target_phoneme_indices - indices for each phoneme position (initialized to sequence 0, 1, 2, ...)
            // Note: This tensor is 1D (textLength), not 2D (1, textLength)
            int[] targetIndices = new int[textLength];
            for (int idx = 0; idx < textLength; idx++)
            {
                targetIndices[idx] = idx;
            }
            Tensor<int> targetPhonemeIndices = new(
                new TensorShape(requestedMarkerIndices.Count), requestedMarkerIndices.ToArray()
            );

            TensorPool.SetTensor("phoneme_keys", phonemeKeysTensor);
            TensorPool.SetTensor("text_lengths", textLengthsTensor);
            TensorPool.SetTensor("target_phoneme_indices", targetPhonemeIndices);
            TensorPool.SetTensor("emotions", emotions);
            TensorPool.SetTensor("actors.1", characters);
            TensorPool.SetTensor("languages.1", languages);
            TensorPool.SetTensor("speed", speed);
            TensorPool.SetTensor("loudness", loudness);
        }

    }
}