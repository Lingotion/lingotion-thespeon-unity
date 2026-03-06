// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

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

namespace Lingotion.Thespeon.Language
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
        /// <exception cref="NotSupportedException">Thrown if the module is not a valid phonemizer config file.</exception>
        /// <exception cref="ArgumentException">Thrown if grapheme or phoneme vocabularies are not defined in the module.</exception>
        public LanguageModule(ModuleEntry moduleInfo) : base(moduleInfo)
        {
            string configPath = RuntimeFileLoader.GetRuntimePath(JsonPath);
            string fileText = RuntimeFileLoader.LoadFileAsString(configPath);
            JObject config = JObject.Parse(fileText);

            // Validate config type is "phonemizer"
            ConfigFormatDetector.ValidateLanguageConfig(config);

            // Parse files array with {name, md5, extension}
            ParseModuleFiles((JArray)config["files"], ext => ext == "onnx" || ext == "sentis");

            // Parse vocabularies directly from config root
            JObject vocabs = (JObject)config["vocabularies"];
            if (vocabs != null)
            {
                foreach ((string name, JToken vocab) in vocabs)
                {
                    switch (name)
                    {
                        case "grapheme_vocab":
                            _graphemeToID = vocab.ToObject<Dictionary<string, int>>();
                            continue;

                        case "grapheme_ivocab":
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
            }

            if (_graphemeToID == null || _phonemeToID == null || _IDToPhoneme == null)
            {
                throw new ArgumentException("Grapheme or phoneme vocabularies are not defined in the module.");
            }

            // Parse languages array at root level
            JArray languages = (JArray)config["languages"];
            if (languages == null || !languages.Any())
            {
                throw new ArgumentException("Languages are not defined in the language module config.");
            }
            moduleLanguage = languages.First.ToObject<ModuleLanguage>();

            // Parse lookuptable_size at root level
            JToken lookupTableSizeToken = config["lookuptable_size"];
            if (lookupTableSizeToken == null)
            {
                throw new ArgumentException("lookuptable_size is not defined in the language module configuration.");
            }
            lookupTableSize = lookupTableSizeToken.ToObject<int>();
            if (lookupTableSize <= 0)
            {
                throw new ArgumentException("lookuptable_size must be a positive integer in the language module configuration.");
            }
        }

        /// <summary>
        /// Creates runtime bindings for this specific module setup.
        /// </summary>
        /// <param name="workloadIDs">List of workloadIDs strings that are *already loaded*, thus should be skipped.</param>
        /// <returns>A dictionary of workloadIDs strings to corresponding runtime binding.</returns>
        /// <exception cref="NotSupportedException">Thrown if a non-CPU backend is specified, as LanguageModule currently only supports CPU.</exception>
        public override Dictionary<string, ModelRuntimeBinding> CreateRuntimeBindings(HashSet<string> workloadIDs, BackendType preferredBackendType)
        {
            if (preferredBackendType != BackendType.CPU)
            {
                throw new NotSupportedException($"LanguageModule currently only supports CPU backend. Attempted to create runtime binding for backend type {preferredBackendType}.");
            }
            Dictionary<string, ModelRuntimeBinding> idModelMapping = new();

            Dictionary<string, ModuleFile> standardFiles = InternalFileMappings
                .Where(kvp => !workloadIDs.Contains(Module.GetWorkloadID(kvp.Value.md5, preferredBackendType)) && kvp.Key != "lookuptable")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach ((string internalName, ModuleFile fileInfo) in standardFiles)
            {
                Model model = ModelLoader.Load(RuntimeFileLoader.LoadFileAsStream(fileInfo.filePath));
                string workloadID = Module.GetWorkloadID(fileInfo.md5, preferredBackendType);
                idModelMapping[workloadID] = new ModelRuntimeBinding
                {
                    model = model,
                    worker = new Worker(model, preferredBackendType),
                };
            }
            return idModelMapping;
        }

        public override IEnumerator CreateRuntimeBindingsCoroutine(HashSet<string> md5s, BackendType preferredBackendType, Action<Dictionary<string, ModelRuntimeBinding>> onComplete)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Thespeon LanguageModule.CreateRuntimeBindingsCoroutine");
            Dictionary<string, ModelRuntimeBinding> idModelMapping = new();

            Dictionary<string, ModuleFile> standardFiles = InternalFileMappings
                .Where(kvp => !md5s.Contains(Module.GetWorkloadID(kvp.Value.md5, preferredBackendType)) && kvp.Key != "lookuptable")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach ((string internalName, ModuleFile fileInfo) in standardFiles)
            {
                UnityEngine.Profiling.Profiler.BeginSample($"Thespeon Load Model {internalName}");
                Model model = ModelLoader.Load(RuntimeFileLoader.LoadFileAsStream(fileInfo.filePath));
                if (preferredBackendType != BackendType.CPU)
                {
                    throw new NotSupportedException($"LanguageModule currently only supports CPU backend. Attempted to create runtime binding for backend type {preferredBackendType}.");
                }
                string workloadID = Module.GetWorkloadID(fileInfo.md5, preferredBackendType);
                idModelMapping[workloadID] = new ModelRuntimeBinding
                {
                    model = model,
                    worker = new Worker(model, preferredBackendType),
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

            string jsonContent = RuntimeFileLoader.LoadFileAsString(file.filePath);

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

            Dictionary<string, string> lookupDict = new(lookupTableSize);
            var loadLookup = RuntimeFileLoader.LoadLookupTable(file.filePath, dict =>
            {
                foreach (var kvp in dict)
                {
                    lookupDict[kvp.Key] = kvp.Value;
                }
            }, yieldCondition, onYield);
            while (loadLookup.MoveNext()) { yield return loadLookup.Current; }
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
