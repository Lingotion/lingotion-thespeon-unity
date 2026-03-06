// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Lingotion.Thespeon.Core;

namespace Lingotion.Thespeon.EditorTools
{
    [CustomEditor(typeof(ThespeonDefaultSettings))]
    public class ThespeonDefaultSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);

            if (GUILayout.Button("Reset to Defaults"))
            {
                var settings = (ThespeonDefaultSettings)target;
                Undo.RecordObject(settings, "Reset Thespeon Default Settings");
                settings.ResetToHardCodedDefaults();
                EditorUtility.SetDirty(settings);
            }
        }

        /// <summary>
        /// Automatically ensures the ThespeonDefaultSettings asset exists after every recompile.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void EnsureSettingsAssetExists()
        {
            // Delay to avoid running during import/compile when AssetDatabase isn't ready.
            EditorApplication.delayCall += CreateSettingsAssetIfMissing;
        }
        public static void CreateSettingsAssetIfMissing()
        {
            const string folder = "Assets/Lingotion Thespeon/Resources";
            const string path   = folder + "/ThespeonDefaultSettings.asset";

            if (AssetDatabase.LoadAssetAtPath<ThespeonDefaultSettings>(path) != null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Lingotion Thespeon"))
                AssetDatabase.CreateFolder("Assets", "Lingotion Thespeon");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Lingotion Thespeon", "Resources");

            var asset = ScriptableObject.CreateInstance<ThespeonDefaultSettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Debug.Log("Created ThespeonDefaultSettings asset at " + path);
        }
    }
}
#endif