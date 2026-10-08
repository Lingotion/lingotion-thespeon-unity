// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

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
    /// Automatically generates character assets for all imported characters and module types.
    /// The assets are stored in your Project under Assets/Lingotion Thespeon/CharacterAssets and can be used to easily select the desired character in your scene.
    /// </summary>
    public static class LingotionCharacterAssetGenerator
    {
        private readonly static string targetFolder = Path.Combine("Assets", "Lingotion Thespeon", "CharacterAssets");
        static LingotionCharacterAssetGenerator()
        {
            ManifestHandler.OnDataChanged += GenerateAssets;
        }
        /// <summary>
        /// Generates or updates character assets based on the current Manifest data.
        /// This method is called automatically on changes to folder LingotionRuntimeFiles
        /// </summary>
        private static void GenerateAssets()
        {
            List<(string characterName, ModuleType moduleType, string version)> characterData = ManifestHandler.Instance
                .GetAllCharacters()
                .SelectMany(characterName =>
                ManifestHandler.Instance.GetAllModuleTypesForCharacter(characterName)
                    .Select(moduleType => (characterName, moduleType, version: ManifestHandler.Instance.GetCharacterModuleVersion(characterName, moduleType))))
                .ToList();

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
                AssetDatabase.Refresh();
            }

            HashSet<string> expectedFiles = new(
                characterData.Select(a => $"{SanitizeFileName(a.characterName)}-{a.moduleType}.asset")
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
            int totalChangedAssets = 0;
            foreach (var (characterName, moduleType, version) in characterData)
            {
                string fileName = $"{SanitizeFileName(characterName)}-{moduleType}.asset";
                string assetPath = Path.Combine(targetFolder, fileName).Replace(Path.DirectorySeparatorChar, '/');
                ThespeonCharacterAsset existing = AssetDatabase.LoadAssetAtPath<ThespeonCharacterAsset>(assetPath);
                if (existing != null)
                {
                    if(existing.characterName == characterName && existing.moduleType == moduleType && existing.moduleVersion == version)
                    {
                        continue;
                    }
                    asset = existing;
                    LingotionLogger.Debug($"Outdated asset exists for character {characterName} with module type {moduleType}, updating content to version {version}.");
                    asset.characterName = characterName;
                    asset.moduleType = moduleType;
                    asset.moduleVersion = version;
                    EditorUtility.SetDirty(asset);
                    totalChangedAssets++;
                }
                else
                {
                    LingotionLogger.Debug($"Creating new asset for character {characterName} with module type {moduleType} at version {version}.");
                    asset = ScriptableObject.CreateInstance<ThespeonCharacterAsset>();
                    asset.characterName = characterName;
                    asset.moduleType = moduleType;
                    asset.moduleVersion = version;
                    AssetDatabase.CreateAsset(asset, assetPath);
                    totalChangedAssets++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (totalChangedAssets > 0)
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
