// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Class representing the language selection for a module.
    /// </summary>
    public class ModuleLanguage
    {

        // [DevComment] In actuality this should probably be alternatively required with glottocode or customdialect. i.e. At least 1 of them is required and the others not. 
        [JsonProperty("iso639_2", Required = Required.Always)]
        public readonly string Iso639_2;
#nullable enable
        [JsonProperty("iso639_3", NullValueHandling = NullValueHandling.Ignore)]
        public readonly string? Iso639_3;
        [JsonProperty("glottocode", NullValueHandling = NullValueHandling.Ignore)]
        public readonly string? Glottocode;
        [JsonProperty("iso3166_1", NullValueHandling = NullValueHandling.Ignore)]
        public readonly string? Iso3166_1;
        [JsonProperty("iso3166_2", NullValueHandling = NullValueHandling.Ignore)]
        public readonly string? Iso3166_2;
        [JsonProperty("customdialect", NullValueHandling = NullValueHandling.Ignore)]
        public readonly string? CustomDialect;
#nullable disable
        // [DevComment] for JSON deserialization
        public ModuleLanguage() { }
        public ModuleLanguage(string iso639_2, string iso639_3 = null, string glottocode = null, string customDialect = null, string iso3166_1 = null, string iso3166_2 = null)
        {
            Iso639_2 = iso639_2;
            Iso639_3 = iso639_3;
            Glottocode = glottocode;
            CustomDialect = customDialect;
            Iso3166_1 = iso3166_1;
            Iso3166_2 = iso3166_2;
        }
        public static ModuleLanguage CopyOrNull(ModuleLanguage other)
        {
            if (other == null)
                return null;
            return new ModuleLanguage(
                other.Iso639_2,
                other.Iso639_3,
                other.Glottocode,
                other.CustomDialect,
                other.Iso3166_1,
                other.Iso3166_2
            );
        }

        public static ModuleLanguage BestMatch(List<ModuleLanguage> candidateLanguages, string language, string dialect = null)
        {
            if(string.IsNullOrEmpty(language))
            {
                return null;
            }
            if (candidateLanguages == null || candidateLanguages.Count == 0)
            {
                throw new ArgumentException("No languages provided for matching.");
            }
            int bestScore = -1;
            List<ModuleLanguage> bestLangs = new();

            foreach (ModuleLanguage lang in candidateLanguages)
            {
                int langScore = 0;
                if (string.Equals(lang.CustomDialect, language, StringComparison.OrdinalIgnoreCase))
                {
                    langScore = 10;
                }
                else if (string.Equals(lang.Glottocode, language, StringComparison.OrdinalIgnoreCase))
                {
                    langScore = 3;
                }
                else if (string.Equals(lang.Iso639_3, language, StringComparison.OrdinalIgnoreCase))
                {
                    langScore = 2;
                }
                else if (string.Equals(lang.Iso639_2, language, StringComparison.OrdinalIgnoreCase))
                {
                    langScore = 1;
                }

                if (langScore > bestScore)
                {
                    bestScore = langScore;
                    bestLangs.Clear();
                    bestLangs.Add(lang);
                }
                else if (langScore == bestScore)
                {
                    bestLangs.Add(lang);
                }
            }

            if (bestLangs.Count == 0)
            {
                return candidateLanguages[0];
            }
            if (string.IsNullOrEmpty(dialect))
            {
                return bestLangs[0];
            }

            ModuleLanguage bestLang = null;
            bestScore = -1;
            foreach (ModuleLanguage lang in bestLangs)
            {
                int regionScore = 0;
                if (string.Equals(lang.Iso3166_2, dialect, StringComparison.OrdinalIgnoreCase))
                {
                    regionScore = 2;
                }
                else if (string.Equals(lang.Iso3166_1, dialect, StringComparison.OrdinalIgnoreCase))
                {
                    regionScore = 1;
                }

                if (regionScore > bestScore)
                {
                    bestScore = regionScore;
                    bestLang = lang;
                }
            }

            if (bestLang == null)
            {
                throw new ArgumentException($"No matching language found for '{language}' with dialect '{dialect}'. Should never happen as the first entry will be default match.");
            }

            LingotionLogger.Debug($"Best match for language '{language}' and dialect '{dialect}' found: {bestLang}. Returning best language match.");


            return bestLang;
        }
        public override string ToString()
        {
            if (Iso3166_1 != null)
                return $"{Iso639_2} ({Iso3166_1})";

            return Iso639_2;
        }

        public override bool Equals(object obj)
        {
            if (obj is ModuleLanguage other)
            {
                return Iso639_2 == other.Iso639_2 &&
                       Iso639_3 == other.Iso639_3 &&
                       Glottocode == other.Glottocode &&
                       CustomDialect == other.CustomDialect &&
                       Iso3166_1 == other.Iso3166_1 &&
                       Iso3166_2 == other.Iso3166_2;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Iso639_2, Iso639_3, Glottocode, CustomDialect, Iso3166_1, Iso3166_2);
        }

        public string ToJson()
        {
            JObject jsonObj = new()
            {
                ["iso639_2"] = Iso639_2,
                ["iso639_3"] = Iso639_3,
                ["glottocode"] = Glottocode,
                ["customdialect"] = CustomDialect,
                ["iso3166_1"] = Iso3166_1,
                ["iso3166_2"] = Iso3166_2
            };

            List<string> keysToRemove = new();
            foreach (var property in jsonObj.Properties())
            {
                if (property.Value.Type == JTokenType.Null ||
                    (property.Value.Type == JTokenType.String && string.IsNullOrEmpty((string)property.Value)))
                {
                    keysToRemove.Add(property.Name);
                }
            }

            foreach (string key in keysToRemove)
            {
                jsonObj.Remove(key);
            }

            return jsonObj.ToString(Formatting.None);
        }

    }

}
