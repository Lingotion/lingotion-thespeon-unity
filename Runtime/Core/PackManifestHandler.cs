// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Lingotion.Thespeon.Core.IO;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Enum denoting the different actor module types.
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
    /// Singleton that handles parsing and distributing information found in the pack manifest file.
    /// </summary>
    public class PackManifestHandler
    {

        private static PackManifestHandler _instance;
        /// <summary>
        /// Singleton reference.
        /// </summary>
        public static PackManifestHandler Instance => _instance ??= new PackManifestHandler();

        private static readonly string mappingInfoJsonPath = RuntimeFileLoader.PackManifestPath;
        private JObject packManifestData;
        public static readonly Dictionary<string, ModuleType> StringToModuleType = new Dictionary<string, ModuleType>(StringComparer.OrdinalIgnoreCase)
        {
            { "ultralow", ModuleType.XS },
            { "low", ModuleType.S },
            { "mid", ModuleType.M },
            { "high", ModuleType.L },
            { "ultrahigh", ModuleType.XL }
        };
        public static event Action OnDataChanged;

        private static readonly Dictionary<ModuleType, string> ModuleTypeToString = StringToModuleType.ToDictionary(pair => pair.Value, pair => pair.Key);

        private PackManifestHandler()
        {
            packManifestData = JObject.Parse(RuntimeFileLoader.LoadFileAsString(mappingInfoJsonPath));
            if (packManifestData["actorpack_modules"] == null) packManifestData["actorpack_modules"] = new JObject();
            if (packManifestData["languagepack_modules"] == null) packManifestData["languagepack_modules"] = new JObject();

        }


        /// <summary>
        /// Forces the handler to re-parse the manifest and signal its update.
        /// </summary>
        public void UpdateMappings()
        {
            try
            {
                packManifestData = JObject.Parse(RuntimeFileLoader.LoadFileAsString(mappingInfoJsonPath));
                if (packManifestData["actorpack_modules"] == null) packManifestData["actorpack_modules"] = new JObject();
                if (packManifestData["languagepack_modules"] == null) packManifestData["languagepack_modules"] = new JObject();
                OnDataChanged?.Invoke();
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Failed to parse PackManifest.json: {e.Message} {e.Source} {e.StackTrace}");
            }

        }

        /// <summary>
        /// Fetches all languages in the parsed manifest.
        /// </summary>
        /// <returns> A list of all languages found.</returns>
        public List<ModuleLanguage> GetAllLanguageModuleLanguages()
        {
            return packManifestData["languagepack_modules"].Children<JProperty>()
                .SelectMany(module => module.Value["languages"])
                .SelectMany(langProp => langProp.Values())
                .Select(langObj => langObj.ToObject<ModuleLanguage>())
                .Distinct()
                .ToList();
        }
        /// <summary>
        /// Fetches all actor names in the parsed manifest.
        /// </summary>
        /// <returns> A list of all actors found.</returns>
        public List<string> GetAllActors()
        {
            return packManifestData["actorpack_modules"].Children<JProperty>()
                .SelectMany(module => module.Value["actors"])
                .Select(name => name.ToString())
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Fetches all unique available module types for a given actor. Assumes there is only one module type per module.
        /// </summary>
        /// <param name="actor">The name of the actor to fetch module types for.</param>
        public List<ModuleType> GetAllModuleTypesForActor(string actor)
        {
            return packManifestData["actorpack_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var actors = module["actors"]?.Values<string>() ?? Enumerable.Empty<string>();
                    return actors.Contains(actor);
                })
                .Select(p => p.Value["quality"]?.ToString())
                .Where(q => !string.IsNullOrEmpty(q))
                .Select(q => StringToModuleType.TryGetValue(q, out var type) ? type : ModuleType.None)
                .Distinct()
                .OrderByDescending(type => type)
                .ToList();
        }

        /// <summary>
        /// Fetches all languages available for a given actor and module type.
        /// </summary>
        /// <param name="actorName">The name of the actor to fetch languages for.</param>
        /// <param name="type">The module type to filter languages by.</param>
        public Dictionary<string, ModuleLanguage> GetAllLanguagesForActorAndModuleType(string actorName, ModuleType type)
        {
            string moduleTypeString = ModuleTypeToString[type];

            return packManifestData["actorpack_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var actors = module["actors"]?.Values<string>() ?? Enumerable.Empty<string>();
                    var quality = module["quality"]?.ToString();

                    return actors.Contains(actorName) && quality == moduleTypeString;
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
        /// Fetches all languages available for a given actor and module type.
        /// </summary>
        /// <param name="actorName">The name of the actor to fetch languages for.</param>
        /// <param name="type">The module type to filter languages by.</param>
        public Dictionary<string, ModuleLanguage> GetAllDialectsInModuleLanguage(string actorName, ModuleType type, string iso639_2)
        {
            string moduleTypeString = ModuleTypeToString[type];

            return packManifestData["actorpack_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var actors = module["actors"]?.Values<string>() ?? Enumerable.Empty<string>();
                    var quality = module["quality"]?.ToString();

                    return actors.Contains(actorName) && quality == moduleTypeString;
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
        /// Fetches all supported languages for a given actor and module type that have an imported language pack.
        /// </summary>
        /// <param name="actorName">The name of the actor to fetch languages for.</param>
        /// <param name="type">The module type to fetch languages for.</param>
        /// <returns>A list of ModuleLanguage objects representing the supported languages.</returns>
        public List<ModuleLanguage> GetAllSupportedLanguages(string actorName, ModuleType type)
        {
            if (type == ModuleType.None) return new();
            string moduleTypeString = ModuleTypeToString[type];
            List<ModuleLanguage> languages = GetAllLanguagesForActorAndModuleType(actorName, type).Values
                .ToList();
            var availableIso639_2 = packManifestData["languagepack_modules"]
                .Children<JProperty>()
                .SelectMany(p => p.Value["languages"]
                    .Children<JProperty>()
                    .SelectMany(lang => lang.Value.Children<JObject>())
                    .Select(langObj => langObj["iso639_2"]?.ToString()))
                .Where(code => !string.IsNullOrEmpty(code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var requiredIso639_2 = packManifestData["actorpack_modules"].Children<JProperty>()
            .Where(p =>
            {
                var module = p.Value;
                var actors = module["actors"]?.Values<string>() ?? Enumerable.Empty<string>();
                var quality = module["quality"]?.ToString();

                return actors.Contains(actorName) && quality == moduleTypeString;
            })
            .SelectMany(p => p.Value["required_language_modules"].Children<JProperty>().Select(kvp => kvp.Name.ToString()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
            HashSet<string> supportedIso639_2 = availableIso639_2.Intersect(requiredIso639_2, StringComparer.OrdinalIgnoreCase).ToHashSet();
            return languages.Where(lang => supportedIso639_2.Contains(lang.Iso639_2, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Fetches all supported language codes for a given actor and module type.
        /// </summary>
        /// <param name="actorName">The name of the actor to fetch languages for.</param>
        /// <param name="type">The module type to fetch languages for.</param>
        /// <returns>A Dictionary mapping the English name of the language to the ISO639-2 language code.</returns>
        public Dictionary<string, string> GetAllSupportedLanguageCodes(string actorName, ModuleType type)
        {
            string moduleTypeString = ModuleTypeToString[type];

            return packManifestData["actorpack_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var actors = module["actors"]?.Values<string>() ?? Enumerable.Empty<string>();
                    var quality = module["quality"]?.ToString();

                    return actors.Contains(actorName) && quality == moduleTypeString;
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
        /// Fetches the language codes of all languages
        /// </summary>
        /// <returns>An IEnumerator containing all installed language codes.</returns>
        public IEnumerable<string> GetAllLanguageCodes()
        {
            return GetAllLanguagesPerModule()
                .Values
                .SelectMany(list => list)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code);
        }

        /// <summary>
        /// Finds a specific actor module.
        /// </summary>
        /// <param name="actorName">Target actor name.</param>
        /// <param name="type">Target module type.</param>
        /// <returns>A module entry of the corresponding actor pack.</returns>
        public ModuleEntry GetActorPackModuleEntry(string actorName, ModuleType type)
        {
            string moduleTypeString = ModuleTypeToString[type];

            var moduleEntryData = packManifestData["actorpack_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var actors = module["actors"]?.Values<string>() ?? Enumerable.Empty<string>();
                    var quality = module["quality"]?.ToString();

                    return actors.Contains(actorName) && quality == moduleTypeString;
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
        /// <returns>A module entry of the corresponding language pack.</returns>
        public ModuleEntry GetLanguagePackModuleEntry(string moduleName)
        {
            var moduleEntryData = packManifestData["languagepack_modules"].Children<JProperty>()
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
                LingotionLogger.Warning($"A language pack has not been imported and use of its language will not be possible. Check Thespeon Info window for more information.");
                return new(string.Empty, string.Empty);
            }
            ModuleEntry result = new(moduleEntryData.ModuleId, moduleEntryData.JsonPath);

            return result;
        }

        /// <summary>
        /// Fetches the information about a language spoken by an actor.
        /// </summary>
        /// <param name="actorName">Target actor name.</param>
        /// <param name="iso639_2">Language of which to find parameters for.</param>
        /// <returns>A list of ModuleLanguages for that specific actor and language.</returns>
        public List<ModuleLanguage> GetAccentsForActorAndLanguage(string actorName, string iso639_2)
        {

            return packManifestData["actorpack_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var actors = module["actors"]?.Values<string>() ?? Enumerable.Empty<string>();

                    return actors.Contains(actorName);
                })
                .SelectMany(p =>
                {
                    if (p.Value["languages"] is not JObject languages)
                        return Enumerable.Empty<ModuleLanguage>();

                    return languages.Properties().SelectMany(lang =>
                    {
                        if (lang.Value["dialects"] is not JObject dialects)
                            return Enumerable.Empty<ModuleLanguage>();

                        return dialects.Properties().Select(dialect =>
                        {
                            var langEntry = dialect.Value.ToObject<ModuleLanguage>();
                            return langEntry;
                        });
                    });
                })
                .ToList();
        }

        /// <summary>
        /// Fetches all available actor pack names.
        /// </summary>
        /// <returns>List of all actor pack names.</returns>
        public List<string> GetAllActorPackNames()
        {
            return packManifestData["actorpack_modules"].Children<JProperty>()
                .Select(module => module.Value["packname"].ToString())
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Returns the location of a pack
        /// </summary>
        /// <param name="packName">The specific pack to find</param>
        /// <returns>A string containing the unity-relative path to the pack</returns>
        public string GetPackDirectory(string packName)
        {
            return packManifestData["imported_pack_directories"][packName].ToString();
        }

        /// <summary>
        /// Fetches all available language pack names.
        /// </summary>
        /// <returns>List of all language pack names.</returns>
        public List<string> GetAllLanguagePackNames()
        {
            return packManifestData["languagepack_modules"].Children<JProperty>()
                .Select(module => module.Value["packname"].ToString())
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Fetches all available pack names.
        /// </summary>
        /// <returns>List of all pack names.</returns>
        public List<string> GetAllPackNames()
        {
            List<string> actorPackNames = GetAllActorPackNames();
            List<string> languagePackNames = GetAllLanguagePackNames();
            actorPackNames.AddRange(languagePackNames);

            return actorPackNames.Distinct().ToList();
        }

        /// <summary>
        /// Summarizes all module info inside an actor pack.
        /// </summary>
        /// <param name="packName">The specific pack to find.</param>
        /// <returns>A list of strings summarizing the modules inside the actor pack.</returns>
        public List<string> GetAllModuleInfoInActorPack(string packName)
        {
            return packManifestData["actorpack_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var modulePackName = module["packname"]?.ToString();
                    return packName.Equals(modulePackName);
                })
                .Select(module =>
                {
                    var actorname = module.Value["actors"]?[0].ToString();
                    StringToModuleType.TryGetValue(module.Value["quality"]?.ToString(), out ModuleType quality);
                    JObject languages = (JObject)module.Value["languages"];
                    var languageNames = languages?.Properties().Select((lang) => lang.Name.ToString());
                    return "Actor: " + actorname + ", Module Type: " + quality + ", Languages: " + string.Join(", ", languageNames);
                })
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Summarizes all module info inside an language pack.
        /// </summary>
        /// <param name="packName">The specific pack to find.</param>
        /// <returns>A list of strings summarizing the modules inside the language pack.</returns>
        public List<string> GetAllModuleInfoInLanguagePack(string packName)
        {
            return packManifestData["languagepack_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var modulePackName = module["packname"]?.ToString();
                    return packName.Equals(modulePackName);
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
        /// Fetches all missing language packs that are required by the actor packs.
        /// This is useful for identifying which language packs need to be installed for the actor packs to function correctly.
        /// </summary>
        public List<string> GetMissingLanguagePacks()
        {
            var available = packManifestData["languagepack_modules"]
                .Children<JProperty>()
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingLanguageKeys = packManifestData["actorpack_modules"]
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
        /// Fetches all installed language modules and their languages.
        /// </summary>
        /// <returns>A dictionary of module name to language names.</returns>
        public IReadOnlyDictionary<string, List<string>> GetAllLanguagesPerModule()
        {
            return packManifestData["languagepack_modules"]
                .Children<JProperty>()
                .ToDictionary(
                    p => p.Name,
                    p => p.Value["languages"]
                              .Values<string>()
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList(),
                    StringComparer.OrdinalIgnoreCase
                );
        }
        
        /// <summary>
        /// Scans the manifest for all installed languages and returns a map of the language code to its pack information.
        /// </summary>
        /// <returns>A dictionary mapping a language's ISO 639-2 code to a tuple containing the pack name and version.</returns>
        public Dictionary<string, (string packName, Version version)> GetAllLanguagesAndTheirPacks()
        {
            Dictionary<string, (string packName, Version version)> languageMap = new Dictionary<string, (string, Version)>(StringComparer.OrdinalIgnoreCase);

            JObject langPackModules = packManifestData?["languagepack_modules"] as JObject;
            if (langPackModules == null)
            {
                return languageMap;
            }

            foreach (JProperty moduleProperty in langPackModules.Properties())
            {
                JObject moduleData = moduleProperty.Value as JObject;
                if (moduleData == null) continue;

                string packName = moduleData["packname"]?.ToString();
                string jsonPath = moduleData["jsonpath"]?.ToString();

                if (string.IsNullOrEmpty(packName) || string.IsNullOrEmpty(jsonPath)) continue;

                Version version = new Version(0, 0, 0);
                try
                {
                    string fullJsonPath = RuntimeFileLoader.GetLanguagePackFile(jsonPath);
                    JObject packConfig = JObject.Parse(RuntimeFileLoader.LoadFileAsString(fullJsonPath));
                    JToken versionToken = packConfig?["version"];
                    if (versionToken != null)
                    {
                        version = new Version(versionToken["major"].Value<int>(), versionToken["minor"].Value<int>(), versionToken["patch"].Value<int>());
                    }
                }
                catch (Exception ex)
                {
                    LingotionLogger.Warning($"Could not parse version for language pack '{packName}'. It may be corrupt. Error: {ex.Message}");
                }

                JObject languagesObj = moduleData["languages"] as JObject;
                if (languagesObj == null) continue;

                List<string> languageCodes = languagesObj.Properties()
                    .SelectMany(prop => prop.Value)
                    .Select(langToken => langToken["iso639_2"]?.ToString())
                    .Where(code => !string.IsNullOrEmpty(code))
                    .ToList();

                foreach (string langCode in languageCodes)
                {
                    if (!languageMap.ContainsKey(langCode))
                    {
                        languageMap[langCode] = (packName, version);
                    }
                }
            }
            return languageMap;
        }

    }

}