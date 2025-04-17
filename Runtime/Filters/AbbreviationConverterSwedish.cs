// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Lingotion.Thespeon.Filters
{
    public class AbbreviationConverterSwedish : IAbbrevationToWordsConverter 
    {
        private static readonly Dictionary<string, string> abbreviationsAllowedToStart = new()
        {
            { "Dr.", "Doktor" },
            { "Dr",  "Doktor" },
            { "Prof.", "Professor" },
            { "Sgt.", "Sergeant" },
            { "St.",  "Saint" },
            { "AB", "aktiebolag" },
            { "t.ex.", "till exempel" },
            { "t. ex.", "till exempel" },
            { "t.o.m.", "till och med" },
            { "t. o. m.", "till och med" },
            { "fr.o.m.", "från och med" },
            { "fr. o. m.", "från och med" },
            { "p.g.a.", "på grund av" },
            { "p. g. a.", "på grund av" },
            { "m.fl.", "med flera" },
            { "m. fl.", "med flera" },
            { "m.m.", "med mera" },
            { "bl.a.", "bland annat" },
            { "bl. a.", "bland annat" },
            { "dvs.", "det vill säga" },
            { "d.v.s.", "det vill säga" },
            { "d. v. s.", "det vill säga" },
            { "osv.", "och så vidare" },
            { "o.s.v.", "och så vidare" },
            { "o. s. v.", "och så vidare" },
            { "S:t", "Sankt" },
            { "Mag.", "Magister" },
            { "Fil.", "Filosofie" },
            { "Jur.", "Juridisk" },
            { "Med.", "Medicinsk" },
            { "Tek.", "Teknisk" },
            { "Kand.", "Kandidat" },
            { "Lic.", "Licentiat" },
            { "Doc.", "Docent" },
            { "Rekt.", "Rektor" },
            { "nr", "nummer" },
            { "nr.", "nummer" },
            { "sid.", "sida" },
            { "kr.", "kronor" },
            { "ca.", "cirka" },
            { "ca", "cirka" },
            { "&", "och" }
        };

        private static readonly Dictionary<string, string> abbreviationsNotAllowedToStart = new()
        {
            { "etc.", "et cetera" },
            { "etc",  "et cetera" },
            { "HB", "handelsbolag" },
            { "KB", "kommanditbolag" },
            { "Ek. för.", "ekonomisk förening" },
            { "f.d.", "före detta" },
            { "cm", "centimeter" },
            { "mm", "millimeter" },
            { "dm", "decimeter" },
            { "m", "meter" },
            { "km", "kilometer" },
            { "m2", "kvadratmeter" },
            { "km2", "kvadratkilometer" },
            { "L", "liter" },
            { "ml", "milliliter" },
            { "cl", "centiliter" },
            { "dl", "deciliter" },
            { "kg", "kilogram" },
            { "g", "gram" },
            { "mg", "milligram" },
            { "ton", "ton" },
            { "h", "timme" },
            { "min", "minut" },
            { "s", "sekund" },
            { "ms", "millisekund" },
            { "år", "år" },
            { "v.", "vecka" },
            { "km/h", "kilometer i timmen" },
            { "m/s", "meter per sekund" },
            { "kr", "kronor" },
            { "öre", "öre" },
            { "€", "euro" },
            { "USD", "amerikanska dollar" },
            { "GBP", "brittiska pund" },
            { "%", "procent" },
            { "vs.", "versus" },
            { "c.", "cirka" },
            { "tfn.", "telefon" }
        };

        /// <summary>
        /// Simple one-parameter version if you DON'T need the log.
        /// </summary>
        public string ConvertAbbreviationsOriginal(string text)
        {
            if (string.IsNullOrEmpty(text)) 
                return text;
            
            text = ReplaceAllowedToStart(text);
            text = ReplaceNotAllowedToStart(text);
            return text;
        }

        /// <summary>
        /// Tuple-returning version: returns (convertedText, changesLog).
        /// Instead of counting how many times each abbreviation was replaced,
        /// we record each abbreviation + its translation in a dictionary.
        /// </summary>
        public (string , string ) ConvertAbbreviations(string text)
        {
            if (string.IsNullOrEmpty(text))
                return (text, "No text given.");

            // Instead of counts, we map Abbrev -> Replacement
            var changesMap = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

            // Pass A
            string result = ReplaceAllowedToStart(text, changesMap);
            // Pass B
            result = ReplaceNotAllowedToStart(result, changesMap);

            // Build a log that shows "Abbrev -> Translation"
            string log = BuildChangesLog(changesMap);

            return (result, log);
        }

        // ----------------------------------------------------------------
        //  Pass A: Replacements that can appear at the start
        // ----------------------------------------------------------------
        private string ReplaceAllowedToStart(string text, Dictionary<string,string> changesMap = null)
        {
            foreach (var kvp in abbreviationsAllowedToStart)
            {
                string abbr = kvp.Key;
                string repl = kvp.Value;
                string pattern = CreateRegExpAllowedToStart(Regex.Escape(abbr));

                text = Regex.Replace(text, pattern, m =>
                {
                    // Once we see at least one match, record it in changesMap
                    changesMap?.TryAdd(abbr, repl);

                    // m.Groups[1] is the boundary (start-of-line or whitespace)
                    return m.Groups[1].Value + repl;
                }, RegexOptions.IgnoreCase);
            }
            return text;
        }

        // ----------------------------------------------------------------
        //  Pass B: Replacements that cannot appear at start
        // ----------------------------------------------------------------
        private string ReplaceNotAllowedToStart(string text, Dictionary<string,string> changesMap = null)
        {
            foreach (var kvp in abbreviationsNotAllowedToStart)
            {
                string abbr = kvp.Key;
                string repl = kvp.Value;
                string pattern = CreateRegExpNotStart(Regex.Escape(abbr));

                text = Regex.Replace(text, pattern, m =>
                {
                    changesMap?.TryAdd(abbr, repl);

                    // m.Groups[1] is the whitespace before the abbreviation
                    return m.Groups[1].Value + repl;
                }, RegexOptions.IgnoreCase);
            }
            return text;
        }

        // ----------------------------------------------------------------
        //  Regex Helpers
        // ----------------------------------------------------------------
        private static string CreateRegExpAllowedToStart(string escapedAbbreviation)
        {
            // (^|\s)(ABBR)(?=\s|$)
            return $@"(^|\s)({escapedAbbreviation})(?=\s|$)";
        }

        private static string CreateRegExpNotStart(string escapedAbbreviation)
        {
            // (\s)(ABBR)(?=\s|$)
            return $@"(\s)({escapedAbbreviation})(?=\s|$)";
        }

        // ----------------------------------------------------------------
        //  Build log showing original->translation
        // ----------------------------------------------------------------
        private static string BuildChangesLog(Dictionary<string,string> changesMap)
        {
            if (changesMap.Count == 0)
                return "";

            // For each abbreviation actually found, show "abbr -> repl"
            // E.g.: "'Dr.' -> 'Doktor'"
            return "Abbreviations converted:" + Environment.NewLine + 
                   string.Join(
                       Environment.NewLine,
                       changesMap.Select(kvp => $"'{kvp.Key}' -> '{kvp.Value}'")
                   );
        }
    }
}
