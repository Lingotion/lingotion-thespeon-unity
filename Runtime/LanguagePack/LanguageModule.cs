// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Core.IO;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine;
using UnityEngine;

namespace Lingotion.Thespeon.LanguagePack
{
    /// <summary>
    /// Virtual language module.
    /// </summary>
    public class LanguageModule : Module
    {
        public readonly ModuleLanguage moduleLanguage;
        private int lookupTableSize;

        private Dictionary<string, int> _graphemeToID;
        private Dictionary<string, int> _phonemeToID;
        private Dictionary<int, string> _IDToPhoneme;

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageModule"/> class with the specified module information.
        /// </summary>
        /// <param name="moduleInfo">Module entry containing module information.</param>
        /// <exception cref="System.Exception">Thrown if the module is not a valid language pack config file.</exception>
        /// <exception cref="System.Exception">Thrown if the module is not found in the config file.</exception>
        /// <exception cref="System.Exception">Thrown if grapheme or phoneme vocabularies are not defined in the module.</exception>
        public LanguageModule(ModuleEntry moduleInfo) : base(moduleInfo)
        {
            string fileText = RuntimeFileLoader.LoadFileAsString(RuntimeFileLoader.GetLanguagePackFile(JsonPath));
            JObject languagePackJson = JObject.Parse(fileText);
            string packType = languagePackJson["type"]?.ToString();
            JToken configFiles = languagePackJson["files"];

            if (packType != "LANGUAGEPACK")
            {
                throw new System.Exception($"Config file path {JsonPath} is not a valid languagepack config file.");
            }

            JToken module = null;

            foreach (var entry in languagePackJson["modules"])
            {
                if (entry["base_module_id"].ToString() == ModuleID)
                {
                    module = entry;
                    break;
                }
            }

            if (module == null)
            {
                throw new ArgumentException($"Module {ModuleID} not found in the configuration file {JsonPath}. Ensure the configuration file is valid and contains the required module.");
            }

            JObject files = (JObject)module["files"];
            Dictionary<string, ModuleFile> internalModuleFiles = new();
            Dictionary<string, string> internalModelMappings = new();
            foreach ((string name, JToken md5) in files)
            {
                JToken fileEntry = configFiles[md5.ToString()];
                string fileName = fileEntry["filename"].ToString();

                ModuleFile moduleFile = new(Path.Combine(DirectoryPath, fileName), md5.ToString());
                internalModuleFiles.Add(name, moduleFile);
                if (Path.GetExtension(fileName) == ".sentis")
                {
                    internalModelMappings.Add(name, md5.ToString());
                }
                else if (name != "lookuptable")
                {
                    LingotionLogger.Error($"File {fileName} in module {ModuleID} is not a recognized file type.");
                    throw new FileNotFoundException($"File {fileName} in module {ModuleID} is corrupt or of the wrong format. Make sure your language pack is up to date and valid.");
                }

            }
            InternalFileMappings = internalModuleFiles;
            InternalModelMappings = internalModelMappings;

            JObject vocabs = (JObject)module["vocabularies"];
            foreach ((string name, JToken vocab) in vocabs)
            {
                switch (name)
                {
                    case "grapheme_vocab":
                        _graphemeToID = vocab.ToObject<Dictionary<string, int>>();
                        continue;

                    case "grapheme_ivocab":
                        // [DevComment] Inverse grapheme vocabulary is not used in this module, but can be useful for debugging. Not used in production.
                        // /*[DevComment]*/ _IDToGrapheme = vocab.ToObject<Dictionary<int, string>>();
                        continue;

                    case "phoneme_vocab":
                        _phonemeToID = vocab.ToObject<Dictionary<string, int>>();
                        continue;

                    case "phoneme_ivocab":
                        _IDToPhoneme = vocab.ToObject<Dictionary<int, string>>();
                        continue;

                    default:
                        LingotionLogger.Warning($"Unknown vocabulary {name} encountered in Language Module. Ignoring.");
                        continue;
                }
            }

            if (_graphemeToID == null || _phonemeToID == null || _IDToPhoneme == null)
            {
                throw new ArgumentException("Grapheme or phoneme vocabularies are not defined in the module.");
            }

            JArray languages = (JArray)module["languages"];
            moduleLanguage = languages.First.ToObject<ModuleLanguage>();
            lookupTableSize = module["lookuptable_size"].ToObject<int>();
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
                .Where(kvp => !md5s.Contains(kvp.Value.md5) && kvp.Key != "lookuptable")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach ((string internalName, ModuleFile fileInfo) in standardFiles)
            {
                Model model = ModelLoader.Load(RuntimeFileLoader.LoadFileAsStream(RuntimeFileLoader.GetLanguagePackFile(fileInfo.filePath)));
                idModelMapping[fileInfo.md5] = new ModelRuntimeBinding
                {
                    model = model,
                    worker = new Worker(model, BackendType.CPU),
                };
            }
            return idModelMapping;
        }
        public override IEnumerator CreateRuntimeBindingsCoroutine(HashSet<string> md5s, BackendType preferredBackendType, Action<Dictionary<string, ModelRuntimeBinding>> onComplete)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Thespeon LanguageModule.CreateRuntimeBindingsCoroutine");
            Dictionary<string, ModelRuntimeBinding> idModelMapping = new();

