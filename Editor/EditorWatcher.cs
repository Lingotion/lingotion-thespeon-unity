// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.IO;
using UnityEditor;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Core.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Lingotion.Thespeon.Editor
{
    /// <summary>
    /// Watches for changes to the directories and updates the manifest.
    /// </summary>
    [InitializeOnLoad]
    public class EditorWatcher : IPreprocessBuildWithReport
    {
        private static FileSystemWatcher _runtimeFilesWatcher;
        private static readonly string _mappingInfoJsonPath = RuntimeFileLoader.ManifestPath;

        public int callbackOrder => 1;

        /// <summary>
        /// Called before build. Performs final write to manifest.
        /// </summary>
        /// <param name="report"></param>
        public void OnPreprocessBuild(BuildReport report)
        {
            UpdateMappingsInfo();
        }

        static EditorWatcher()
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
            if (!File.Exists(_mappingInfoJsonPath))
            {
                LingotionLogger.Info("Lingotion manifest not found. Creating...");
                JObject manifest = new JObject();
                File.WriteAllText(_mappingInfoJsonPath, manifest.ToString(Formatting.Indented));
            }
        }


        private static void SetupWatcher(ref FileSystemWatcher watcher, string folderPath)
        {
            string fullPath = Path.GetFullPath(folderPath);

            if (!Directory.Exists(fullPath))
            {
                LingotionLogger.Error($"Lingotion folder watcher failed! Folder not found: {fullPath}. Create it manually if the issue persists.");
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

            // Skip meta files and the manifest itself
            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                fileName == RuntimeFileLoader.ManifestFileName)
                return;

            // Watchers are threaded, force this to execute on main thread
            EditorApplication.delayCall += () =>
            {
                UpdateMappingsInfo();
                AssetDatabase.Refresh();
                ManifestHandler.Instance.UpdateMappings();
            };
        }

        private static void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            string fullPath = e.FullPath;
            string fileName = Path.GetFileName(fullPath);

            // Skip meta files
            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                return;

            EditorApplication.delayCall += () =>
            {
                UpdateMappingsInfo();
                AssetDatabase.Refresh();
                ManifestHandler.Instance.UpdateMappings();
            };
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            EditorApplication.delayCall += UpdateMappingsInfo;
            EditorApplication.delayCall += () =>
            {
                ManifestHandler.Instance.UpdateMappings();
            };
        }

        private static void UpdateMappingsInfo()
        {
            VerifyRuntimeDirectory();

            JObject mappingInfoObject = new JObject
            {
                ["character_modules"] = new JObject(),
                ["language_modules"] = new JObject(),
                ["imported_configs"] = new JObject(),
                ["file_usage"] = new JObject()
            };

            JObject characterModules = new JObject();
            JObject languageModules = new JObject();
            JObject importedConfigs = new JObject();
            JObject fileUsage = new JObject();

            // Scan RuntimeFiles for all JSON config files
            string runtimePath = RuntimeFileLoader.RelativeRuntimeFiles;
            if (!Directory.Exists(runtimePath))
            {
                LingotionLogger.Error($"Lingotion folder watcher failed! Runtime directory not found: {runtimePath}. Create it manually if the issue persists.");
                return;
            }

            foreach (string jsonPath in Directory.GetFiles(runtimePath, "*.json"))
            {
                string fileName = Path.GetFileName(jsonPath);

                // Skip the manifest file itself
                if (fileName == RuntimeFileLoader.ManifestFileName)
                    continue;

                try
                {
                    string fileText = File.ReadAllText(jsonPath);
                    JObject config = JObject.Parse(fileText);

                    EditorConfigImporter.ParsedModuleConfig? parsed = EditorConfigImporter.TryParseConfigFile(config, fileName);
                    if (parsed == null)
                        continue;

                    // Track file usage for this config
                    foreach (string md5 in parsed.Value.FileMd5s)
                    {
                        if (fileUsage[md5] == null)
                            fileUsage[md5] = new JArray();
                        ((JArray)fileUsage[md5]).Add(fileName);
                    }

                    // Place in correct manifest section
                    if (parsed.Value.ConfigType == ConfigFormatDetector.LARA_TYPE)
                        characterModules[parsed.Value.ModuleId] = parsed.Value.ModuleMapping;
                    else
                        languageModules[parsed.Value.ModuleId] = parsed.Value.ModuleMapping;

                    importedConfigs[parsed.Value.FrontFacingName] = fileName;
                }
                catch (Exception ex)
                {
                    LingotionLogger.Error($"Lingotion folder watcher failed! Error processing {jsonPath}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            mappingInfoObject["character_modules"] = characterModules;
            mappingInfoObject["language_modules"] = languageModules;
            mappingInfoObject["imported_configs"] = importedConfigs;
            mappingInfoObject["file_usage"] = fileUsage;

            string jsonString = JsonConvert.SerializeObject(mappingInfoObject, Formatting.Indented);
            File.WriteAllText(_mappingInfoJsonPath, jsonString);
        }

    }
}