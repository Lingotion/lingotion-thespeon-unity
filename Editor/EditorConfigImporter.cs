// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lingotion.Thespeon.Core;
using Newtonsoft.Json.Linq;

namespace Lingotion.Thespeon.Editor
{
    /// <summary>
    /// Parses module config JSON files into manifest format.
    /// Decouples config-format knowledge from the file watcher.
    /// </summary>
    public static class EditorConfigImporter
    {
        /// <summary>
        /// Result of parsing a module config file into manifest format.
        /// </summary>
        public struct ParsedModuleConfig
        {
            /// <summary>The unique identifier of the parsed module.</summary>
            public string ModuleId;
            /// <summary>The user-facing display name for this module (e.g. "Elias-L").</summary>
            public string FrontFacingName;
            /// <summary>The config type string (e.g. "lara" or "phonemizer").</summary>
            public string ConfigType;
            /// <summary>The JSON object representing this module's entry in the manifest.</summary>
            public JObject ModuleMapping;
            /// <summary>MD5 hashes of all files referenced by this module config.</summary>
            public List<string> FileMd5s;
        }

        /// <summary>
        /// Parses a module config into manifest format.
        /// Returns null if the config is not a recognized module config.
        /// </summary>
        /// <param name="config">The parsed JSON config.</param>
        /// <param name="configFilename">The config's filename (used as reference in manifest).</param>
        /// <returns>A parsed module config, or null if not a valid module config.</returns>
        /// <exception cref="InvalidDataException">Thrown when a recognized module config carries no usable version.</exception>
        /// <exception cref="UnsupportedModuleVersionException">Thrown when a character module config is of a major this package cannot run.</exception>
        public static ParsedModuleConfig? TryParseConfigFile(JObject config, string configFilename)
        {
            string configType = config["type"]?.ToString();

            if (configType == ConfigFormatDetector.LARA_TYPE)
                return ParseCharacterConfig(config, configFilename);

            if (configType == ConfigFormatDetector.PHONEMIZER_TYPE)
                return ParseLanguageConfig(config, configFilename);

            return null;
        }

        /// <summary>
        /// Extracts all file MD5 hashes referenced by a config's "files" array.
        /// </summary>
        /// <param name="config">The parsed JSON config.</param>
        /// <returns>A list of MD5 strings for file usage tracking.</returns>
        public static List<string> ExtractFileMd5s(JObject config)
        {
            List<string> md5s = new();
            JArray files = (JArray)config["files"];
            if (files == null) return md5s;

            foreach (JObject fileEntry in files)
            {
                string md5 = fileEntry["md5"]?.ToString();
                if (!string.IsNullOrEmpty(md5))
                    md5s.Add(md5);
            }
            return md5s;
        }

        /// <summary>
        /// Parses a module config's version. A module without a valid version is rejected,
        /// so an unversioned module can never enter the manifest.
        /// </summary>
        /// <param name="versionToken">The config's "version" token.</param>
        /// <param name="configFilename">The config's filename, used in the error message.</param>
        /// <returns>The parsed version.</returns>
        /// <exception cref="InvalidDataException">Thrown when the config carries no usable version.</exception>
        private static ModuleVersion ParseVersion(JToken versionToken, string configFilename)
        {
            if (!ModuleVersion.TryParse(versionToken, out ModuleVersion version))
            {
                throw new InvalidDataException(
                    $"Module config \"{configFilename}\" has no valid \"version\" field. Expected an object with integer " +
                    $"\"major\", \"minor\" and \"patch\". Re-download the module from the Lingotion portal.");
            }

            return version;
        }

        /// <summary>
        /// Checks that a character module config was built for the module major this package runs.
        /// </summary>
        /// <param name="config">The parsed character module config.</param>
        /// <param name="configFilename">The config's filename, used in the error message.</param>
        /// <returns>The config's version.</returns>
        /// <exception cref="InvalidDataException">Thrown when the config carries no usable version.</exception>
        /// <exception cref="UnsupportedModuleVersionException">Thrown when the config is of a major this package cannot run.</exception>
        public static ModuleVersion EnsureSupportedCharacterVersion(JObject config, string configFilename)
        {
            ModuleVersion version = ParseVersion(config["version"], configFilename);
            if (version.Major != ModuleVersion.SupportedCharacterModuleMajor)
            {
                string frontFacingName = GetCharacterFrontFacingName(config);
                throw new UnsupportedModuleVersionException(
                    $"Character module \"{frontFacingName}\" ({configFilename}) is version {version}, but this version of " +
                    $"Thespeon only runs {ModuleVersion.SupportedCharacterModuleMajor}.x character modules. Download it " +
                    $"again from the Lingotion portal.",
                    frontFacingName,
                    version);
            }

            return version;
        }

