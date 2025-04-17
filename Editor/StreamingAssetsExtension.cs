// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

#if (UNITY_EDITOR)
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Lingotion.Thespeon
{
    public static class StreamingAssetsExtension
    {
        /// <summary>
        /// Recursively traverses each folder under <paramref name="path"/> and returns the list of file paths. 
        /// It will only work in Editor mode.
        /// </summary>
        /// <param name="path">Relative to Application.streamingAssetsPath.</param>
        /// <param name="paths">List of file path strings.</param>
        /// <returns>List of file path strings.</returns>
        public static List<string> GetPathsRecursively(string path, ref List<string> paths)
        {
            var fullPath = Path.Combine(Application.streamingAssetsPath, path);
            DirectoryInfo dirInfo = new DirectoryInfo(fullPath);
            foreach (var file in dirInfo.GetFiles())
            {
                if (!file.Name.Contains(".meta"))
                {
                    paths.Add(Path.Combine(path, file.Name)); // With file extension
                }
            }

            foreach (var dir in dirInfo.GetDirectories())
            {
                GetPathsRecursively(Path.Combine(path, dir.Name), ref paths);
            }

            return paths;
        }

        public static List<string> GetPathsRecursively(string path)
        {
            List<string> paths = new List<string>();
            return GetPathsRecursively(path, ref paths);
        }
    }
}
#endif