// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.


using UnityEngine;
using Unity.Sentis;
using System.Collections.Generic;
using System.IO;
using System;
using Newtonsoft.Json;
using System.Linq;
using Lingotion.Thespeon.API;
using Lingotion.Thespeon.FileLoader;

namespace Lingotion.Thespeon.Utils
{
    [Serializable]
    public class LanguagePack
    {
        [JsonProperty("type")]
        public string type { get; set; }

        [JsonProperty("platform")]
        public string platform { get; set; }

        [JsonProperty("version")]
        public Version version { get; set; }

        [JsonProperty("modules")]
        public List<Module> modules { get; set; }

        [JsonProperty("files")]
        public Dictionary<string, FileItem> files { get; set; }

        [JsonProperty("compatibility")]
        public Dictionary<string, object> compatibility { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("createtime")]
        public string createtime { get; set; }
    }



    [Serializable]
    public class Module
    {
        [JsonProperty("type")]
        public string type { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("module_id")]
        public string module_id { get; set; }

        [JsonProperty("languages")]
        public List<Language> languages { get; set; }

        [JsonProperty("properties")]
        public Dictionary<string, string> properties { get; set; }

        [JsonProperty("vocabularies")]
        public Vocabularies vocabularies { get; set; }

        [JsonProperty("files")]
        public ModuleFiles files { get; set; }

        [JsonProperty("base_module_id")]
        public string base_module_id { get; set; }
    }

    [Serializable]
    public class Vocabularies
    {
        [JsonProperty("grapheme_vocab")]
        public Dictionary<string, int> grapheme_vocab { get; set; }

        [JsonProperty("phoneme_vocab")]
        public Dictionary<string, int> phoneme_vocab { get; set; }

        [JsonProperty("grapheme_ivocab")]
        public Dictionary<int, string> grapheme_ivocab { get; set; }

        [JsonProperty("phoneme_ivocab")]
        public Dictionary<int, string> phoneme_ivocab { get; set; }
    }

    /* [Serializable]
    public class Language
    {
        [JsonProperty("iso639_2")]
        public string iso639_2 { get; set; }

        [JsonProperty("iso639_3")]
        public string iso639_3 { get; set; }

        [JsonProperty("glottocode")]
        public string glottocode { get; set; }

        [JsonProperty("iso3166_1")]
        public string iso3166_1 { get; set; }

        [JsonProperty("iso3166_2")]
        public string iso3166_2 { get; set; }

        [JsonProperty("customdialect")]
        public string customdialect { get; set; }

        public List<string> GetValues()
        {
            return new List<string> { iso639_2, iso639_3, glottocode, iso3166_1, iso3166_2, customdialect };
        }
        public Dictionary<string,string> GetItems()
        {
            return new Dictionary<string, string>
            {
                {"iso639_2", iso639_2},
                {"iso639_3", iso639_3},
                {"glottocode", glottocode},
                {"iso3166_1", iso3166_1},
                {"iso3166_2", iso3166_2},
                {"customdialect", customdialect}
            };
        } 

        public string toString()
        {
            return "iso639_2: " + iso639_2 + " iso639_3: " + iso639_3 + " glottocode: " + glottocode + " iso3166_1: " + iso3166_1 + " iso3166_2: " + iso3166_2 + " customdialect: " + customdialect;
        }
    }*/

    [Serializable]
    public class ModuleFiles
    {

        [JsonProperty("phonemizer")]
        public string phonemizer { get; set; }

        [JsonProperty("lookuptable")]
        public string lookuptable { get; set; }
    }

    [Serializable]
    public class FileItem
    {

        [JsonProperty("type")]
        public string type { get; set; }

        [JsonProperty("md5sum")]
        public string md5sum { get; set; }

        [JsonProperty("filename")]
        public string filename { get; set; }
    }

