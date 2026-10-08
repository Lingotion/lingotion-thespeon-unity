// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEngine;
using Lingotion.Thespeon.Core;
using System.Collections.Generic;
using Unity.InferenceEngine;
using Newtonsoft.Json.Linq;
using System.Linq;
using Lingotion.Thespeon.Core.IO;
using System;
using System.Collections;

namespace Lingotion.Thespeon.Character
{
    /// <summary>
    /// Virtual character module, containing character-specific information and specifications.
    /// </summary>
    public class CharacterModule : Module
    {
        public readonly Dictionary<string, string> languageModuleIDs = new();

        private readonly Dictionary<ModuleLanguage, int> _langToLangKey = new();

        private int _characterKey;

        private Dictionary<string, int> _phonemeToEncoderID;

        /// <summary>
        /// Creates a new CharacterModule instance.
        /// </summary>
        /// <param name="moduleInfo">Module entry information.</param>
        public CharacterModule(ModuleEntry moduleInfo)
            : base(moduleInfo)
        {
            string configPath = RuntimeFileLoader.GetRuntimePath(JsonPath);
            string fileText = RuntimeFileLoader.LoadFileAsString(configPath);
            JObject config = JObject.Parse(fileText);

            // Validate config type is "lara"
            ConfigFormatDetector.ValidateCharacterConfig(config);

            // Parse phonemes_table (symbol_to_id vocabulary)
            JObject vocabs = (JObject)config["phonemes_table"];
            if (vocabs != null)
            {
                JToken symbolToId = vocabs["symbol_to_id"];
                if (symbolToId != null)
                {
                    _phonemeToEncoderID = symbolToId.ToObject<Dictionary<string, int>>();
                }
            }

            if (_phonemeToEncoderID == null)
            {
                throw new ArgumentException("Phoneme vocabularies (phonemes_table.symbol_to_id) are not defined in the config.");
            }

            // Parse files array with {name, md5, extension}
            ParseModuleFiles((JArray)config["files"], ext => ext == "onnx" || ext == "sentis");

            // Parse phonemizer_setup.modules array (new format uses array instead of object)
            JArray phonemizerModules = (JArray)config["phonemizer_setup"]?["modules"];
            if (phonemizerModules != null)
            {
                foreach (JObject phonemizerEntry in phonemizerModules)
                {
                    string iso639_2 = phonemizerEntry["iso639_2"]?.ToString();
                    string baseModuleId = phonemizerEntry["base_module_id"]?.ToString();

                    if (!string.IsNullOrEmpty(iso639_2) && !string.IsNullOrEmpty(baseModuleId))
                    {
                        ModuleVersion? requiredVersion = ModuleVersion.TryParse(phonemizerEntry["version"], out ModuleVersion parsedVersion)
                            ? (ModuleVersion?)parsedVersion
                            : null;

                        // Create a ModuleLanguage with just the iso639_2 code
                        ModuleLanguage moduleLang = new(iso639_2, null, null, null, null, null);
                        languageModuleIDs[moduleLang.ToJson()] = ResolveImportedLanguageModuleID(iso639_2, baseModuleId, requiredVersion);
                    }
                }
            }

            // Parse languages array at root level
            JArray languages = (JArray)config["languages"];
            _langToLangKey = languages?.Select(lang =>
            {
                ModuleLanguage language = new(
                    lang["iso639_2"]?.ToString(),
                    lang["iso639_3"]?.ToString(),
                    lang["glottocode"]?.ToString(),
                    lang["customdialect"]?.ToString(),
                    lang["iso3166_1"]?.ToString(),
                    lang["iso3166_2"]?.ToString()
                );
                return new KeyValuePair<ModuleLanguage, int>(language, lang["languagekey"]?.ToObject<int>() ?? -1);
            }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<ModuleLanguage, int>();

            if (_langToLangKey.Any(kvp => kvp.Value == -1))
            {
                throw new ArgumentException("One or more languages in the character module do not have a valid language key.");
            }

            // Parse character info (replaces character_options.characters)
            JObject character = (JObject)config["character"];
            _characterKey = character?["characterkey"]?.ToObject<int>() ?? -1;
            if (_characterKey == -1)
            {
                throw new ArgumentException("Character key (character.characterkey) not found in the module configuration.");
            }

            // Load MetaGraph from file mapping (not hardcoded meta.bin)
            TryLoadMetaGraph();
        }

        /// <summary>
        /// Resolves the language module a character pins to one that is actually imported. A module ID
        /// encodes the content it was built from, so the same language rebuilt gets a different ID and the
        /// pinned one stops resolving. Rather than lose the language entirely, an imported module serving it
        /// at a compatible version is substituted. Only the requested major version is accepted, since a
        /// differing major may carry a phonemizer this character was never built against.
        /// </summary>
        /// <param name="iso639_2">ISO 639-2 code the pinned module serves.</param>
        /// <param name="requestedID">Language module ID the character was built against.</param>
        /// <param name="requestedVersion">Version of that module, or null if the character pins no version.</param>
        /// <returns>An imported module ID serving the language, or <paramref name="requestedID"/> if none does.</returns>
        private static string ResolveImportedLanguageModuleID(string iso639_2, string requestedID, ModuleVersion? requestedVersion)
        {
            if (ManifestHandler.Instance.HasLanguageModule(requestedID))
            {
                return requestedID;
            }

            string fallbackID = ManifestHandler.Instance.FindLanguageModuleIDForISO(iso639_2, requestedVersion);
            if (string.IsNullOrEmpty(fallbackID))
            {
                string versionRequirement = requestedVersion.HasValue ? $" at version {requestedVersion.Value.Major}.x" : string.Empty;
                LingotionLogger.Warning($"No imported language module serves '{iso639_2}'{versionRequirement}. Import a '{iso639_2}' language module{versionRequirement} to use this language.");
                return requestedID;
            }

            string fallbackVersion = ManifestHandler.Instance.GetLanguageModuleVersion(fallbackID);
            LingotionLogger.Warning($"Language module '{requestedID}' requested for '{iso639_2}' is not imported. Falling back to imported module '{fallbackID}' (version {fallbackVersion}). Pronunciation may differ from what this character was built against.");
            return fallbackID;
        }

        /// <summary>
        /// Creates runtime bindings for this specific module setup.
        /// </summary>
        /// <param name="workloadIDs">List of workloadIDs strings that are *already loaded*, thus should be skipped.</param>
        /// <returns>A dictionary of workloadIDs strings to corresponding runtime binding.</returns>
        public override Dictionary<string, ModelRuntimeBinding> CreateRuntimeBindings(HashSet<string> workloadIDs, BackendType preferredBackendType)
        {
            Dictionary<string, ModelRuntimeBinding> idModelMapping = new();
            Dictionary<string, ModuleFile> standardFiles = InternalFileMappings
                .Where(kvp => !workloadIDs.Contains(Module.GetWorkloadID(kvp.Value.md5, preferredBackendType)) && kvp.Key != "metagraph")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            foreach ((string internalName, ModuleFile fileInfo) in standardFiles)
            {
                string workloadID = Module.GetWorkloadID(fileInfo.md5, preferredBackendType);
                idModelMapping[workloadID] = CreateSingleRuntimeBinding(internalName, fileInfo, preferredBackendType);
            }
            return idModelMapping;
        }

        /// <summary>
        /// Creates runtime bindings for this specific module setup, yielding between each binding creation to avoid blocking the main thread.
        /// </summary>
        /// <param name="md5s">List of model MD5 strings that are *already loaded*, thus should be skipped.</param>
        /// <param name="preferredBackendType">The preferred backend type for the models.</param>
        /// <param name="onComplete">Callback to invoke when all bindings are created.</param>
        /// <returns>A dictionary of model MD5 strings to corresponding runtime binding.</returns>
        public override IEnumerator CreateRuntimeBindingsCoroutine(HashSet<string> md5s, BackendType preferredBackendType, Action<Dictionary<string, ModelRuntimeBinding>> onComplete)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Thespeon CharacterModule.CreateRuntimeBindingsCoroutine");
            Dictionary<string, ModelRuntimeBinding> idModelMapping = new();
            Dictionary<string, ModuleFile> standardFiles = InternalFileMappings
                .Where(kvp => !md5s.Contains(Module.GetWorkloadID(kvp.Value.md5, preferredBackendType)) && kvp.Key != "metagraph")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            foreach ((string internalName, ModuleFile fileInfo) in standardFiles)
            {
                string workloadID = Module.GetWorkloadID(fileInfo.md5, preferredBackendType);
                idModelMapping[workloadID] = CreateSingleRuntimeBinding(internalName, fileInfo, preferredBackendType);
                UnityEngine.Profiling.Profiler.EndSample();
                yield return null;
                yield return new WaitForEndOfFrame();
                UnityEngine.Profiling.Profiler.BeginSample($"Thespeon CharacterModule.CreateRuntimeBindingsCoroutine");
            }
            onComplete?.Invoke(idModelMapping);
            UnityEngine.Profiling.Profiler.EndSample();
        }