        /// <summary>
        /// Builds the user-facing display name of a character module, e.g. "Elias-M".
        /// </summary>
        /// <param name="config">The parsed character module config.</param>
        /// <returns>The character name followed by the module quality.</returns>
        public static string GetCharacterFrontFacingName(JObject config)
        {
            string characterName = config["character"]?["charactername"]?.ToString() ?? "Unknown";
            string qualityTag = config["tags"]?["module_type"]?.ToString() ?? "mid";
            ModuleType quality = ManifestHandler.StringToModuleType.TryGetValue(qualityTag, out ModuleType qualityType) ? qualityType : ModuleType.M;
            return $"{characterName}-{quality}";
        }

        private static ParsedModuleConfig ParseCharacterConfig(JObject config, string configFilename)
        {
            string id = config["source_id"]?.ToString() ?? "unknown";

            // Extract character info
            JObject character = (JObject)config["character"];
            string characterName = character?["charactername"]?.ToString() ?? "Unknown";

            string qualityTag = config["tags"]?["module_type"]?.ToString() ?? "mid";
            string frontFacingName = GetCharacterFrontFacingName(config);

            // Extract required language modules from phonemizer_setup
            JObject requiredLanguageModules = new();
            JArray phonemizerModules = (JArray)config["phonemizer_setup"]?["modules"];
            if (phonemizerModules != null)
            {
                foreach (JObject pm in phonemizerModules)
                {
                    string iso639_2 = pm["iso639_2"]?.ToString();
                    string baseModuleId = pm["base_module_id"]?.ToString();
                    if (string.IsNullOrEmpty(iso639_2) || string.IsNullOrEmpty(baseModuleId))
                    {
                        continue;
                    }
                    JObject requirement = new JObject { ["id"] = baseModuleId };

                    if (ModuleVersion.TryParse(pm["version"], out ModuleVersion requiredVersion))
                    {
                        requirement["version"] = requiredVersion.ToJson();
                    }
                    requiredLanguageModules[iso639_2] = requirement;
                }
            }

            // Extract languages
            JObject languages = new();
            JArray languagesArray = (JArray)config["languages"];
            if (languagesArray != null)
            {
                foreach (JObject languageInfo in languagesArray)
                {
                    string languageName = languageInfo["nameinenglish"]?.ToString() ?? languageInfo["autonym"]?.ToString() ?? "Unknown";

                    JObject currentLanguage = (JObject)languages[languageName];
                    if (currentLanguage == null)
                    {
                        currentLanguage = new JObject
                        {
                            ["languagecode"] = languageInfo["iso639_2"]?.ToString(),
                            ["dialects"] = new JObject()
                        };
                    }

                    JObject subLanguage = new JObject
                    {
                        ["iso639_2"] = languageInfo["iso639_2"]?.ToString(),
                        ["iso639_3"] = languageInfo["iso639_3"]?.ToString(),
                        ["glottocode"] = languageInfo["glottocode"]?.ToString(),
                        ["customdialect"] = languageInfo["customdialect"]?.ToString(),
                        ["iso3166_1"] = languageInfo["iso3166_1"]?.ToString(),
                        ["iso3166_2"] = languageInfo["iso3166_2"]?.ToString(),
                    };

                    string dialectCode = subLanguage["iso639_2"]?.ToString() ?? "";
                    string subLanguageName = languageName;
                    string iso3166_1 = subLanguage["iso3166_1"]?.ToString();
                    string iso3166_2 = subLanguage["iso3166_2"]?.ToString();

                    if (!string.IsNullOrEmpty(iso3166_1))
                    {
                        dialectCode += $"_{iso3166_1}";
                        subLanguageName += $" {iso3166_1}";
                    }
                    else if (!string.IsNullOrEmpty(iso3166_2))
                    {
                        dialectCode += $"_{iso3166_2}";
                        subLanguageName += $" {iso3166_2}";
                    }
                    else
                    {
                        dialectCode += "_unspecified";
                        subLanguageName += " unspecified";
                    }

                    subLanguage["languagecode"] = dialectCode;
                    currentLanguage["dialects"][subLanguageName] = subLanguage;
                    languages[languageName] = currentLanguage;
                }
            }

            JObject moduleMapping = new JObject
            {
                ["name"] = frontFacingName,
                ["jsonpath"] = configFilename,
                ["required_language_modules"] = requiredLanguageModules,
                ["characters"] = new JArray(characterName),
                ["quality"] = qualityTag,
                ["languages"] = languages,
                ["version"] = EnsureSupportedCharacterVersion(config, configFilename).ToJson(),
                ["tags"] = config["tags"]?.DeepClone()
            };

            return new ParsedModuleConfig
            {
                ModuleId = id,
                FrontFacingName = frontFacingName,
                ConfigType = ConfigFormatDetector.LARA_TYPE,
                ModuleMapping = moduleMapping,
                FileMd5s = ExtractFileMd5s(config)
            };
        }

