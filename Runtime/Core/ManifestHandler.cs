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
        /// The version of the module, formatted as "major.minor.patch".
        /// </summary>
        public readonly string Version;

        /// <summary>
        /// Initializes a new instance of the ModuleEntry struct.
        /// </summary>
        /// <param name="id">The ID of the module.</param>
        /// <param name="path">The path to the module's JSON file.</param>
        /// <param name="version">The version of the module, formatted as "major.minor.patch".</param>
        public ModuleEntry(string id, string path, string version = null)
        {
            ModuleID = id;
            JsonPath = path;
            Version = version;
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
    /// A module's semantic version as it is written to the manifest.
    /// </summary>
    public readonly struct ModuleVersion : IComparable<ModuleVersion>
    {
        /// <summary>
        /// The only character module major this package can run. Character modules of any other major are
        /// rejected on import, and the portal is told to serve only this major.
        /// </summary>
        public const int SupportedCharacterModuleMajor = 4;

        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;

        /// <summary>
        /// Initializes a new instance of the ModuleVersion struct.
        /// </summary>
        /// <param name="major">Major version, bumped when a module stops being interchangeable with earlier builds.</param>
        /// <param name="minor">Minor version.</param>
        /// <param name="patch">Patch version.</param>
        public ModuleVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Reads a version from its manifest or config representation.
        /// </summary>
        /// <param name="versionToken">Token to read, expected to be an object with integer "major", "minor" and "patch".</param>
        /// <param name="version">The parsed version, or default if the token holds no version.</param>
        /// <returns>True if the token held a complete version.</returns>
        public static bool TryParse(JToken versionToken, out ModuleVersion version)
        {
            version = default;
            if (versionToken is not JObject versionObject
                || versionObject["major"]?.Type != JTokenType.Integer
                || versionObject["minor"]?.Type != JTokenType.Integer
                || versionObject["patch"]?.Type != JTokenType.Integer)
            {
                return false;
            }

            version = new ModuleVersion(
                versionObject["major"].Value<int>(),
                versionObject["minor"].Value<int>(),
                versionObject["patch"].Value<int>());
            return true;
        }

        /// <summary>
        /// Writes the version back in its manifest representation.
        /// </summary>
        /// <returns>An object with integer "major", "minor" and "patch".</returns>
        public JObject ToJson()
        {
            return new JObject
            {
                ["major"] = Major,
                ["minor"] = Minor,
                ["patch"] = Patch
            };
        }

        /// <inheritdoc/>
        public int CompareTo(ModuleVersion other)
        {
            if (Major != other.Major)
            {
                return Major.CompareTo(other.Major);
            }
            if (Minor != other.Minor)
            {
                return Minor.CompareTo(other.Minor);
            }
            return Patch.CompareTo(other.Patch);
        }

        public override string ToString() => $"{Major}.{Minor}.{Patch}";

        public override bool Equals(object obj) => obj is ModuleVersion other && CompareTo(other) == 0;

        public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);
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
        /// Reported in place of a version when a module entry predates versions being written to the manifest.
        /// </summary>
        private const string UnknownVersion = "Unknown";
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
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(group => group.Key, group => group.First().Value);
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
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(group => group.Key, group => group.First().Value);
        }

        /// <summary>
        /// Fetches all supported and imported languages for a given character and module type.
        /// </summary>
        /// <param name="characterName">The name of the character to fetch languages for.</param>
        /// <param name="type">The module type to fetch languages for.</param>
        /// <returns>A list of ModuleLanguage objects representing the supported languages.</returns>
        public List<ModuleLanguage> GetAllSupportedLanguages(string characterName, ModuleType type)
        {
            if (type == ModuleType.None)
            {
                return new();
            }
            string moduleTypeString = ModuleTypeToString[type];
            List<ModuleLanguage> languages = GetAllLanguagesForCharacterAndModuleType(characterName, type).Values
                .ToList();
            HashSet<string> supportedIso639_2 = manifestData["character_modules"].Children<JProperty>()
            .Where(p =>
            {
                var module = p.Value;
                var characters = module["characters"]?.Values<string>() ?? Enumerable.Empty<string>();
                var quality = module["quality"]?.ToString();

                return characters.Contains(characterName) && quality == moduleTypeString;
            })
            .SelectMany(p => p.Value["required_language_modules"].Children<JProperty>())
            .Where(p => HasLanguageModule(GetRequiredModuleID(p.Value))
                || !string.IsNullOrEmpty(FindLanguageModuleIDForISO(p.Name, GetRequiredModuleVersion(p.Value))))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(group => group.Key, group => group.First().Value);
        }

        /// <summary>
        /// Formats a manifest version object as "major.minor.patch".
        /// </summary>
        /// <param name="versionToken">The "version" token of a module entry.</param>
        /// <returns>The formatted version, or "Unknown" if the entry carries no version.</returns>
        private static string FormatVersion(JToken versionToken)
        {
            return ModuleVersion.TryParse(versionToken, out ModuleVersion version) ? version.ToString() : UnknownVersion;
        }

        /// <summary>
        /// Finds the manifest property of a specific character module.
        /// </summary>
        /// <param name="characterName">Target character name.</param>
        /// <param name="type">Target module type.</param>
        /// <returns>The matching manifest property, or null if none matches.</returns>
        private JProperty FindCharacterModuleProperty(string characterName, ModuleType type)
        {
            if (!ModuleTypeToString.TryGetValue(type, out string moduleTypeString)) return null;

            var matches = manifestData["character_modules"].Children<JProperty>()
                .Where(p =>
                {
                    var module = p.Value;
                    var characters = module["characters"]?.Values<string>() ?? Enumerable.Empty<string>();
                    var quality = module["quality"]?.ToString();

                    return characters.Contains(characterName) && quality == moduleTypeString;
                })
                .ToList();

            if (matches.Count > 1)
                LingotionLogger.Debug($"Found {matches.Count} modules for character {characterName} with module type {type}. Using {matches[0].Name}.");

            return matches.FirstOrDefault();
        }

        /// <summary>
        /// Finds the manifest property of a specific language module.
        /// </summary>
        /// <param name="moduleName">Target module name.</param>
        /// <returns>The matching manifest property, or null if none matches.</returns>
        private JProperty FindLanguageModuleProperty(string moduleName)
        {
            return manifestData["language_modules"].Children<JProperty>()
                .FirstOrDefault(p => p.Name == moduleName);
        }

        /// <summary>
        /// Finds a specific character module.
        /// </summary>
        /// <param name="characterName">Target character name.</param>
        /// <param name="type">Target module type.</param>
        /// <returns>A module entry of the corresponding character.</returns>
        public ModuleEntry GetCharacterModuleEntry(string characterName, ModuleType type)
        {
            JProperty moduleProperty = FindCharacterModuleProperty(characterName, type);
            if (moduleProperty == null)
            {
                return new ModuleEntry(null, null);
            }

            return new ModuleEntry(moduleProperty.Name, moduleProperty.Value["jsonpath"]?.ToString(), FormatVersion(moduleProperty.Value["version"]));
        }

        /// <summary>
        /// Finds a specific language module.
        /// </summary>
        /// <param name="moduleName">Target module name.</param>
        /// <returns>A module entry of the corresponding language.</returns>
        public ModuleEntry GetLanguageModuleEntry(string moduleName)
        {
            JProperty moduleProperty = FindLanguageModuleProperty(moduleName);
            if (moduleProperty == null)
            {
                LingotionLogger.Warning($"The language {moduleName} has not been imported and use of its language will not be possible. Check Thespeon Info window for more information.");
                return new(string.Empty, string.Empty);
            }

            return new ModuleEntry(moduleProperty.Name, moduleProperty.Value["jsonpath"]?.ToString(), FormatVersion(moduleProperty.Value["version"]));
        }

        /// <summary>
        /// Checks whether a language module with the exact given ID has been imported.
        /// </summary>
        /// <param name="moduleID">Target language module ID.</param>
        /// <returns>True if the module is present in the manifest.</returns>
        public bool HasLanguageModule(string moduleID)
        {
            return !string.IsNullOrEmpty(moduleID) && FindLanguageModuleProperty(moduleID) != null;
        }

        /// <summary>
        /// Finds the ID of an imported language module serving the given language at a compatible version. Used to
        /// substitute a language module a character pins but which is not imported, since a module ID also encodes
        /// the content it was built from and so differs between builds of the same language. Several versions of the
        /// same language may be imported side by side, so the requested version is taken where it is present and the
        /// highest minor.patch sharing its major otherwise. A differing major is never substituted, as it may carry an
        /// incompatible phonemizer.
        /// </summary>
        /// <param name="iso639_2">ISO 639-2 code of the language to find a module for.</param>
        /// <param name="requestedVersion">Version the character was built against, or null to accept the highest imported version of any major.</param>
        /// <returns>The ID of an imported module serving that language at a compatible version, or null if none does.</returns>
        public string FindLanguageModuleIDForISO(string iso639_2, ModuleVersion? requestedVersion = null)
        {
            if (string.IsNullOrEmpty(iso639_2))
            {
                return null;
            }

            List<(string Name, ModuleVersion? Version)> candidates = manifestData["language_modules"].Children<JProperty>()
                .Where(module => module.Value["languages"]
                    .SelectMany(langProp => langProp.Values())
                    .Any(langObj => string.Equals(langObj["iso639_2"]?.ToString(), iso639_2, StringComparison.OrdinalIgnoreCase)))
                .Select(module => (Name: module.Name, Version: ModuleVersion.TryParse(module.Value["version"], out ModuleVersion parsed) ? parsed : (ModuleVersion?)null))
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            if (requestedVersion == null)
            {
                return candidates.OrderByDescending(candidate => candidate.Version ?? default(ModuleVersion)).First().Name;
            }

            ModuleVersion requested = requestedVersion.Value;
            List<(string Name, ModuleVersion? Version)> compatible = candidates
                .Where(candidate => candidate.Version.HasValue && candidate.Version.Value.Major == requested.Major)
                .ToList();

            if (compatible.Count == 0)
            {
                return null;
            }

            (string Name, ModuleVersion? Version) exact = compatible.FirstOrDefault(candidate => candidate.Version.Value.CompareTo(requested) == 0);
            if (exact.Name != null)
            {
                return exact.Name;
            }

            return compatible.OrderByDescending(candidate => candidate.Version.Value).First().Name;
        }

        /// <summary>
        /// Fetches the version of a specific character module.
        /// </summary>
        /// <param name="characterName">Target character name.</param>
        /// <param name="type">Target module type.</param>
        /// <returns>The module version formatted as "major.minor.patch", or "Unknown" if it cannot be determined.</returns>
        public string GetCharacterModuleVersion(string characterName, ModuleType type)
        {
            JProperty moduleProperty = FindCharacterModuleProperty(characterName, type);
            return moduleProperty == null ? UnknownVersion : FormatVersion(moduleProperty.Value["version"]);
        }

        /// <summary>
        /// Fetches the version of a specific language module.
        /// </summary>
        /// <param name="moduleName">Target module name.</param>
        /// <returns>The module version formatted as "major.minor.patch", or "Unknown" if it cannot be determined.</returns>
        public string GetLanguageModuleVersion(string moduleName)
        {
            JProperty moduleProperty = FindLanguageModuleProperty(moduleName);
            return moduleProperty == null ? UnknownVersion : FormatVersion(moduleProperty.Value["version"]);
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
        /// <returns>A list of summary, version and formatted tag lines, one per module inside the character.</returns>
        public List<(string info, string version, List<string> tags)> GetAllModuleInfoInCharacter(string name)
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
                    JObject languages = (JObject)module.Value["languages"];
                    var languageNames = languages?.Properties().Select((lang) => lang.Name.ToString());
                    var tags = (module.Value["tags"] as JObject)?.Properties()
                        .Select(tag => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tag.Name.Replace('_', ' '))
                            + ": "
                            + (tag.Name == "module_type" && StringToModuleType.TryGetValue(tag.Value.ToString(), out ModuleType tagModuleType)
                                ? tagModuleType.ToString()
                                : FormatTagValue(tag.Value)))
                        .ToList() ?? new List<string>();
                    return (
                        info: "Character: " + charactername + ", Languages: " + string.Join(", ", languageNames),
                        version: FormatVersion(module.Value["version"]),
                        tags: tags,
                        quality: module.Value["quality"]?.ToString()
                    );
                })
                .GroupBy(entry => (entry.info, entry.quality, entry.version))
                .Select(group => (group.First().info, group.First().version, group.First().tags))
                .ToList();
        }

        /// <summary>
        /// Formats a tag value as readable text: arrays become comma separated lists, everything else its plain string.
        /// </summary>
        private static string FormatTagValue(JToken value)
        {
            return value is JArray array
                ? string.Join(", ", array.Select(item =>
                {
                    string text = FormatTagValue(item);
                    return text.Length > 0 ? char.ToUpper(text[0]) + text.Substring(1) : text;
                }))
                : value.ToString();
        }

        /// <summary>
        /// Summarizes all module info inside an language module.
        /// </summary>
        /// <param name="name">The specific language name to find.</param>
        /// <returns>A list of summary and version pairs, one per language module.</returns>
        public List<(string info, string version)> GetAllModuleInfoInLanguage(string name)
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
                    return (
                        "Languages: " + string.Join(", ", languages),
                        FormatVersion(module.Value["version"])
                    );
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
            var missingLanguageKeys = manifestData["character_modules"]
                .Children<JProperty>()
                .SelectMany(module =>
                {
                    if (module.Value["required_language_modules"] is not JObject requiredLangs)
                        return Enumerable.Empty<string>();

                    return requiredLangs.Properties()
                        .Where(p => !HasLanguageModule(GetRequiredModuleID(p.Value))
                            && string.IsNullOrEmpty(FindLanguageModuleIDForISO(p.Name, GetRequiredModuleVersion(p.Value))))
                        .Select(p => p.Name);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return missingLanguageKeys;
        }

        /// <summary>
        /// Reads the module ID out of a "required_language_modules" entry. Entries written before required
        /// versions were manifested are the bare ID string, and are still read here rather than discarded.
        /// </summary>
        /// <param name="entry">The manifest entry to read.</param>
        /// <returns>The required module ID, or null if the entry holds none.</returns>
        private static string GetRequiredModuleID(JToken entry)
        {
            return entry is JObject entryObject ? entryObject["id"]?.ToString() : entry?.ToString();
        }

        /// <summary>
        /// Reads the required module version out of a "required_language_modules" entry.
        /// </summary>
        /// <param name="entry">The manifest entry to read.</param>
        /// <returns>The required version, or null if the entry pins no version.</returns>
        private static ModuleVersion? GetRequiredModuleVersion(JToken entry)
        {
            if (entry is JObject entryObject && ModuleVersion.TryParse(entryObject["version"], out ModuleVersion version))
            {
                return version;
            }

            return null;
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
        /// Fetches the config filenames of every module that uses a file, including unsupported modules.
        /// </summary>
        /// <param name="md5">The MD5 of the file.</param>
        /// <returns>The config filenames using the file, or an empty list if the manifest does not track it.</returns>
        public IReadOnlyList<string> GetFileUsers(string md5)
        {
            if (manifestData?["file_usage"]?[md5] is JArray usageArray)
            {
                return usageArray.Values<string>().ToList();
            }
            return Array.Empty<string>();
        }

        /// <summary>
        /// Fetches the character modules on disk that this package cannot run, because they were built for another
        /// module major. They are left out of every other lookup and are only listed so they can be deleted.
        /// </summary>
        /// <returns>The display name, config filename and version of each unsupported module.</returns>
        public List<(string Name, string ConfigFilename, string Version)> GetUnsupportedModules()
        {
            if (manifestData?["unsupported_modules"] is not JObject unsupportedModules)
            {
                return new List<(string Name, string ConfigFilename, string Version)>();
            }

            return unsupportedModules.Properties()
                .Select(module => (
                    Name: module.Value["name"]?.ToString() ?? module.Name,
                    ConfigFilename: module.Name,
                    Version: FormatVersion(module.Value["version"])))
                .OrderBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
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