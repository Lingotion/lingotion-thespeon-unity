// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using UnityEditor;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Lingotion.Thespeon.FileLoader;


       
namespace Lingotion.Thespeon
{
    public class PackImporterEditor : Editor
    {
        public static void ImportActorPack()
        {

            //Create folder if not exist
            if (!Directory.Exists(RuntimeFileLoader.GetActorPacksPath()))
            {
                Directory.CreateDirectory(RuntimeFileLoader.GetActorPacksPath());
            }

            string zipPath = EditorUtility.OpenFilePanel("Select Actor Pack Archive", "", "lingotion");
            if (string.IsNullOrEmpty(zipPath))
            {
                return;
            }

            // Use a global temp extraction path
            string tempExtractPath = Path.Combine(Application.persistentDataPath, "LingotionTempExtract");
            if (Directory.Exists(tempExtractPath))
            {
                Directory.Delete(tempExtractPath, true);
                File.Delete(tempExtractPath + ".meta");
            }

            ExtractZip(zipPath, tempExtractPath);

            string[] configFiles = Directory.GetFiles(tempExtractPath, "lingotion-*.json", SearchOption.AllDirectories);
            if (configFiles.Length == 0)
            {
                Debug.LogError($"No 'lingotion-' config file was found in the zip file, importing {zipPath} failed!.");
                Cleanup(tempExtractPath);
                return;
            }

            string configFilePath = configFiles[0];
            var config = ExtractJSONConfig(configFilePath);

            if (!config.TryGetValue("name", out JToken configNameToken))
            {
                Debug.LogError($"Config file at '{configFilePath}' is missing a 'name' field.");
                Cleanup(tempExtractPath);
                return;
            }

            // e.g. "lingotion-actorpack-mycoolactor-derynxxx"
            string finalFolderName = configNameToken.ToString();
            string finalFolderPath = Path.Combine(RuntimeFileLoader.GetActorPacksPath(), finalFolderName);

            if (!config.TryGetValue("platform", out JToken platformTok) || !string.Equals(platformTok.ToString(), "sentis", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Unsupported Platform",
                    "This pack was built for another platform and can’t be imported.\n\n" +
                    "Required:  platform = \"sentis\"",
                    "OK");
                Cleanup(tempExtractPath);   // or Cleanup(finalFolderPath) inside ImportLanguagePack
                return;
            }


            if (!config.TryGetValue("type", out JToken configTypeToken))
            {
                Debug.LogError($"Config file at '{configFilePath}' is missing a 'type' field.");
                Cleanup(tempExtractPath);
                return;
            }


            string configType = configTypeToken.ToString();
            if (configType == "LANGUAGEPACK")
            {
                EditorUtility.DisplayDialog(
                    "Wrong Pack Type",
                    "You selected a *Language* Pack but clicked “Import Actor Pack”.\n" +
                    "Please use the correct import button.",
                    "OK");
                Cleanup(tempExtractPath);
                return;
            }
            else if (configType != "ACTORPACK")
            {
                Debug.LogError($"Selected archive \"{zipPath}\" is corrupt or not a Lingotion Pack archive.");
                Cleanup(tempExtractPath);
                return;
            }

            // *** Here we do the Actor collision check ***
            if (ActorCollisionDetected(config))
            {
                // We block the import
                Cleanup(tempExtractPath);
                return;
            }

            JObject configAsJObject = JObject.FromObject(config);
            if (!VerifyPackFiles(configAsJObject, tempExtractPath, out string fileError))
            {
                EditorUtility.DisplayDialog("Incomplete Actor Pack", fileError, "OK");
                Cleanup(tempExtractPath);          // wipe the half-import
                return;
            }

            // If no collision, proceed
            if (Directory.Exists(finalFolderPath))
            {
                Directory.Delete(finalFolderPath, true);
            }
			
			if (Path.GetPathRoot(tempExtractPath) == Path.GetPathRoot(finalFolderPath))
			{
				Directory.Move(tempExtractPath, finalFolderPath);
			} else 
			{
				CopyFolder(tempExtractPath, finalFolderPath);
			}

            // Cleanup the temp extraction path after moving files
            if (Directory.Exists(tempExtractPath))
            {
                Directory.Delete(tempExtractPath, true);
                File.Delete(tempExtractPath + ".meta");
            }

            AssetDatabase.Refresh();
            PackFoldersWatcher.UpdateActorDetailedInfo();
            Debug.Log($"Successfully imported Actor Pack: {finalFolderName}");
        }

