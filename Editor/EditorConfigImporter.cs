// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System.Collections.Generic;
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

        private static ParsedModuleConfig ParseCharacterConfig(JObject config, string configFilename)
        {
            string id = config["source_id"]?.ToString() ?? "unknown";

            // Extract character info
            JObject character = (JObject)config["character"];
            string characterName = character?["charactername"]?.ToString() ?? "Unknown";

            // Extract quality tag
            string qualityTag = config["tags"]?["module_type"]?.ToString() ?? "mid";
            ModuleType quality = ManifestHandler.StringToModuleType.TryGetValue(qualityTag, out ModuleType qualityType) ? qualityType : ModuleType.M;

            // Build front-facing name
            string frontFacingName = $"{characterName}-{quality}";

            // Extract required language modules from phonemizer_setup
            Dictionary<string, string> requiredLanguageModules = new();
            JArray phonemizerModules = (JArray)config["phonemizer_setup"]?["modules"];
            if (phonemizerModules != null)
            {
                foreach (JObject pm in phonemizerModules)
                {
                    string iso639_2 = pm["iso639_2"]?.ToString();
                    string baseModuleId = pm["base_module_id"]?.ToString();
                    if (!string.IsNullOrEmpty(iso639_2) && !string.IsNullOrEmpty(baseModuleId))
                    {
                        requiredLanguageModules[iso639_2] = baseModuleId;
                    }
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

            // Extract version (handle NOTFOUND placeholder)
            JObject version = new JObject { ["major"] = 1, ["minor"] = 0, ["patch"] = 0 };
            JToken versionToken = config["version"];
            if (versionToken != null && versionToken.Type == JTokenType.Object)
            {
                version["major"] = versionToken["major"]?.Value<int>() ?? 1;
                version["minor"] = versionToken["minor"]?.Value<int>() ?? 0;
                version["patch"] = versionToken["patch"]?.Value<int>() ?? 0;
            }

            JObject moduleMapping = new JObject
            {
                ["name"] = frontFacingName,
                ["jsonpath"] = configFilename,
                ["required_language_modules"] = JObject.FromObject(requiredLanguageModules),
                ["characters"] = new JArray(characterName),
                ["quality"] = qualityTag,
                ["languages"] = languages,
                ["version"] = version
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
                ["jsonpath"] = configFilename
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
}
