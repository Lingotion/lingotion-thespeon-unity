// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

#if UNITY_EDITOR
using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using Lingotion.Thespeon.Core.IO;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Inference;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace Lingotion.Thespeon.Editor
{
    /// <summary>
    /// Validates the Lingotion license key against the licensing server on editor load.
    /// </summary>
    [InitializeOnLoad] // ensures static ctor runs once editor loads
    public static class EditorLicenseKeyValidator
    {
        /// <summary>
        /// Represents the outcome of a license validation attempt.
        /// </summary>
        public enum ValidationResult { Valid, Invalid, Indeterminate }
        /// <summary>
        /// Event raised when license validation completes with the resulting <see cref="ValidationResult"/>.
        /// </summary>
        public static Action<ValidationResult> OnValidationComplete;
        private static readonly HttpClient _httpClient = new();
        /// <summary>
        /// True when this package was installed from the Unity Asset Store (delivered via the default
        /// Unity registry), as opposed to a git/local/embedded install. Used to gate Asset Store-only
        /// behaviour such as Verified Solutions Attribution.
        /// </summary>
        public static bool IsAssetStoreInstall { get; } = IsInstalledFromAssetStore();

        /// <summary>
        /// Path to the ProjectSettings directory.
        /// </summary>
        public static readonly string ProjectSettingsPath = "ProjectSettings";
        /// <summary>
        /// Path to the license key file for this project.
        /// </summary>
        public static readonly string LicenseKeyFilePath = Path.Combine(ProjectSettingsPath, "Lingotion.Thespeon.license");
        private static readonly string DataCacheFilePath = Path.Combine(ProjectSettingsPath, "Lingotion.Thespeon.datacache");

        /// <summary>
        /// Path to the ProjectSettings.asset file for this project.
        /// </summary>
        public static readonly string ProjectSettingsAssetFilePath = Path.Combine(ProjectSettingsPath, "ProjectSettings.asset");



        const string url = EditorLingotionUrls.LicenseVerify;

        static EditorLicenseKeyValidator()
        {
            InferenceEditorSignals.OnSynthesisDataSignal -= AddDataToCacheFile;
            InferenceEditorSignals.OnSynthesisDataSignal += AddDataToCacheFile;
            EditorApplication.delayCall += () => { ValidateFromFileAsync().Forget(); };
        }

        /// <summary>
        /// Validates the provided license key against the Lingotion licensing server.
        /// </summary>
        /// <param name="licenseKey">The license key string to validate.</param>
        /// <returns><see cref="ValidationResult.Valid"/> on success, <see cref="ValidationResult.Invalid"/> on 401, or <see cref="ValidationResult.Indeterminate"/> on network/server errors.</returns>
        public static async Task<ValidationResult> ValidateLicenseAsync(string licenseKey)
        {
            if (string.IsNullOrEmpty(licenseKey))
            {
                return ValidationResult.Invalid;
            }

            List<string> Modules = ManifestHandler.Instance.GetAllModuleIDs();
            string projectGuid = GetProjectGuid();
            if (string.IsNullOrEmpty(projectGuid))
            {
                return ValidationResult.Indeterminate;
            }
            CacheData cacheData = LoadDataCacheFromFile();
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());
            string version = info?.version;
            // Full payload
            var payload = new JObject
            {
                ["licenseKey"] = licenseKey,
                ["projectGuid"] = projectGuid,
                ["data"] = new JObject { 
                    ["modules"] = JArray.FromObject(Modules),
                    ["source"] = IsAssetStoreInstall ? "AssetStore" : "Other",
                    ["cacheData"] = JObject.FromObject(cacheData.Data),
                    ["packageVersion"] = version
                },
                ["platform"] = "Unity",
            };

            string json = payload.ToString(Formatting.None);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var resp = await _httpClient.PostAsync(url, content);
                var body = await resp.Content.ReadAsStringAsync();
                var code = (int)resp.StatusCode;

                // 200 means valid (resp.IsSuccessStatusCode is true for any 2xx)
                if (code == 200)
                {
                    ClearDataCacheFile();
                    return ValidationResult.Valid;
                }

                // Treat 401 as definitive "invalid", anything else (5xx etc.) as indeterminate
                if (code == 401)
                {
                    return ValidationResult.Invalid;
                }

                return ValidationResult.Indeterminate;
            }
            catch (Exception)
            {
                return ValidationResult.Indeterminate;
            }
        }

        private static async Task ValidateFromFileAsync()
        {
            string license = LoadLicenseFromFile();
            ValidationResult result = await ValidateLicenseAsync(license);
            OnValidationComplete?.Invoke(result);
        }

        // Tiny helper to fire-and-forget async Tasks
        private static async void Forget(this Task task)
        {
            try { await task; } catch { }
        }

        /// <summary>
        /// Persists the license key to the project's license key file.
        /// </summary>
        /// <param name="text">The license key string to save.</param>
        public static void SaveLicenseToFile(string text)
        {
            WriteTextToFile(LicenseKeyFilePath, text);
        }

        /// <summary>
        /// Loads the license key from the project's license key file.
        /// </summary>
        /// <returns>The stored license key string, or an empty string if not found or on error.</returns>
        public static string LoadLicenseFromFile()
        {
            return ReadTextFromFile(LicenseKeyFilePath);
        }

        /// <summary>
        /// Adds the provided data to the data cache file, merging with existing content when keys already exist.
        /// </summary>
        /// <param name="data">The data to add to the cache.</param>
        public static void AddDataToCacheFile(CacheData data)
        {
            CacheData cacheData = LoadDataCacheFromFile();
            cacheData = cacheData.AddContent(data);
            WriteTextToFile(DataCacheFilePath, cacheData.ToJson());
        }

        private static CacheData LoadDataCacheFromFile()
        {
            return new CacheData(ReadTextFromFile(DataCacheFilePath));
        }

        private static void ClearDataCacheFile()
        {
            try
            {
                if (File.Exists(DataCacheFilePath))
                {
                    File.Delete(DataCacheFilePath);
                }
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error clearing data cache file: {e.Message}");
            }
        }

        private static void WriteTextToFile(string path, string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text ?? "", Encoding.UTF8);
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error writing to file {path}: {e.Message}");
            }
        }

        private static string ReadTextFromFile(string path)
        {
            if(!File.Exists(path))
            {
                return "";
            }
            try
            {
                return RuntimeFileLoader.LoadFileAsString(path) ?? "";
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Error reading file {path}: {e.Message}");
            }
            return "";
        }

        private static bool IsInstalledFromAssetStore()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());
            return info?.source == PackageSource.Registry && info.registry?.isDefault == true;
        }

        /// <summary>
        /// Reads the project GUID from the Unity ProjectSettings.asset file.
        /// </summary>
        /// <returns>The project GUID string, or an empty string if not found.</returns>
        public static string GetProjectGuid()
        {
            if (!File.Exists(ProjectSettingsAssetFilePath))
                return "";

            using (Stream stream = RuntimeFileLoader.LoadFileAsStream(ProjectSettingsAssetFilePath))
            {
                if (stream == null)
                {
                    LingotionLogger.Error("License verification could not complete! Failed to load ProjectSettings.asset file.");
                    return "";
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.TrimStart().StartsWith("productGUID:"))
                        {
                            return line.Split(':')[1].Trim();
                        }
                    }
                }
            }

            LingotionLogger.Error("License verification could not complete! Failed to find project GUID.");
            return "";
        }

    }
}
#endif