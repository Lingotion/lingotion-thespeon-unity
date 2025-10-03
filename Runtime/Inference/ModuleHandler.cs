// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.ActorPack;
using System.Linq;

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
        /// <returns>The acquired module of type T.</returns>
        /// <exception cref="InvalidCastException">Thrown when a module with the same ID exists but has a different type.</exception>
        public T AcquireModule<T>(ModuleEntry moduleEntry) where T : Module
        {
            if (_availableModules.TryGetValue(moduleEntry.ModuleID, out Module result))
            {
                if (result is T typedResult)
                {
                    return typedResult;
                }

                throw new InvalidCastException($"Module type mismatch: requested '{typeof(T)}', but found '{result.GetType()}'");
            }
            else
            {
                RegisterModule<T>(moduleEntry);
                return (T)_availableModules[moduleEntry.ModuleID];
            }
        }

        /// <summary>
        /// Deregisters and removes a module instance from the available modules.
        /// </summary>
        /// <typeparam name="T">The expected type of the module to deregister.</typeparam>
        /// <param name="moduleEntry">The entry containing the ID of the module to be removed.</param>
        /// <returns>The deregistered module cast to type <typeparamref name="T"/> if found and the type matches; otherwise, returns `default(T)`.</returns>
        /// <exception cref="InvalidCastException">Thrown if a module with the specified ID is found, but its actual type does not match the requested type <typeparamref name="T"/>.</exception>
        public T DeregisterModule<T>(ModuleEntry moduleEntry)
        {
            if (_availableModules.TryGetValue(moduleEntry.ModuleID, out Module module))
            {
                _availableModules.Remove(moduleEntry.ModuleID);
                if (module is T typedResult)
                {
                    return typedResult;
                }

                throw new InvalidCastException($"Module type mismatch: requested '{typeof(T)}', but found '{module.GetType()}'");
            }
            else
            {
                return default;
            }

        }

        /// <summary>
        /// Returns a set of MD5 hashes of all model files that are not used by any other module of the same type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="module">The module to check for overlapping model MD5s.</param>
        /// <returns></returns>
        public HashSet<string> GetNonOverlappingModelMD5s<T>(T module) where T : Module
        {
            HashSet<string> currentMD5s = module.GetAllFileMD5s();
            HashSet<string> otherMD5s = new();
            foreach (Module entry in _availableModules.Values)
            {
                if (entry is T entryTyped && entry != module)
                {
                    otherMD5s.UnionWith(entryTyped.GetAllFileMD5s());
                }
            }
            currentMD5s.ExceptWith(otherMD5s);
            return currentMD5s;
        }

        /// <summary>
        /// Returns a set of language module IDs used by the provided actorModule that are not used by any other actor module.
        /// </summary>
        /// <param name="actorModule">The actor module to check for unused language modules.</param>
        /// <returns>A set of unused language module IDs.</returns>
        public HashSet<string> GetNonOverlappingLangModules(ActorModule actorModule)
        {
            HashSet<string> unusedLanguageModules = actorModule.languageModuleIDs.Values.ToHashSet();
            HashSet<string> usedLanguageModules = new();
            foreach (Module module in _availableModules.Values)
            {
                if (module is ActorModule otherActorModule && otherActorModule != actorModule)
                {
                    usedLanguageModules.UnionWith(otherActorModule.languageModuleIDs.Values);
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