        /// <summary>
        /// Encodes phonemes into their corresponding IDs based on the encoder id vocabulary.
        /// </summary>
        /// <param name="phonemes">String of phonemes to encode.</param>
        /// <returns>A tuple containing a list of encoded phoneme IDs and a list of char indices for not found phonemes.</returns>
        public (List<int>, List<int>) EncodePhonemes(string phonemes)
        {
            if (_phonemeToEncoderID.TryGetValue(phonemes, out int wholeID))
            {
                return (new List<int> { wholeID }, new());
            }
            List<int> encodedPhonemes = new(phonemes.Length);
            List<int> notFoundIndices = new();
            int index = 0;
            while (index < phonemes.Length)
            {
                int length = char.IsSurrogatePair(phonemes, index) ? 2 : 1;
                string symbol = phonemes.Substring(index, length);
                if (_phonemeToEncoderID.TryGetValue(symbol, out int id))
                {
                    encodedPhonemes.Add(id);
                }
                else
                {
                    LingotionLogger.Warning($"Unknown phonetic symbol '{symbol}' present in input! Phoneme will be ignored. Please ensure your input contains valid symbols.");
                    notFoundIndices.Add(index);
                }
                index += length;
            }

            return (encodedPhonemes, notFoundIndices);
        }

        /// <summary>
        /// Gets the language key for a given language. If the language is not found, it tries to find the closest supported key.
        /// </summary>
        /// <param name="language">The language to get the key for.</param>
        /// <returns>The language key if found, otherwise defaults to the first language found spoken by the current character.</returns>
        public int GetLanguageKey(ModuleLanguage language)
        {
            if (string.IsNullOrEmpty(language.Iso639_2))
            {
                LingotionLogger.Warning($"ISO639-2 code is null or empty for {language}. Defaulting to first language found spoken by the current character. Please ensure the language has a valid ISO639-2 code.");
                return _langToLangKey.Values.FirstOrDefault();
            }
            int res = _langToLangKey
                .Where(kvp => kvp.Key.Equals(language))
                .Select(kvp => kvp.Value)
                .DefaultIfEmpty(-1).First();

            if (res == -1)
            {
                res = _langToLangKey
                .Where(kvp => kvp.Key.Iso639_2 == language.Iso639_2)
                .Select(kvp => kvp.Value)
                .DefaultIfEmpty(-1).First();
                if (res == -1)
                {
                    LingotionLogger.Warning($"Language with ISO639-2 '{language.Iso639_2}' was not found in character module config. Defaulting to first language found spoken by the current character. Please ensure the language has a valid ISO639-2 code.");
                    return _langToLangKey.Values.FirstOrDefault();
                }
                else
                {
                    LingotionLogger.Warning($"Language key for language '{language}' not found. Defaulting to first found in the same ISO639-2 language family.");
                }
            }
            LingotionLogger.Debug($"CharacterModule.GetLanguageKey: Language '{language}' has key {res}.");
            return res;
        }

