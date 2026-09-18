// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lingotion.Thespeon.Core.IO;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine;

namespace Lingotion.Thespeon.Core
{

    /// <summary>
    /// Class describing the common parameters of a module.
    /// </summary>
    public abstract class Module
    {
        /// <summary>
        /// The unique identifier for this module.
        /// </summary>
        public readonly string ModuleID;
        /// <summary>
        /// The path to the module's JSON configuration file.
        /// </summary>
        public readonly string JsonPath;
        /// <summary>
        /// The version string of this module.
        /// </summary>
        public readonly string Version;
        protected Dictionary<string, ModuleFile> InternalFileMappings;

        protected Dictionary<string, string> InternalModelMappings;

        protected HashSet<BackendType> LoadedBackends = new();

        private Metaonnx.MetaGraph _metaGraph;

        /// <summary>
        /// Gets whether this module has a MetaGraph describing how its models are executed.
        /// </summary>
        public bool HasMetaGraph => _metaGraph != null;

        /// <summary>
        /// Gets the MetaGraph for this module, or null if the module ships without one.
        /// </summary>
        public Metaonnx.MetaGraph MetaGraph => _metaGraph;

        /// <summary>
        /// Initializes a new instance of the <see cref="Module"/> class with the specified module
        /// entry information.
        /// </summary>
        /// <param name="moduleInfo">The module entry containing the ID, JSON path, and version.</param>
        /// <exception cref="ArgumentNullException">Thrown when the module ID or JSON path is null.</exception>
        protected Module(ModuleEntry moduleInfo)
        {
            if (moduleInfo.ModuleID == null || moduleInfo.JsonPath == null)
                throw new ArgumentNullException("Module entry parameter is null.");
            ModuleID = moduleInfo.ModuleID;
            JsonPath = moduleInfo.JsonPath;
            Version = moduleInfo.Version;
        }

        /// <summary>
        /// Parses the files array from a module config, building file and model mappings.
        /// </summary>
        /// <param name="files">The JSON "files" array from the config.</param>
        /// <param name="isModelFile">Predicate that determines whether a file extension represents a model file for runtime binding purposes.</param>
        protected void ParseModuleFiles(JArray files, Func<string, bool> isModelFile)
        {
            Dictionary<string, ModuleFile> internalModuleFiles = new();
            Dictionary<string, string> internalModelMappings = new();

            if (files != null)
            {
                foreach (JObject fileEntry in files)
                {
                    string name = fileEntry["name"]?.ToString();
                    string md5 = fileEntry["md5"]?.ToString();
                    string ext = fileEntry["extension"]?.ToString();

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(md5) || string.IsNullOrEmpty(ext))
                        continue;

                    string filename = $"{md5}.{ext}";
                    string filePath = RuntimeFileLoader.GetRuntimePath(filename);

                    internalModuleFiles.Add(name, new ModuleFile(filePath, md5, ext));

                    if (isModelFile(ext) && !internalModelMappings.ContainsKey(name))
                    {
                        internalModelMappings.Add(name, md5);
                    }
                }
            }

            InternalFileMappings = internalModuleFiles;
            InternalModelMappings = internalModelMappings;
        }

        /// <summary>
        /// Attempts to load the module's MetaGraph from its file mappings. Call after
        /// <see cref="ParseModuleFiles"/>, which is what populates the mappings this reads.
        /// </summary>
        protected void TryLoadMetaGraph()
        {
            try
            {
                // Look for metagraph in file mappings
                if (!InternalFileMappings.TryGetValue("metagraph", out ModuleFile metagraphFile))
                {
                    // MetaGraph is optional
                    _metaGraph = null;
                    return;
                }

                using System.IO.Stream stream = RuntimeFileLoader.LoadFileAsStream(metagraphFile.filePath);
                if (stream != null)
                {
                    _metaGraph = Metaonnx.MetaGraph.Parser.ParseFrom(stream);
                    LingotionLogger.Info($"Loaded MetaGraph from {metagraphFile.GetFilename()} (version {_metaGraph.MajorVersion}.{_metaGraph.MinorVersion}.{_metaGraph.PatchVersion})");
                }
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Failed to load MetaGraph: {e.Message}");
                _metaGraph = null;
            }
        }

        /// <summary>
        /// Creates runtime bindings for the module's models, pairing them with their MD5s.
        /// </summary>
        /// <param name="md5s">MD5 strings of already existing bindings.</param>
        /// <param name="preferredBackedType">The preferred backend type for the models.</param>
        /// <returns>A dictionary mapping MD5 strings to their corresponding model runtime bindings.</returns>
        /// <exception cref="NotImplementedException">Thrown if the method is not implemented in the derived class.</exception>
        public abstract Dictionary<string, ModelRuntimeBinding> CreateRuntimeBindings(HashSet<string> md5s, BackendType preferredBackedType);

        /// <summary>
        /// Creates runtime bindings for the module's models, pairing them with their MD5s and yielding between each binding creation.
        /// </summary>
        /// <param name="md5s">MD5 strings of already existing bindings.</param>
        /// <param name="preferredBackendType">The preferred backend type for the models.</param>
        /// <param name="onComplete">Callback to invoke when all bindings are created.</param>
        /// <exception cref="NotImplementedException">Thrown if the method is not implemented in the derived class.</exception>
        public abstract IEnumerator CreateRuntimeBindingsCoroutine(HashSet<string> md5s, BackendType preferredBackendType, Action<Dictionary<string, ModelRuntimeBinding>> onComplete);

