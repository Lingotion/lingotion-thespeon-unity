// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Core.IO;
using Lingotion.Thespeon.Editor.VS;

namespace Lingotion.Thespeon.Editor
{
    public partial class EditorInfoWindow
    {
        private const int SupportedModuleVersionMajor = 3;
        private VisualElement _registerRoot;
        private VisualElement _licenseRoot;
        private VisualElement _downloadTokenRoot;
        private VisualElement _functionalRoot;
        private TextField _licenseField;
        private VisualElement _downloadProgressContainer;
        private Label _downloadProgressLabel;
        private HelpBox _licenseErrorHelpBox;
        private HelpBox _downloadTokenErrorHelpBox;

        private void SetupLicenseGate()
        {
            _registerRoot = rootVisualElement.Q<VisualElement>("RegisterRoot");
            _licenseRoot = rootVisualElement.Q<VisualElement>("LicenseRoot");
            _downloadTokenRoot = rootVisualElement.Q<VisualElement>("DownloadTokenRoot");
            _functionalRoot = rootVisualElement.Q<VisualElement>("FunctionalRoot");
            _licenseField = rootVisualElement.Q<TextField>("LicenseField");

            _downloadProgressContainer = rootVisualElement.Q<VisualElement>("DownloadProgressContainer");
            _downloadProgressLabel = rootVisualElement.Q<Label>("DownloadProgressLabel");
            _licenseErrorHelpBox = rootVisualElement.Q<HelpBox>("LicenseErrorHelpBox");
            _downloadTokenErrorHelpBox = rootVisualElement.Q<HelpBox>("DownloadTokenErrorHelpBox");
            string savedLicense = EditorLicenseKeyValidator.LoadLicenseFromFile();
            _licenseField.SetValueWithoutNotify(savedLicense);

            rootVisualElement.Q<Button>("LicenseHelpBoxButton").clicked += () => Application.OpenURL(EditorLingotionUrls.PortalHome);

            rootVisualElement.Q<Button>("AlreadyHaveAccountButton").clicked += () => NavigateTo(_licenseRoot);
            rootVisualElement.Q<Button>("CreateAccountButton").clicked += () => {
                // find out where the user got the package from
                string originString = EditorLicenseKeyValidator.IsAssetStoreInstall ? "assetstore" : "other";
                string client = UnityWebRequest.EscapeURL(BuildClientCapabilities());
                Application.OpenURL(EditorLingotionUrls.Activate(originString, client));
                NavigateTo(_downloadTokenRoot);
            };
            rootVisualElement.Q<Button>("LicenseBackButton").clicked += () => NavigateTo(_registerRoot);
            rootVisualElement.Q<Button>("LicenseSubmitButton").clicked += () => {ValidateAndGate();};
            rootVisualElement.Q<Button>("DownloadTokenBackButton").clicked += () => NavigateTo(_registerRoot);
            rootVisualElement.Q<Button>("DownloadTokenSubmitButton").clicked += RedeemDownloadToken;

            NavigateTo(string.IsNullOrEmpty(savedLicense) ? _registerRoot : _licenseRoot);
        }

