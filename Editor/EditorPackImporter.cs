// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 558341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEngine;
using Lingotion.Thespeon.Core.IO;
using Newtonsoft.Json.Linq;
using System.IO;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO.Compression;
using Lingotion.Thespeon.Core;
using Newtonsoft.Json;

namespace Lingotion.Thespeon.Editor
{
    /// <summary>
    /// Allows for importing and verification of packs.
    /// </summary>
    public class EditorPackImporter
    {
        private const string TempExtractDir = "LingotionTempExtract";
        private const string TempCollectDir = "LingotionTempCollect";
        private const string ConfigNamePattern = "lingotion-*.json";
        private const string ConfigsSubDir = "configs";
        private const string BinariesSubDir = "binaries";
        /// <summary>
        /// Routes importer to fit model version.
        /// </summary>
        public static void RouteImporter()
        {

            try
            {

                string zipPath = EditorUtility.OpenFilePanel("Select Lingotion Pack", "", "lingotion");
                if (string.IsNullOrEmpty(zipPath))
                {
                    return;
                }

                string tempExtractPath = Path.Combine(Application.dataPath, TempExtractDir);

                RuntimeFileLoader.DeleteDirectory(tempExtractPath, true);

                ZipFile.ExtractToDirectory(zipPath, tempExtractPath, true);

                string[] configFiles = Directory.GetFiles(tempExtractPath, ConfigNamePattern, SearchOption.AllDirectories);
                bool useBinariesSubDir = false;
                if (configFiles.Length == 0)
                {
                    string configsPath = Path.Combine(tempExtractPath, ConfigsSubDir);
                    string binariesPath = Path.Combine(tempExtractPath, BinariesSubDir);

                    if (!Directory.Exists(configsPath) || !Directory.Exists(binariesPath))
                    {
                        LingotionLogger.Error("Selected Lingotion file is corrupt. Try re-downloading the file.");
                        RuntimeFileLoader.DeleteDirectory(tempExtractPath, true);
                        return;
                    }
                    configFiles = Directory.GetFiles(configsPath, "*.json", SearchOption.TopDirectoryOnly);
                    if (configFiles.Length == 0)
                    {
                        LingotionLogger.Error("Selected Lingotion file is corrupt. Try re-downloading the file.");
                        RuntimeFileLoader.DeleteDirectory(tempExtractPath, true);
                        return;
                    }
                    useBinariesSubDir = true;
                }
                else
                {
                    configFiles = new string[] { configFiles[0] };
                }

                ImportThespeonPack(tempExtractPath, configFiles, useBinariesSubDir);

            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Pack import failed with error: {e.Message}");
                throw;
            }
        }
        /// <summary>
        /// Extracts, verifies and imports a Lingotion Pack.
        /// </summary>
        private static void ImportThespeonPack(string tempRootExtractPath, string[] configFiles, bool useBinariesSubDir)
        {
            try
            {
                if (configFiles.Length == 0)
                {
                    LingotionLogger.Error("No 'lingotion-' config file was found in the .lingotion file.");
                    RuntimeFileLoader.DeleteDirectory(tempRootExtractPath, true);
                    return;
                }

                string tempCollectPath = Path.Combine(tempRootExtractPath, TempCollectDir);
                foreach (string configFilePath in configFiles)
                {
                    RuntimeFileLoader.DeleteDirectory(tempCollectPath, true);
                    string jsonContent = RuntimeFileLoader.LoadFileAsString(configFilePath);
                    var config = JsonConvert.DeserializeObject<JObject>(jsonContent);

                    if (!config.TryGetValue("name", out var configNameToken) ||
                        !config.TryGetValue("platform", out var platformTok) ||
                        !config.TryGetValue("type", out var configTypeToken))
                    {
                        LingotionLogger.Error($"Config file at '{configFilePath}' is missing required fields.");
                        RuntimeFileLoader.DeleteDirectory(tempRootExtractPath, true);
                        return;
                    }

                    if (!string.Equals(platformTok.ToString(), "sentis", StringComparison.OrdinalIgnoreCase))
                    {
                        EditorUtility.DisplayDialog(
                            "Unsupported Platform",
                            "This pack was built for another platform and can't be imported.\n\nRequired: platform = \"sentis\"",
                            "OK");
                        RuntimeFileLoader.DeleteDirectory(tempRootExtractPath, true);
                        return;
                    }

                    string configType = configTypeToken.ToString();
                    string configName = configNameToken.ToString();
                    Version packVersion = new Version(
                        config["version"]["major"].Value<int>(),
                        config["version"]["minor"].Value<int>(),
                        config["version"]["patch"].Value<int>());

                    string finalFolderPath;

                    switch (configType)
                    {
                        case "ACTORPACK":
                            if (!Directory.Exists(RuntimeFileLoader.GetActorPacksPath()))
                                Directory.CreateDirectory(RuntimeFileLoader.GetActorPacksPath());

                            finalFolderPath = Path.Combine(RuntimeFileLoader.GetActorPacksPath(), configName);

                            if (ActorCollisionDetected(config, configName, packVersion, out string packToDelete))
                            {
                                continue;
                            }
                            break;


                        case "LANGUAGEPACK":
                            if (!Directory.Exists(RuntimeFileLoader.GetLanguagePacksPath()))
                                Directory.CreateDirectory(RuntimeFileLoader.GetLanguagePacksPath());

                            finalFolderPath = Path.Combine(RuntimeFileLoader.GetLanguagePacksPath(), configName);

                            if (LanguagePackCollisionDetected(config, configName, out string langPackToDelete))
                            {
                                continue;
                            }
                            if (!string.IsNullOrEmpty(langPackToDelete))
                            {
                                DeletePack(langPackToDelete);
                            }
                            break;


                        default:
                            LingotionLogger.Error($"Selected Lingotion file is corrupt. Try re-downloading the file.");
                            RuntimeFileLoader.DeleteDirectory(tempRootExtractPath, true);
                            return;
                    }

                    if (config["files"] is not JObject filesObj)
                    {
                        LingotionLogger.Error("Config has no \"files\" section.");
                        RuntimeFileLoader.DeleteDirectory(tempRootExtractPath, true);
                        return;
                    }

                    HashSet<string> configBinaryFiles = new HashSet<string>();
                    foreach (var kv in filesObj.Properties())
                    {
                        string fname = kv.Value?["filename"]?.ToString();
                        if (!string.IsNullOrEmpty(fname))
                        {
                            configBinaryFiles.Add(fname);
                        }
                    }

                    if (!VerifyPackFiles(configBinaryFiles, tempRootExtractPath))
                    {
                        LingotionLogger.Error($"Pack verification failed. The pack is invalid due to missing files.");
                        RuntimeFileLoader.DeleteDirectory(tempRootExtractPath, true);
                        return;
                    }

                    foreach (string filename in configBinaryFiles)
                    {
                        string sourceFilePath;
                        if (useBinariesSubDir)
                        {
                            sourceFilePath = Path.Combine(tempRootExtractPath, BinariesSubDir, filename);
                        }
                        else
                        {
                            sourceFilePath = Path.Combine(tempRootExtractPath, filename);
                        }

                        string destFilePath = Path.Combine(tempCollectPath, filename);

                        if (!Directory.Exists(tempCollectPath))
                        {
                            Directory.CreateDirectory(tempCollectPath);
                        }

                        File.Copy(sourceFilePath, destFilePath, overwrite: true);
                    }
                    File.Copy(configFilePath, Path.Combine(tempCollectPath, $"{configName}.json"), overwrite: true);


                    RuntimeFileLoader.DeleteDirectory(finalFolderPath, true);


                    if (Path.GetPathRoot(tempCollectPath) == Path.GetPathRoot(finalFolderPath))
                    {
                        RuntimeFileLoader.MoveDirectory(tempCollectPath, finalFolderPath, true);
                    }
                    else
                    {
                        RuntimeFileLoader.CopyDirectory(tempCollectPath, finalFolderPath);
                    }
                    LingotionLogger.Info($"Successfully imported {configType.ToLower()}: {configName}");
                }

                RuntimeFileLoader.DeleteDirectory(tempRootExtractPath, true);

                AssetDatabase.Refresh();

            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Pack import failed with error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a pack by its name, including its meta file.
        /// </summary>
        /// <param name="packName">The name of the pack to delete.</param>
        public static void DeletePack(string packName)
        {
            string packPath = PackManifestHandler.Instance.GetPackDirectory(packName);
            RuntimeFileLoader.DeleteDirectory(packPath, true);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Verifies that all files in the config are extracted.
        /// </summary>
        /// <param name="configRoot"> The config to be verified</param>
        /// <param name="extractRoot"> The directory where the files were extracted.</param>
        /// <returns> true if the files are valid, otherwise false.</returns>
        private static bool VerifyPackFiles(HashSet<string> expectedNames, string extractRoot)
        {
            if (expectedNames.Count == 0)
            {
                LingotionLogger.Error("Selected Lingotion file is corrupt. Try re-downloading the file.");
                return false;
            }

            HashSet<string> actualNames =
            Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);



            var missing = expectedNames.Where(n => !actualNames.Contains(n)).ToList();
            if (missing.Count == 0)
            {
                return true;
            }

            const int PREVIEW = 10;
            string preview = string.Join("\n• ", missing.Take(PREVIEW));
            if (missing.Count > PREVIEW) preview += $"\n… and {missing.Count - PREVIEW} more";

            LingotionLogger.Error("Selected Lingotion file is corrupt. Try re-downloading the file.");
            return false;
        }

        private struct IncomingModuleInfo
        {
            public string Username;
            public ModuleType ModuleType;
            public Version Version;
        }

        private static bool ActorCollisionDetected(JObject config, string incomingPackName, Version incomingPackVersion, out string packToDelete)
        {
            packToDelete = null;
            List<IncomingModuleInfo> incomingModules = (
                from module in config?["modules"]?.Children<JObject>() ?? Enumerable.Empty<JObject>()
                let tags = module["tags"] as JObject
                let qualityStr = (tags != null && tags.TryGetValue("quality", out JToken qt)) ? qt.ToString() : ""
                let moduleType = PackManifestHandler.StringToModuleType.TryGetValue(qualityStr, out ModuleType mt) ? mt : ModuleType.None
                let modelOptions = module["model_options"]
                let versionToken = modelOptions?["version"]
                let version = versionToken != null ? new Version(versionToken["major"].Value<int>(), versionToken["minor"].Value<int>(), versionToken["patch"].Value<int>()) : new Version(0, 0, 0)
                where !string.IsNullOrEmpty(qualityStr) && moduleType != ModuleType.None
                let actors = modelOptions?["recording_data_info"]?["actors"] as JObject
                where actors != null
                from actor in actors.Properties()
                let username = actor.Value["actor"]?["username"]?.ToString()
                where !string.IsNullOrEmpty(username)
                select new IncomingModuleInfo { Username = username, ModuleType = moduleType, Version = version }
            ).ToList();

            if (incomingModules.Count == 0)
            {
                LingotionLogger.Warning("Could not find any valid actor modules in the pack to check for collision.");
                return false;
            }

            List<(string, Version)> collisionModules = new();
            string existingPackName = config["name"].ToString();
            Version existingPackVersion = null;

            foreach (IncomingModuleInfo incomingModule in incomingModules)
            {
                ModuleEntry existingModuleEntry = PackManifestHandler.Instance.GetActorPackModuleEntry(incomingModule.Username, incomingModule.ModuleType);
                if (existingModuleEntry.IsEmpty())
                {
                    continue;
                }

                JObject existingModuleConfig = JObject.Parse(RuntimeFileLoader.LoadFileAsString(RuntimeFileLoader.GetActorPackFile(existingModuleEntry.JsonPath)));
                existingPackVersion = new(
                    existingModuleConfig["version"]["major"].Value<int>(),
                    existingModuleConfig["version"]["minor"].Value<int>(),
                    existingModuleConfig["version"]["patch"].Value<int>());
                JToken existingVersionToken = null;
                IEnumerable<JObject> existingModules = existingModuleConfig?["modules"]?.Children<JObject>();

                if (existingModules != null)
                {
                    foreach (JObject existingModule in existingModules)
                    {
                        string existingQuality = existingModule?["tags"]?["quality"]?.ToString();
                        if (string.IsNullOrEmpty(existingQuality) || !PackManifestHandler.StringToModuleType.TryGetValue(existingQuality, out ModuleType existingModuleType) || existingModuleType != incomingModule.ModuleType)
                        {
                            continue;
                        }

                        JObject existingActors = existingModule?["model_options"]?["recording_data_info"]?["actors"] as JObject;
                        if (existingActors == null)
                        {
                            continue;
                        }

                        bool actorFound = false;
                        foreach (JProperty actorProperty in existingActors.Properties())
                        {
                            string username = actorProperty.Value?["actor"]?["username"]?.ToString();
                            if (username == incomingModule.Username)
                            {
                                actorFound = true;
                                break;
                            }
                        }

                        if (actorFound)
                        {
                            existingVersionToken = existingModule?["model_options"]?["version"];
                            break;
                        }
                    }
                }

                if (existingVersionToken == null)
                {
                    LingotionLogger.Warning($"Could not read version from existing pack for actor '{incomingModule.Username}'. Overwriting.");
                    continue;
                }
                Version existingVersion = new Version(existingVersionToken["major"].Value<int>(), existingVersionToken["minor"].Value<int>(), existingVersionToken["patch"].Value<int>());

                collisionModules.Add(($"{incomingModule.Username}-{incomingModule.ModuleType}", existingVersion));
            }
            if (collisionModules.Count != 0)
            {
                bool importIncoming = EditorUtility.DisplayDialog(
                        "Actor Pack Import Conflict",
                        $"An existing pack containing {string.Join(", ", collisionModules.Select(pair => $"{pair.Item1}"))} was found.\n\n" +
                        $"Existing pack: {existingPackName} (version: {existingPackVersion})\n" +
                        $"Incoming pack: {incomingPackName} (version: {incomingPackVersion})",
                        "Import incoming",
                        "Keep existing"
                    );
                if (importIncoming)
                {
                    LingotionLogger.Info($"User chose to import incoming pack. Marking old pack '{existingPackName}' for deletion to install '{incomingPackName}'.");
                    packToDelete = existingPackName;
                    return false;
                }
                else
                {
                    LingotionLogger.Info($"User chose to keep the existing version of '{existingPackName}'. Skipping import of '{incomingPackName}'.");
                    return true;
                }
            }
            return false;
        }

        private static bool LanguagePackCollisionDetected(JObject config, string incomingPackName, out string packToDelete)
        {
            packToDelete = null;

            List<(string LanguageCode, Version Version)> incomingLanguages = (
                from JObject module in config?["modules"]?.Children<JObject>() ?? Enumerable.Empty<JObject>()
                let versionToken = config?["version"]
                let version = versionToken != null ? new Version(versionToken["major"].Value<int>(), versionToken["minor"].Value<int>(), versionToken["patch"].Value<int>()) : new Version(0, 0, 0)
                from JObject language in module["languages"]?.Children<JObject>() ?? Enumerable.Empty<JObject>()
                let isoCode = language["iso639_2"]?.ToString()
                where !string.IsNullOrEmpty(isoCode)
                select (isoCode, version)
            ).Distinct().ToList();

            if (incomingLanguages.Count == 0)
            {
                LingotionLogger.Warning("Could not find any valid language modules in the pack to check for collision.");
                return false;
            }

            Dictionary<string, (string packName, Version version)> installedLanguageToPackMap = PackManifestHandler.Instance.GetAllLanguagesAndTheirPacks();

            foreach ((string incomingLangCode, Version incomingVersion) in incomingLanguages)
            {
                if (installedLanguageToPackMap.TryGetValue(incomingLangCode, out (string existingPackName, Version existingVersion) existingPackInfo))
                {

                    bool importIncoming = EditorUtility.DisplayDialog(
                        "Language Pack Import Conflict",
                        $"An existing pack providing support for '{incomingLangCode}' was found.\n\n" +
                        $"Existing pack: {existingPackInfo.existingPackName} (version: {existingPackInfo.existingVersion})\n" +
                        $"Incoming pack: {incomingPackName} (version: {incomingVersion})",
                        "Import incoming",
                        "Keep existing"
                    );

                    if (importIncoming)
                    {
                        LingotionLogger.Info($"User chose to import incoming pack. Marking old pack '{existingPackInfo.existingPackName}' for deletion to install '{incomingPackName}'.");
                        packToDelete = existingPackInfo.existingPackName;
                        return false;
                    }
                    else
                    {
                        LingotionLogger.Info($"User chose to keep the existing version of '{existingPackInfo.existingPackName}'. Skipping import of '{incomingPackName}'.");
                        return true;
                    }
                }
            }

            return false;
        }

    }
}
