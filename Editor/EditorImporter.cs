// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

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
using Unity.InferenceEngine;

namespace Lingotion.Thespeon.Editor
{
    /// <summary>
    /// Allows for importing and verification of files.
    /// </summary>
    public class EditorImporter
    {
        /// <summary>
        /// Opens a file dialog and imports the selected Lingotion file.
        /// </summary>
        public static void ImportThespeon()
        {
            string zipPath = EditorUtility.OpenFilePanel("Select Lingotion file", "", "lingotion");
            if (string.IsNullOrEmpty(zipPath))
                return;
            ImportThespeonFromPath(zipPath);
        }

        /// <summary>
        /// Extracts, verifies and imports a Lingotion file from the given path.
        /// </summary>
        public static void ImportThespeonFromPath(string zipPath)
        {
            try
            {
                string tempExtractPath = Path.Combine(Application.dataPath, "LingotionTempExtract");
                RuntimeFileLoader.DeleteDirectory(tempExtractPath, true);
                ZipFile.ExtractToDirectory(zipPath, tempExtractPath, true);

                // Find all config files with type "lara" or "phonemizer"
                List<(string path, JObject config)> configFiles = FindConfigs(tempExtractPath);

                if (configFiles.Count == 0)
                {
                    LingotionLogger.Debug($"No config files found in {zipPath}.");
                    LingotionLogger.Error("Import failure! The imported file is corrupted. Please re-download and try again, or contact support if the issue persists.");
                    RuntimeFileLoader.DeleteDirectory(tempExtractPath, true);
                    return;
                }

                // Ensure runtime directory exists
                if (!Directory.Exists(RuntimeFileLoader.RelativeRuntimeFiles))
                {
                    Directory.CreateDirectory(RuntimeFileLoader.RelativeRuntimeFiles);
                }

                int importedCount = 0;
                LingotionLogger.Debug($"Found {configFiles.Count} config file(s) in {zipPath}. Starting import...");
                foreach ((string configPath, JObject config) in configFiles)
                {
                    string configName = config["name"]?.ToString() ?? Path.GetFileNameWithoutExtension(configPath);

                    // Verify all referenced files exist in extract
                    if (!VerifyFiles(config, tempExtractPath))
                    {
                        LingotionLogger.Debug($"Verification failed for config {configPath}. Skipping.");
                        LingotionLogger.Error("Partial import failure! The imported file is corrupted or incomplete. Please re-download and try again, or contact support if the issue persists.");
                        continue;
                    }

                    // Import files to flat runtime directory
                    if (!ImportFiles(config, configPath, tempExtractPath))
                    {
                        LingotionLogger.Debug($"Import failed for config {configPath}. Skipping.");
                        LingotionLogger.Error("Partial import failure! An error occurred during file import. Please re-download and try again, or contact support if the issue persists.");
                        continue;
                    }
                    importedCount++;
                    LingotionLogger.Info($"Successfully imported {configName}");
                }

                RuntimeFileLoader.DeleteDirectory(tempExtractPath, true);
                AssetDatabase.Refresh();

                if (importedCount == 0)
                {
                    LingotionLogger.Error($"No files were successfully imported.");
                }
                else
                {
                    LingotionLogger.Info($"Imported {importedCount}/{configFiles.Count} file(s)");
                }
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"File import failed with error: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Finds all JSON files that are valid configs (type: lara or phonemizer).
        /// </summary>
        private static List<(string path, JObject config)> FindConfigs(string extractRoot)
        {
            var configs = new List<(string path, JObject config)>();

            foreach (string jsonPath in Directory.GetFiles(extractRoot, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    string content = File.ReadAllText(jsonPath);
                    JObject config = JObject.Parse(content);
                    string type = config["type"]?.ToString();

                    if (type == ConfigFormatDetector.LARA_TYPE || type == ConfigFormatDetector.PHONEMIZER_TYPE)
                    {
                        configs.Add((jsonPath, config));
                    }
                }
                catch
                {
                    LingotionLogger.Debug($"Malformed file found at: {jsonPath}, skipping.");
                }
            }

            return configs;
        }

        /// <summary>
        /// Imports files to the flat runtime directory.
        /// ONNX files are serialized to Sentis format during import.
        /// </summary>
        private static bool ImportFiles(JObject config, string configPath, string extractRoot)
        {
            string destPath = RuntimeFileLoader.RelativeRuntimeFiles;
            string tempOnnxFolder = Path.Combine(Application.dataPath, "LingotionTempOnnx");
            List<string> tempOnnxFiles = new List<string>();
            bool configModified = false;

            try
            {
                // Copy referenced files
                foreach (var (md5, ext, fileEntry) in GetValidFileEntries((JArray)config["files"]))
                {
                    string targetName = $"{md5}.{ext}";
                    string sourcePath = FindFileInTree(extractRoot, targetName);

                    if (sourcePath == null)
                    {
                        LingotionLogger.Error($"Required file not found: {targetName}");
                        return false;
                    }

                    if (ext.ToLowerInvariant() == "onnx")
                    {
                        // ONNX files need to be serialized to Sentis format
                        if (!SerializeOnnxToSentis(sourcePath, md5, destPath, tempOnnxFolder, tempOnnxFiles))
                        {
                            LingotionLogger.Error($"Failed to serialize ONNX file: {targetName}");
                            return false;
                        }

                        // Update config to reflect .sentis extension
                        fileEntry["extension"] = "sentis";
                        configModified = true;
                    }
                    else
                    {
                        // Non-ONNX files are copied directly
                        string destFilePath = Path.Combine(destPath, targetName);
                        File.Copy(sourcePath, destFilePath, overwrite: true);
                    }
                }

                // Copy the config file itself (potentially modified with .sentis extensions)
                string configFileName = Path.GetFileName(configPath);
                string configDestPath = Path.Combine(destPath, configFileName);

                if (configModified)
                {
                    // Write modified config with updated extensions
                    string updatedConfigJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText(configDestPath, updatedConfigJson);
                }
                else
                {
                    File.Copy(configPath, configDestPath, overwrite: true);
                }

                return true;
            }
            finally
            {
                // Clean up temporary ONNX files
                CleanupTempOnnxFiles(tempOnnxFiles, tempOnnxFolder);
            }
        }

        /// <summary>
        /// Serializes an ONNX file to Sentis format.
        /// </summary>
        /// <param name="onnxSourcePath">Source path of the ONNX file.</param>
        /// <param name="md5">MD5 hash to use as filename base.</param>
        /// <param name="destPath">Destination path for the .sentis file.</param>
        /// <param name="tempOnnxFolder">Temporary folder for ONNX files in Assets.</param>
        /// <param name="tempOnnxFiles">List to track temporary files for cleanup.</param>
        /// <returns>True if serialization succeeded, false otherwise.</returns>
        private static bool SerializeOnnxToSentis(string onnxSourcePath, string md5, string destPath, string tempOnnxFolder, List<string> tempOnnxFiles)
        {
            try
            {
                // Ensure temp folder exists
                if (!Directory.Exists(tempOnnxFolder))
                {
                    Directory.CreateDirectory(tempOnnxFolder);
                }

                // Copy ONNX to temp location in Assets
                string tempOnnxPath = Path.Combine(tempOnnxFolder, $"{md5}.onnx");
                File.Copy(onnxSourcePath, tempOnnxPath, overwrite: true);
                tempOnnxFiles.Add(tempOnnxPath);

                // Get Unity-relative path for AssetDatabase
                string unityRelativePath = "Assets/LingotionTempOnnx/" + $"{md5}.onnx";

                // Import the asset so Unity recognizes it as a model
                AssetDatabase.ImportAsset(unityRelativePath, ImportAssetOptions.ForceSynchronousImport);

                // Load the ONNX model as a ModelAsset through Unity's asset system
                ModelAsset modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(unityRelativePath);
                if (modelAsset == null)
                {
                    LingotionLogger.Error($"Failed to load model file as ModelAsset: {md5}.onnx");
                    return false;
                }

                // Save as .sentis format to runtime files
                string sentisDestPath = Path.Combine(destPath, $"{md5}.sentis");
                ModelWriter.Save(sentisDestPath, modelAsset);

                LingotionLogger.Debug($"Serialized file: {md5}");
                return true;
            }
            catch (Exception ex)
            {
                LingotionLogger.Error($"Failed to serialize file {md5}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cleans up temporary ONNX files from Assets folder.
        /// </summary>
        private static void CleanupTempOnnxFiles(List<string> tempOnnxFiles, string tempOnnxFolder)
        {
            try
            {
                // Delete individual temp files
                foreach (string tempFile in tempOnnxFiles)
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                    string metaFile = tempFile + ".meta";
                    if (File.Exists(metaFile))
                    {
                        File.Delete(metaFile);
                    }
                }

                // Delete temp folder if empty
                if (Directory.Exists(tempOnnxFolder))
                {
                    RuntimeFileLoader.DeleteDirectory(tempOnnxFolder, true);
                }

                // Refresh to clean up Unity's asset database
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                LingotionLogger.Warning($"Failed to clean up temporary ONNX files: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively searches for a file by name in all subdirectories.
        /// </summary>
        private static string FindFileInTree(string root, string filename)
        {
            // Check root directory first
            string directPath = Path.Combine(root, filename);
            if (File.Exists(directPath))
                return directPath;

            // Search all subdirectories
            foreach (string filePath in Directory.GetFiles(root, filename, SearchOption.AllDirectories))
            {
                return filePath;
            }

            return null;
        }

        /// <summary>
        /// Deletes a module by its config filename, safely handling shared files.
        /// </summary>
        /// <param name="configFilename">The config filename to delete.</param>
        public static void DeleteModule(string configFilename)
        {
            string configPath = RuntimeFileLoader.GetRuntimePath(configFilename);
            if (!File.Exists(configPath))
            {
                LingotionLogger.Error($"Delete failed! Import manifest is out of sync. Try recompiling your project or contact support if the issue persists. Missing config: {configFilename}");
                return;
            }

            try
            {
                string content = File.ReadAllText(configPath);
                JObject config = JObject.Parse(content);

                // Delete non-shared files
                foreach (var (md5, ext, _) in GetValidFileEntries((JArray)config["files"]))
                {
                    // Check if file is shared before deleting
                    if (!ManifestHandler.Instance.IsFileShared(md5))
                    {
                        string filename = $"{md5}.{ext}";
                        string filePath = RuntimeFileLoader.GetRelativeRuntimePath(filename);
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            string metaPath = filePath + ".meta";
                            if (File.Exists(metaPath))
                                File.Delete(metaPath);
                        }
                    }
                }

                // Delete the config file
                File.Delete(configPath);
                string configMetaPath = configPath + ".meta";
                if (File.Exists(configMetaPath))
                    File.Delete(configMetaPath);

                AssetDatabase.Refresh();
                LingotionLogger.Info($"Deleted module: {configFilename}");
            }
            catch (Exception ex)
            {
                LingotionLogger.Error($"Delete failed! An error occurred while deleting module {configFilename}: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifies that all files referenced in the config are present in the extract location.
        /// </summary>
        /// <param name="configRoot">The config to verify.</param>
        /// <param name="extractRoot">The directory where files were extracted.</param>
        /// <returns>True if all files are valid, otherwise false.</returns>
        private static bool VerifyFiles(JObject configRoot, string extractRoot)
        {
            JArray files = configRoot["files"] as JArray;
            if (files == null)
            {
                LingotionLogger.Error("Config has no \"files\" array.");
                return false;
            }

            HashSet<string> expectedNames = new HashSet<string>();
            foreach (var (md5, ext, _) in GetValidFileEntries(files))
            {
                expectedNames.Add($"{md5}.{ext}");
            }

            if (expectedNames.Count == 0)
            {
                LingotionLogger.Error("\"files\" array contains no valid entries.");
                return false;
            }

            // Get all files in extract tree
            HashSet<string> actualNames = Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = expectedNames.Where(n => !actualNames.Contains(n)).ToList();
            if (missing.Count == 0)
            {
                return true;
            }

            const int PREVIEW = 10;
            string preview = string.Join("\n• ", missing.Take(PREVIEW));
            if (missing.Count > PREVIEW)
                preview += $"\n… and {missing.Count - PREVIEW} more";

            LingotionLogger.Error($"File is invalid - missing files:\n• {preview}");
            return false;
        }

        /// <summary>
        /// Enumerates valid file entries with md5 and extension from a JSON files array.
        /// </summary>
        private static IEnumerable<(string md5, string ext, JObject entry)> GetValidFileEntries(JArray files)
        {
            if (files == null) yield break;
            foreach (JObject fileEntry in files)
            {
                string md5 = fileEntry["md5"]?.ToString();
                string ext = fileEntry["extension"]?.ToString();
                if (!string.IsNullOrEmpty(md5) && !string.IsNullOrEmpty(ext))
                    yield return (md5, ext, fileEntry);
            }
        }

    }
}