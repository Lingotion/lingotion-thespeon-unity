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

        // Tracks how many Profiler samples this class has opened, so an exception thrown while samples are
        // nested can unwind all of them. Unity force-closes leaked samples at the end of the frame, which
        // makes every later EndSample report as "Non-matching Profiler.EndSample" and buries the real error.
        private static int openProfilerSamples;

        public ThespeonInference(string sessionID, Action<ThespeonDataPacket> packetCallback)
        : base(sessionID, packetCallback)
        {
        }

        private static void BeginProfilerSample(string name)
        {
            Profiler.BeginSample(name);
            openProfilerSamples++;
        }

        private static void EndProfilerSample()
        {
            if (openProfilerSamples <= 0)
            {
                return;
            }
            Profiler.EndSample();
            openProfilerSamples--;
        }

        /// <summary>
        /// Closes every Profiler sample this class still has open. Call from error paths so a throw from
        /// within a nested sample cannot leave the profiler stack unbalanced.
        /// </summary>
        private static void UnwindProfilerSamples()
        {
            while (openProfilerSamples > 0)
            {
                EndProfilerSample();
            }
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
                UnwindProfilerSamples();
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
            BeginProfilerSample("Thespeon Inference preparation");
            CharacterModule characterModule;
            Dictionary<string, LanguageModule> languageModules;
            ThespeonInput processedInput;
            Dictionary<string, List<string>> unknownWordsByLanguage;
            List<List<float>> markerPositionsBySegment;
            try
            {
                BeginProfilerSample("Thespeon Setup modules");
                (characterModule, languageModules) = SetupModules(input.CharacterName, input.ModuleType, config);
                EndProfilerSample();
                #if UNITY_EDITOR
                if(SessionID != Thespeon.Engine.ThespeonComponent.WARMUP_SESSION_ID)
                {
                    Dictionary<Emotion, int> emotionCharCounts = input.Segments.GroupBy(segment => segment.Emotion == Emotion.None ? config.FallbackEmotion : segment.Emotion)
                        .ToDictionary(group => group.Key, group => group.Sum(segment => segment.Text.Length));
                    Dictionary<string, int> moduleData = new()
                    {
                        { DataCacheKeys.nbrSynths.ToString(), 1 }
                    };
                    foreach(var kvp in emotionCharCounts)
                    {
                        moduleData[kvp.Key.ToString().ToLowerInvariant()] = kvp.Value;
                    }
                    InferenceEditorSignals.OnSynthesisDataSignal?.Invoke(new CacheData(characterModule.ModuleID, moduleData));
                }
                #endif
                input.DefaultLanguage ??= config.FallbackLanguage;
                input.DefaultEmotion = input.DefaultEmotion == Emotion.None ? config.FallbackEmotion : input.DefaultEmotion;
                BeginProfilerSample("Thespeon Text preprocessing");
                processedInput = TextPreprocessor.PreprocessInput(input, language => GetTextPreprocessingRules(characterModule, languageModules, language));
                EndProfilerSample();
                BeginProfilerSample("Thespeon Find unknown words");
                (unknownWordsByLanguage, markerPositionsBySegment) = FindUnknownWordsAndMarkerPositions(processedInput, characterModule, languageModules);
                EndProfilerSample();
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error during inference preparation: {e.Message}");
                TensorPool.Dispose();
                UnwindProfilerSamples();
                SendPacketCallback(ThespeonDataPacket.CreateErrorPacket(e.Message));
                yield break;
            }
            EndProfilerSample();

            int nbrWordsNotInLookup = unknownWordsByLanguage.Values.Sum(x => x.Count);

            Func<bool> shouldStop = () => TensorPool.IsDisposed();

            // Run phonemizer if needed
            if (nbrWordsNotInLookup > 0)
            {
                foreach ((string languageAsJson, List<string> uniqueWords) in unknownWordsByLanguage)
                {
                    ModuleLanguage language = JsonConvert.DeserializeObject<ModuleLanguage>(languageAsJson);
                    BeginProfilerSample("Thespeon Phonemizer preparation " + language.Iso639_2);
                    LanguageModule currentLanguageModule = ResolveLanguageModule(characterModule, languageModules, language);
                    int maxInLength = 0;
                    List<List<int>> phonemizerInputs = uniqueWords.Select(word => currentLanguageModule.EncodeGraphemes(word)).ToList();

                    phonemizerInputs.ForEach(wordIDs =>
                    {
                        currentLanguageModule.InsertStringBoundaries(wordIDs);
                        maxInLength = Math.Max(maxInLength, wordIDs.Count);
                    });

                    // The decode loop, its seed tensors and its termination condition all live in the
                    // pack's MetaGraph, so only 'src' - the graph's one external input - is built here.
                    BuildPhonemizerSrcTensor(maxInLength, phonemizerInputs);

                    EndProfilerSample();

                    if (currentLanguageModule.MetaGraph == null)
                    {
                        string errorMessage = $"Language module {currentLanguageModule.ModuleID} does not contain a MetaGraph. Cannot phonemize. Please re-import your language pack.";
                        LingotionLogger.Error(errorMessage);
                        TensorPool.Dispose();
                        UnwindProfilerSamples();
                        SendPacketCallback(ThespeonDataPacket.CreateErrorPacket(errorMessage));
                        yield break;
                    }

                    yield return null;
                    yield return new WaitForEndOfFrame();
                    LingotionLogger.Info($"Starting phonemizer inference for language {language.Iso639_2} with {uniqueWords.Count} unknown words.");

                    MetaGraphRunner phonemizerRunner = new(
                        TensorPool,
                        currentLanguageModule,
                        config,
                        SendPacketCallback,
                        shouldStop
                    );
                    var runPhonemizer = phonemizerRunner.Run(currentLanguageModule.MetaGraph, config.Verbosity == VerbosityLevel.Debug);
                    while (runPhonemizer.MoveNext()) { yield return runPhonemizer.Current; }

                    if (CheckInferenceAbort())
                    {
                        SendPacketCallback(ThespeonDataPacket.CreateErrorPacket("Inference aborted during synthesis"));
                        yield break;
                    }
                    try
                    {
                        BeginProfilerSample("Thespeon Phonemizer resolution");
                        ResolvePhonemizerResult(currentLanguageModule, uniqueWords);
                        EndProfilerSample();
                    }
                    catch (Exception e)
                    {
                        LingotionLogger.Error($"Error resolving phonemizer result: {e.Message} {e.StackTrace}");
                        TensorPool.Dispose();
                        UnwindProfilerSamples();
                        SendPacketCallback(ThespeonDataPacket.CreateErrorPacket(e.Message));
                        yield break;
                    }
                }
            }

            // Prepare input tensors for MetaGraph
            BeginProfilerSample("Thespeon MetaGraph preparation");
            try
            {
                (_, List<int> globalMarkerPositions) = PhonemizeInput(ref processedInput, characterModule, languageModules, markerPositionsBySegment);
                if (LingotionLogger.CurrentLevel >= VerbosityLevel.Debug)
                {
                    LingotionLogger.Debug($"Phonemized input for MetaGraph: {processedInput.ToJson()}");
                }
                SetMetaGraphInputTensors(characterModule, processedInput, globalMarkerPositions);
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error preparing tensors for MetaGraph inference: {e.Message} {e.StackTrace}");
                TensorPool.Dispose();
                UnwindProfilerSamples();
                SendPacketCallback(ThespeonDataPacket.CreateErrorPacket(e.Message));
                yield break;
            }
            EndProfilerSample();

            yield return null;
            yield return new WaitForEndOfFrame();

            // Create and run the MetaGraphRunner
            LingotionLogger.Info($"Starting MetaGraph execution for character {processedInput.CharacterName}");

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
            BeginProfilerSample($"Thespeon Loading {characterName} {moduleType}");

            BeginProfilerSample("Thespeon Get character module entry");
            ModuleEntry targetModuleInfo = ManifestHandler.Instance.GetCharacterModuleEntry(characterName, moduleType);
            EndProfilerSample();
            BeginProfilerSample("Thespeon Acquire module");
            CharacterModule characterModule = ModuleHandler.Instance.AcquireModule<CharacterModule>(targetModuleInfo);
            EndProfilerSample();
            Dictionary<string, LanguageModule> languageModules = new();

            foreach (var kvp in characterModule.languageModuleIDs)
            {
                BeginProfilerSample($"Thespeon Get language module entry {kvp.Key}");
                string id = kvp.Value;
                targetModuleInfo = ManifestHandler.Instance.GetLanguageModuleEntry(id);
                EndProfilerSample();
                if (targetModuleInfo.IsEmpty())
                {
                    LingotionLogger.Warning($"Skipping language {kvp.Key} for character {characterName}: its language module '{id}' is not imported. Text in this language will not be phonemized correctly.");
                    continue;
                }
                BeginProfilerSample($"Thespeon Acquire language module {kvp.Key}");
                LanguageModule langModule = ModuleHandler.Instance.AcquireModule<LanguageModule>(targetModuleInfo);
                languageModules.Add(id, langModule);
                EndProfilerSample();
                BeginProfilerSample($"Thespeon Register language module {langModule.moduleLanguage.Iso639_2}");
                InferenceConfig langConfig = new InferenceConfig
                {
                    PreferredBackendType = BackendType.CPU,
                };
                InferenceWorkloadManager.Instance.RegisterModule(langModule, langConfig);
                EndProfilerSample();
                BeginProfilerSample($"Thespeon Register lookup table {langModule.moduleLanguage.Iso639_2}");
                LookupTableHandler.Instance.RegisterLookupTable(langModule);
                EndProfilerSample();
            }

            BeginProfilerSample("Thespeon Register character module");
            InferenceWorkloadManager.Instance.RegisterModule(characterModule, config);
            EndProfilerSample();
            EndProfilerSample();

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
                    LingotionLogger.Warning($"Skipping language {kvp.Key} for character {characterName}: its language module '{id}' is not imported. Text in this language will not be phonemized correctly.");
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

        /// <summary>
        /// Looks up the loaded language module serving a segment's language, distinguishing a character that
        /// declares no module for the language from one whose module failed to load.
        /// </summary>
        /// <param name="characterModule">Character whose declared language modules are consulted.</param>
        /// <param name="languageModules">Language modules loaded for this character, keyed by module ID.</param>
        /// <param name="language">Language to resolve, already matched against the imported languages.</param>
        /// <returns>The loaded language module for that language.</returns>
        /// <exception cref="FileNotFoundException">No module for the language is declared or loaded.</exception>
        private static LanguageModule ResolveLanguageModule(CharacterModule characterModule, Dictionary<string, LanguageModule> languageModules, ModuleLanguage language)
        {
            if (!characterModule.languageModuleIDs.TryGetValue(language.ToJson(), out string moduleID))
            {
                throw new FileNotFoundException($"Character '{characterModule.ModuleID}' declares no language module for '{language.ToJson()}'. Its available languages are: {string.Join(", ", characterModule.languageModuleIDs.Keys)}.");
            }
            if (!languageModules.TryGetValue(moduleID, out LanguageModule languageModule))
            {
                throw new FileNotFoundException($"Language module '{moduleID}' for '{language.ToJson()}' is not imported. Please import a language module for '{language.Iso639_2}'.");
            }
            return languageModule;
        }

        private static TextPreprocessingRules GetTextPreprocessingRules(CharacterModule characterModule, Dictionary<string, LanguageModule> languageModules, ModuleLanguage segmentLanguage)
        {
            ModuleLanguage language = ModuleLanguage.BestMatch(ManifestHandler.Instance.GetAllLanguageModuleLanguages(), segmentLanguage.Iso639_2, null);
            return RequireTextPreprocessingRules(ResolveLanguageModule(characterModule, languageModules, language));
        }

        private static TextPreprocessingRules RequireTextPreprocessingRules(LanguageModule languageModule)
        {
            if (languageModule.TextPreprocessingRules == null)
            {
                throw new InvalidDataException($"Language module {languageModule.ModuleID} does not contain valid text preprocessing rules. Cannot preprocess text. Please re-import your language pack.");
            }
            return languageModule.TextPreprocessingRules;
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
                LanguageModule segmentLanguageModule = ResolveLanguageModule(characterModule, languageModules, language);
                RuntimeLookupTable lookupTable = LookupTableHandler.Instance.GetLookupTable(segmentLanguageModule.GetLookupTableID());

                List<Word> matches = RequireTextPreprocessingRules(segmentLanguageModule).Words.Split(segment.Text, lookupTable.ContainsKey);
                StringBuilder sb = new(segment.Text);
                int offset = 0;
                int wordCount = 0;
                int markerIdx = 0;
                foreach (Word match in matches)
                {
                    string word = match.Text;
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
                LanguageModule segmentLanguageModule = ResolveLanguageModule(characterModule, languageModules, language);
                RuntimeLookupTable lookupTable = LookupTableHandler.Instance.GetLookupTable(segmentLanguageModule.GetLookupTableID());
                (string cleanedText, List<int> markerCleanIdx) = StripMarkers(segment.Text);
                List<Word> matches = RequireTextPreprocessingRules(segmentLanguageModule).Words.Split(cleanedText, lookupTable.ContainsKey);
                foreach (Word match in matches)
                {
                    string word = match.Text;
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

        /// <summary>
        /// Builds 'src', the phonemizer graph's only external input: one row of grapheme IDs per word,
        /// zero-padded to the longest. The graph supplies its own decode-state tensors ('tgt.in',
        /// 'mask_tensor.in', 'finished_indices.in') from declared defaults sized against 'src' dim 0.
        /// </summary>
        /// <param name="maxInLength">Row width to pad every word out to.</param>
        /// <param name="phonemizerInputs">Grapheme IDs per word, boundary tokens already inserted.</param>
        private void BuildPhonemizerSrcTensor(int maxInLength, List<List<int>> phonemizerInputs)
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

                TensorPool.SetTensor("src", inputTensor);
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
            // The graph's post-actions rename each step's outputs back onto the '.in' names, so these
            // hold the final decode state once the loop has exited.
            Tensor<int> castedTensor = TensorPool.GetTensor("tgt.in") as Tensor<int>;
            int[] phonemeIndices = castedTensor.DownloadToArray();
            Tensor<int> finished_indicesTensor = TensorPool.GetTensor("finished_indices.in") as Tensor<int>;
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

        private static List<float> ComputeMarkerPositions(List<Word> matches, List<int> markerIdx)
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
        /// Dumps every per-token control value fed to the metagraph, so a run can be checked against the
        /// per-segment keypoints that produced it. Only builds its strings at Debug verbosity.
        /// </summary>
        private static void LogControlTensors(
            ThespeonInput input,
            List<Emotion> distinctEmotions,
            List<SegmentControlSpan> controlSpans,
            float[] blending,
            float[] speedValues,
            float[] loudnessValues,
            int textLength,
            int k)
        {
            if (LingotionLogger.CurrentLevel < VerbosityLevel.Debug)
            {
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine($"=== Metagraph control tensors (k={k}, N={textLength}) ===");

            sb.AppendLine("Per-segment keypoints (post curve resampling):");
            for (int s = 0; s < input.Segments.Count; s++)
            {
                ThespeonInputSegment segment = input.Segments[s];
                int tokenCount = s < controlSpans.Count ? controlSpans[s].TokenCount : -1;
                sb.AppendLine($"  [{s}] tokens={tokenCount} custom={segment.IsCustomPronounced}");
                sb.AppendLine($"       emotion  {BlendText(segment.StartEmotion)} -> {BlendText(segment.EndEmotion)}");
                sb.AppendLine($"       speed    {segment.StartSpeed:0.###} -> {segment.EndSpeed:0.###}");
                sb.AppendLine($"       loudness {segment.StartLoudness:0.###} -> {segment.EndLoudness:0.###}");
                sb.AppendLine($"       text     '{segment.Text}'");
            }

            // Token 0 is SOS and token N-1 is EOS; the tokens between belong to the segments in order.
            sb.AppendLine("Per-token values (token: speed loudness | blend):");
            for (int c = 0; c < textLength; c++)
            {
                string label = c == 0 ? "SOS" : c == textLength - 1 ? "EOS" : c.ToString();
                // speed holds two entries per token; they are written identically, so report the first.
                sb.Append($"  {label,5}: spd={speedValues[2 * c]:0.###} lou={loudnessValues[c]:0.###} |");
                for (int r = 0; r < k; r++)
                {
                    sb.Append($" {distinctEmotions[r]}={blending[r * textLength + c]:0.###}");
                }
                sb.AppendLine();
            }

            // A column that does not sum to 1 means the blend was built wrong, which is easy to miss by eye.
            float minSum = float.MaxValue;
            float maxSum = float.MinValue;
            for (int c = 0; c < textLength; c++)
            {
                float sum = 0f;
                for (int r = 0; r < k; r++)
                {
                    sum += blending[r * textLength + c];
                }
                minSum = Math.Min(minSum, sum);
                maxSum = Math.Max(maxSum, sum);
            }
            sb.AppendLine($"Blend column sums range [{minSum:0.####}, {maxSum:0.####}] (should both be 1)");
            sb.AppendLine($"Speed range [{speedValues.Min():0.###}, {speedValues.Max():0.###}], loudness range [{loudnessValues.Min():0.###}, {loudnessValues.Max():0.###}]");
            LingotionLogger.Debug(sb.ToString());
        }

        /// <summary>
        /// Formats an emotion blend as a compact "Emotion:weight" list for logging.
        /// </summary>
        private static string BlendText(Dictionary<Emotion, float> blend)
        {
            if (blend == null || blend.Count == 0)
            {
                return "{}";
            }
            return "{" + string.Join(", ", blend.Select(pair => $"{pair.Key}:{pair.Value:0.###}")) + "}";
        }

        /// <summary>
        /// Per-segment token span and its emotion/speed/loudness boundary keypoints, recorded while encoding.
        /// </summary>
        private struct SegmentControlSpan
        {
            public int TokenCount;
            public float[] StartVec;
            public float[] EndVec;
            public float StartSpeed;
            public float EndSpeed;
            public float StartLoudness;
            public float EndLoudness;
        }

        /// <summary>
        /// Sets up input tensors for MetaGraph inference.
        /// The encoder token sequence is SOS (⏩), every segment's encoded phoneme tokens in order, then EOS (⏪).
        /// Emotion blending, speed and loudness are expanded from each segment's boundary keypoints onto that token layout.
        /// </summary>
        private void SetMetaGraphInputTensors(
            CharacterModule characterModule,
            ThespeonInput input,
            List<int> requestedMarkerIndices)
        {
            List<int> phonemeKeys = new() { characterModule.EncodePhonemes("⏩").Item1[0] };
            List<int> languageKeys = new();

            // Collect the distinct emotions used across all segments' start/end blends in first-appearance order.
            // Each becomes a row in the k x N emotions / emotions_blending tensors. The blends have already been
            // sanitized by the keypoint population pass, so they contain no Emotion.None and sum to 1.
            List<Emotion> distinctEmotions = new();
            Dictionary<Emotion, int> emotionRow = new();
            foreach (ThespeonInputSegment segment in input.Segments)
            {
                foreach (Dictionary<Emotion, float> blend in new[] { segment.StartEmotion, segment.EndEmotion })
                {
                    foreach (Emotion emotion in blend.Keys)
                    {
                        if (!emotionRow.ContainsKey(emotion))
                        {
                            emotionRow[emotion] = distinctEmotions.Count;
                            distinctEmotions.Add(emotion);
                        }
                    }
                }
            }
            int k = distinctEmotions.Count;
            if (k == 0)
            {
                throw new ArgumentException("No emotions were resolved for this input. At least one emotion keypoint is required.");
            }

            float[] MapToVector(Dictionary<Emotion, float> blend)
            {
                float[] vector = new float[k];
                foreach (KeyValuePair<Emotion, float> pair in blend)
                {
                    vector[emotionRow[pair.Key]] = pair.Value;
                }
                return vector;
            }

            List<SegmentControlSpan> controlSpans = new(input.Segments.Count);
            int lastLanguageKey = 0;
            bool isFirstSegment = true;

            foreach (ThespeonInputSegment segment in input.Segments)
            {
                ModuleLanguage segmentLanguage = segment.Language ?? input.DefaultLanguage;
                int languageKey = characterModule.GetLanguageKey(segmentLanguage);
                if (languageKey == -1)
                {
                    throw new ArgumentException($"Character module does not have a valid language key for language: {segmentLanguage.ToJson()}");
                }
                (List<int> segmentPhonemes, _) = characterModule.EncodePhonemes(segment.Text);
                phonemeKeys.AddRange(segmentPhonemes);
                languageKeys.AddRange(Enumerable.Repeat(languageKey, segmentPhonemes.Count + (isFirstSegment ? 1 : 0)));
                isFirstSegment = false;
                lastLanguageKey = languageKey;

                controlSpans.Add(new SegmentControlSpan
                {
                    TokenCount = segmentPhonemes.Count,
                    StartVec = MapToVector(segment.StartEmotion),
                    EndVec = MapToVector(segment.EndEmotion),
                    StartSpeed = segment.StartSpeed,
                    EndSpeed = segment.EndSpeed,
                    StartLoudness = segment.StartLoudness,
                    EndLoudness = segment.EndLoudness
                });
            }

            phonemeKeys.Add(characterModule.EncodePhonemes("⏪").Item1[0]);
            languageKeys.Add(lastLanguageKey);

            int textLength = phonemeKeys.Count;
            if (languageKeys.Count != textLength)
            {
                throw new ArgumentException($"Mismatch in tensor lengths: phonemeKeys: {textLength}, languageKeys: {languageKeys.Count}.");
            }

            // Build the scalar controls and the k x N emotion tensors from the keypoints.
            // emotions_blending[:,c] is the blend at token c (sums to 1), lerped between each segment's boundaries.
            // emotions[:,c] is the constant list of the k distinct emotion IDs. Both are row-major for shape {k, textLength}.
            // Speed follows the encoder's doubled sequence, so each token's interpolated speed is written twice.
            float[] speedValues = new float[2 * textLength];
            float[] loudnessValues = new float[textLength];
            float[] blending = new float[k * textLength];
            for (int idx = 0; idx < speedValues.Length; idx++)
            {
                speedValues[idx] = 1f;
            }
            for (int idx = 0; idx < loudnessValues.Length; idx++)
            {
                loudnessValues[idx] = 1f;
            }

            void WriteControlSample(int tokenIndex, float speedValue, float loudnessValue)
            {
                speedValues[2 * tokenIndex] = speedValue;
                speedValues[2 * tokenIndex + 1] = speedValue;
                loudnessValues[tokenIndex] = loudnessValue;
            }

            if (controlSpans.Count > 0)
            {
                // SOS takes the first segment's start keypoints.
                SegmentControlSpan first = controlSpans[0];
                WriteControlSample(0, first.StartSpeed, first.StartLoudness);
                for (int r = 0; r < k; r++)
                {
                    blending[r * textLength] = first.StartVec[r];
                }

                int cursor = 1;
                foreach (SegmentControlSpan span in controlSpans)
                {
                    int spanLength = span.TokenCount;
                    for (int j = 0; j < spanLength; j++)
                    {
                        float t = spanLength == 1 ? 0.5f : j / (float)(spanLength - 1);
                        WriteControlSample(
                            cursor + j,
                            Mathf.Lerp(span.StartSpeed, span.EndSpeed, t),
                            Mathf.Lerp(span.StartLoudness, span.EndLoudness, t)
                        );
                        for (int r = 0; r < k; r++)
                        {
                            blending[r * textLength + cursor + j] = span.StartVec[r] + (span.EndVec[r] - span.StartVec[r]) * t;
                        }
                    }
                    cursor += spanLength;
                }

                // EOS takes the last segment's end keypoints.
                SegmentControlSpan last = controlSpans[^1];
                WriteControlSample(textLength - 1, last.EndSpeed, last.EndLoudness);
                for (int r = 0; r < k; r++)
                {
                    blending[r * textLength + textLength - 1] = last.EndVec[r];
                }
            }

            int[] emotionIds = new int[k * textLength];
            for (int r = 0; r < k; r++)
            {
                int id = (int)distinctEmotions[r];
                for (int c = 0; c < textLength; c++)
                {
                    emotionIds[r * textLength + c] = id;
                }
            }

            int characterKey = characterModule.GetCharacterKey();
            if (characterKey == -1)
            {
                LingotionLogger.Warning("Character module does not have a valid character key. Falling back to 1.");
                characterKey = 1;
            }

            if (LingotionLogger.CurrentLevel >= VerbosityLevel.Debug)
            {
                LingotionLogger.Debug($"Phoneme keys: {string.Join(", ", phonemeKeys)}");
                LingotionLogger.Debug($"Character key: {characterKey}");
                LingotionLogger.Debug($"Language keys: {string.Join(", ", languageKeys)}");
                LingotionLogger.Debug($"Emotion tensors: k={k}, N={textLength}, emotions=[{string.Join(", ", distinctEmotions.Select(emotion => $"{emotion}({(int)emotion})"))}]");
            }
            LogControlTensors(input, distinctEmotions, controlSpans, blending, speedValues, loudnessValues, textLength, k);

            TensorPool.SetTensor("phoneme_keys", new Tensor<int>(new TensorShape(1, textLength), phonemeKeys.ToArray()));
            TensorPool.SetTensor("emotions", new Tensor<int>(new TensorShape(1, k, textLength), emotionIds));
            TensorPool.SetTensor("emotions_blending", new Tensor<float>(new TensorShape(1, k, textLength), blending));
            TensorPool.SetTensor("actors", new Tensor<int>(new TensorShape(1, 1), new int[] { characterKey }));
            TensorPool.SetTensor("languages", new Tensor<int>(new TensorShape(1, textLength), languageKeys.ToArray()));
            TensorPool.SetTensor("text_lengths", new Tensor<int>(new TensorShape(1), new int[] { textLength }));
            TensorPool.SetTensor("speed", new Tensor<float>(new TensorShape(1, 2 * textLength), speedValues));
            TensorPool.SetTensor("loudness", new Tensor<float>(new TensorShape(1, 1, textLength), loudnessValues));
            TensorPool.SetTensor("target_phoneme_indices", new Tensor<int>(new TensorShape(requestedMarkerIndices.Count), requestedMarkerIndices.ToArray()));
        }

    }
}