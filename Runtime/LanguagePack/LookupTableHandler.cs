// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using Lingotion.Thespeon.Core;
using System.Collections.Generic;
using System;
using System.Collections;

namespace Lingotion.Thespeon.LanguagePack
{
    /// <summary>
    /// Singleton that manages and registers runtime lookup tables for language modules.
    /// </summary>
    public class LookupTableHandler
    {
        private static LookupTableHandler _instance;
        /// <summary>
        /// Singleton reference.
        /// </summary>
        public static LookupTableHandler Instance => _instance ??= new LookupTableHandler();

        private Dictionary<string, RuntimeLookupTable> _availableLookupTables;

        private LookupTableHandler()
        {
            _availableLookupTables = new Dictionary<string, RuntimeLookupTable>();
        }

        /// <summary>
        /// Registers a language module's lookup table if not already registered.
        /// </summary>
        /// <param name="module">The language module to register lookup table for.</param>
        public void RegisterLookupTable(LanguageModule module)
        {
            string md5 = module.GetLookupTableID();

            if (_availableLookupTables.ContainsKey(md5))
            {
                return;
            }
            RuntimeLookupTable lookupTable = new(module.GetLookupTable());
            _availableLookupTables[md5] = lookupTable;
        }
        /// <summary>
        /// Registers a language module's lookup table if not already registered, yielding whenever necessary.
        /// </summary>
        /// <param name="module">The language module to register lookup table for.</param>
        /// <remarks>
        /// This method allows for asynchronous loading of the lookup table, allowing for non-blocking reading of large tables.
        /// </remarks>
        public IEnumerator RegisterLookupTableCoroutine(LanguageModule module, Func<bool> yieldCondition, Action onYield)
        {
            UnityEngine.Profiling.Profiler.BeginSample($"Thespeon Register lookup table coroutine for {module.moduleLanguage.Iso639_2}");
            string md5 = module.GetLookupTableID();

            if (_availableLookupTables.ContainsKey(md5))
            {
                UnityEngine.Profiling.Profiler.EndSample();
                yield break;
            }
            RuntimeLookupTable lookupTable = null;
            UnityEngine.Profiling.Profiler.EndSample();
            yield return module.GetLookupTableCoroutine(
                lookupTableDict =>
                {
                    lookupTable = new RuntimeLookupTable(lookupTableDict);
                },
                yieldCondition, onYield
            );
            if(lookupTable == null)
            {
                LingotionLogger.Error($"Failed to load lookup table for module: {module.ModuleID}");
                yield break;
            }
            _availableLookupTables[md5] = lookupTable;
        }
        /// <summary>
        /// Deregisters a language module's lookup table by its MD5 identifier.
        /// </summary>
        /// <param name="module">The language module to deregister lookup table for.</param>
        public void DeregisterTable(LanguageModule module)
        {
            string md5 = module.GetLookupTableID();

            if (!IsRegistered(md5)) return;
            _availableLookupTables.Remove(md5);
        }

        /// <summary>
        /// Retrieves a registered lookup table by its MD5 identifier.
        /// </summary>
        /// <param name="md5">The MD5 identifier of the lookup table.</param>
        /// <returns>The RuntimeLookupTable associated with the MD5 identifier, or null if not found.</returns>
        public RuntimeLookupTable GetLookupTable(string md5)
        {
            if (_availableLookupTables.TryGetValue(md5, out RuntimeLookupTable lookupTable))
            {
                return lookupTable;
            }
            else
            {
                LingotionLogger.Error($"Lookup table with MD5: {md5} not found.");
                return null;
            }
        }
        /// <summary>
        /// Clears all registered lookup tables for garbage collection.
        /// </summary>
        public void DisposeAndClear()
        {
            _availableLookupTables.Clear();
        }

        private bool IsRegistered(string md5)
        {
            return _availableLookupTables.ContainsKey(md5);
        }

    }

}