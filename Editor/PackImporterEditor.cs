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
                EditorUtility.DisplayDialog("Invalid Archive", "The selected archive is invalid or missing.", "OK");
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

            if (!config.TryGetValue("type", out JToken configTypeToken))
            {
                Debug.LogError($"Config file at '{configFilePath}' is missing a 'type' field.");
                Cleanup(tempExtractPath);
                return;
            }

            string configType = configTypeToken.ToString();
            if (configType == "LANGUAGEPACK")
            {
                Debug.LogWarning($"Selected archive \"{zipPath}\" is a Language Pack, not an Actor Pack. Please import it as a Language Pack instead.");
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
                EditorUtility.DisplayDialog("Invalid Archive", "The selected archive is invalid or missing.", "OK");
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

            if (!config.TryGetValue("name", out JToken configNameToken))
            {
                Debug.LogError($"Config file at '{configFilePath}' is missing a 'name' field.");
                Cleanup(tempExtractPath);
                return;
            }

            string finalFolderName = configNameToken.ToString();
            string finalFolderPath = Path.Combine(RuntimeFileLoader.GetLanguagePacksPath(), finalFolderName);

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
                Debug.LogWarning($"Selected archive \"{zipPath}\" is an Actor Pack, not a Language Pack. Please import it as an Actor Pack instead.");
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
    }

}