        /// <summary>
        /// Describes this package to the portal so it only hands back models this version can import.
        /// Shape: {"sdkVersion":"x.y.z","capabilities":{"moduleVersion":{"major":n}}}
        /// </summary>
        private static string BuildClientCapabilities()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(EditorInfoWindow).Assembly);
            var client = new JObject
            {
                ["sdkVersion"] = info?.version,
                ["capabilities"] = new JObject
                {
                    ["moduleVersion"] = new JObject { ["major"] = SupportedModuleVersionMajor }
                }
            };
            return client.ToString(Formatting.None);
        }

        private void NavigateTo(VisualElement target)
        {
            _registerRoot.style.display = DisplayStyle.None;
            _licenseRoot.style.display = DisplayStyle.None;
            _downloadTokenRoot.style.display = DisplayStyle.None;
            _functionalRoot.style.display = DisplayStyle.None;
            _licenseErrorHelpBox.style.display = DisplayStyle.None;
            _downloadTokenErrorHelpBox.style.display = DisplayStyle.None;
            target.style.display = DisplayStyle.Flex;
        }

        private void ToggleValid(bool enabled)
        {
            if (enabled)
            {
                NavigateTo(_functionalRoot);
            }
            else if (_functionalRoot.style.display == DisplayStyle.Flex)
            {

                NavigateTo(_licenseRoot);
            }
            // Otherwise: stay on current gate screen
        }

        private void GateValidationResult(EditorLicenseKeyValidator.ValidationResult result)
        {
            switch (result)
            {
                case EditorLicenseKeyValidator.ValidationResult.Valid:
                    ToggleValid(true);
                    break;

                case EditorLicenseKeyValidator.ValidationResult.Invalid:
                    ToggleValid(false);
                    break;

                case EditorLicenseKeyValidator.ValidationResult.Indeterminate:
                    // Do nothing — keep current state (e.g., connectivity/server issue)
                    break;
            }
        }

        private async void ValidateAndGate()
        {
            string licenseKey = _licenseField.value.Trim();
            EditorLicenseKeyValidator.SaveLicenseToFile(licenseKey);
            var result = await EditorLicenseKeyValidator.ValidateLicenseAsync(licenseKey);
            _licenseErrorHelpBox.style.display = result == EditorLicenseKeyValidator.ValidationResult.Invalid
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _downloadTokenErrorHelpBox.style.display = DisplayStyle.None;
            if (result == EditorLicenseKeyValidator.ValidationResult.Valid)
                SendAttribution(licenseKey, "Login");
            GateValidationResult(result);
        }

        /// <summary>
        /// Sends a Unity Verified Solutions Attribution event, but only for Asset Store installs.
        /// Git/local/embedded installs are intentionally never attributed.
        /// </summary>
        private void SendAttribution(string licenseKey, string actionName)
        {
            if (!EditorLicenseKeyValidator.IsAssetStoreInstall)
                return;

            const string partnerName = "Lingotion";
            VSAttribution.SendAttributionEvent(actionName, partnerName, licenseKey);
        }

        private async void RedeemDownloadToken()
        {
            string token = rootVisualElement.Q<TextField>("DownloadTokenField").value.Trim();
            using var request = UnityWebRequest.PostWwwForm(EditorLingotionUrls.RedeemLicenseToken(token), "");
            var tcs = new TaskCompletionSource<bool>();
            request.SendWebRequest().completed += _ => tcs.SetResult(true);
            await tcs.Task;
            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<RedeemResponse>(request.downloadHandler.text);
                _licenseField.SetValueWithoutNotify(response.licenseKey);
                EditorLicenseKeyValidator.SaveLicenseToFile(response.licenseKey);

                var validationResult = await EditorLicenseKeyValidator.ValidateLicenseAsync(response.licenseKey);
                if (validationResult != EditorLicenseKeyValidator.ValidationResult.Valid)
                {
                    GateValidationResult(validationResult);
                    return;
                }

                SendAttribution(response.licenseKey, "Registration");

                _downloadProgressContainer.style.display = DisplayStyle.Flex;
                var tempPaths = new List<string>();
                for (int i = 0; i < response.modelUrls.Length; i++)
                {
                    string tempPath = await DownloadModelFile(EditorLingotionUrls.PortalRoot + response.modelUrls[i], i + 1, response.modelUrls.Length);
                    if (tempPath != null)
                        tempPaths.Add(tempPath);
                }
                _downloadProgressLabel.text = "Importing, please wait...";
                foreach (string path in tempPaths)
                {
                    try { EditorImporter.ImportThespeonFromPath(path); }
                    finally { if (File.Exists(path)) File.Delete(path); }
                }
                _downloadProgressContainer.style.display = DisplayStyle.None;
                _downloadTokenErrorHelpBox.style.display = DisplayStyle.None;
                GateValidationResult(validationResult);
            }
            else
            {
                _downloadTokenErrorHelpBox.text = request.result == UnityWebRequest.Result.ConnectionError
                    ? "Could not reach the server. Check your internet connection and try again."
                    : "Download token is invalid.";
                _downloadTokenErrorHelpBox.style.display = DisplayStyle.Flex;
            }
        }

        private async Task<string> DownloadModelFile(string url, int index, int total)
        {
            string tempPath = Path.Combine(Application.temporaryCachePath, $"{Guid.NewGuid()}.lingotion");
            using var request = UnityWebRequest.Get(url);
            DownloadHandlerFile downloadHandler = new DownloadHandlerFile(tempPath);
            downloadHandler.removeFileOnAbort = true;
            request.downloadHandler = downloadHandler;
            _ = request.SendWebRequest();

            while (!request.isDone)
            {
                float mb = request.downloadedBytes / 1048576f;
                _downloadProgressLabel.text = $"Downloading file {index} of {total} ({mb:F1} MB)";
                await Task.Yield();
            }
            float finalMb = request.downloadedBytes / 1048576f;
            _downloadProgressLabel.text = $"Downloading file {index} of {total} ({finalMb:F1} MB)";

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to download model from {url}: {request.error}");
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                return null;
            }

            return tempPath;
        }
        
        [Serializable]
        private class RedeemResponse
        {
            public string licenseKey;
            public string[] modelUrls;
        }
    }
}
