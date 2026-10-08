// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lingotion.Thespeon.Core;

namespace Lingotion.Thespeon.Editor
{
    /// <summary>
    /// Reports the one step of the signup funnel that cannot be measured server-side: a user who
    /// opens the Thespeon Info Window but has not activated the package, and so may never reach
    /// the portal at all.
    /// </summary>
    /// <remarks>
    /// The report carries nothing but the anonymous <see cref="EditorInstallId"/>, the install
    /// origin, and the package and Unity versions. Everything from account creation onward is
    /// already measured by the portal.
    /// </remarks>
    public static class EditorInstallReporter
    {
        private const string SessionKey = "Lingotion.Thespeon.InstallOpenReported";
        private const int TimeoutSeconds = 3;

        /// <summary>
        /// Reports, at most once per editor session, that the Info Window was opened by an install
        /// that has not been activated yet.
        /// </summary>
        /// <remarks>
        /// Fire-and-forget: it never blocks the UI, and a failure is invisible to the user. The
        /// endpoint is still being built portal-side, so failures are expected for now.
        /// </remarks>
        public static void ReportUnactivatedWindowOpen()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }
            SessionState.SetBool(SessionKey, true);

            try
            {
                Send(BuildPayload());
            }
            catch (Exception e)
            {
                LingotionLogger.Debug($"Could not send the Thespeon install event: {e.Message}");
            }
        }

        private static string BuildPayload()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());
            var payload = new JObject
            {
                ["installId"] = EditorInstallId.Value,
                ["platform"] = "unity",
                ["origin"] = EditorLicenseKeyValidator.IsAssetStoreInstall ? "assetstore" : "other",
                ["sdkVersion"] = info?.version,
                ["engineVersion"] = Application.unityVersion
            };
            return payload.ToString(Formatting.None);
        }

        private static void Send(string json)
        {
            var request = new UnityWebRequest(EditorLingotionUrls.InstallEvents, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");

            request.SendWebRequest().completed += _ =>
            {
                LingotionLogger.Debug($"Thespeon install event: {request.result} ({request.responseCode}).");
                request.Dispose();
            };
        }
    }
}
#endif
