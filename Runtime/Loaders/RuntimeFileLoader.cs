// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace Lingotion.Thespeon.FileLoader
{

    public static class RuntimeFileLoader
    {
        public static readonly string streamingAssetsSubPath = "LingotionRuntimeFiles";

        public static readonly string packMappingsPath = Path.Combine(Application.streamingAssetsPath, streamingAssetsSubPath, "PackMappings.json");

        public static readonly string RUNTIME_FILES = Path.Combine(Application.streamingAssetsPath, streamingAssetsSubPath);


        public static string[] GetLoadedActorPacks()
        {
            var config = ExtractJSON(GetPackagePath("LoadedModules.json"));
            return config["ActorPacks"].ToObject<string []>();
        }

            
        private static string GetPackagePath(string subdirectory)
        {
        
            string path = Path.Combine(RUNTIME_FILES, subdirectory);

            return path;
            
        }

        public static string GetActorPacksPath(bool relative = false)
        {
            return relative ? Path.Combine(RUNTIME_FILES, "ActorModules") : GetPackagePath("ActorModules");
        }

        public static string GetLanguagePacksPath(bool relative = false)
        {
            return relative ? Path.Combine(RUNTIME_FILES, "LanguagePacks") : GetPackagePath("LanguagePacks");
        }
        

        private static Dictionary<string, JToken> ExtractJSON(string jsonpath)
        {
            using (FileStream fs = File.OpenRead(jsonpath))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    string result = sr.ReadToEnd();
                    var config = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(result);
                    return config;
                }
            }
        }

        /// <summary>
        /// Loads a file and returns it as a Stream.
        /// </summary>
        /// <param name="filePath">The absolute path to the file.</param>
        /// <returns>A Stream (FileStream or MemoryStream) if the file is successfully loaded; otherwise, null.</returns>
        public static Stream LoadFileAsStream(string filePath)
        {
            // Magic spell to make windows not cut off paths longer than 260 chars
            string path = Path.GetFullPath(filePath);
        
            if(Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                path = @"\\?\" + path;  
            }

            if (Application.platform == RuntimePlatform.Android)
            {
                // Load file using UnityWebRequest on Android
                UnityWebRequest request = UnityWebRequest.Get(filePath);
                request.SendWebRequest();

                while (!request.isDone)
                {
                    // Wait for the request to complete
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    byte[] data = request.downloadHandler.data;
                    return new MemoryStream(data);
                }
                else
                {
                    Debug.LogError($"Failed to load file {path}: {request.error}");
                    return null;
                }
            }
            else
            {
                // Load file directly using FileStream on other platforms
                if (File.Exists(path))
                {
                    try
                    {
                        return new FileStream(path, FileMode.Open, FileAccess.Read);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error loading file {path}: {e.Message}");
                        return null;
                    }
                }
                else
                {
                    Debug.LogError($"File not found: {path}");
                    return null;
                }
            }
        }

        public static string LoadFileAsString(string filePath)
        {
            
            using (Stream stream = LoadFileAsStream(filePath))
            {
                if (stream == null)
                    return null;

                using (StreamReader reader = new StreamReader(stream))
                {
                    string content = reader.ReadToEnd();
                    return content;
                }
            }
        }

    }


}