            Dictionary<string, ModuleFile> standardFiles = InternalFileMappings
                .Where(kvp => !md5s.Contains(kvp.Value.md5) && kvp.Key != "lookuptable")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach ((string internalName, ModuleFile fileInfo) in standardFiles)
            {
                UnityEngine.Profiling.Profiler.BeginSample($"Thespeon Load Model {internalName}");
                Model model = ModelLoader.Load(RuntimeFileLoader.LoadFileAsStream(RuntimeFileLoader.GetLanguagePackFile(fileInfo.filePath)));
                idModelMapping[fileInfo.md5] = new ModelRuntimeBinding
                {
                    model = model,
                    worker = new Worker(model, BackendType.CPU),
                };
                UnityEngine.Profiling.Profiler.EndSample();
                UnityEngine.Profiling.Profiler.EndSample();
                yield return null;
                yield return new WaitForEndOfFrame();
                UnityEngine.Profiling.Profiler.BeginSample($"Thespeon LanguageModule.CreateRuntimeBindingsCoroutine");
            }
            onComplete?.Invoke(idModelMapping);
            UnityEngine.Profiling.Profiler.EndSample();
        }

        /// <summary>
        /// Checks if the module is fully included in the provided set of existing MD5 strings.
        /// </summary>
        /// <param name="md5s">Set of MD5 strings to check against.</param>
        /// <returns>True if the module is included, false otherwise.</returns>
        public override bool IsIncludedIn(HashSet<string> md5s)
        {
            return InternalFileMappings
                .Where(kvp => kvp.Key != "lookuptable")
                .All(kvp => md5s.Contains(kvp.Value.md5));
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
        /// Encodes graphemes into their corresponding IDs based on the grapheme vocabulary.
        /// </summary>
        /// <param name="graphemes">String of graphemes to encode.</param>
        /// <returns>A list of encoded grapheme IDs.</returns>
        public List<int> EncodeGraphemes(string graphemes)
        {
            return graphemes
                .Select(c =>
                {
                    if (_graphemeToID.TryGetValue(c.ToString(), out int id))
                    {
                        return id;
                    }
                    else
                    {
                        LingotionLogger.Warning($"Lookup for grapheme '{c}' not found in vocabulary. Character will be filtered out.");
                        return -1;
                    }
                }).Where(id => id != -1)
                .ToList();
        }

        /// <summary>
        /// Encodes phonemes into their corresponding IDs based on the phoneme vocabulary.
        /// </summary>
        /// <param name="phonemes">String of phonemes to encode.</param>
        /// <returns>A list of encoded phoneme IDs and a list of indices for not found phonemes.</returns>
        public List<int> EncodePhonemes(string phonemes)
        {
            if (_phonemeToID.TryGetValue(phonemes, out int id))
                return new List<int> { id };

            return phonemes
                .Select(c =>
                {
                    if (_phonemeToID.TryGetValue(c.ToString(), out int id))
                    {
                        return id;
                    }
                    else
                    {
                        LingotionLogger.Warning($"Lookup for phoneme '{c}' not found in vocabulary. Character will be filtered out.");
                        return -1;
                    }
                }).Where(id => id != -1)
                .ToList();
        }

        /// <summary>
        /// Decodes phoneme IDs back into their corresponding phoneme strings based on the phoneme vocabulary.
        /// </summary>
        /// <param name="ids">List of phoneme IDs to decode.</param>
        /// <returns>A string representation of the decoded phonemes.</returns>
        public string DecodePhonemes(List<int> ids)
        {
            return ids.Select(id =>
            {
                if (_IDToPhoneme.TryGetValue(id, out string phoneme))
                {
                    return phoneme;
                }
                else
                {
                    LingotionLogger.Warning($"Lookup for phoneme id '{id}' not found in vocabulary. Character will be filtered out.");
                    return null;
                }
            }).Where(phoneme => phoneme != null).Aggregate(string.Empty, (current, next) => current + next);
        }

        /// <summary>
        /// Inserts start-of-sequence and end-of-sequence tokens into the provided list of word IDs.
        /// </summary>
        /// <param name="wordIDs">List of word IDs to modify.</param>
        public void InsertStringBoundaries(List<int> wordIDs)
        {
            _graphemeToID.TryGetValue("<sos>", out int sosID);
            _graphemeToID.TryGetValue("<eos>", out int eosID);
            wordIDs.Insert(0, sosID);
            wordIDs.Add(eosID);
        }

        /// <summary>
        /// Get lookup table as Dictionary for language modules.
        /// </summary>
        /// <returns> Lookup table as Dictionary</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the lookup table is not found in the internal file mappings.</exception>
        public Dictionary<string, string> GetLookupTable()
        {
            if (!InternalFileMappings.TryGetValue("lookuptable", out var file))
            {
                throw new KeyNotFoundException("Lookup table not found in internal file mappings.");
            }

            string lookupTablePath = RuntimeFileLoader.GetLanguagePackFile(file.filePath);

            string jsonContent = RuntimeFileLoader.LoadFileAsString(lookupTablePath);

            try
            {
                Dictionary<string, string> lookupDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);
                lookupDict.Remove("license_terms");
                return lookupDict;
            }
            catch (Exception ex)
            {
                LingotionLogger.Error($"Failed to parse lookup table JSON: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get lookup table as Dictionary for language modules batchwise, yielding whenever necessary.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown if the lookup table is not found in the internal file mappings.</exception>
        public IEnumerator GetLookupTableCoroutine(Action<Dictionary<string, string>> onComplete, Func<bool> yieldCondition, Action onYield)
        {
            if (!InternalFileMappings.TryGetValue("lookuptable", out var file))
            {
                throw new KeyNotFoundException("Lookup table not found in internal file mappings.");
            }

            string lookupTablePath = RuntimeFileLoader.GetLanguagePackFile(file.filePath);
            Dictionary<string, string> lookupDict = new(lookupTableSize);
            yield return RuntimeFileLoader.LoadLookupTable(lookupTablePath, dict =>
            {
                foreach (var kvp in dict)
                {
                    lookupDict[kvp.Key] = kvp.Value;
                }
            }, yieldCondition, onYield);
            onComplete?.Invoke(lookupDict);
        }

        /// <summary>
        /// Gets the ID of the lookup table file.
        /// </summary>
        /// <returns>The MD5 of the lookup table file.</returns>
        public string GetLookupTableID()
        {
            if (!InternalFileMappings.TryGetValue("lookuptable", out var file))
            {
                throw new KeyNotFoundException("Lookup table not found in internal file mappings.");
            }
            return file.md5;
        }

    }

}