    public class LanguagePackService
    {
        public LanguagePack CurrentLanguagePackMeta { get; set; }

        
        public List<string> GetModuleFileNames(string langPackName, string base_module_id)
        {
            LanguagePack pack = LoadLanguagePackFromFile(Path.Combine(RuntimeFileLoader.GetLanguagePacksPath(true), langPackName, $"{langPackName}.json"));
            List<string> result = new List<string>();
            if (pack == null || pack.modules == null)
            {
                Debug.LogError("LanguagePack or its modules are null.");
                return result;
            }

            var module = pack.modules.FirstOrDefault(m => m.base_module_id == base_module_id);
            if (module == null)
            {
                Debug.LogError($"No module found for base_module_id: {base_module_id}");
                return result;
            }

            var fileKeys = new List<string>();
            if (!string.IsNullOrEmpty(module.files.phonemizer))
            {
                fileKeys.Add(module.files.phonemizer);
            }
            if (!string.IsNullOrEmpty(module.files.lookuptable))
            {
                fileKeys.Add(module.files.lookuptable);
            }

            foreach (var key in fileKeys)
            {
                if (pack.files.TryGetValue(key, out var fileItem))
                {
                    // Add ".json" if file is lookup table, otherwise add ".sentis"
                    result.Add(fileItem.filename);
                }
            }

            return result;
        }

        public Vocabularies GetModuleVocabularies(string langPackName, string base_module_id)
    {
        LanguagePack pack = LoadLanguagePackFromFile(Path.Combine(RuntimeFileLoader.GetLanguagePacksPath(true), langPackName, $"{langPackName}.json"));
        if (pack == null || pack.modules == null)
        {
            Debug.LogError("Language pack is null or has no modules.");
            return null;
        }

        var module = pack.modules.FirstOrDefault(m => m.base_module_id == base_module_id);
        if (module == null)
        {
            Debug.LogError($"No module found for base_module_id: {base_module_id}");
            return null;
        }

        return module.vocabularies;
    }
        public List<Language> GetLanguages(string langPackName, string langPackNameModule)
        {

            LanguagePack pack = LoadLanguagePackFromFile(Path.Combine(RuntimeFileLoader.GetLanguagePacksPath(true), langPackName, $"{langPackName}.json"));
            Module module = pack.modules.Find(m => m.base_module_id == langPackNameModule);

            // Debug.Log($"Getting Language list for {langPackNameModule} in {langPackName}");
            return module.languages;
        }

        public LanguagePack LoadLanguagePackFromFile(string filePath)
        {
            string textAsset = RuntimeFileLoader.LoadFileAsString(filePath);
            if (textAsset == null)
            {
                Debug.LogError($"Failed to load {filePath}");
                return null;
            }

            //Debug.Log($"Raw JSON content: {textAsset.text}");

            try
            {
                var langPack = JsonConvert.DeserializeObject<LanguagePack>(textAsset);
                //Debug.Log($"Deserialized LanguagePack: {JsonConvert.SerializeObject(langPack, Formatting.Indented)}");
                return langPack;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to deserialize the file: {filePath}\n{e}");
                return null;
            }
        }

        public Dictionary<string, string> GetIsoSentisPhonemzierDict(LanguagePack langPack)
        {
            var result = new Dictionary<string, string>();

            if (langPack == null || langPack.modules == null)
            {
                Debug.LogWarning("No modules in the LanguagePack JSON.");
                return result;
            }

            foreach (var module in langPack.modules)
            {
                if (module.languages == null || module.files == null)
                    continue;

                string phonemizerKey = module.files.phonemizer;
                if (string.IsNullOrEmpty(phonemizerKey))
                    continue;

                if (!langPack.files.ContainsKey(phonemizerKey))
                {
                    Debug.LogWarning($"No matching record in 'files' for phonemizer key '{phonemizerKey}'");
                    continue;
                }

                FileItem fileItem = langPack.files[phonemizerKey];
                if (fileItem == null || string.IsNullOrEmpty(fileItem.filename))
                    continue;

                string sentisFileName = fileItem.filename;
                if (!sentisFileName.EndsWith(".sentis", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"File '{sentisFileName}' does not end with .sentis but is expected to be a .sentis model.");
                }

                foreach (var lang in module.languages)
                {
                    if (lang == null) continue;

                    string languageCode = !string.IsNullOrEmpty(lang.iso639_2) 
                                        ? lang.iso639_2 
                                        : !string.IsNullOrEmpty(lang.iso639_2) 
                                            ? lang.iso639_2 
                                            : "unknown_lang";



                    if (!result.ContainsKey(languageCode))
                    {
                        result[languageCode]=sentisFileName;
                    }
                }
            }

            return result;
        }

