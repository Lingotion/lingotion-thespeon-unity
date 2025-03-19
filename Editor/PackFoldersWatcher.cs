// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Lingotion.Thespeon.FileLoader;
using System.Text;


namespace Lingotion.Thespeon
{
    [InitializeOnLoad]
    public static class PackFoldersWatcher
    {
        // -----------------------
        //   FILE WATCHERS
        // -----------------------
        private static FileSystemWatcher actorModulesWatcher;
        private static FileSystemWatcher languagePacksWatcher;

        
        private static readonly string actorModulesPath = RuntimeFileLoader.GetActorPacksPath(true);
        private static readonly string languagePacksPath = RuntimeFileLoader.GetLanguagePacksPath(true);
        private static readonly string mappingInfoJsonPath = RuntimeFileLoader.packMappingsPath;

        // -----------------------
        //   IN-MEMORY CACHES
        // -----------------------
        /// <summary>
        /// Detailed Actor info (one entry per actor username):
        ///   - modules, aggregated languages, aggregated tags, etc.
        /// </summary>
        public static Dictionary<string, ActorData> ActorDataCache 
            = new Dictionary<string, ActorData>();

        /// <summary>
        /// Languages found in the Language Packs only. 
        /// We'll unify nameinenglish from actor data if iso639_2 matches.
        /// </summary>
        public static List<LanguageData> LanguageDataCache 
            = new List<LanguageData>();

        public static event Action OnActorDataUpdated;

        static PackFoldersWatcher()
        {
            // Delay initialization so we don’t do file ops too early
            EditorApplication.delayCall += InitializeWatchers;
        }

        private static void InitializeWatchers()
        {
            SetupWatcher(ref actorModulesWatcher, actorModulesPath);
            SetupWatcher(ref languagePacksWatcher, languagePacksPath);
        }

        private static void SetupWatcher(ref FileSystemWatcher watcher, string folderPath)
        {
            string fullPath = Path.GetFullPath(folderPath);

            if (!Directory.Exists(fullPath))
            {
                Debug.LogWarning($"Folder not found: {fullPath}");
                return;
            }

            watcher = new FileSystemWatcher(fullPath)
            {
                NotifyFilter = NotifyFilters.FileName | 
                            NotifyFilters.DirectoryName | 
                            NotifyFilters.LastWrite,
                Filter = "*.json",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // Hook the events
            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileRenamed;
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Trigger a delayed update on main thread
            EditorApplication.delayCall += UpdateActorDetailedInfo;
            EditorApplication.delayCall += UpdatePackMappings;

        }

        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            EditorApplication.delayCall += UpdateActorDetailedInfo;
            EditorApplication.delayCall += UpdatePackMappings;

        }

        public static void UpdatePackMappings()
        {
            if (!File.Exists(mappingInfoJsonPath))
            {
                StringBuilder sb = new StringBuilder();
                StringWriter sw = new StringWriter(sb);
                
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    writer.Formatting = Formatting.Indented;
                
                    writer.WriteStartObject();
                    writer.WritePropertyName("actorpacks");
                    writer.WriteValue("{}");
                    writer.WritePropertyName("languagepacks");
                    writer.WriteValue("{}");
                    writer.WriteEndObject();
                    File.WriteAllText(mappingInfoJsonPath, writer.ToString());
                }

            }
            Dictionary<string, JObject> mappingInfoObject;
            try
            {
                string jsonData = UpdatePackMappingsInfo();
                mappingInfoObject = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(jsonData);
                if(mappingInfoObject==null) Debug.LogWarning("--------------SHOULDNT BE HERE--------------");
                if(mappingInfoObject==null) mappingInfoObject = new Dictionary<string, JObject>();

            } catch (Exception ex)
            {
                Debug.LogError($"Error reading or parsing JSON file: {ex.Message}");
                mappingInfoObject = new Dictionary<string, JObject>();
            }

            if (!mappingInfoObject.ContainsKey("actorpacks"))
            {
                mappingInfoObject["actorpacks"] = new JObject();
            }
            JObject actorPacks = mappingInfoObject["actorpacks"] as JObject;