        public static void ImportLanguagePack()
        {


            //Create folder if not exist
            if (!Directory.Exists(RuntimeFileLoader.GetLanguagePacksPath()))
            {
                Directory.CreateDirectory(RuntimeFileLoader.GetLanguagePacksPath());
            }

            string zipPath = EditorUtility.OpenFilePanel("Select Language Pack Archive", "", "lingotion");
            if (string.IsNullOrEmpty(zipPath))
            {
                return;
            }

            // Use a global temp extraction path
            string tempExtractPath = Path.Combine(Application.persistentDataPath, "LingotionTempExtract");
            if (Directory.Exists(tempExtractPath))
            {
                Directory.Delete(tempExtractPath, true);
                File.Delete(tempExtractPath + ".meta");
            }

            ExtractZip(zipPath, tempExtractPath);

            string[] configFiles = Directory.GetFiles(tempExtractPath, "lingotion-*.json", SearchOption.AllDirectories);
            if (configFiles.Length == 0)
            {
                Debug.LogError($"No 'lingotion-' config file was found in the extracted archive.");
                Cleanup(tempExtractPath);
                return;
            }

            string configFilePath = configFiles[0];
            var config = ExtractJSONConfig(configFilePath);

            if (!config.TryGetValue("platform", out JToken platformTok) || !string.Equals(platformTok.ToString(), "sentis", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Unsupported Platform",
                    "This pack was built for another platform and can’t be imported.\n\n" +
                    "Required:  platform = \"sentis\"",
                    "OK");
                Cleanup(tempExtractPath);   // or Cleanup(finalFolderPath) inside ImportLanguagePack
                return;
            }

            if (!config.TryGetValue("name", out JToken configNameToken))
            {
                Debug.LogError($"Config file at '{configFilePath}' is missing a 'name' field.");
                Cleanup(tempExtractPath);
                return;
            }

            string finalFolderName = configNameToken.ToString();
            string finalFolderPath = Path.Combine(RuntimeFileLoader.GetLanguagePacksPath(), finalFolderName);


            JObject configAsJObject = JObject.FromObject(config);
            if (!VerifyPackFiles(configAsJObject, tempExtractPath, out string fileError))
            {
                EditorUtility.DisplayDialog("Incomplete Actor Pack", fileError, "OK");
                Cleanup(tempExtractPath);          // wipe the half-import
                return;
            }


            if (Directory.Exists(finalFolderPath))
            {
                Directory.Delete(finalFolderPath, true);
            }
			
			if (Path.GetPathRoot(tempExtractPath) == Path.GetPathRoot(finalFolderPath))
			{
				Directory.Move(tempExtractPath, finalFolderPath);
			} else 
			{
				CopyFolder(tempExtractPath, finalFolderPath);
			}

            // Cleanup the temp extraction path after moving files
            if (Directory.Exists(tempExtractPath))
            {
                Directory.Delete(tempExtractPath, true);
                File.Delete(tempExtractPath + ".meta");
            }

            if (!config.TryGetValue("type", out JToken configTypeToken))
            {
                Debug.LogError($"Config file at '{configFilePath}' is missing a 'type' field.");
                Cleanup(finalFolderPath);
                return;
            }

            string configType = configTypeToken.ToString();
            if (configType == "ACTORPACK")
            {
                EditorUtility.DisplayDialog(
                    "Wrong Pack Type",
                    "You selected an *Actor* Pack but clicked “Import Language Pack”.\n" +
                    "Please use the correct import button.",
                    "OK");                
                Cleanup(finalFolderPath);
                return;
            }
            else if (configType != "LANGUAGEPACK")
            {
                Debug.LogError($"Selected archive \"{zipPath}\" is corrupt or not a Lingotion Pack archive.");
                Cleanup(finalFolderPath);
                return;
            }

            AssetDatabase.Refresh();
            PackFoldersWatcher.UpdateActorDetailedInfo();
            Debug.Log($"Successfully imported Language Pack: {finalFolderName}");
        }


