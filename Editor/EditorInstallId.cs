// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;
using Lingotion.Thespeon.Core;

namespace Lingotion.Thespeon.Editor
{
    /// <summary>
    /// The anonymous identifier for this Thespeon install.
    /// </summary>
    /// <remarks>
    /// The id is a randomly generated UUIDv4 with no personal or machine-derived content. It is
    /// generated locally and never requested from the server, so it exists even with no network
    /// access, and it is written exactly once and reused forever after.
    /// </remarks>
    public static class EditorInstallId
    {
        private const string StateFolder = "UserSettings";
        private const string StateFileName = "Lingotion.Thespeon.install.json";
        private static string StatePath => Path.Combine(StateFolder, StateFileName);

        [Serializable]
        private struct InstallState
        {
            public string installId;
        }

        private static InstallState? _cached;

        /// <summary>
        /// The anonymous install id for this project and user, generated on first access.
        /// Never null or empty.
        /// </summary>
        public static string Value
        {
            get
            {
                InstallState state = Load();
                if (string.IsNullOrEmpty(state.installId))
                {
                    state.installId = Guid.NewGuid().ToString("D");
                    Save(state);
                }
                return state.installId;
            }
        }

        private static InstallState Load()
        {
            if (_cached.HasValue)
            {
                return _cached.Value;
            }
            InstallState state = default;
            try
            {
                if (File.Exists(StatePath))
                {
                    state = JsonUtility.FromJson<InstallState>(File.ReadAllText(StatePath, Encoding.UTF8));
                }
            }
            catch (Exception e)
            {
                LingotionLogger.Debug($"Could not read the Thespeon install state: {e.Message}");
            }

            _cached = state;
            return state;
        }

        private static void Save(InstallState state)
        {
            _cached = state;
            try
            {
                Directory.CreateDirectory(StateFolder);
                File.WriteAllText(StatePath, JsonUtility.ToJson(state), Encoding.UTF8);
            }
            catch (Exception e)
            {
                LingotionLogger.Debug($"Could not persist the Thespeon install state: {e.Message}");
            }
        }
    }
}
#endif
