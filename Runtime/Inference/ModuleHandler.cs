// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Character;
using System.Linq;
using Unity.InferenceEngine;

namespace Lingotion.Thespeon.Inference
{
    /// <summary>
    /// Handles the registration and management of modules used in inference.
    /// </summary>
    public class ModuleHandler
    {
        private static ModuleHandler _instance;
        /// <summary>
        /// Singleton reference.
        /// </summary>
        public static ModuleHandler Instance => _instance ??= new ModuleHandler();
        private Dictionary<string, Module> _availableModules;
        private ModuleHandler()
        {
            _availableModules = new Dictionary<string, Module>();
        }

        /// <summary>
        /// Registers a new module of type T with the provided module entry.
        /// </summary>
        /// <typeparam name="T">The type of the module to register.</typeparam>
        /// <param name="moduleEntry">The module entry containing the module information.</param>
        public void RegisterModule<T>(ModuleEntry moduleEntry) where T : Module
        {
            if (!_availableModules.ContainsKey(moduleEntry.ModuleID))
            {
                T newModule = (T)Activator.CreateInstance(typeof(T), moduleEntry);
                _availableModules[moduleEntry.ModuleID] = newModule;
            }
        }

        /// <summary>
        /// Acquires a module of type T using the provided module entry.
        /// If the module is not already registered, it will be created and registered.
        /// </summary>
        /// <typeparam name="T">The type of the module to acquire.</typeparam>
        /// <param name="moduleEntry">The module entry containing the module information.</param>
        /// <param name="shouldCreate">Optional default true. If true, returns module found or creates if it does not exist. If false, the method will return default(T) if the module is not found.</param>
        /// <returns>The acquired module of type T.</returns>
        /// <exception cref="InvalidCastException">Thrown when a module with the same ID exists but has a different type.</exception>
        public T AcquireModule<T>(ModuleEntry moduleEntry, bool shouldCreate=true) where T : Module
        {
            if (_availableModules.TryGetValue(moduleEntry.ModuleID, out Module result))
            {
                if (result is T typedResult)
                {
                    return typedResult;
                }

                throw new InvalidCastException($"Module type mismatch: requested '{typeof(T)}', but found '{result.GetType()}'");
            }
            else if (shouldCreate)
            {
                RegisterModule<T>(moduleEntry);
                LingotionLogger.Info($"Registering module {moduleEntry.ModuleID}.");
                return (T)_availableModules[moduleEntry.ModuleID];
            }
            else
            {
                return default;
            }
            
        }

        /// <summary>
        /// Deregisters and removes a module instance from the available modules. Does nothing if the module is not found. 
        /// If a backend type is provided, only removes the backend from the module's loaded backends, and only removes the module entirely if it has no more loaded backends after the removal.
        /// </summary>
        /// <param name="moduleEntry">The entry containing the ID of the module to be removed.</param>
        public void DeregisterModule(string moduleID)
        {
            if (_availableModules.TryGetValue(moduleID, out Module module))
            {
                if(module.GetLoadedBackends().Count == 0)
                {
                    LingotionLogger.Info($"Deregistering module {module.ModuleID}.");
                    _availableModules.Remove(moduleID);
                }
            }
        }

        /// <summary>
        /// Returns a set of MD5 hashes of all model files that are not used by any other module of the same type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="module">The module to check for overlapping model MD5s.</param>
        /// <returns></returns>
        public HashSet<string> GetWorkloadIDsToRemove<T>(T module, BackendType backendType) where T : Module
        {
            if(!module.GetLoadedBackends().Contains(backendType))
            {
                LingotionLogger.Debug($"Module {module.ModuleID} does not have any workloads for backend {backendType}, nothing to deregister.");
                return new HashSet<string>();
            }
            HashSet<string> currentMD5s = module.GetLoadedWorkloadIDs(backendType);
            HashSet<string> otherMD5s = new();
            foreach (Module entry in _availableModules.Values)
            {
                if (entry is T entryTyped && entry != module)
                {
                    otherMD5s.UnionWith(entryTyped.GetLoadedWorkloadIDs(backendType));
                }
            }
            currentMD5s.ExceptWith(otherMD5s);
            return currentMD5s;
        }

        /// <summary>
        /// Returns a set of language module IDs used by the provided characterModule that are not used by any other character module.
        /// </summary>
        /// <param name="characterModule">The character module to check for unused language modules.</param>
        /// <returns>A set of unused language module IDs.</returns>
        public HashSet<string> GetNonOverlappingLangModules(CharacterModule characterModule)
        {
            HashSet<string> unusedLanguageModules = characterModule.languageModuleIDs.Values.ToHashSet();
            HashSet<string> usedLanguageModules = new();
            foreach (Module module in _availableModules.Values)
            {
                if (module is CharacterModule otherCharacterModule && otherCharacterModule != characterModule)
                {
                    usedLanguageModules.UnionWith(otherCharacterModule.languageModuleIDs.Values);
                }
            }
            unusedLanguageModules.ExceptWith(usedLanguageModules);
            return unusedLanguageModules;
        }

        /// <summary>
        /// Clears all registered modules.
        /// </summary>
        public void Clear()
        {
            _availableModules.Clear();
        }

    }

}