        private static bool ActorCollisionDetected(Dictionary<string, JToken> config)
        {

            // 1) Gather username-to-tagPairs from this new config
            Dictionary<string, HashSet<(string, string)>> newActorTagSets =
            new Dictionary<string, HashSet<(string, string)>>();

            // Parse the "modules" array from the config
            if (config.TryGetValue("modules", out JToken modulesToken) && modulesToken is JArray modulesArray)
            {
                foreach (var moduleToken in modulesArray)
                {
                    if (moduleToken is JObject moduleObj)
                    {
                        JObject modelOptions = moduleObj["model_options"] as JObject;
                        JObject recordingDataInfo = modelOptions?["recording_data_info"] as JObject;
                        JObject actorsDict = recordingDataInfo?["actors"] as JObject;
                        if (actorsDict == null)
                        {
                            continue;
                        }

                        // Gather this module's tags once, then apply to all actors it contains
                        HashSet<(string, string)> moduleTagSet = new HashSet<(string, string)>();
                        JObject moduleTags = moduleObj["tags"] as JObject;
                        if (moduleTags != null)
                        {
                            foreach (var tagProperty in moduleTags.Properties())
                            {
                                string tagName = tagProperty.Name;
                                string tagValue = tagProperty.Value?.ToString() ?? "";
                                moduleTagSet.Add((tagName, tagValue));
                            }
                        }

                        // For each actor in this module
                        foreach (var actorProperty in actorsDict.Properties())
                        {
                            JObject actorNode = actorProperty.Value as JObject;
                            JObject actorObj = actorNode?["actor"] as JObject;
                            if (actorObj == null)
                            {
                                continue;
                            }

                            string username = actorObj["username"]?.ToString();
                            if (string.IsNullOrEmpty(username))
                            {
                                continue;
                            }

                            // Ensure we have a set for this user
                            if (!newActorTagSets.ContainsKey(username))
                            {
                                newActorTagSets[username] = new HashSet<(string, string)>();
                            }

                            // Merge module-level tags into the actor’s set
                            foreach (var t in moduleTagSet)
                            {
                                newActorTagSets[username].Add(t);
                            }
                        }
                    }
                }
            }


            // If no actors were found, we can skip the collision check
            if (newActorTagSets.Count == 0)
            {
                return false;
            }

            // 2) Load watchers so ActorDataCache is fully up-to-date
            PackFoldersWatcher.UpdateActorDetailedInfo();

            // Build username-to-tagPairs dictionary from existing data
            Dictionary<string, HashSet<(string, string)>> existingActorTagSets =
            new Dictionary<string, HashSet<(string, string)>>();

            foreach (var kvp in PackFoldersWatcher.ActorDataCache)
            {
                string existingUsername = kvp.Key;
                ActorData actorInfo = kvp.Value;

                if (!existingActorTagSets.ContainsKey(existingUsername))
                {
                    existingActorTagSets[existingUsername] = new HashSet<(string, string)>();
                }

                // actorInfo.aggregatedTags is a Dictionary<string, HashSet<string>>
                // We'll flatten each (tagName, [tagValue...]) into (tagName, tagValue)
                if (actorInfo?.aggregatedTags != null)
                {
                    foreach (var tagKvp in actorInfo.aggregatedTags)
                    {
                        string tagName = tagKvp.Key;
                        foreach (string tagValue in tagKvp.Value)
                        {
                            existingActorTagSets[existingUsername].Add((tagName, tagValue));
                        }
                    }
                }
            }

            // 3) Check for an exact match: same username + exact same set of tag pairs
            foreach (var entry in newActorTagSets)
            {
                string username = entry.Key;
                var newTagsForUser = entry.Value;
                if (existingActorTagSets.TryGetValue(username, out var existingTagsForUser))
                {
                    // If the sets match exactly, user+tags combination is a duplicate
                    bool isPartialDuplicate = newTagsForUser.All(t => existingTagsForUser.Contains(t));
                    if (isPartialDuplicate)
                    {
                        string conflictMessage =
                            "Import blocked! The following actor/tag combination already exists:\n\n" +
                            $"Actor: {username}\n" +
                            $"Tags: {string.Join(", ", newTagsForUser.Select(t => $"{t.Item1}={t.Item2}"))}\n\n" +
                            "Only one pack with the same actor/tag combination is allowed.\n" +
                            "Please remove or rename the existing pack before importing again.\n\n" +
                            $"Find existing actor pack for this actor under: {RuntimeFileLoader.GetActorPacksPath(true)}";

                        Debug.LogError(conflictMessage);
                        EditorUtility.DisplayDialog("Actor Import Blocked", conflictMessage, "OK");
                        return true;
                    }
                }
            }

            // No collisions found
            return false;
        }

