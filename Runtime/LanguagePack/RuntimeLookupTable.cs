// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using Lingotion.Thespeon.Core;
using System.Collections.Generic;

namespace Lingotion.Thespeon.LanguagePack
{
    /// <summary>
    /// Represents a runtime lookup table that can dynamically add entries and check for existing keys.
    /// </summary>
    public class RuntimeLookupTable
    {
        private Dictionary<string, string> staticLookupTable;
        private Dictionary<string, string> dynamicLookupTable;

        /// <summary>
        /// Initializes a new instance of the RuntimeLookupTable with a static lookup table.
        /// </summary>
        /// <param name="staticLookupTable">A dictionary containing static key-value pairs.</param>
        public RuntimeLookupTable(Dictionary<string, string> staticLookupTable)
        {
            this.staticLookupTable = staticLookupTable;
            dynamicLookupTable = new();
        }

        /// <summary>
        /// Tries to get the value associated with the specified key from the lookup tables.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The value associated with the key if found.</param>
        /// <returns>True if the key exists in either table, otherwise false.</returns>
        public bool TryGetValue(string key, out string value)
        {
            // [DevComment] Check static table first, then dynamic. After TUNI-271 the user provided should be checked first.
            if (staticLookupTable.TryGetValue(key, out value) || dynamicLookupTable.TryGetValue(key, out value))
            {
                return true;
            }

            LingotionLogger.Info($"Lookup for key '{key}' not found in either static or dynamic tables. Running phonemizer.");
            return false;
        }

        /// <summary>
        /// Checks if the specified key exists in either the static or dynamic lookup tables.
        /// </summary>
        /// <param name="key">The key to check for existence.</param>
        /// <returns>True if the key exists in either table, otherwise false.</returns>
        public bool ContainsKey(string key)
        {
            return staticLookupTable.ContainsKey(key) || dynamicLookupTable.ContainsKey(key);
        }

        /// <summary>
        /// Adds or updates an entry in the dynamic lookup table.
        /// If the key already exists, its value is updated; otherwise, a new entry is added.
        /// </summary>
        /// <param name="key">The key to add or update.</param>
        /// <param name="value">The value to associate with the key.</param>
        public void AddOrUpdateDynamicEntry(string key, string value)
        {
            if (dynamicLookupTable.ContainsKey(key))
            {
                dynamicLookupTable[key] = value;
            }
            else
            {
                dynamicLookupTable.Add(key, value);
            }
        }
    }
}
