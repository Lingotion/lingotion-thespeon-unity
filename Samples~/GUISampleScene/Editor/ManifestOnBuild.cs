using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;


[InitializeOnLoad]
public class ManifestOnBuild : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } } // Order of execution
    private static readonly string folderPath = Path.Combine(Application.dataPath, "Samples", "Lingotion Thespeon", "0.1.0", "Demo GUI", "ModelInputSamples");
    private static readonly string targetDirectory = Path.Combine(Application.streamingAssetsPath, "LingotionRuntimeFiles", "ModelInputSamples");
    private static readonly string manifestPath = Path.Combine(targetDirectory, "lingotion_model_input.manifest");

    // Called before the build starts
    static ManifestOnBuild() 
    {
        ProcessFiles(PlayModeStateChange.ExitingEditMode);
        AssetDatabase.Refresh();
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        ProcessFiles(PlayModeStateChange.ExitingEditMode);
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

            // Debug.LogError($"Manifest and files moved to: {targetDirectory}");
        }
    }
}