        private static ParsedModuleConfig ParseLanguageConfig(JObject config, string configFilename)
        {
            string id = config["base_module_id"]?.ToString() ?? "unknown";

            StringBuilder frontFacingName = new();
            JObject languages = new();

            JArray languagesArray = (JArray)config["languages"];
            if (languagesArray != null)
            {
                foreach (JObject languageInfo in languagesArray)
                {
                    string nameInEnglish = languageInfo["nameinenglish"]?.ToString() ?? languageInfo["autonym"]?.ToString() ?? "Unknown";
                    frontFacingName.Append(nameInEnglish + " - ");

                    if (languages[nameInEnglish] == null)
                    {
                        languages[nameInEnglish] = new JArray();
                    }

                    JObject language = new JObject
                    {
                        ["iso639_2"] = languageInfo["iso639_2"]?.ToString(),
                        ["iso639_3"] = languageInfo["iso639_3"]?.ToString(),
                        ["glottocode"] = languageInfo["glottocode"]?.ToString(),
                        ["customdialect"] = languageInfo["customdialect"]?.ToString(),
                        ["iso3166_1"] = languageInfo["iso3166_1"]?.ToString(),
                        ["iso3166_2"] = languageInfo["iso3166_2"]?.ToString(),
                    };
                    ((JArray)languages[nameInEnglish]).Add(language);
                }
            }

            // Remove trailing " - "
            if (frontFacingName.Length >= 3)
                frontFacingName.Remove(frontFacingName.Length - 3, 3);

            string name = frontFacingName.ToString();
            if (string.IsNullOrEmpty(name))
                name = config["name"]?.ToString() ?? "Unknown Language";

            JObject moduleMapping = new JObject
            {
                ["name"] = name,
                ["languages"] = languages,
                ["jsonpath"] = configFilename,
                ["version"] = ParseVersion(config["version"], configFilename).ToJson()
            };

            return new ParsedModuleConfig
            {
                ModuleId = id,
                FrontFacingName = name,
                ConfigType = ConfigFormatDetector.PHONEMIZER_TYPE,
                ModuleMapping = moduleMapping,
                FileMd5s = ExtractFileMd5s(config)
            };
        }
    }

    /// <summary>
    /// Thrown when a character module was built for a module major this package cannot run.
    /// </summary>
    public class UnsupportedModuleVersionException : Exception
    {
        /// <summary>The user-facing display name of the module, e.g. "Elias-M".</summary>
        public string FrontFacingName { get; }

        /// <summary>The version the module was built as.</summary>
        public ModuleVersion Version { get; }

        /// <summary>
        /// Initializes a new instance of the UnsupportedModuleVersionException class.
        /// </summary>
        /// <param name="message">The message describing the rejection.</param>
        /// <param name="frontFacingName">The user-facing display name of the module.</param>
        /// <param name="version">The version the module was built as.</param>
        public UnsupportedModuleVersionException(string message, string frontFacingName, ModuleVersion version)
            : base(message)
        {
            FrontFacingName = frontFacingName;
            Version = version;
        }
    }
}
