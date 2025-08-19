// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lingotion.Thespeon.Inputs;
using Lingotion.Thespeon.Core;

namespace Lingotion.Thespeon.Editor
{
    [InitializeOnLoad]
    /// <summary>
    /// Automatically generates character assets for all imported actors and module types.
    /// The assets are stored in your Project under Assets/Lingotion Thespeon/CharacterAssets and can be used to easily select the desired actor in your scene.
    /// </summary>
    public static class LingotionCharacterAssetGenerator
    {
        private readonly static string targetFolder = Path.Combine("Assets", "Lingotion Thespeon", "CharacterAssets");
        static LingotionCharacterAssetGenerator()
        {
            PackManifestHandler.OnDataChanged += GenerateAssets;
        }
        /// <summary>
        /// Generates or updates character assets based on the current PackManifest data.
        /// This method is called automatically on changes to folder LingotionRuntimeFiles
        /// </summary>
        private static void GenerateAssets()
        {
            List<(string actorName, ModuleType moduleType)> actorData = PackManifestHandler.Instance
                .GetAllActors()
                .SelectMany(actorName =>
                PackManifestHandler.Instance.GetAllModuleTypesForActor(actorName)
                    .Select(moduleType => (actorName, moduleType)))
                .ToList();

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
                AssetDatabase.Refresh();
            }

            HashSet<string> expectedFiles = new(
                actorData.Select(a => $"{SanitizeFileName(a.actorName)}-{a.moduleType}.asset")
            );

            string[] existingAssets = Directory.GetFiles(targetFolder, "*.asset", SearchOption.TopDirectoryOnly);
            foreach (string fullPath in existingAssets)
            {
                string fileName = Path.GetFileName(fullPath);
                if (!expectedFiles.Contains(fileName))
                {
                    string assetDbPath = fullPath.Replace(Path.DirectorySeparatorChar, '/');
                    AssetDatabase.DeleteAsset(assetDbPath);
                }
            }

            ThespeonCharacterAsset asset = null;

            foreach (var (actorName, moduleType) in actorData)
            {
                string fileName = $"{SanitizeFileName(actorName)}-{moduleType}.asset";
                string assetPath = Path.Combine(targetFolder, fileName).Replace(Path.DirectorySeparatorChar, '/');
                if (AssetDatabase.LoadAssetAtPath<ThespeonCharacterAsset>(assetPath) != null) continue;
                asset = ScriptableObject.CreateInstance<ThespeonCharacterAsset>();
                asset.actorName = actorName;
                asset.moduleType = moduleType;

                AssetDatabase.CreateAsset(asset, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        private static string SanitizeFileName(string input)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return new string(input.Where(ch => !invalidChars.Contains(ch)).ToArray()).Trim();
        }
    }
}
#endif
