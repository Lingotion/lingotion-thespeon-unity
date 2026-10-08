// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;
using System.Collections;

namespace Lingotion.Thespeon.Core.IO
{
    /// <summary>
    /// Static class that fetches files for the Thespeon Package.
    /// </summary>
    public static class RuntimeFileLoader
    {
        /// <summary>
        /// Runtime files subdirectory name.
        /// </summary>
        private static readonly string StreamingAssetsSubdirectory = "LingotionRuntimeFiles";

        /// <summary>
        /// Manifest file name.
        /// </summary>
        public static readonly string ManifestFileName = "LingotionThespeonManifest.json";

        /// <summary>
        /// Path to the manifest.
        /// </summary>
        public static readonly string ManifestPath = Path.Combine(Application.streamingAssetsPath, StreamingAssetsSubdirectory, ManifestFileName);
        /// <summary>
        /// Path to the used runtime files.
        /// </summary>
        public static readonly string RuntimeFiles = Path.Combine(Application.streamingAssetsPath, StreamingAssetsSubdirectory);
        /// <summary>
        /// Unity-relative path to the used runtime files.
        /// </summary>
        public static readonly string RelativeRuntimeFiles = Path.Combine("Assets", "StreamingAssets", StreamingAssetsSubdirectory);

        /// <summary>
        /// Creates an absolute runtime path to a file by filename.
        /// </summary>
        /// <param name="filename">The filename to resolve.</param>
        /// <returns>An absolute path pointing to the runtime file.</returns>
        public static string GetRuntimePath(string filename)
        {
            return Path.Combine(RuntimeFiles, filename);
        }