        private static Dictionary<string, JToken> ExtractJSONConfig(string jsonpath)
        {
            string jsonContent = RuntimeFileLoader.LoadFileAsString(jsonpath);
            var config = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(jsonContent);
            return config;
        }

        private static void ExtractZip(string source, string destination)
        {
            try
            {
                ZipFile.ExtractToDirectory(source, destination, true);
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Extraction failed: {ex.Message}");
            }
        }

		private static void CopyFolder(string src, string dest)
		{
			Directory.CreateDirectory(dest);
			foreach (var srcFile in Directory.GetFiles(src))
			{
				var destFile = Path.Combine(dest, Path.GetFileName(srcFile));
				File.Copy(srcFile, destFile, overwrite: true);
			}

			foreach (var srcDir in Directory.GetDirectories(src))
			{
				var destSubdir = Path.Combine(dest, Path.GetFileName(srcDir));
				CopyFolder(srcDir, destSubdir);
			}
		}


        /// <summary>
        /// Deletes <paramref name="targetPath"/> (folder or file) plus its
        /// <c>.meta</c> file if present, then refreshes the <see cref="AssetDatabase"/>.
        /// </summary>
        private static void Cleanup(string targetPath)
        {
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
            }
            string metaFile = targetPath + ".meta";
            if (File.Exists(metaFile))
            {
                File.Delete(metaFile);
            }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Lets the user pick an Actor-Pack folder (limited to the Actor-Packs root)
        /// and—after confirmation—permanently deletes it together with its meta file.
        /// Also refreshes the <see cref="AssetDatabase"/> and updates pack mappings.
        /// </summary>
        public static void DeleteActorPack()
        {
            string root = RuntimeFileLoader.GetActorPacksPath();
            if (!Directory.Exists(root))
            {
                EditorUtility.DisplayDialog("No Actor Packs",
                    "The actor-packs folder does not exist.", "OK");
                return;
            }

            string folder = EditorUtility.OpenFolderPanel(
                "Select Actor Pack to DELETE", root, "");
            if (string.IsNullOrEmpty(folder)) return;     // user cancelled

            if (!IsInside(folder, root))
            {
                EditorUtility.DisplayDialog("Wrong location",
                    $"Please pick a folder that lives under:\n{root}", "OK");
                return;
            }

            string packName = Path.GetFileName(folder);

            if (!EditorUtility.DisplayDialog(
                    "Delete Actor Pack?",
                    $"Are you sure you want to permanently delete “{packName}”?\n\n"
                + "This cannot be undone without reimport.",
                    "Delete", "Cancel"))
                return;

            Cleanup(folder);
            PackFoldersWatcher.UpdatePackMappings();

            Debug.Log($"Deleted Actor Pack: {packName}");
        }



