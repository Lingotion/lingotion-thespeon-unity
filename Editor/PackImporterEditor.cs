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

            string zipPath = EditorUtility.OpenFilePanel("Select Actor Pack .zip Archive", "", "zip");
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
                Debug.LogWarning($"Zip file \"{zipPath}\" is a Language Pack, not an Actor Pack. Please import it as a Language Pack instead.");
                Cleanup(tempExtractPath);
                return;
            }
            else if (configType != "ACTORPACK")
            {
                Debug.LogError($"Zip file \"{zipPath}\" is corrupt or not a Lingotion Pack archive.");
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

            Directory.Move(tempExtractPath, finalFolderPath);

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

            string zipPath = EditorUtility.OpenFilePanel("Select Language Pack .zip Archive", "", "zip");
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

            Directory.Move(tempExtractPath, finalFolderPath);

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
                Debug.LogWarning($"Zip file \"{zipPath}\" is an Actor Pack, not a Language Pack. Please import it as an Actor Pack instead.");
                Cleanup(finalFolderPath);
                return;
            }
            else if (configType != "LANGUAGEPACK")
            {
                Debug.LogError($"Zip file \"{zipPath}\" is corrupt or not a Lingotion Pack archive.");
                Cleanup(finalFolderPath);
                return;
            }

            AssetDatabase.Refresh();
            PackFoldersWatcher.UpdateActorDetailedInfo();
            Debug.Log($"Successfully imported Language Pack: {finalFolderName}");
        }


        private static bool ActorCollisionDetected(Dictionary<string, JToken> config)
        {
            // 1) Gather usernames from this new config
            HashSet<string> newUsernames = new HashSet<string>();

            if (config.TryGetValue("modules", out JToken modulesToken) && modulesToken is JArray modulesArray)
            {
                foreach (var moduleToken in modulesArray)
                {
                    if (moduleToken is JObject moduleObj)
                    {
                        JObject modelOptions = moduleObj["model_options"] as JObject;
                        JObject recordingDataInfo = modelOptions?["recording_data_info"] as JObject;
                        JObject actorsDict = recordingDataInfo?["actors"] as JObject;
                        if (actorsDict == null) continue;

                        foreach (var actorProperty in actorsDict.Properties())
                        {
                            JObject actorNode = actorProperty.Value as JObject;
                            JObject actorObj = actorNode?["actor"] as JObject;
                            if (actorObj == null) continue;

                            string username = actorObj["username"]?.ToString();
                            if (!string.IsNullOrEmpty(username))
                            {
                                newUsernames.Add(username);
                            }
                        }
                    }
                }
            }

            // 2) Load watchers so we have up-to-date ActorDataCache
            PackFoldersWatcher.UpdateActorDetailedInfo();
            var existingActors = new HashSet<string>(PackFoldersWatcher.ActorDataCache.Keys);

            // 3) Check for overlap
            var conflicts = newUsernames.Intersect(existingActors).ToList();
            if (conflicts.Count > 0)
            {
                // Build an error message
                string conflictMessage = 
                    "Import blocked! The following actor(s) already have an imported pack:\n\n" 
                    + string.Join(", ", conflicts)
                    + "\n\nOnly one pack per actor is allowed at this time.\n"
                    + "Please delete the existing actor pack for this actor before importing a new one.\n\n"
                    + $"Find existing actor pack for this actor under: {RuntimeFileLoader.GetActorPacksPath(true)}";

                Debug.LogError(conflictMessage);
                EditorUtility.DisplayDialog("Actor Import Blocked", conflictMessage, "OK");
                return true;
            }

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
