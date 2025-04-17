// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

// SharedModels/Language.cs
using Newtonsoft.Json;
using System.Collections.Generic;
using System;

namespace Lingotion.Thespeon.Utils
{
    public class Language
    {

        [JsonProperty("iso639_2", Required = Required.Always)]
        public string iso639_2 { get; set; } = "";
        #nullable enable

        [JsonProperty("iso639_3", NullValueHandling = NullValueHandling.Ignore)]
        public string? iso639_3 { get; set; }

        [JsonProperty("glottocode", NullValueHandling = NullValueHandling.Ignore)]
        public string? glottoCode { get; set; }

        [JsonProperty("iso3166_1", NullValueHandling = NullValueHandling.Ignore)]
        public string? iso3166_1 { get; set; }

        [JsonProperty("iso3166_2", NullValueHandling = NullValueHandling.Ignore)]
        public string? iso3166_2 { get; set; }

        [JsonProperty("customdialect", NullValueHandling = NullValueHandling.Ignore)]
        public string? customDialect { get; set; }

        [JsonProperty("languagekey", NullValueHandling = NullValueHandling.Ignore)]
        public int? languageKey { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the Language class.
        /// </summary>
        public Language() { }
        /// <summary>
        /// Initializes a new instance of the Language class by copying properties from another instance.
        /// </summary>
        /// /// <param name="other">The Language instance to copy from.</param>

        public Language(Language lang)
        {
            iso639_2 = lang.iso639_2;
            iso639_3 = lang.iso639_3;
            glottoCode = lang.glottoCode;
            iso3166_1 = lang.iso3166_1;
            iso3166_2 = lang.iso3166_2;
            customDialect = lang.customDialect;
            languageKey = lang.languageKey;
        }
        /// <summary>
        /// Retrieves a dictionary representation of the language properties.
        /// </summary>
        /// <returns>A dictionary where the keys are property names and values are their corresponding values.</returns>
        public Dictionary<string,string?> GetItems()
        {
            return new Dictionary<string, string?>()
            {
                { "iso639_2", iso639_2 },
                { "iso639_3", iso639_3 },
                { "glottocode", glottoCode },
                { "iso3166_1", iso3166_1 },
                { "iso3166_2", iso3166_2 },
                { "customdialect", customDialect },
                { "languagekey", languageKey?.ToString() }
            };
        }
        #nullable disable
        /// <summary>
        /// Returns a string representation of the Language object.
        /// </summary>
        /// <returns>A formatted string containing all language properties.</returns>
        public override string ToString()
        {
            return $"iso639_2: {iso639_2}, iso639_3: {iso639_3}, " +
                    $"glottocode: {glottoCode}, iso3166_1: {iso3166_1}, " +
                    $"iso3166_2: {iso3166_2}, customdialect: {customDialect}, " +
                    $"languagekey: {languageKey}";
        }

        /// <summary>
        /// Returns a display-friendly string representation of the language.
        /// </summary>
        /// <returns>A string summarizing the language and optional dialect.</returns>
        public string ToDisplay()
        {
            string res = $"{iso639_2}";
            if(iso3166_1 != null && customDialect!=null)
            {
                res += $" ({iso3166_1} - {customDialect})";
            }
            else if(iso3166_1!=null)
            {
                res += $" ({iso3166_1})";
            }
            else if(customDialect!=null)
            {
                res += $" ({customDialect})";
            }
            return res;
        }
        /// <summary>
        /// Determines whether the specified object is equal to the current Language instance.
        /// Ignores 'languageKey' property in comparisons.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns>True if the specified object properties are equal to this instance's; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is not Language other)
                return false;

            return iso639_2 == other.iso639_2 &&
                iso639_3 == other.iso639_3 &&
                glottoCode == other.glottoCode &&
                iso3166_1 == other.iso3166_1 &&
                iso3166_2 == other.iso3166_2 &&
                customDialect == other.customDialect;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// Ignores 'languageKey' property in hash generation.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(iso639_2, iso639_3, glottoCode, iso3166_1, iso3166_2, customDialect);
        }
        #nullable enable