        /// <summary>
        /// Gets the character key for this module.
        /// </summary>
        /// <returns>The character key if found, otherwise -1.</returns>
        public int GetCharacterKey()
        {
            if (_characterKey == -1)
            {
                LingotionLogger.Error("Module is malformed and contains no characters. This is due to a corrupt file on import. Please try re-downloading and re-importing the module or contact support if the problem persists.");
            }
            return _characterKey;
        }
        /// <summary>
        /// Creates a single runtime binding for a model based on its internal name and file information.
        /// </summary>
        /// <param name="internalName">The internal name of the model.</param>
        /// <param name="fileInfo">The file information containing the file path and MD5 hash.</param>
        /// <param name="preferredBackendType">The preferred backend type for the model.</param>
        /// <returns>A ModelRuntimeBinding containing the loaded model and its worker.</returns>
        private ModelRuntimeBinding CreateSingleRuntimeBinding(string internalName, ModuleFile fileInfo, BackendType preferredBackendType)
        {
            UnityEngine.Profiling.Profiler.BeginSample($"Thespeon CharacterModule.CreateSingleRuntimeBinding - {internalName}");
            UnityEngine.Profiling.Profiler.BeginSample($"Thespeon Load Model {internalName}");
            Model model = ModelLoader.Load(RuntimeFileLoader.LoadFileAsStream(fileInfo.filePath));
            UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample($"Thespeon Create Worker {internalName}");
            ModelRuntimeBinding res = new()
            {
                model = model,
                worker = new Worker(model, preferredBackendType),
            };
            UnityEngine.Profiling.Profiler.EndSample();

            if (!InternalModelMappings.ContainsKey(internalName)) InternalModelMappings.Add(internalName, fileInfo.md5);
            UnityEngine.Profiling.Profiler.EndSample();
            return res;
        }

    }

}