            if (!mappingInfoObject.ContainsKey("languagepacks"))
            {
                mappingInfoObject["languagepacks"] = new JObject();
            }
            JObject languagePacks = mappingInfoObject["languagepacks"] as JObject;

            if (actorPacks == null || languagePacks == null)
            {
                Debug.LogError("Invalid JSON structure: Missing 'actorpacks' or 'languagepacks'.");
                return;
            }
            JObject updatedActorPacks = new JObject();
            foreach (var actorPack in actorPacks)
            {
                var actorPackName = actorPack.Key;
                var modules = actorPack.Value as JObject;
                if (modules == null) continue;

                var updatedModules = new JObject();

                // Iterate through each module in the actor pack
                foreach (var module in modules)
                {
                    var updatedLanguagePackMapping = new JObject();
                    string moduleName = module.Key;
                    if(moduleName=="actors")
                    {
                        updatedModules[moduleName] = module.Value;
                        continue;
                    }
                    JArray languagePackIds = module.Value as JArray;
                    if (languagePackIds == null) continue;

                    

                    // Iterate through each language pack in the actor pack module and search for the corresponding language pack name. 
                    foreach (var languagePackId in languagePackIds) 
                    {
                        string id = languagePackId.ToString();
                        // Find the corresponding language pack name.

                        var languagePackName = languagePacks.Properties()
                                .FirstOrDefault(lp => lp.Value.Any(arr => arr.ToString()==id))?.Name;
                        if (languagePackName != null)
                        {
                            updatedLanguagePackMapping[id] = languagePackName;
                        }
                        else
                        {
                            Debug.LogWarning($"Language pack ID '{id}' not found in 'languagepacks'. The pack has not been imported. This is ok but the actor will not support the language of the language pack."); //This should mean the language pack has not been imported. Should be ok but will reduce functionality.
                        }
                    }
                    updatedModules[moduleName] = updatedLanguagePackMapping;
                }
                updatedActorPacks[actorPackName] = updatedModules;
            }

            // Create the final structure
            var finalJsonObject = new JObject
            {
                ["actorpacks"] = updatedActorPacks
            };

