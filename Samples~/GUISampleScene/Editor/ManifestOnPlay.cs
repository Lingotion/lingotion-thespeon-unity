using UnityEngine;
using UnityEditor;
using System.IO;


[InitializeOnLoad]
public class ManifestOnPlay 
{
    private static readonly string folderPath = Path.Combine(Application.dataPath, "Samples", "Lingotion Thespeon", "0.2.0", "Demo GUI", "ModelInputSamples");
    private static readonly string targetDirectory = Path.Combine(Application.streamingAssetsPath, "LingotionRuntimeFiles", "ModelInputSamples");
    private static readonly string manifestPath = Path.Combine(targetDirectory, "lingotion_model_input.manifest");

    // Called before playmode starts
    static ManifestOnPlay()
    {
        ProcessFiles(PlayModeStateChange.ExitingEditMode);
        EditorApplication.playModeStateChanged += ProcessFiles;
    }

    private static void ProcessFiles(PlayModeStateChange state)
    {
        if(state == PlayModeStateChange.ExitingEditMode)
        {
            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning($"ManifestGenerator: Source folder '{folderPath}' does not exist.");
                return;
            }

            // Ensure target directory exists
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            string[] files = Directory.GetFiles(folderPath, "*.json");
            using (StreamWriter writer = new StreamWriter(manifestPath))
            {
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string targetPath = Path.Combine(targetDirectory, fileName);
                    File.Copy(file, targetPath, true);
                    writer.WriteLine(fileName);
                }
            }

            // Debug.Log($"Manifest and files moved to: {targetDirectory}");
            AssetDatabase.Refresh();
        }
    }
} 