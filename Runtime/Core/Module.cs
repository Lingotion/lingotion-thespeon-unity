// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections;
using System.Collections.Generic;
using Lingotion.Thespeon.Core.IO;
using Unity.InferenceEngine;

namespace Lingotion.Thespeon.Core
{

    /// <summary>
    /// Class describing the common parameters of a module.
    /// </summary>
    public abstract class Module
    {
        public readonly string ModuleID, JsonPath, Version, DirectoryPath;
        protected Dictionary<string, ModuleFile> InternalFileMappings;
        
        protected Dictionary<string, string> InternalModelMappings;

        /// <summary>
        /// Initializes a new instance of the <see cref="Module"/> class with the specified module
        /// entry information.
        /// </summary>
        /// <param name="moduleInfo">The module entry containing the ID, JSON path, and version.</param>
        /// <exception cref="ArgumentNullException">Thrown when the module ID or JSON path is null.</exception>
        protected Module(ModuleEntry moduleInfo)
        {
            // add check if ID is empty, or check if path actually exists
            if (moduleInfo.ModuleID == null || moduleInfo.JsonPath == null)
                throw new ArgumentNullException("Module entry parameter is null.");
            ModuleID = moduleInfo.ModuleID;
            JsonPath = moduleInfo.JsonPath;
            DirectoryPath = RuntimeFileLoader.GetDirectoryPath(JsonPath);
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
        /// <param name="md5s">A set of MD5 strings of already existing module bindings.</param>
        /// <returns>True if the module is fully contained in the set of MD5s, false otherwise.</returns>
        /// <exception cref="NotImplementedException">Thrown if the method is not implemented in the derived class.</exception>
        public abstract bool IsIncludedIn(HashSet<string> md5s);

        /// <summary>
        /// Gets all MD5s of the files in this module.
        /// </summary>
        /// <returns>A set of MD5 strings representing all files in the module.</returns>
        /// <exception cref="NotImplementedException">Thrown if the method is not implemented in the derived class.</exception>
        public abstract HashSet<string> GetAllFileMD5s();

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
    }

    /// <summary>
    /// Represents a runtime binding holding a worker and its model.
    /// </summary>
    public struct ModelRuntimeBinding
    {
        public Worker worker;
        public Model model;
    }

    /// <summary>
    /// Represents a file associated with a module, including its path, MD5 hash, and type.
    /// </summary>
    public struct ModuleFile
    {
        public string filePath;
        public string md5;

        public ModuleFile(string filePath, string md5)
        {
            this.filePath = filePath;
            this.md5 = md5;
        }
    }


}