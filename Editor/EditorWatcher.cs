// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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

        private static int _suspendCount;
        private static int _rebuildQueued;
        private static int _rebuildPending;

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
            EditorApplication.delayCall += CheckInstalledCharacterModuleVersions;
        }

        /// <summary>
        /// Rebuilds the manifest if it lists a character module of a major this package cannot run.
        /// </summary>
        private static void CheckInstalledCharacterModuleVersions()
        {
            if (!File.Exists(_mappingInfoJsonPath))
            {
                return;
            }

            JObject manifest;
            try
            {
                manifest = JObject.Parse(File.ReadAllText(_mappingInfoJsonPath));
            }
            catch (Exception ex)
            {
                LingotionLogger.Debug($"Could not read the manifest to check module versions: {ex.Message}");
                return;
            }

            if (manifest["character_modules"] is not JObject characterModules)
            {
                return;
            }

            foreach (JProperty module in characterModules.Properties())
            {
                if (!ModuleVersion.TryParse(module.Value["version"], out ModuleVersion version)
                    || version.Major != ModuleVersion.SupportedCharacterModuleMajor)
                {
                    RequestRebuild();
                    return;
                }
            }
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
            string fileName = Path.GetFileName(e.FullPath);

            // Skip meta files and the manifest itself
            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                fileName == RuntimeFileLoader.ManifestFileName)
            {
                return;
            }

            RequestRebuild();
        }

        private static void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            string fileName = Path.GetFileName(e.FullPath);

            // Skip meta files
            if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RequestRebuild();
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            RequestRebuild();
        }

        /// <summary>
        /// Suspends manifest rebuilds until the matching <see cref="EndBulkChange"/>. Nesting is counted, so
        /// nested bulk operations resolve to a single rebuild at the outermost exit.
        /// </summary>
        public static void BeginBulkChange()
        {
            Interlocked.Increment(ref _suspendCount);
        }

        /// <summary>
        /// Ends a bulk change and performs one rebuild if anything was written while suspended.
        /// </summary>
        /// <returns>
        /// True when a rebuild will run <c>AssetDatabase.Refresh</c> on the caller's behalf, or when an outer
        /// bulk change still owns that decision. False only when no rebuild is coming, so a caller that needs
        /// its own writes made visible has to refresh itself.
        /// </returns>
        public static bool EndBulkChange()
        {
            if (Interlocked.Decrement(ref _suspendCount) > 0)
            {
                // Not the outermost exit - the refresh belongs to whoever ends the outermost bulk change.
                return true;
            }

            if (Interlocked.Exchange(ref _rebuildPending, 0) != 1)
            {
                return false;
            }

            RequestRebuild();
            return true;
        }

        /// <summary>
        /// Asks for a manifest rebuild on the next editor tick, folding together every request that arrives
        /// before that tick runs.
        /// </summary>
        private static void RequestRebuild()
        {
            if (Volatile.Read(ref _suspendCount) > 0)
            {
                Interlocked.Exchange(ref _rebuildPending, 1);
                if (Volatile.Read(ref _suspendCount) > 0)
                {
                    return;
                }

                if (Interlocked.Exchange(ref _rebuildPending, 0) == 0)
                {
                    return;
                }
            }

            if (Interlocked.CompareExchange(ref _rebuildQueued, 1, 0) != 0)
            {
                return;
            }

            // Watchers are threaded, force this to execute on main thread
            EditorApplication.delayCall += RunRebuild;
        }

        private static void RunRebuild()
        {
            Interlocked.Exchange(ref _rebuildQueued, 0);

            UpdateMappingsInfo();
            AssetDatabase.Refresh();
            ManifestHandler.Instance.UpdateMappings();
        }

        private static void UpdateMappingsInfo()
        {
            VerifyRuntimeDirectory();

            JObject mappingInfoObject = new JObject
            {
                ["character_modules"] = new JObject(),
                ["language_modules"] = new JObject(),
                ["imported_configs"] = new JObject(),
                ["unsupported_modules"] = new JObject(),
                ["file_usage"] = new JObject()
            };

            JObject characterModules = new JObject();
            JObject languageModules = new JObject();
            JObject importedConfigs = new JObject();
            JObject unsupportedModules = new JObject();
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

                JObject config = null;
                try
                {
                    string fileText = File.ReadAllText(jsonPath);
                    config = JObject.Parse(fileText);

                    EditorConfigImporter.ParsedModuleConfig? parsed = EditorConfigImporter.TryParseConfigFile(config, fileName);
                    if (parsed == null)
                        continue;

                    TrackFileUsage(fileUsage, parsed.Value.FileMd5s, fileName);

                    // Place in correct manifest section
                    if (parsed.Value.ConfigType == ConfigFormatDetector.LARA_TYPE)
                        characterModules[parsed.Value.ModuleId] = parsed.Value.ModuleMapping;
                    else
                        languageModules[parsed.Value.ModuleId] = parsed.Value.ModuleMapping;

                    importedConfigs[parsed.Value.FrontFacingName] = fileName;
                }
                catch (UnsupportedModuleVersionException ex)
                {
                    unsupportedModules[fileName] = new JObject
                    {
                        ["name"] = ex.FrontFacingName,
                        ["version"] = ex.Version.ToJson()
                    };
                    TrackFileUsage(fileUsage, EditorConfigImporter.ExtractFileMd5s(config), fileName);
                    LingotionLogger.Error($"Lingotion skipped {fileName}: {ex.Message} It is listed under Outdated Modules in the Thespeon info window.");
                }
                catch (InvalidDataException ex)
                {
                    LingotionLogger.Error($"Lingotion import rejected {fileName}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    LingotionLogger.Error($"Lingotion folder watcher failed! Error processing {jsonPath}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            mappingInfoObject["character_modules"] = characterModules;
            mappingInfoObject["language_modules"] = languageModules;
            mappingInfoObject["imported_configs"] = importedConfigs;
            mappingInfoObject["unsupported_modules"] = unsupportedModules;
            mappingInfoObject["file_usage"] = fileUsage;

            string jsonString = JsonConvert.SerializeObject(mappingInfoObject, Formatting.Indented);
            File.WriteAllText(_mappingInfoJsonPath, jsonString);
        }

        /// <summary>
        /// Records that a config uses each of the given files.
        /// </summary>
        /// <param name="fileUsage">The manifest's file usage section, keyed by MD5.</param>
        /// <param name="md5s">The MD5s of the files the config uses.</param>
        /// <param name="configFilename">The config's filename.</param>
        private static void TrackFileUsage(JObject fileUsage, List<string> md5s, string configFilename)
        {
            foreach (string md5 in md5s)
            {
                if (fileUsage[md5] == null)
                {
                    fileUsage[md5] = new JArray();
                }
                ((JArray)fileUsage[md5]).Add(configFilename);
            }
        }

    }
}