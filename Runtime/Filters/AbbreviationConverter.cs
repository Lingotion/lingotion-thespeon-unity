// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Lingotion.Thespeon.Filters
{
    public class AbbreviationConverter : IAbbrevationToWordsConverter 
    {
        // 1) Abbreviations that can appear at the start (no "St." here, we handle it separately)
        private static readonly Dictionary<string, string> abbreviationsAllowedToStart = new()
        {
            { "Mr.",  "Mister " },
            { "Mr ",  "Mister " },
            { "Mrs.", "Missis " },
            { "Mrs ", "Missis " },
            { "Ms.",  "Miss " },
            { "Ms ",  "Msss " },
            { "Dr.",  "Doctor " },
            { "Dr ",  "Doctor " },
            { "Capt.", "Captain " },
            { "Col.",  "Colonel " },
            { "Gen.",  "General " },
            { "Lt.",   "Lieutenant " },
            { "Prof.", "Professor " },
            { "Sgt.",  "Sergeant " },
            { "e.g.",  "For Example " },
            { "i.e.",  "For Example " },
            { "no.",   "number " },
            { "nbr ",  "number " },
            { "&",     " and " },
            { "U.S.",  "US " },
            { "U.S.A.", "USA " }
        };

        // 2) Abbreviations that cannot appear at the very start (again, no "St." here)
        private static readonly Dictionary<string, string> abbreviationsNotAllowedToStart = new()
        {
            { "etc.", "et cetera " },
            { "etc",  "et cetera " },
            { "Ltd.", "limited " },
            { "Inc.", "incorporated " },
            { "Sr.",  "senior " },
            { "Jr.",  "junior " },
            { "Rd.",  "road " },
            { "Gal.", "gallon " },
            { "Lb.",  "pounds " },
            { "Pt.",  "pints " },
            { "Qt.",  "quarts " },
            { "G",    "grams " },
            { "Kg",   "kilograms " },
            { "Cm",   "centimeter " },
            { "M",    "meter " },
            { "ft.",  "foot " },
            { "in.",  "inch " },
            { "mi.",  "mile " },
            { "mph.", "miles per hour " },
            { "mg.",  "milligram " },
            { "mm",   "millimeter " },
            { "oz",   "ounce " },
            { "sq",   "square " },
            { "ft2",  "square feet " },
            { "sqft", "square feet " },
            { "m2",   "square meter " },
            { "km",   "kilometer " },
            { "h",    "hour " },
            { "vs.",  "versus " },
            { "%",    " percent" }
        };

        // Combine everything (except St.) into a single, sorted list
        private class AbbrevEntry
        {
            public string Abbrev { get; }
            public string Replacement { get; }
            public bool AllowStart { get; }

            public AbbrevEntry(string abbrev, string replacement, bool allowStart)
            {
                Abbrev = abbrev;
                Replacement = replacement;
                AllowStart = allowStart;
            }
        }

        private static readonly List<AbbrevEntry> _abbrevList;

        /* -------------------------------------------------------
         * 2) The specialized data sets for "St." logic
         * ------------------------------------------------------ */
        // Known saint names (not strictly needed for this demonstration)
        private static readonly HashSet<string> SaintNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Peter", "Paul", "John", "Mary", "Joseph", "Michael",
            "Francis", "Patrick", "Louis", "Clare", "Teresa", "Anthony",
            "George", "James", "Anne", "Nicholas"
        };

        // Known street indicators
        private static readonly HashSet<string> StreetIndicators = new(StringComparer.OrdinalIgnoreCase)
        {
            "street", "st", "road", "rd", "ave", "avenue", "blvd", "boulevard",
            "lane", "drive", "dr", "court", "ct", "way", "road.", "rd.",
            "ave.", "avenue.", "blvd.", "lane.", "drive.", "dr.", "court.", "ct.", "way."
        };

        /// <summary>
        /// Static constructor merges the dictionaries and sorts them by descending length
        /// so e.g. "U.S.A." is matched before "U.S.".
        /// </summary>
        static AbbreviationConverter()
        {
            var tempList = new List<AbbrevEntry>();

            // 1) From abbreviationsAllowedToStart
            foreach (var kvp in abbreviationsAllowedToStart)
            {
                tempList.Add(new AbbrevEntry(kvp.Key, kvp.Value, true));
            }

            // 2) From abbreviationsNotAllowedToStart
            foreach (var kvp in abbreviationsNotAllowedToStart)
            {
                tempList.Add(new AbbrevEntry(kvp.Key, kvp.Value, false));
            }

            // Sort by descending abbreviation length
            _abbrevList = tempList
                .OrderByDescending(x => x.Abbrev.Length)
                .ToList();
        }

        /// <summary>
        /// Original 1-parameter version: just returns the converted text, no log.
        /// </summary>
        public string ConvertAbbreviationsOriginal(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = text.ToLower();

            // Pass 1: Replace dictionary-based abbreviations
            text = ReplaceGenericAbbreviations(text);

            // Pass 2: Handle "St." => "Saint" or "Street"
            text = HandleStAbbreviation(text);

            return text;
        }

        /// <summary>
        /// NEW method that returns a tuple: (convertedText, changesLog).
        /// Instead of counting how many times each abbreviation was replaced,
        /// we log the original abbreviation and its final expansion.
        /// </summary>
        public (string , string ) ConvertAbbreviations(string text)
        {
            if (string.IsNullOrEmpty(text))
                return (text, "No text provided.");

            // We'll store each abbreviation => final expansion used
            var changesMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1) Replace dictionary-based abbreviations with logging
            string processed = ReplaceGenericAbbreviationsWithLog(text.ToLower(), changesMap);

            // 2) Handle "St." => "Street" or "Saint" with logging
            processed = HandleStAbbreviationWithLog(processed, changesMap);

            // 3) Build a log that shows "'Mr.' -> 'Mister '", etc.
            string log = BuildChangesLog(changesMap);

            return (processed, log);
        }

        /* -------------------------------------------------------
         * Pass 1) Generic dictionary-based abbreviation replacement
         * (Original version)
         * ------------------------------------------------------ */
        private static string ReplaceGenericAbbreviations(string input)
        {
            foreach (var entry in _abbrevList)
            {
                Regex rx = BuildRegex(entry.Abbrev, entry.AllowStart);

                input = rx.Replace(input, m =>
                {
                    string leading = m.Groups[1].Value;
                    return leading + entry.Replacement;
                });
            }
            return input;
        }

        /* 
         * Pass 1) Generic dictionary-based abbreviation replacement 
         * WITH logging (stores "Abbrev -> Replacement") in changesMap.
         */
        private static string ReplaceGenericAbbreviationsWithLog(string input, Dictionary<string,string> changesMap)
        {
            // We'll do the same as above, but each time we see a match, 
            // we record that abbreviation in changesMap, if not already present.
            foreach (var entry in _abbrevList)
            {
                Regex rx = BuildRegex(entry.Abbrev, entry.AllowStart);

                input = rx.Replace(input, m =>
                {
                    // Group(1): leading boundary
                    string leading = m.Groups[1].Value;

                    // Record the fact that we replaced "entry.Abbrev" -> "entry.Replacement"
                    // If "entry.Abbrev" has already been seen, .TryAdd does nothing.
                    changesMap?.TryAdd(entry.Abbrev, entry.Replacement);

                    return leading + entry.Replacement;
                });
            }
            return input;
        }

        /// <summary>
        /// Builds a regex for the abbreviation that enforces:
        ///   - A leading boundary (start of text or non-alphanumeric)
        ///   - A trailing boundary (end of text or non-alphanumeric)
        /// So abbreviations won't match partial strings inside words.
        /// </summary>
        private static Regex BuildRegex(string abbreviation, bool allowStart)
        {
            string escaped = Regex.Escape(abbreviation);
            const string trailingBoundary = @"(?=$|[^\p{L}\p{N}])";

            if (allowStart)
            {
                return new Regex(
                    $@"(^|[^\p{{L}}\p{{N}}])({escaped}){trailingBoundary}",
                    RegexOptions.IgnoreCase
                );
            }
            else
            {
                return new Regex(
                    $@"([^\p{{L}}\p{{N}}])({escaped}){trailingBoundary}",
                    RegexOptions.IgnoreCase
                );
            }
        }

        /* -------------------------------------------------------
         * Pass 2) Special "St." logic
         * (Original version)
         * ------------------------------------------------------ */
        private static string HandleStAbbreviation(string input)
        {
            string[] tokens = Regex.Split(input, @"(\s+)");

            for (int i = 0; i < tokens.Length; i++)
            {
                if (StringEqualsIgnoreCase(tokens[i], "St."))
                {
                    // Default => "Saint"
                    string replacement = "Saint";

                    string nextToken = GetNextNonWhitespace(tokens, i);
                    string prevToken = GetPrevNonWhitespace(tokens, i);

                    // If next token is recognized as a street indicator => "Street"
                    // or if the previous token is numeric => "Street"
                    if (!string.IsNullOrEmpty(nextToken) &&
                        StreetIndicators.Contains(StripPunctuation(nextToken).ToLowerInvariant()))
                    {
                        replacement = "Street";
                    }
                    else if (!string.IsNullOrEmpty(prevToken) &&
                             IsNumeric(StripPunctuation(prevToken)))
                    {
                        replacement = "Street";
                    }

                    tokens[i] = replacement;
                }
            }

            return string.Join("", tokens);
        }

        /*
         * Pass 2) "St." logic WITH logging
         */
        private static string HandleStAbbreviationWithLog(string input, Dictionary<string,string> changesMap)
        {
            string[] tokens = Regex.Split(input, @"(\s+)");
            for (int i = 0; i < tokens.Length; i++)
            {
                if (StringEqualsIgnoreCase(tokens[i], "St."))
                {
                    // Default => "Saint"
                    string replacement = "Saint";
                    bool changedToStreet = false;

                    string nextToken = GetNextNonWhitespace(tokens, i);
                    string prevToken = GetPrevNonWhitespace(tokens, i);

                    if (!string.IsNullOrEmpty(nextToken) &&
                        StreetIndicators.Contains(StripPunctuation(nextToken).ToLowerInvariant()))
                    {
                        replacement = "Street";
                        changedToStreet = true;
                    }
                    else if (!string.IsNullOrEmpty(prevToken) &&
                             IsNumeric(StripPunctuation(prevToken)))
                    {
                        replacement = "Street";
                        changedToStreet = true;
                    }

                    tokens[i] = replacement;

                    // Log "St." -> "Street" or -> "Saint"
                    // If the user has multiple "St." expansions in the same text (Street vs. Saint),
                    // the dictionary approach will record only the first one encountered if you use .TryAdd().
                    // If you want to track both expansions for "St.", you'd need a different approach (like a list).
                    var finalExpansion = changedToStreet ? "Street" : "Saint";
                    changesMap?.TryAdd("St.", finalExpansion);
                }
            }
            return string.Join("", tokens);
        }

        /* -------------------------------------------------------
         * Helper methods
         * ------------------------------------------------------ */
        private static bool StringEqualsIgnoreCase(string a, string b)
        {
            return a != null && b != null && a.Equals(b, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetNextNonWhitespace(string[] tokens, int currentIndex)
        {
            for (int j = currentIndex + 1; j < tokens.Length; j++)
            {
                if (!string.IsNullOrWhiteSpace(tokens[j]))
                {
                    return tokens[j];
                }
            }
            return null;
        }

        private static string GetPrevNonWhitespace(string[] tokens, int currentIndex)
        {
            for (int j = currentIndex - 1; j >= 0; j--)
            {
                if (!string.IsNullOrWhiteSpace(tokens[j]))
                {
                    return tokens[j];
                }
            }
            return null;
        }

        private static bool IsNumeric(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (!char.IsDigit(c)) return false;
            }
            return true;
        }

        private static string StripPunctuation(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Trim(' ', ',', '.', '?', '!', ';', ':', '"', '\'');
        }

        /* 
         * Build a log that shows abbreviations -> expansions.
         * For example:
         *   'Mr.' -> 'Mister '
         *   'etc.' -> 'et cetera '
         *   'St.' -> 'Street'
         */
        private static string BuildChangesLog(Dictionary<string,string> changesMap)
        {
            if (changesMap.Count == 0)
            {
                return "";
            }
            return "Abbreviations converted:" + Environment.NewLine + 
                   string.Join(
                       Environment.NewLine,
                       changesMap.Select(kvp => $"'{kvp.Key}' -> '{kvp.Value}'")
                   );
        }
    }
}
