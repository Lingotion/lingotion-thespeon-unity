// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using Lingotion.Thespeon.Core;
using System.Collections.Generic;
using Unity.InferenceEngine;
using Newtonsoft.Json.Linq;
using System.Linq;
using Lingotion.Thespeon.Core.IO;
using System.IO;
using System;
using System.Collections;

namespace Lingotion.Thespeon.ActorPack
{
    /// <summary>
    /// Virtual character module, containing character-specific information and specifications.
    /// </summary>
    public class ActorModule : Module
    {
        public readonly int chunk_length;
        public readonly Dictionary<string, string> languageModuleIDs = new();

        private readonly Dictionary<ModuleLanguage, int> _langToLangKey = new();

        private int _actorKey;

        private Dictionary<string, int> _phonemeToEncoderID;

        /// <summary>
        /// Creates a new ActorModule instance.
        /// </summary>
        /// <param name="moduleInfo">Module entry information.</param>
        public ActorModule(ModuleEntry moduleInfo)
            : base(moduleInfo)
        {

            string fileText = RuntimeFileLoader.LoadFileAsString(RuntimeFileLoader.GetActorPackFile(JsonPath));
            JObject actorPackJson = JObject.Parse(fileText);
            string packType = actorPackJson["type"]?.ToString();
            JToken configFiles = actorPackJson["files"];

            if (packType != "ACTORPACK")
            {
                throw new System.Exception($"Config file path {JsonPath} is not a valid actorpack config file.");
            }

            JToken module = null;

            foreach (var entry in actorPackJson["modules"])
            {
                if (entry["actorpack_base_module_id"].ToString() == ModuleID)
                {
                    module = entry;
                    break;
                }
            }

            if (module == null)
            {
                throw new System.Exception($"Module {ModuleID} not found in config file {JsonPath}.");
            }

            JObject vocabs = (JObject)module["phonemes_table"];
            foreach ((string name, JToken vocab) in vocabs)
            {
                switch (name)
                {
                    case "symbol_to_id":
                        _phonemeToEncoderID = vocab.ToObject<Dictionary<string, int>>();
                        continue;

                    default:
                        continue;
                }
            }

            if (_phonemeToEncoderID == null)
            {
                throw new ArgumentException("Phoneme vocabularies are not defined in the module.");
            }

            JObject files = (JObject)module["sentisfiles"];
            Dictionary<string, ModuleFile> internalModuleFiles = new();
            InternalModelMappings = new();
            foreach ((string name, JToken md5) in files)
            {
                if (!InternalModelMappings.ContainsKey(name))
                {
                    InternalModelMappings.Add(name, md5.ToString());
                }
                JToken fileEntry = configFiles[md5.ToString()];
                internalModuleFiles.Add(name, new ModuleFile(Path.Combine(DirectoryPath, fileEntry["filename"].ToString()), md5.ToString()));
            }

            InternalFileMappings = internalModuleFiles;


            try
            {
                chunk_length = module["decoder_chunk_length"].ToObject<int>();
            }
            catch (Exception e)
            {
                throw new FormatException($"Error parsing decoder_chunk_length in {JsonPath}: {e.Message}");
            }

            var phonemizerSetup = module["phonemizer_setup"]["modules"];
            foreach (JProperty kvp in phonemizerSetup.Cast<JProperty>())
            {
                JToken langModule = kvp.Value;
                JArray moduleLanguages = (JArray)langModule["languages"];

                JObject moduleLanguage = (JObject)moduleLanguages.First;
                ModuleLanguage moduleLang = moduleLanguage.ToObject<ModuleLanguage>();

                languageModuleIDs[moduleLang.ToJson()] = langModule["module_id"].ToString();
            }

            JArray languages = (JArray)module["language_options"]?["languages"];


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
                throw new ArgumentException("One or more languages in the actor module do not have a valid language key.");
            }
            JArray actors = (JArray)module["actor_options"]?["actors"];
            _actorKey = actors?.FirstOrDefault()?["actorkey"]?.ToObject<int>() ?? -1;
            if (_actorKey == -1)
            {
                throw new ArgumentException("Actor key not found in the module configuration.");
            }
        }