        public Dictionary<string, Vocabularies> BuildLanguageVocab(LanguagePack langPack)
        {
            var languageVocabMap = new Dictionary<string, Vocabularies>();

            if (langPack == null || langPack.modules == null)
                return languageVocabMap;

            foreach (var module in langPack.modules)
            {
                if (module.languages == null || module.vocabularies == null)
                    continue;

                foreach (var lang in module.languages)
                {
                    if (lang == null)
                        continue;

                    string code = !string.IsNullOrEmpty(lang.iso639_2)
                        ? lang.iso639_2
                        : ( !string.IsNullOrEmpty(lang.iso639_2)
                            ? lang.iso639_2 
                            : "unknown_lang" );

                    if (!languageVocabMap.ContainsKey(code))
                    {
                        languageVocabMap[code] = module.vocabularies;
                    }
                }
            }

            return languageVocabMap;
        }

        public Dictionary<string, string> GetISOLookupTableDict(LanguagePack langPack)
        {
            var lookupTableDict = new Dictionary<string, string>();

            if (langPack?.modules == null)
                return lookupTableDict;

            foreach (var module in langPack.modules)
            {
                if (module?.files?.lookuptable == null || module.languages == null)
                    continue;

                string lookupTableFileName = module.files.lookuptable;

                foreach (var lang in module.languages)
                {
                    if (lang == null)
                        continue;

                    // Pick whichever language code is set
                    string languageCode = !string.IsNullOrEmpty(lang.iso639_2)
                        ? lang.iso639_2
                        : (!string.IsNullOrEmpty(lang.iso639_3) ? lang.iso639_3 : "unknown_lang");

                    // Store the lookup table file name keyed by language code
                    if (!lookupTableDict.ContainsKey(languageCode))
                    {
                        lookupTableDict[languageCode] = lookupTableFileName;
                    }
                }
            }

            return lookupTableDict;
        }

        public Dictionary<string, Dictionary<string, string>> CreateLookupDictionaries(Dictionary<string, string> DictLookupTables)
        {
            var lookups = new Dictionary<string, Dictionary<string, string>>();

            foreach (var kvp in DictLookupTables)
            {
                var languageCode = kvp.Key;
                var fileName = kvp.Value;

                // Expect the lookup table file to be a JSON.

                // Expect the lookup table to begin with "lookuptable-" 
                if (fileName.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                {

                    fileName = "lookuptable-" +fileName+ ".json";
                    var path = Path.Combine(Application.streamingAssetsPath,fileName);
                    TextAsset textAsset=null;
                    if (textAsset == null)
                    {
                        Debug.LogError($"Failed to load lookup table from '{fileName}'");
                        continue;
                    }

                    try
                    {
                        // Deserialize the JSON into a Dictionary<string, string>
                        var lookupDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(textAsset.text);
                        lookups[languageCode] = lookupDict;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error deserializing lookup table '{fileName}': {ex}");
                    }
                }
                else
                {
                    Debug.LogWarning($"File '{fileName}' does not end with .json but is expected to be a .json lookup table.");
                }
            }

            return lookups;
        }


        public Dictionary<string, Vocabularies> GetISOVocabsDict(LanguagePack langPack)
        {
            var vocabsDict = new Dictionary<string, Vocabularies>();

            if (langPack?.modules == null)
                return vocabsDict;

            foreach (var module in langPack.modules)
            {
                if (module?.vocabularies == null || string.IsNullOrEmpty(module.base_module_id))
                    continue;

                // Use base_module_id as the key
                string baseModuleId = module.base_module_id;

                // Store the vocabularies in the dictionary
                if (!vocabsDict.ContainsKey(baseModuleId))
                {
                    vocabsDict[baseModuleId] = module.vocabularies;
                }
            }

            return vocabsDict;
        }
    }    
}