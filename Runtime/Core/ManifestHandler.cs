// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Lingotion.Thespeon.Core.IO;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Enum denoting the different character module types.
    /// </summary>
    public enum ModuleType
    {
        None,
        XS,
        S,
        M,
        L,
        XL
    }

    /// <summary>
    /// A simple representation of a module with its properties.
    /// </summary>
    public readonly struct ModuleEntry
    {
        public readonly string ModuleID;
        public readonly string JsonPath;

        /// <summary>
        /// Initializes a new instance of the ModuleEntry struct.
        /// </summary>
        /// <param name="id">The ID of the module.</param>
        /// <param name="path">The path to the module's JSON file.</param>
        public ModuleEntry(string id, string path)
        {
            ModuleID = id;
            JsonPath = path;
        }

        /// <summary>
        /// Checks if the module entry is empty.
        /// </summary>
        /// <returns>True if the ModuleID or JsonPath is empty, otherwise false.</returns>
        public bool IsEmpty()
        {
            return string.IsNullOrEmpty(ModuleID) || string.IsNullOrEmpty(JsonPath);
        }
    }

    /// <summary>
    /// Singleton that handles parsing and distributing information found in the manifest file.
    /// </summary>
    public class ManifestHandler
    {

        private static ManifestHandler _instance;
        /// <summary>
        /// Singleton reference.
        /// </summary>
        public static ManifestHandler Instance => _instance ??= new ManifestHandler();

        private static readonly string mappingInfoJsonPath = RuntimeFileLoader.ManifestPath;
        private JObject manifestData;
        /// <summary>
        /// Maps quality tag strings (e.g. "low", "high") to their corresponding <see cref="ModuleType"/> values.
        /// </summary>
        public static readonly Dictionary<string, ModuleType> StringToModuleType = new Dictionary<string, ModuleType>(StringComparer.OrdinalIgnoreCase)
        {
            { "ultralow", ModuleType.XS },
            { "low", ModuleType.S },
            { "mid", ModuleType.M },
            { "high", ModuleType.L },
            { "ultrahigh", ModuleType.XL }
        };
        /// <summary>
        /// Event raised when the manifest data has been re-parsed and updated.
        /// </summary>
        public static event Action OnDataChanged;

        private static readonly Dictionary<ModuleType, string> ModuleTypeToString = StringToModuleType.ToDictionary(pair => pair.Value, pair => pair.Key);

        private ManifestHandler()
        {
            manifestData = JObject.Parse(RuntimeFileLoader.LoadFileAsString(mappingInfoJsonPath));
            if (manifestData["character_modules"] == null) manifestData["character_modules"] = new JObject();
            if (manifestData["language_modules"] == null) manifestData["language_modules"] = new JObject();

        }


        /// <summary>
        /// Forces the handler to re-parse the manifest and signal its update.
        /// </summary>
        public void UpdateMappings()
        {
            try
            {
                manifestData = JObject.Parse(RuntimeFileLoader.LoadFileAsString(mappingInfoJsonPath));
                if (manifestData["character_modules"] == null) manifestData["character_modules"] = new JObject();
                if (manifestData["language_modules"] == null) manifestData["language_modules"] = new JObject();
                OnDataChanged?.Invoke();
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Failed to parse Lingotion manifest! Ensure you have imported at least one .lingotion file and recompile your project. {e.Message} {e.Source} {e.StackTrace}");
            }

        }

        /// <summary>
        /// Fetches all languages in the parsed manifest.
        /// </summary>
        /// <returns> A list of all languages found.</returns>
        public List<ModuleLanguage> GetAllLanguageModuleLanguages()
        {
            return manifestData["language_modules"].Children<JProperty>()
                .SelectMany(module => module.Value["languages"])
                .SelectMany(langProp => langProp.Values())
                .Select(langObj => langObj.ToObject<ModuleLanguage>())
                .Distinct()
                .ToList();
        }
        /// <summary>
        /// Fetches all character names in the parsed manifest.
        /// </summary>
        /// <returns> A list of all characters found.</returns>
        public List<string> GetAllCharacters()
        {
            return manifestData["character_modules"].Children<JProperty>()
                .SelectMany(module => module.Value["characters"])
                .Select(name => name.ToString())
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Fetches all unique available module types for a given character. Assumes there is only one module type per module.
        /// </summary>
        /// <param name="character">The name of the character to fetch module types for.</param>
        public List<ModuleType> GetAllModuleTypesForCharacter(string character)
        {
            return manifestData["character_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var characters = module["characters"]?.Values<string>() ?? Enumerable.Empty<string>();
                    return characters.Contains(character);
                })
                .Select(p => p.Value["quality"]?.ToString())
                .Where(q => !string.IsNullOrEmpty(q))
                .Select(q => StringToModuleType.TryGetValue(q, out var type) ? type : ModuleType.None)
                .Distinct()
                .OrderByDescending(type => type)
                .ToList();
        }

        /// <summary>
        /// Fetches all languages available for a given character and module type.
        /// </summary>
        /// <param name="characterName">The name of the character to fetch languages for.</param>
        /// <param name="type">The module type to filter languages by.</param>
        public Dictionary<string, ModuleLanguage> GetAllLanguagesForCharacterAndModuleType(string characterName, ModuleType type)
        {
            string moduleTypeString = ModuleTypeToString[type];

            return manifestData["character_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var characters = module["characters"]?.Values<string>() ?? Enumerable.Empty<string>();
                    var quality = module["quality"]?.ToString();

                    return characters.Contains(characterName) && quality == moduleTypeString;
                })
                .SelectMany(p =>
                {
                    if (p.Value["languages"] is not JObject languages)
                        return Enumerable.Empty<KeyValuePair<string, ModuleLanguage>>();

                    return languages.Properties().SelectMany(lang =>
                    {
                        if (lang.Value["dialects"] is not JObject dialects)
                            return Enumerable.Empty<KeyValuePair<string, ModuleLanguage>>();

                        return dialects.Properties().Select(dialect =>
                        {
                            var langEntry = dialect.Value.ToObject<ModuleLanguage>();
                            return new KeyValuePair<string, ModuleLanguage>(dialect.Name, langEntry);
                        });
                    });
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Fetches all languages available for a given character and module type.
        /// </summary>
        /// <param name="characterName">The name of the character to fetch languages for.</param>
        /// <param name="type">The module type to filter languages by.</param>
        public Dictionary<string, ModuleLanguage> GetAllDialectsInModuleLanguage(string characterName, ModuleType type, string iso639_2)
        {
            string moduleTypeString = ModuleTypeToString[type];

            return manifestData["character_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var characters = module["characters"]?.Values<string>() ?? Enumerable.Empty<string>();
                    var quality = module["quality"]?.ToString();

                    return characters.Contains(characterName) && quality == moduleTypeString;
                })
                .SelectMany(p =>
                {
                    if (p.Value["languages"] is not JObject languages)
                        return Enumerable.Empty<KeyValuePair<string, ModuleLanguage>>();

                    return languages.Properties().Where(languages => { return languages.Value["languagecode"].ToString() == iso639_2; }).SelectMany(lang =>
                    {
                        if (lang.Value["dialects"] is not JObject dialects)
                            return Enumerable.Empty<KeyValuePair<string, ModuleLanguage>>();

                        return dialects.Properties().Select(dialect =>
                        {
                            var langEntry = dialect.Value.ToObject<ModuleLanguage>();
                            return new KeyValuePair<string, ModuleLanguage>(dialect.Name, langEntry);
                        });
                    });
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Fetches all supported and imported languages for a given character and module type.
        /// </summary>
        /// <param name="characterName">The name of the character to fetch languages for.</param>
        /// <param name="type">The module type to fetch languages for.</param>
        /// <returns>A list of ModuleLanguage objects representing the supported languages.</returns>
        public List<ModuleLanguage> GetAllSupportedLanguages(string characterName, ModuleType type)
        {
            if (type == ModuleType.None) return new();
            string moduleTypeString = ModuleTypeToString[type];
            List<ModuleLanguage> languages = GetAllLanguagesForCharacterAndModuleType(characterName, type).Values
                .ToList();
            var availableIso639_2 = manifestData["language_modules"]
                .Children<JProperty>()
                .SelectMany(p => p.Value["languages"]
                    .Children<JProperty>()
                    .SelectMany(lang => lang.Value.Children<JObject>())
                    .Select(langObj => langObj["iso639_2"]?.ToString()))
                .Where(code => !string.IsNullOrEmpty(code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var requiredIso639_2 = manifestData["character_modules"].Children<JProperty>()
            .Where(p =>
            {
                var module = p.Value;
                var characters = module["characters"]?.Values<string>() ?? Enumerable.Empty<string>();
                var quality = module["quality"]?.ToString();

                return characters.Contains(characterName) && quality == moduleTypeString;
            })
            .SelectMany(p => p.Value["required_language_modules"].Children<JProperty>().Select(kvp => kvp.Name.ToString()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
            HashSet<string> supportedIso639_2 = availableIso639_2.Intersect(requiredIso639_2, StringComparer.OrdinalIgnoreCase).ToHashSet();
            return languages.Where(lang => supportedIso639_2.Contains(lang.Iso639_2, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Fetches all supported language codes for a given character and module type.
        /// </summary>
        /// <param name="characterName">The name of the character to fetch languages for.</param>
        /// <param name="type">The module type to fetch languages for.</param>
        /// <returns>A Dictionary mapping the English name of the language to the ISO639-2 language code.</returns>
        public Dictionary<string, string> GetAllSupportedLanguageCodes(string characterName, ModuleType type)
        {
            string moduleTypeString = ModuleTypeToString[type];

            return manifestData["character_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var characters = module["characters"]?.Values<string>() ?? Enumerable.Empty<string>();
                    var quality = module["quality"]?.ToString();

                    return characters.Contains(characterName) && quality == moduleTypeString;
                })
                .SelectMany(p =>
                {
                    if (p.Value["languages"] is not JObject languages)
                        return Enumerable.Empty<KeyValuePair<string, string>>();

                    return languages.Properties().Select(lang =>
                    {
                        var code = lang.Value["languagecode"]?.ToString();
                        var name = lang.Name;
                        return new KeyValuePair<string, string>(name, code);
                    }).Where(code => code.Key != null && code.Value != null);
                })
                .Distinct()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Finds a specific character module.
        /// </summary>
        /// <param name="characterName">Target character name.</param>
        /// <param name="type">Target module type.</param>
        /// <returns>A module entry of the corresponding character.</returns>
        public ModuleEntry GetCharacterModuleEntry(string characterName, ModuleType type)
        {
            string moduleTypeString = ModuleTypeToString[type];

            var moduleEntryData = manifestData["character_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var characters = module["characters"]?.Values<string>() ?? Enumerable.Empty<string>();
                    var quality = module["quality"]?.ToString();

                    return characters.Contains(characterName) && quality == moduleTypeString;
                })
                .Select(p => new
                {
                    ModuleId = p.Name,
                    JsonPath = p.Value["jsonpath"]?.ToString()
                })
                .FirstOrDefault();

            if (moduleEntryData == null)
            {
                return new ModuleEntry(null, null);
            }

            ModuleEntry result = new(moduleEntryData.ModuleId, moduleEntryData.JsonPath);

            return result;
        }

        /// <summary>
        /// Finds a specific language module.
        /// </summary>
        /// <param name="moduleName">Target module name.</param>
        /// <returns>A module entry of the corresponding language.</returns>
        public ModuleEntry GetLanguageModuleEntry(string moduleName)
        {
            var moduleEntryData = manifestData["language_modules"].Children<JProperty>()
                .Where(p =>
                {
                    return p.Name == moduleName;
                })
                .Select(p => new
                {
                    ModuleId = p.Name,
                    JsonPath = p.Value["jsonpath"]?.ToString()
                })
                .FirstOrDefault();
            if (moduleEntryData == null)
            {
                LingotionLogger.Warning($"The language {moduleName} has not been imported and use of its language will not be possible. Check Thespeon Info window for more information.");
                return new(string.Empty, string.Empty);
            }
            ModuleEntry result = new(moduleEntryData.ModuleId, moduleEntryData.JsonPath);

            return result;
        }

        /// <summary>
        /// Fetches all available character names.
        /// </summary>
        /// <returns>List of all character names.</returns>
        public List<string> GetAllCharacterNames()
        {
            return manifestData["character_modules"].Children<JProperty>()
                .Select(module => module.Value?["name"]?.ToString())
                .Where(name => name != null)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Returns the config filename for a file by its display name.
        /// </summary>
        /// <param name="name">The specific display name to find.</param>
        /// <returns>The config filename, or null if not found.</returns>
        public string GetConfigFilename(string name)
        {
            JToken importedConfigs = manifestData?["imported_configs"];
            if (importedConfigs != null && importedConfigs[name] != null)
            {
                return importedConfigs[name].ToString();
            }
            return null;
        }

        /// <summary>
        /// Fetches all available language names.
        /// </summary>
        /// <returns>List of all language names.</returns>
        public List<string> GetAllLanguageNames()
        {
            return manifestData["language_modules"].Children<JProperty>()
                .Select(module => module.Value?["name"]?.ToString())
                .Where(name => name != null)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Summarizes all module info inside a character.
        /// </summary>
        /// <param name="name">The specific name to find.</param>
        /// <returns>A list of strings summarizing the modules inside the character.</returns>
        public List<string> GetAllModuleInfoInCharacter(string name)
        {
            return manifestData["character_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var moduleName = module["name"]?.ToString();
                    return name.Equals(moduleName);
                })
                .Select(module =>
                {
                    var charactername = module.Value["characters"]?[0].ToString();
                    StringToModuleType.TryGetValue(module.Value["quality"]?.ToString(), out ModuleType quality);
                    JObject languages = (JObject)module.Value["languages"];
                    var languageNames = languages?.Properties().Select((lang) => lang.Name.ToString());
                    return "Character: " + charactername + ", Module Type: " + quality + ", Languages: " + string.Join(", ", languageNames);
                })
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Summarizes all module info inside an language module.
        /// </summary>
        /// <param name="name">The specific language name to find.</param>
        /// <returns>A list of strings summarizing the language modules.</returns>
        public List<string> GetAllModuleInfoInLanguage(string name)
        {
            return manifestData["language_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var moduleName = module["name"]?.ToString();
                    return name.Equals(moduleName);
                })
                .Select(module =>
                {
                    List<string> languages = module.Value["languages"]
                        .Children<JProperty>()
                        .SelectMany(prop => prop.Value
                            .Children()
                            .Select(lang => $"{prop.Name}: {lang["iso639_2"]?.ToString()}"))
                    .ToList();
                    return "Languages: " + string.Join(", ", languages);
                })
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Fetches all missing languages that are required by the character.
        /// This is useful for identifying which languages need to be installed for the character to function correctly.
        /// </summary>
        public List<string> GetMissingLanguages()
        {
            var available = manifestData["language_modules"]
                .Children<JProperty>()
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingLanguageKeys = manifestData["character_modules"]
                .Children<JProperty>()
                .SelectMany(module =>
                {
                    if (module.Value["required_language_modules"] is not JObject requiredLangs)
                        return Enumerable.Empty<string>();

                    return requiredLangs.Properties()
                        .Where(p => !available.Contains(p.Value.ToString()))
                        .Select(p => p.Name);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return missingLanguageKeys;
        }

        /// <summary>
        /// Fetches all module IDs from both characters and languages.
        /// </summary>
        /// <returns>A combined list of all module IDs.</returns>
        public List<string> GetAllModuleIDs()
        {
            HashSet<string> characterModuleIDs = manifestData["character_modules"].Children<JProperty>()
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            HashSet<string> languageModuleIDs = manifestData["language_modules"].Children<JProperty>()
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            characterModuleIDs.UnionWith(languageModuleIDs);
            return characterModuleIDs.ToList();
        }

        /// <summary>
        /// Checks if a file (by MD5) is shared across multiple modules.
        /// </summary>
        /// <param name="md5">The MD5 hash of the file to check.</param>
        /// <returns>True if the file is used by more than one module.</returns>
        public bool IsFileShared(string md5)
        {
            JToken fileUsage = manifestData?["file_usage"]?[md5];
            if (fileUsage is JArray usageArray)
            {
                return usageArray.Count > 1;
            }
            return false;
        }
    }

}