        /// <summary>
        /// Same workflow as <see cref="DeleteActorPack"/> but for Language Packs.
        /// </summary>
        public static void DeleteLanguagePack()
        {
            string root = RuntimeFileLoader.GetLanguagePacksPath();
            if (!Directory.Exists(root))
            {
                EditorUtility.DisplayDialog("No Language Packs",
                    "The language-packs folder does not exist.", "OK");
                return;
            }

            string folder = EditorUtility.OpenFolderPanel(
                "Select Language Pack to DELETE", root, "");
            if (string.IsNullOrEmpty(folder)) return;     // user cancelled

            // safety-guard: allow only folders that live *inside* the packs root
            if (!IsInside(folder, root))
            {
                EditorUtility.DisplayDialog("Wrong location",
                    $"Please pick a folder that lives under:\n{root}", "OK");
                return;
            }

            string packName = Path.GetFileName(folder);

            if (!EditorUtility.DisplayDialog(
                    "Delete Language Pack?",
                    $"Are you sure you want to permanently delete “{packName}”?\n\n"
                + "This cannot be undone without reimport.",
                    "Delete", "Cancel"))
                return;

            Cleanup(folder);
            PackFoldersWatcher.UpdatePackMappings();

            Debug.Log($"Deleted Language Pack: {packName}");
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="folder"/> is physically located
        /// inside <paramref name="root"/> (after both paths are normalised and
        /// slashes unified).
        /// </summary>
        private static bool IsInside(string folder, string root)
        {
            folder = Path.GetFullPath(folder).Replace('\\', '/');
            root   = Path.GetFullPath(root  ).Replace('\\', '/')
                        .TrimEnd('/');                 // no trailing “/”

            return folder.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }



        /// <summary>
        /// Ensures every <c>files.*.filename</c> entry in the pack config
        /// was actually extracted to <paramref name="extractRoot"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> when all files are found; <c>false</c> otherwise and
        /// <paramref name="error"/> contains a user-friendly message.
        /// </returns>
        private static bool VerifyPackFiles(
            JObject configRoot,            // parsed config json
            string  extractRoot,           // temp extraction folder
            out string error)
        {
            error = "";

            // 1) Gather expected filenames from "files" dictionary.
            if (!(configRoot["files"] is JObject filesObj))
            {
            error = "Config has no \"files\" section.";
            Debug.LogError(error);
            return false;
            }

            HashSet<string> expectedNames = new HashSet<string>();
            foreach (var kv in filesObj.Properties())
            {
            string fname = kv.Value?["filename"]?.ToString();
            if (!string.IsNullOrEmpty(fname))
            {
                expectedNames.Add(fname);
            }
            }

            if (expectedNames.Count == 0)
            {
            error = "\"files\" section contains no filenames.";
            Debug.LogError(error);
            return false;
            }

            // 2) Scan the extracted folder for actual file names (leaf only).
            HashSet<string> actualNames =
            Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);



            // 3) Compute missing.
            var missing = expectedNames.Where(n => !actualNames.Contains(n)).ToList();
            if (missing.Count == 0)
            {
            return true;
            }

            // Build readable message (truncate if huge).
            const int PREVIEW = 10;
            string preview = string.Join("\n• ", missing.Take(PREVIEW));
            if (missing.Count > PREVIEW) preview += $"\n… and {missing.Count - PREVIEW} more";

            error = "Error! The pack is unvalid due files missing, contact Lingotion support";
            Debug.LogError(error);
            return false;
        }

    }

}
