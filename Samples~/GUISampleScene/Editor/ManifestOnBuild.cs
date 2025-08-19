// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;



[InitializeOnLoad]
public class ManifestOnBuild : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } } // Order of execution
    private static readonly string folderPath = GetInputSamplesPackagePath();
    private static readonly string targetDirectory = Path.Combine(Application.streamingAssetsPath, "LingotionRuntimeFiles", "ModelInputSamples");
    private static readonly string manifestPath = Path.Combine(targetDirectory, "lingotion_model_input.manifest");

    // Called before the build starts
    static ManifestOnBuild() 
    {
        ProcessFiles(PlayModeStateChange.ExitingEditMode);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// This method is called before the build process starts.
    /// It processes the files in the specified folder and generates a manifest file.
    /// </summary>
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
                Debug.LogError($"ManifestGenerator: Source folder '{folderPath}' does not exist.");
                return;
            }

            // Ensure target directory exists and clean it
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, true); // true = recursive delete
            }
            Directory.CreateDirectory(targetDirectory);

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
        }
    }
    private static string GetInputSamplesPackagePath()
        {
            string packageName= "com.lingotion.thespeon";
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ManifestOnBuild).Assembly);

            if (packageInfo != null && packageInfo.name == packageName)
            {
                return Path.Combine(packageInfo.resolvedPath, "ModelInputSamples");
            }

            // Otherwise search manually
            foreach (var package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()) 
            {
                if (package.name == packageName)
                    return Path.Combine(package.resolvedPath, "ModelInputSamples");
            }

            Debug.LogError($"Package '{packageName}' not found.");
            return null;

        }
}