        /// <summary>
        /// Creates runtime bindings for this specific module setup.
        /// </summary>
        /// <param name="md5s">List of model MD5 strings that are *already loaded*, thus should be skipped.</param>
        /// <returns>A dictionary of model MD5 strings to corresponding runtime binding.</returns>
        public override Dictionary<string, ModelRuntimeBinding> CreateRuntimeBindings(HashSet<string> md5s, BackendType preferredBackendType)
        {
            Dictionary<string, ModelRuntimeBinding> idModelMapping = new();
            Dictionary<string, ModuleFile> standardFiles = InternalFileMappings
                .Where(kvp => !md5s.Contains(kvp.Value.md5))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            foreach ((string internalName, ModuleFile fileInfo) in standardFiles)
            {
                idModelMapping[fileInfo.md5] = CreateSingleRuntimeBinding(internalName, fileInfo, preferredBackendType);
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
            UnityEngine.Profiling.Profiler.BeginSample("Thespeon ActorModule.CreateRuntimeBindingsCoroutine");
            Dictionary<string, ModelRuntimeBinding> idModelMapping = new();
            Dictionary<string, ModuleFile> standardFiles = InternalFileMappings
                .Where(kvp => !md5s.Contains(kvp.Value.md5))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            foreach ((string internalName, ModuleFile fileInfo) in standardFiles)
            {
                idModelMapping[fileInfo.md5] = CreateSingleRuntimeBinding(internalName, fileInfo, preferredBackendType);
                UnityEngine.Profiling.Profiler.EndSample();
                yield return null;
                yield return new WaitForEndOfFrame();
                UnityEngine.Profiling.Profiler.BeginSample($"Thespeon ActorModule.CreateRuntimeBindingsCoroutine");
            }
            onComplete?.Invoke(idModelMapping);
            UnityEngine.Profiling.Profiler.EndSample();
        }

        /// <summary>
        /// Checks if the module is fully included in the provided MD5 set.
        /// </summary>
        /// <param name="md5s">Set of MD5 strings to check against.</param>
        /// <returns>True if the module is included, false otherwise.</returns>
        public override bool IsIncludedIn(HashSet<string> md5s)
        {
            return InternalFileMappings.Values.All(file => md5s.Contains(file.md5));
        }

        /// <summary>
        /// Gets all MD5s of the files in this module.
        /// </summary>
        /// <returns>A set of MD5 strings representing all files in the module.</returns>
        public override HashSet<string> GetAllFileMD5s()
        {
            return InternalFileMappings.Values.Select(file => file.md5).ToHashSet();
        }

        /// <summary>
        /// Encodes phonemes into their corresponding IDs based on the encoder id vocabulary.
        /// </summary>
        /// <param name="phonemes">String of phonemes to encode.</param>
        /// <returns>A tuple containing a list of encoded phoneme IDs and a list of indices for not found phonemes.</returns>
        public (List<int>, List<int>) EncodePhonemes(string phonemes)
        {
            if (_phonemeToEncoderID.TryGetValue(phonemes, out int id))
                return (new List<int> { id }, new());
            List<int> encodedPhonemes = phonemes
                .Select(c =>
                {
                    if (_phonemeToEncoderID.TryGetValue(c.ToString(), out int id))
                    {
                        return id;
                    }
                    else
                    {
                        LingotionLogger.Warning($"Lookup for phoneme '{c}' not found in vocabulary. Phoneme will be filtered out.");
                        return -1;
                    }
                }).Where(id => id != -1)
                .ToList();
            List<int> notFoundIndices = phonemes
                .Select((c, idx) => _phonemeToEncoderID.ContainsKey(c.ToString()) ? -1 : idx)
                .Where(idx => idx != -1)
                .ToList();

            return (encodedPhonemes, notFoundIndices);
        }

        /// <summary>
        /// Gets the language key for a given language. If the language is not found, it tries to find the first matching ISO639-2 code.
        /// </summary>
        /// <param name="language">The language to get the key for.</param>
        /// <returns>The language key if found, otherwise -1.</returns>
        public int GetLanguageKey(ModuleLanguage language)
        {
            if (string.IsNullOrEmpty(language.Iso639_2))
            {
                LingotionLogger.Warning("ISO639-2 code is null or empty.");
                return -1;
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
                    LingotionLogger.Warning($"Language key for ISO639-2 '{language.Iso639_2}' not found in actor module config. Using default language key.");
                    return -1;
                }
                else
                {
                    LingotionLogger.Warning($"Language key for language '{language}' not found. Using default language key.");
                }
            }
            LingotionLogger.Debug($"ActorModule.GetLanguageKey: Language '{language}' has key {res}.");
            return res;
        }

        /// <summary>
        /// Gets the actor key for this module.
        /// </summary>
        /// <returns>The actor key if found, otherwise -1.</returns>
        public int GetActorKey()
        {
            if (_actorKey == -1)
            {
                LingotionLogger.Error("No actorkey found in the actor module.");
            }
            return _actorKey;
        }
        /// <summary>
        /// Creates a single runtime binding for a model based on its internal name and file information.
        /// </summary>
        /// <param name="internalName">The internal name of the model.</param>
        /// <param name="fileInfo">The file information containing the file path and MD5 hash.</param>
        /// <param name="preferredBackendType">The preferred backend type for the model.</param
        /// <returns>A ModelRuntimeBinding containing the loaded model and its worker.</returns>
        private ModelRuntimeBinding CreateSingleRuntimeBinding(string internalName, ModuleFile fileInfo, BackendType preferredBackendType)
        {
            UnityEngine.Profiling.Profiler.BeginSample($"Thespeon ActorModule.CreateSingleRuntimeBinding - {internalName}");
            UnityEngine.Profiling.Profiler.BeginSample($"Thespeon Load Model {internalName}");
            Model model = ModelLoader.Load(RuntimeFileLoader.LoadFileAsStream(RuntimeFileLoader.GetActorPackFile(fileInfo.filePath)));
            UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample($"Thespeon Create Worker {internalName}");
            ModelRuntimeBinding res = new()
            {
                model = model,
                worker = new Worker(model, internalName == "encoder" || internalName == "decoder_preprocess" ? BackendType.CPU : preferredBackendType),
            };
            UnityEngine.Profiling.Profiler.EndSample();
            if (!InternalModelMappings.ContainsKey(internalName)) InternalModelMappings.Add(internalName, fileInfo.md5);
            UnityEngine.Profiling.Profiler.EndSample();
            return res;
        }

    }

}