            // Serialize and write to the mapping JSON file
            File.WriteAllText(mappingInfoJsonPath, finalJsonObject.ToString(Formatting.Indented));
            AssetDatabase.Refresh();


        }
        private static string UpdatePackMappingsInfo()
        {
            // Start fresh by creating a new JObject to overwrite previous data
            JObject mappingInfoObject = new JObject();
            List<string> jsonsToDump = new List<string>();

            if (Directory.Exists(actorModulesPath))
            {
                foreach (string dir in Directory.GetDirectories(actorModulesPath))
                {
                    jsonsToDump.AddRange(Directory.GetFiles(dir, "*.json"));
                }
            } else
            {
                Debug.LogError($"Directory not found: {actorModulesPath}. Actor Pack Directory has been deleted.");
            }

            if (Directory.Exists(languagePacksPath))
            {
                foreach (string dir in Directory.GetDirectories(languagePacksPath))
                {
                    jsonsToDump.AddRange(Directory.GetFiles(dir, "lingotion-languagepack*.json"));
                }
            } else
            {
                Debug.LogWarning($"Directory not found: {languagePacksPath}. Language pack directory has been deleted.");
            }

            foreach (string jsonPath in jsonsToDump)
            {
                try
                {

                    JObject config = JObject.Parse(RuntimeFileLoader.LoadFileAsString(jsonPath));
                    string packType = config["type"]?.ToString();
                    string packName = config["name"]?.ToString();

                    if (string.IsNullOrEmpty(packType) || string.IsNullOrEmpty(packName))
                    {
                        Debug.LogWarning($"Skipping {jsonPath} due to missing type or name.");
                        continue;
                    }

                    if (packType == "ACTORPACK")
                    {
                        JObject actorPackMapping = new JObject();
                        List<string> actorUsernames = new List<string>();

                        if (config["modules"] is JArray modules)
                        {
                            foreach (var module in modules)
                            {
                                string moduleName = module["name"]?.ToString();
                                if (string.IsNullOrEmpty(moduleName)) continue;

                                JObject phonemizerModules = module["phonemizer_setup"]?["modules"] as JObject;
                                List<string> moduleIds = phonemizerModules?
                                    .Properties()
                                    .Select(p => p.Value["module_id"]?.ToString())
                                    .Where(id => !string.IsNullOrEmpty(id))
                                    .ToList() ?? new List<string>();

                                actorPackMapping[moduleName] = JArray.FromObject(moduleIds);

                                foreach (var actor in module["actor_options"]?["actors"] ?? new JArray())
                                {
                                    string actorUsername = actor["username"]?.ToString();
                                    if (!string.IsNullOrEmpty(actorUsername))
                                        actorUsernames.Add(actorUsername);
                                }
                            }
                        }

                        actorPackMapping["actors"] = JArray.FromObject(actorUsernames);

                        if (!mappingInfoObject.ContainsKey("actorpacks"))
                            mappingInfoObject["actorpacks"] = new JObject();

                        ((JObject)mappingInfoObject["actorpacks"])[packName] = actorPackMapping;
                    }
                    else if (packType == "LANGUAGEPACK")
                    {
                        List<string> moduleIds = config["modules"]?
                            .Select(m => m["base_module_id"]?.ToString())
                            .Where(id => !string.IsNullOrEmpty(id))
                            .ToList() ?? new List<string>();

                        if (!mappingInfoObject.ContainsKey("languagepacks"))
                            mappingInfoObject["languagepacks"] = new JObject();

                        ((JObject)mappingInfoObject["languagepacks"])[packName] = JArray.FromObject(moduleIds);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error processing {jsonPath}: {ex.Message}");
                }
            }

            return JsonConvert.SerializeObject(mappingInfoObject, Formatting.Indented);
            // File.WriteAllText(mappingInfoJsonPath, outputJson);
        }


        // ------------------------------------------------------------------
        //   CORE UPDATE LOGIC (Actor/Language data in memory)
        // ------------------------------------------------------------------
        public static void UpdateActorDetailedInfo()
        {
            // 1) Clear old
            ActorDataCache.Clear();
            LanguageDataCache.Clear();

            // 2) Load Actor Packs (NOT adding to LanguageDataCache, 
            //    but building aggregated tags & languages)
            LoadActorPacks();

            // 3) Load Language Packs (only these go into LanguageDataCache)
            LoadLanguagePacks();

            // 4) Merge nameinenglish from Actor side into LanguageDataCache (iso639_2 match)
            UnifyLanguageNameInEnglish();

            Debug.Log("PackFoldersWatcher: Actor/Language data updated in memory.");
            OnActorDataUpdated?.Invoke();
        }

        /// <summary>
        /// Reads .json from Actor directory, building up ActorDataCache.
        /// Now we parse 'tags' from each module and store them 
        /// in a Dictionary<string,string>, also aggregate them in ActorData.
        /// </summary>
        private static void LoadActorPacks()
        {
            if (!Directory.Exists(actorModulesPath))
            {
                Debug.LogWarning($"Actor modules path not found: {actorModulesPath}");
                return;
            }

            var allJsons = new List<string>();
            foreach (var subdir in Directory.GetDirectories(actorModulesPath))
            {
                allJsons.AddRange(Directory.GetFiles(subdir, "*.json"));
            }
            allJsons.AddRange(Directory.GetFiles(actorModulesPath, "*.json"));

            foreach (string filePath in allJsons)
            {
                try
                {
                    string text = RuntimeFileLoader.LoadFileAsString(filePath);
                    JObject config = JObject.Parse(text);

                    if ((string)config["type"] != "ACTORPACK") 
                        continue;

                    JArray modulesArray = config["modules"] as JArray;
                    if (modulesArray == null) 
                        continue;

                    // For each TTS module
                    foreach (var moduleToken in modulesArray)
                    {
                        if (moduleToken is not JObject moduleObj) 
                            continue;

                        string moduleName = (string)moduleObj["name"] ?? "UnknownModule";

                        // This is the new generic tags approach:
                        // parse "tags" object => Dictionary<string,string>
                        JObject tagsObj = moduleObj["tags"] as JObject;
                        var moduleTags = new Dictionary<string, string>();
                        if (tagsObj != null)
                        {
                            foreach (var tagProp in tagsObj.Properties())
                            {
                                moduleTags[tagProp.Name] = 
                                    tagProp.Value?.ToString() ?? "unknown";
                            }
                        }

                        // We'll parse the model_options -> recording_data_info -> actors
                        JObject modelOptions = moduleObj["model_options"] as JObject;
                        JObject recordingDataInfo = modelOptions?["recording_data_info"] as JObject;
                        JObject actorsDict = recordingDataInfo?["actors"] as JObject;
                        if (actorsDict == null) continue;

                        // For each "actor" node
                        foreach (var actorProperty in actorsDict.Properties())
                        {
                            JObject actorNode = actorProperty.Value as JObject;
                            if (actorNode == null) continue;

                            JObject actorObj = actorNode["actor"] as JObject;
                            if (actorObj == null) continue;

                            string username = (string)actorObj["username"] ?? "UnknownActor";

                            if (!ActorDataCache.TryGetValue(username, out ActorData actorData))
                            {
                                actorData = new ActorData { username = username };
                                ActorDataCache[username] = actorData;
                            }

                            // Build the new ModuleData
                            ModuleData modData = new ModuleData
                            {
                                moduleName = moduleName,
                                tags = moduleTags, // dictionary of (key->value)
                                languages = new List<LanguageData>()
                            };

                            // parse languages
                            JObject languagesDict = actorNode["languages"] as JObject;
                            if (languagesDict != null)
                            {
                                foreach (var langProp in languagesDict.Properties())
                                {
                                    JObject langEntry = langProp.Value as JObject;
                                    JObject languageObj = langEntry?["language"] as JObject;
                                    if (languageObj == null) continue;

                                    LanguageData parsedLang = ParseLanguageObject(languageObj);

                                    modData.languages.Add(parsedLang);

                                    if (!actorData.aggregatedLanguages.Any(x => x.Equals(parsedLang)))
                                    {
                                        actorData.aggregatedLanguages.Add(parsedLang);
                                    }
                                }
                            }

                            // Add the module to the actor
                            actorData.modules.Add(modData);

                            // Now "aggregate" the tags from this module 
                            // into actorData.aggregatedTags
                            foreach (var kvp in moduleTags)
                            {
                                if (!actorData.aggregatedTags.TryGetValue(kvp.Key, 
                                    out HashSet<string> setOfValues))
                                {
                                    setOfValues = new HashSet<string>();
                                    actorData.aggregatedTags[kvp.Key] = setOfValues;
                                }
                                setOfValues.Add(kvp.Value);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing actor pack {filePath}:\n{ex.Message}");
                }
            }
        }

        private static void LoadLanguagePacks()
        {
            if (!Directory.Exists(languagePacksPath))
            {
                Debug.LogWarning($"Language packs path not found: {languagePacksPath}");
                return;
            }

            var allJsons = new List<string>();
            foreach (var subdir in Directory.GetDirectories(languagePacksPath))
            {
                allJsons.AddRange(Directory.GetFiles(subdir, "*.json"));
            }
            allJsons.AddRange(Directory.GetFiles(languagePacksPath, "*.json"));

            foreach (string filePath in allJsons)
            {
                try
                {
                    string text = RuntimeFileLoader.LoadFileAsString(filePath);
                    JObject config = JObject.Parse(text);

                    if ((string)config["type"] != "LANGUAGEPACK") 
                        continue;

                    JArray modulesArray = config["modules"] as JArray;
                    if (modulesArray == null) 
                        continue;

                    foreach (var moduleToken in modulesArray)
                    {
                        if (moduleToken is not JObject moduleObj) 
                            continue;

                        // e.g. "languages": [ {...}, ... ]
                        JArray langsArr = moduleObj["languages"] as JArray;
                        if (langsArr == null) continue;

                        foreach (var langToken in langsArr)
                        {
                            JObject langObj = langToken as JObject;
                            if (langObj == null) continue;

                            LanguageData parsedLang = ParseLanguageObject(langObj);
                            AddOrMergeLanguage(parsedLang);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing language pack {filePath}:\n{ex.Message}");
                }

                UpdatePackMappings();
            }
        }

        /// <summary>
        /// We parse the JSON chunk for each language into our "LanguageData".
        /// </summary>
        private static LanguageData ParseLanguageObject(JObject languageObj)
        {
            var langData = new LanguageData
            {
                iso639_2 = (string)languageObj["iso639_2"],
                iso639_3 = (string)languageObj["iso639_3"],
                glottocode = (string)languageObj["glottocode"],
                customdialect = (string)languageObj["customdialect"],
                iso3166_1 = (string)languageObj["iso3166_1"]
            };

            JObject langCodeObj = languageObj["languagecode"] as JObject;
            if (langCodeObj != null)
            {
                langData.languagecode = new LanguageCode
                {
                    nameinenglish = (string)langCodeObj["nameinenglish"]
                };
            }

            return langData;
        }

        /// <summary>
        /// Insert or unify the given language into our global LanguageDataCache
        /// if we already have a matching iso639_2 + iso3166_1 + customdialect, 
        /// etc. then unify them.
        /// </summary>
        private static void AddOrMergeLanguage(LanguageData newLang)
        {
            var existing = LanguageDataCache.FirstOrDefault(x => x.Equals(newLang));
            if (existing == null)
            {
                LanguageDataCache.Add(newLang);
            }
            else
            {
                // unify nameinenglish if new has it and existing doesn't
                if (string.IsNullOrEmpty(existing.languagecode?.nameinenglish) 
                    && !string.IsNullOrEmpty(newLang.languagecode?.nameinenglish))
                {
                    if (existing.languagecode == null)
                        existing.languagecode = new LanguageCode();

                    existing.languagecode.nameinenglish = newLang.languagecode.nameinenglish;
                }
            }
        }

        /// <summary>
        /// Only merges languages in the LanguageDataCache (from Language Packs).
        /// If an Actor's language has nameinenglish, we copy it if iso639_2 matches 
        /// (or some other rule).
        /// </summary>
        private static void UnifyLanguageNameInEnglish()
        {
            foreach (var langPackLang in LanguageDataCache)
            {
                if (!string.IsNullOrEmpty(langPackLang.languagecode?.nameinenglish))
                    continue; // already has a name

                string iso2 = langPackLang.iso639_2;
                if (string.IsNullOrEmpty(iso2)) 
                    continue;

                // Search all Actors for a language that has the same iso639_2 + a nameinenglish
                foreach (var actorData in ActorDataCache.Values)
                {
                    var match = actorData.aggregatedLanguages
                        .FirstOrDefault(a => 
                            a.iso639_2 == iso2 
                            && !string.IsNullOrEmpty(a.languagecode?.nameinenglish));

                    if (match != null)
                    {
                        if (langPackLang.languagecode == null)
                            langPackLang.languagecode = new LanguageCode();

                        langPackLang.languagecode.nameinenglish = 
                            match.languagecode.nameinenglish;

                        break; // done
                    }
                }
            }
        }
    }

    // ----------------------------
    //   DATA MODELS
    // ----------------------------
    [Serializable]
    public class ActorData
    {
        public string username;

        // Now storing aggregated tags. Key = tagName, Value = set of possible values
        public Dictionary<string, HashSet<string>> aggregatedTags 
            = new Dictionary<string, HashSet<string>>();

        public List<LanguageData> aggregatedLanguages = new List<LanguageData>();
        public List<ModuleData> modules = new List<ModuleData>();
    }

    [Serializable]
    public class ModuleData
    {
        public string moduleName;

        // Instead of a single "quality" field, we now store all tags
        public Dictionary<string, string> tags = new Dictionary<string, string>();

        public List<LanguageData> languages = new List<LanguageData>();
    }

    [Serializable]
    public class LanguageData
    {
        public string iso639_2;
        public string iso639_3;
        public string glottocode;
        public string customdialect;
        public string iso3166_1;

        public LanguageCode languagecode; 

        public override bool Equals(object obj)
        {
            if (obj is LanguageData other)
            {
                return iso639_2 == other.iso639_2
                    && iso639_3 == other.iso639_3
                    && glottocode == other.glottocode
                    && customdialect == other.customdialect
                    && iso3166_1 == other.iso3166_1;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (iso639_2 + iso639_3 + glottocode 
                    + customdialect + iso3166_1).GetHashCode();
        }
    }

    [Serializable]
    public class LanguageCode
    {
        public string nameinenglish;
    }
}