        /// <summary>
        /// Checks if the module is fully included in the provided set of MD5s.
        /// </summary>
        /// <param name="workloadIDs">A set of WorkloadIDs strings of already existing module bindings.</param>
        /// <param name="backend">The backend type to check the module's workloads against.</param>
        /// <returns>True if the module is fully contained in the set of MD5s, false otherwise.</returns>
        public bool IsIncludedIn(HashSet<string> workloadIDs, BackendType backend)
        {
            return InternalModelMappings.Values.All(md5 => workloadIDs.Contains(GetWorkloadID(md5, backend)));
        }

        /// <summary>
        /// Gets all WorkloadIDs of the given module for a specific backend if it is loaded on that backend.
        /// </summary>
        /// <param name="backend">The backend type to get WorkloadIDs for.</param>
        /// <returns>A set of WorkloadID strings representing combinations of backendtype and file in this module.</returns>
        public HashSet<string> GetLoadedWorkloadIDs(BackendType backend)
        {
            if(!LoadedBackends.Contains(backend))
            {
                return new HashSet<string>();

            }
            return InternalModelMappings.Values.Select(md5 => GetWorkloadID(md5, backend)).ToHashSet();
        }

        /// <summary>
        /// Gets the file MD5 of a given internal name.
        /// </summary>
        /// <param name="internalName">The internal name of the model.</param>
        /// <returns>The md5 of the model with the provided internal name.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the internal name does not exist in the internal model mappings.</exception>
        public string GetInternalModelID(string internalName)
        {
            if (!InternalModelMappings.TryGetValue(internalName, out string moduleID))
                throw new KeyNotFoundException($"Internal module not found: {internalName}");
            return moduleID;
        }

        /// <summary>
        /// Checks if a given MD5 exists in the module's model mappings.
        /// </summary>
        /// <param name="md5">The MD5 hash to check.</param>
        /// <returns>True if the MD5 exists in the model mappings, false otherwise.</returns>
        public bool HasModelMD5(string md5)
        {
            return InternalModelMappings.ContainsValue(md5);
        }

        /// <summary>
        /// Gets the set of backend types that this module is currently loaded on.
        /// </summary>
        /// <returns></returns>
        public HashSet<BackendType> GetLoadedBackends()
        {
            return LoadedBackends;
        }

        /// <summary>
        /// Adds a backend type to the set of loaded backends for this module, indicating that the module is now loaded on that backend.
        /// </summary>
        /// <param name="backend">The backend type to add.</param>
        public void AddLoadedBackend(BackendType backend)
        {
            LoadedBackends.Add(backend);
        }

        /// <summary>
        /// Removes a backend type from the set of loaded backends for this module, indicating that the module is no longer loaded on that backend. 
        /// If the provided backend is null, removes all backends from the set, effectively marking the module as not loaded on any backend.
        /// </summary>
        /// <param name="backend">The backend type to remove, or null to remove all backends.</param>
        public void RemoveLoadedBackend(BackendType? backend)
        {
            if(backend.HasValue)
            {
                LoadedBackends.Remove(backend.Value);
            }
            else
            {
                LoadedBackends.Clear();
            }
        }

        /// <summary>
        /// Generates a WorkloadID string based on the provided MD5 hash and backend type, following the format "{backend}_{md5}".
        /// </summary>
        /// <param name="md5">The MD5 hash of the model file.</param>
        /// <param name="backend">The backend type for which the WorkloadID is being generated.</param>
        /// <returns>>A string representing the WorkloadID, formatted as "{backend}_{md5}".</returns>
        public static string GetWorkloadID(string md5, BackendType backend)
        {
            return $"{backend}_{md5}";
        }
    }

    /// <summary>
    /// Represents a runtime binding holding a worker and its model.
    /// </summary>
    public struct ModelRuntimeBinding
    {
        /// <summary>
        /// The inference engine worker executing this model.
        /// </summary>
        public Worker worker;
        /// <summary>
        /// The loaded model asset.
        /// </summary>
        public Model model;
    }

    /// <summary>
    /// Represents a file associated with a module, including its path, MD5 hash, and extension.
    /// </summary>
    public struct ModuleFile
    {
        /// <summary>
        /// The runtime file path.
        /// </summary>
        public string filePath;
        /// <summary>
        /// The MD5 hash identifying this file.
        /// </summary>
        public string md5;
        /// <summary>
        /// The file extension (e.g. "onnx", "bin").
        /// </summary>
        public string extension;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleFile"/> struct.
        /// </summary>
        /// <param name="filePath">The runtime file path.</param>
        /// <param name="md5">The MD5 hash identifying this file.</param>
        /// <param name="extension">The file extension, or null if not applicable.</param>
        public ModuleFile(string filePath, string md5, string extension = null)
        {
            this.filePath = filePath;
            this.md5 = md5;
            this.extension = extension;
        }

        /// <summary>
        /// Gets the filename in the format "{md5}.{extension}".
        /// </summary>
        public string GetFilename()
        {
            if (string.IsNullOrEmpty(extension))
                return System.IO.Path.GetFileName(filePath);
            return $"{md5}.{extension}";
        }
    }


}