        /// <summary>
        /// Determines whether every non-empty property in the inputLanguage matches the corresponding property in candidateLanguage.
        /// </summary>
        /// <param name="inputLanguage">The reference language to match against.</param>
        /// <param name="candidateLanguage">The candidate language to check.</param>
        /// <returns>True if all non-null properties in inputLanguage match those in candidateLanguage.</returns>
        public bool MatchLanguage(Language inputLanguage, Language candidateLanguage)
        {
            var inputItems = inputLanguage.GetItems();
            var candidateItems = candidateLanguage.GetItems();

            foreach (var kvp in inputItems)
            {
                string key = kvp.Key;
                string? inputValue = kvp.Value;

                // Skip if null or empty => we don't enforce it
                if (string.IsNullOrWhiteSpace(inputValue))
                    continue;

                if (!candidateItems.TryGetValue(key, out string? candidateValue))
                    return false;

                // Case-insensitive compare
                if (!string.Equals(inputValue, candidateValue, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
        #nullable disable
    }

    public static class LanguageExtensions
    {
        /// <summary>
        /// Compares two Language objects in a "hierarchical" manner.
        /// Certain properties are considered higher-level. If they mismatch, we may add a large penalty or skip the rest.
        /// Returns the total "distance" plus a list of differences that explain how we arrived there.
        /// 
        /// Example logic:
        /// 1) iso639_2 mismatch => big penalty (50) + short-circuit (stop comparing).
        /// 2) iso639_3 mismatch => smaller penalty (5), continue.
        /// 3) glottoCode mismatch => big penalty (15), continue.
        /// 4) iso3166_1 mismatch => penalty (5), skip iso3166_2.
        /// 5) iso3166_2 mismatch => penalty (2), if iso3166_1 matched.
        /// 6) customDialect mismatch => penalty (2).
        /// 7) languageKey mismatch => penalty (1).
        /// 
        /// Customize as you wish.
        /// </summary>
        public static (int Distance, List<string> Differences) 
            HierarchicalCompareAndComputeDistance(Language lang1, Language lang2)
        {
            int distance = 0;
            var differences = new List<string>();

            // Compare iso639_2 (top-level)
            if (!string.Equals(lang1.iso639_2, lang2.iso639_2, StringComparison.OrdinalIgnoreCase))
            {
                distance += 50;
                differences.Add($"iso639_2 mismatch => +50 penalty. '{lang1.iso639_2}' vs. '{lang2.iso639_2}'");
                // Short-circuit: we consider them too different if iso639_2 doesn't match
                return (distance, differences);
            }
            else
            {
                differences.Add($"iso639_2 match: '{lang1.iso639_2}'");
            }

            // iso639_2 matched => check iso639_3
            if (!string.Equals(lang1.iso639_3, lang2.iso639_3, StringComparison.OrdinalIgnoreCase))
            {
                distance += 5;
                differences.Add($"iso639_3 mismatch => +10 penalty. {lang1.iso639_3} vs. {lang2.iso639_3}");
            }
            else
            {
                differences.Add($"iso639_3 match: '{lang1.iso639_3}'");
            }

            // Check glottoCode
            if (!string.Equals(lang1.glottoCode, lang2.glottoCode, StringComparison.OrdinalIgnoreCase))
            {
                distance += 15;
                differences.Add($"glottoCode mismatch => +5 penalty. '{lang1.glottoCode}' vs. '{lang2.glottoCode}'");
            }
            else
            {
                differences.Add($"glottoCode match: '{lang1.glottoCode}'");
            }

            // Check iso3166_1
            bool iso3166_1_match = string.Equals(lang1.iso3166_1, lang2.iso3166_1, StringComparison.OrdinalIgnoreCase);
            if (!iso3166_1_match)
            {
                distance += 5;
                differences.Add($"iso3166_1 mismatch => +5 penalty. '{lang1.iso3166_1}' vs. '{lang2.iso3166_1}'");
                // We skip iso3166_2 if top-level country doesn't match
                differences.Add("Skipping iso3166_2 comparison because iso3166_1 differs.");
            }
            else
            {
                differences.Add($"iso3166_1 match: '{lang1.iso3166_1}'");

                // Only compare iso3166_2 if iso3166_1 matched
                if (!string.Equals(lang1.iso3166_2, lang2.iso3166_2, StringComparison.OrdinalIgnoreCase))
                {
                    distance += 2;
                    differences.Add($"iso3166_2 mismatch => +2 penalty. '{lang1.iso3166_2}' vs. '{lang2.iso3166_2}'");
                }
                else
                {
                    differences.Add($"iso3166_2 match: '{lang1.iso3166_2}'");
                }
            }

            // Check customDialect
            if (!string.Equals(lang1.customDialect, lang2.customDialect, StringComparison.OrdinalIgnoreCase))
            {
                distance += 2;
                differences.Add($"customDialect mismatch => +2 penalty. '{lang1.customDialect}' vs. '{lang2.customDialect}'");
            }
            else
            {
                differences.Add($"customDialect match: '{lang1.customDialect}'");
            }


            return (distance, differences);
        }

        /// <summary>
        /// Iterates over the candidate Languages, uses HierarchicalCompareAndComputeDistance,
        /// and returns the single closest match plus a user-friendly feedback string.
        /// </summary>
        public static (Language Closest, int Distance, string Feedback) FindClosestLanguage(Language input, IEnumerable<Language> candidates)
        {
            Language bestMatch = null;
            int bestDistance = int.MaxValue;
            List<string> bestDifferences = null;

            foreach (var candidate in candidates)
            {
                var (dist, diffs) = HierarchicalCompareAndComputeDistance(input, candidate);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestMatch = candidate;
                    bestDifferences = diffs;
                }
            }

            if (bestMatch == null)
            {
                return (null, int.MaxValue, "No candidates were provided.");
            }

            // Build feedback describing the best match
            var feedback = 
                $"Closest match: {bestMatch}\n" +
                $"Distance: {bestDistance}\n" +
                "Differences:\n" +
                string.Join("\n", bestDifferences);

            return (bestMatch, bestDistance, feedback);
        }
    }
}