        /// <summary>
        /// Creates a Unity-relative runtime path to a file by filename.
        /// </summary>
        /// <param name="filename">The filename to resolve.</param>
        /// <returns>A Unity-relative path pointing to the runtime file.</returns>
        public static string GetRelativeRuntimePath(string filename)
        {
            return Path.Combine(RelativeRuntimeFiles, filename);
        }
        /// <summary>
        /// Fetches the subdirectory path of a file, regardless of directory separator.
        /// </summary>
        /// <param name="filename"> A target file path. </param>
        /// <returns> A path pointing to the directory of the file. </returns>
        public static string GetDirectoryPath(string filename)
        {
            return Path.GetDirectoryName(filename.Replace('\\', Path.DirectorySeparatorChar)
                                .Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Gets the directory of a file.
        /// </summary>
        /// <param name="filePath">The absolute path to the file.</param>
        /// <returns>The directory of the file.</returns>
        public static string GetDirectory(string filePath)
        {
            int index = filePath.LastIndexOf(Path.DirectorySeparatorChar);
            if (index < 0) throw new ArgumentException("File path " + filePath + " is a root path.");
            string parentPath = filePath.Substring(0, index);
            return parentPath;
        }

        /// <summary>
        /// Loads a file and returns it as a Stream.
        /// </summary>
        /// <param name="filePath">The absolute path to the file.</param>
        /// <returns>A Stream (FileStream or MemoryStream) if the file is successfully loaded; otherwise, null.</returns>
        public static Stream LoadFileAsStream(string filePath)
        {
            string path = Path.GetFullPath(filePath);

            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                path = @"\\?\" + path;
            }

            if (Application.platform == RuntimePlatform.Android)
            {
                UnityWebRequest request = UnityWebRequest.Get(filePath);
                request.SendWebRequest();

                while (!request.isDone)
                {
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    byte[] data = request.downloadHandler.data;
                    return new MemoryStream(data);
                }
                else
                {
                    LingotionLogger.Error($"Failed to load file {path}: {request.error}");
                    return null;
                }
            }
            else
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return new FileStream(path, FileMode.Open, FileAccess.Read);
                    }
                    catch (Exception e)
                    {
                        LingotionLogger.Error($"Error loading file {path}: {e.Message}");
                        return null;
                    }
                }
                else
                {

                    throw new FileNotFoundException($"File not found: {path}");
                }
            }
        }

        /// <summary>
        /// Moves a directory from a source to a destination.
        /// </summary>
        /// <param name="src">Source directory file path</param>
        /// <param name="dest">Destination directory file path</param>
        /// <param name="isUnityFile">If a corresponding .meta file should be affected as well.</param>
        public static void MoveDirectory(string src, string dest, bool isUnityFile = false)
        {
            CopyDirectory(src, dest, isUnityFile);
            DeleteDirectory(src, isUnityFile);
        }

        /// <summary>
        /// Recursively copies a directory from a source to a destination.
        /// </summary>
        /// <param name="src">Source directory file path</param>
        /// <param name="dest">Destination directory file path</param>
        /// <param name="isUnityFile">If a corresponding .meta file should be affected as well.</param>
        public static void CopyDirectory(string src, string dest, bool isUnityFile = false)
        {
            Directory.CreateDirectory(dest);
            foreach (var srcFile in Directory.GetFiles(src))
            {
                var destFile = Path.Combine(dest, Path.GetFileName(srcFile));

                File.Copy(srcFile, destFile, overwrite: true);
                if (isUnityFile)
                {
                    string metaSrc = srcFile + ".meta";
                    string metaDest = destFile + ".meta";
                    if (File.Exists(metaSrc))
                    {
                        File.Copy(metaSrc, metaDest, overwrite: true);
                    }
                }
            }

            foreach (var srcDir in Directory.GetDirectories(src))
            {
                var destSubdir = Path.Combine(dest, Path.GetFileName(srcDir));
                CopyDirectory(srcDir, destSubdir, isUnityFile);

                if (isUnityFile)
                {
                    string metaSrc = srcDir + ".meta";
                    string metaDest = destSubdir + ".meta";
                    if (File.Exists(metaSrc))
                    {
                        File.Copy(metaSrc, metaDest, overwrite: true);
                    }
                }
            }
        }

        /// <summary>
        /// Deletes a directory.
        /// </summary>
        /// <param name="filePath">Target directory path to delete.</param>
        /// <param name="isUnityFile">If a corresponding .meta file should be affected as well.</param>
        public static void DeleteDirectory(string filePath, bool isUnityFile = false)
        {
            if (Directory.Exists(filePath))
            {
                Directory.Delete(filePath, true);
            }
            if (isUnityFile)
            {
                string metaFile = filePath + ".meta";
                if (File.Exists(metaFile))
                {
                    File.Delete(metaFile);
                }
            }
        }

        /// <summary>
        /// Loads a specifed file as a string.
        /// </summary>
        /// <param name="filePath"> The path to the target file.</param>
        /// <returns>File contents as a string.</returns>
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

        /// <summary>
        /// Loads a lookup table file line by line and yields whenever the condition is met,
        /// invoking the onBatchComplete callback with the current batch of entries and onYield callback.
        /// </summary>
        /// <param name="filePath"> The path to the target lookup table file.</param>
        /// <param name="onBatchComplete"> Callback invoked with the current batch of entries when the yield condition is met.</param>
        /// <param name="yieldCondition"> A function that returns true when the coroutine should yield.</param>
        /// <param name="onYield"> Callback invoked after yielding.</param>
        public static IEnumerator LoadLookupTable(string filePath, Action<Dictionary<string, string>> onBatchComplete, Func<bool> yieldCondition, Action onYield)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Thespeon Load Table Entry Batch");
            using Stream stream = LoadFileAsStream(filePath);
            if (stream == null)
            {
                onBatchComplete?.Invoke(null);
                UnityEngine.Profiling.Profiler.EndSample();
                yield break;
            }

            using StreamReader reader = new(stream);
            string jsonOpen = reader.ReadLine();
            string license = reader.ReadLine();
            Dictionary<string, string> res = new();
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line) || !line.Contains(":"))
                    continue;

                string cleanLine = line.Trim().TrimEnd(',');

                int colonIndex = cleanLine.IndexOf(':');
                if (colonIndex < 0)
                    continue;

                string key = cleanLine.Substring(0, colonIndex).Trim().Trim('"');
                string value = cleanLine.Substring(colonIndex + 1).Trim().Trim('"');

                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    res[key] = value;
                }
                if (yieldCondition.Invoke())
                {
                    onBatchComplete?.Invoke(res);
                    res.Clear();
                    UnityEngine.Profiling.Profiler.EndSample();
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    onYield?.Invoke();
                    UnityEngine.Profiling.Profiler.BeginSample("Thespeon Load Table Entry Batch");
                }
            }

            onBatchComplete?.Invoke(res);
            UnityEngine.Profiling.Profiler.EndSample();
        }

        /// <summary>
        /// Recursively copies a directory to a destination.
        /// </summary>
        /// <param name="src"> Directory to copy.</param>
        /// <param name="dest"> Destination to copy to.</param>
        private static void CopyDirectory(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var srcFile in Directory.GetFiles(src))
            {
                var destFile = Path.Combine(dest, Path.GetFileName(srcFile));
                File.Copy(srcFile, destFile, overwrite: true);
            }

            foreach (var srcDir in Directory.GetDirectories(src))
            {
                var destSubdir = Path.Combine(dest, Path.GetFileName(srcDir));
                CopyDirectory(srcDir, destSubdir);
            }
        }

    }


}