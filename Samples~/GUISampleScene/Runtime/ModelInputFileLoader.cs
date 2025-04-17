using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Lingotion.Thespeon.FileLoader;


namespace Lingotion.Thespeon.ModelInputFileLoader
{
    public static class ModelInputFileLoader
    {
        public static string modelInputSamplesPath=Path.Combine(Application.streamingAssetsPath, "LingotionRuntimeFiles", "ModelInputSamples");
        private static string manifestPath = Path.Combine(Application.streamingAssetsPath, "LingotionRuntimeFiles", "ModelInputSamples", "lingotion_model_input.manifest");

        public static List<string> GetJsonFileList()
        {
            if (string.IsNullOrEmpty(manifestPath))
            {
                Debug.LogError($"Manifest file not found at: {manifestPath}");
                return new List<string>();
            }

            try
            {
                string manifestContent=RuntimeFileLoader.LoadFileAsString(manifestPath);
                List<string> result = new List<string>();
                string[] lines = manifestContent.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);                //split content on newline
                foreach (string line in lines){
                    result.Add(line.Trim());
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read manifest file: {ex.Message}");
                return new List<string>();
            }
        }
    }
}