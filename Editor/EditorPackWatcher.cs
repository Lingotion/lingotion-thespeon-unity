// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Core.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Newtonsoft.Json.Linq;
using System.Linq;
using Newtonsoft.Json;
using System.Text;

namespace Lingotion.Thespeon.Editor
{
    /// <summary>
    /// Watches for changes to the pack directories and updates the manifest.
    /// </summary>
    [InitializeOnLoad]
    public class EditorPackWatcher :IPreprocessBuildWithReport
    {
        private static FileSystemWatcher _runtimeFilesWatcher;

        private static readonly string _actorPacksPath = RuntimeFileLoader.GetActorPacksPath(true);
        private static readonly string _languagePacksPath = RuntimeFileLoader.GetLanguagePacksPath(true);
        private static readonly string _mappingInfoJsonPath = RuntimeFileLoader.PackManifestPath;

        public int callbackOrder => 1;

        /// <summary>
        /// Called before build. Performs final write to pack manifest.
        /// </summary>
        /// <param name="report"></param>
        public void OnPreprocessBuild(BuildReport report)
        {
            UpdatePackMappingsInfo();
        }

        static EditorPackWatcher()
        {
            EditorApplication.delayCall += VerifyRuntimeDirectory;
            EditorApplication.delayCall += InitializeWatchers;
        }

        private static void InitializeWatchers()
        {
            SetupWatcher(ref _runtimeFilesWatcher, RuntimeFileLoader.RelativeRuntimeFiles);
        }
        
        private static void VerifyRuntimeDirectory()
        {
            if (!Directory.Exists(RuntimeFileLoader.RelativeRuntimeFiles))
            {
                LingotionLogger.Info("Lingotion Runtime directory not found. Creating...");

                Directory.CreateDirectory(RuntimeFileLoader.RelativeRuntimeFiles);
            }
            if (!Directory.Exists(_actorPacksPath))
            {
                LingotionLogger.Info("Lingotion Actor pack directory not found. Creating...");

                Directory.CreateDirectory(_actorPacksPath);
            }
            if (!Directory.Exists(_languagePacksPath))
            {
                LingotionLogger.Info("Lingotion Language pack directory not found. Creating...");

                Directory.CreateDirectory(_languagePacksPath);
            }
            if (!File.Exists(_mappingInfoJsonPath))
            {
                LingotionLogger.Info("Lingotion pack manifest not found. Creating...");

                JObject manifest = new JObject();

                File.WriteAllText(_mappingInfoJsonPath, manifest.ToString(Formatting.Indented));
            }
        }


        private static void SetupWatcher(ref FileSystemWatcher watcher, string folderPath)
        {
            string fullPath = Path.GetFullPath(folderPath);

            if (!Directory.Exists(fullPath))
            {
                LingotionLogger.Error($"Folder not found: {fullPath}");
                return;
            }

            watcher = new FileSystemWatcher(fullPath)
            {
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Created += OnFileCreated;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
        }

        private static void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            string fullPath = e.FullPath;
            string fileName = Path.GetFileName(fullPath);

            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)|| fileName.EndsWith(".sentis", StringComparison.OrdinalIgnoreCase) || (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && fileName != RuntimeFileLoader.ManifestFileName))
                return;
            
            // [DevComment] Watchers are threaded, force this to execute on main thread to avoid big issues
            EditorApplication.delayCall += () =>
            {
                UpdatePackMappingsInfo();
                AssetDatabase.Refresh();
                PackManifestHandler.Instance.UpdateMappings();
            };
        }

        private static void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            string fullPath = e.FullPath;
            string fileName = Path.GetFileName(fullPath);

            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)|| fileName.EndsWith(".sentis", StringComparison.OrdinalIgnoreCase) || (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && fileName != RuntimeFileLoader.ManifestFileName))
                return;
            
            if (fileName == RuntimeFileLoader.ManifestFileName || fullPath.Contains(RuntimeFileLoader.ActorPackSubdirectory) || fullPath.Contains(RuntimeFileLoader.LanguagePackSubdirectory))
            {
                EditorApplication.delayCall += () =>
                {
                    UpdatePackMappingsInfo();
                    AssetDatabase.Refresh();
                    PackManifestHandler.Instance.UpdateMappings();
                };
            }
            
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            EditorApplication.delayCall += UpdatePackMappingsInfo;
            EditorApplication.delayCall += () =>
            {
                PackManifestHandler.Instance.UpdateMappings();
            };
        }

        private static void UpdatePackMappingsInfo()
        {
            VerifyRuntimeDirectory();

            JObject mappingInfoObject = new JObject
            {
                ["actorpack_modules"] = new JObject(),
                ["languagepack_modules"] = new JObject(),
            };

            List<string> jsonsToDump = new List<string>();

            if (Directory.Exists(_actorPacksPath))
            {
                foreach (string dir in Directory.GetDirectories(_actorPacksPath))
                {
                    jsonsToDump.AddRange(Directory.GetFiles(dir, "*.json"));
                }
            }
            else
            {
                LingotionLogger.Error($"Directory not found: {_actorPacksPath}. Actor Pack Directory has been deleted.");
            }

            if (Directory.Exists(_languagePacksPath))
            {
                foreach (string dir in Directory.GetDirectories(_languagePacksPath))
                {
                    jsonsToDump.AddRange(Directory.GetFiles(dir, "lingotion-languagepack*.json"));
                }
            }
            else
            {
                LingotionLogger.Error($"Directory not found: {_languagePacksPath}. Language pack directory has been deleted.");
            }

            JObject actorPackModules = new JObject();
            JObject languagePackModules = new JObject();
            JObject packNameToPath = new JObject();
            foreach (string jsonPath in jsonsToDump)
            {
                try
                {
                    string fileText = RuntimeFileLoader.LoadFileAsString(jsonPath);
                    JObject config = JObject.Parse(fileText);
                    string packType = config["type"]?.ToString();
                    string packName = config["name"]?.ToString();

                    packNameToPath[packName] = RuntimeFileLoader.GetDirectory(jsonPath);
                    if (string.IsNullOrEmpty(packType) || string.IsNullOrEmpty(packName))
                    {
                        LingotionLogger.Warning($"Skipping {jsonPath} due to missing type or name.");
                        continue;
                    }

                    StringBuilder frontFacingName = new();
                    if (packType == "ACTORPACK")
                    {
                        foreach (var module in config["modules"])
                        {
                            string id = module["actorpack_base_module_id"]?.ToString();
                            Dictionary<string, string> requiredLanguageModules = new Dictionary<string, string>();
                            foreach (var langModules in module["phonemizer_setup"]["modules"])
                            {
                                foreach (var langModule in langModules)
                                {
                                    JArray moduleLanguages = (JArray)langModule["languages"];
                                    JObject moduleLanguage = (JObject)moduleLanguages.First;
                                    string moduleLang = moduleLanguage["iso639_2"]?.ToString();
                                    requiredLanguageModules[moduleLang] = langModule["module_id"].ToString();
                                }
                            }

                            HashSet<string> actors = new();
                            foreach (var actorInfo in module["model_options"]["recording_data_info"]["actors"])
                            {
                                foreach (var pairing in actorInfo)
                                {
                                    string username = pairing["actor"]["username"]?.ToString();
                                    actors.Add(username);
                                }
                            }
                            string qualityTag = module["tags"]["quality"]?.ToString();
                            ModuleType quality = PackManifestHandler.StringToModuleType.TryGetValue(qualityTag, out ModuleType qualityType) ? qualityType : ModuleType.None;
                            if (quality == ModuleType.None)
                            {
                                LingotionLogger.Error($"Missing quality tag for actor pack module {id} in {jsonPath}. Please verify that the pack is valid.");
                                throw new ArgumentException($"Missing quality tag for actor pack module {id} in {jsonPath}. Please verify that the pack is valid.");
                            }
                            frontFacingName.Append(string.Join((", "), actors) + "-" + quality + " / ");
                            JObject languages = new();
                            foreach (var languageInfo in module["language_options"]["languages"])
                            {
                                string languageName = languageInfo["languagecode"]["nameinenglish"]?.ToString();
                                if (string.IsNullOrEmpty(languageName))
                                {
                                    Debug.LogWarning("Unknown language encountered in actorpack, please verify that the pack is valid.");
                                    continue;
                                }

                                JObject currentLanguage = (JObject)languages[languageName];
                                if (currentLanguage == null)
                                {
                                    currentLanguage = new()
                                    {
                                        ["languagecode"] = languageInfo["iso639_2"]?.ToString(),
                                        ["dialects"] = new JObject()
                                    };
                                }

                                JObject subLanguage = new()
                                {
                                    ["iso639_2"] = languageInfo["iso639_2"]?.ToString(),
                                    ["iso639_3"] = languageInfo["iso639_3"]?.ToString(),
                                    ["glottocode"] = languageInfo["glottocode"]?.ToString(),
                                    ["customdialect"] = languageInfo["customdialect"]?.ToString(),
                                    ["iso3166_1"] = languageInfo["iso3166_1"]?.ToString(),
                                    ["iso3166_2"] = languageInfo["iso3166_2"]?.ToString(),
                                };
                                string dialectCode = subLanguage["iso639_2"].ToString();
                                string subLanguageName = languageName;
                                if (subLanguage["iso3166_1"].ToString() != "")
                                {
                                    dialectCode += $"_{subLanguage["iso3166_1"]}";
                                    subLanguageName += $" {subLanguage["iso3166_1"]}";
                                }
                                else if (subLanguage["iso3166_2"].ToString() != "")
                                {
                                    dialectCode += $"_{subLanguage["iso3166_2"]}";
                                    subLanguageName += $" {subLanguage["iso3166_2"]}";
                                }
                                else
                                {
                                    dialectCode += "_unknown";
                                    subLanguageName += $" unknown dialect";
                                }

                                subLanguage["languagecode"] = dialectCode;

                                currentLanguage["dialects"][subLanguageName] = subLanguage;

                                languages[languageName] = currentLanguage;
                            }
                            // [DevComment] Remove the last " / "
                            frontFacingName.Remove(frontFacingName.Length - 3, 3); 
                            int major, minor, patch;
                            major = module["model_options"]["version"]["major"].Value<int>();
                            minor = module["model_options"]["version"]["minor"].Value<int>();
                            patch = module["model_options"]["version"]["patch"].Value<int>();
                            JObject version = new()
                            {
                                ["major"] = major,
                                ["minor"] = minor,
                                ["patch"] = patch
                            };

                            JObject moduleMapping = new()
                            {
                                ["packname"] = frontFacingName.ToString(),
                                ["jsonpath"] = RuntimeFileLoader.TrimPackFilePath(jsonPath),
                                ["required_language_modules"] = JObject.FromObject(requiredLanguageModules),
                                ["actors"] = new JArray(actors.ToArray()),
                                ["quality"] = qualityTag,
                                ["languages"] = new JObject(languages),
                                ["version"] = version
                            };
                            actorPackModules![id] = moduleMapping;
                        }
                    }
                    else if (packType == "LANGUAGEPACK")
                    {
                        foreach (var module in config["modules"])
                        {
                            string id = module["base_module_id"]?.ToString();

                            JObject languages = new();
                            foreach (var languageInfo in module["languages"])
                            {
                                string nameInEnglish = languageInfo["nameinenglish"]?.ToString();
                                frontFacingName.Append(nameInEnglish + " - ");
                                if (string.IsNullOrEmpty(nameInEnglish))
                                    continue;

                                if (languages[nameInEnglish] == null)
                                {
                                    languages[nameInEnglish] = new JArray();
                                }
                                JObject language = new()
                                {
                                    ["iso639_2"] = languageInfo["iso639_2"]?.ToString(),
                                    ["iso639_3"] = languageInfo["iso639_3"]?.ToString(),
                                    ["glottocode"] = languageInfo["glottocode"]?.ToString(),
                                    ["customdialect"] = languageInfo["customdialect"]?.ToString(),
                                    ["iso3166_1"] = languageInfo["iso3166_1"]?.ToString(),
                                    ["iso3166_2"] = languageInfo["iso3166_2"]?.ToString(),
                                };
                                ((JArray)languages[nameInEnglish]).Add(language);
                            }
                            // [DevComment] Remove the last " - "
                            frontFacingName.Remove(frontFacingName.Length - 3, 3); 


                            JObject moduleMapping = new()
                            {
                                ["packname"] = frontFacingName.ToString(),
                                ["languages"] = new JObject(languages),
                                ["jsonpath"] = RuntimeFileLoader.TrimPackFilePath(jsonPath)
                            };
                            languagePackModules![id] = moduleMapping;
                        }

                    }
                    packNameToPath[frontFacingName.ToString()] = RuntimeFileLoader.GetDirectory(jsonPath);

                }
                catch (Exception ex)
                {
                    LingotionLogger.Error($"Error processing {jsonPath}: {ex.Message}\n {ex.StackTrace}");
                }
            }
            mappingInfoObject["imported_pack_directories"] = packNameToPath;
            mappingInfoObject["actorpack_modules"] = actorPackModules;
            mappingInfoObject["languagepack_modules"] = languagePackModules;

            string jsonString = JsonConvert.SerializeObject(mappingInfoObject, Formatting.Indented);
            File.WriteAllText(_mappingInfoJsonPath, jsonString);
        }
    }
}