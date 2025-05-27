// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.IO;
using System.Text;
using Lingotion.Thespeon.API;


namespace Lingotion.Thespeon.Filters
{
    public class ConverterFilterService
    {
            // Use a static readonly field to define a set of characters commonly used in place of the standard apostrophe
            private static readonly HashSet<char> AmbiguousApostrophes = new HashSet<char>
            {
                '\u2018', // LEFT SINGLE QUOTATION MARK
                '\u2019', // RIGHT SINGLE QUOTATION MARK
                '\u201B', // SINGLE HIGH-REVERSED-9 QUOTATION MARK
                '\u02BC', // MODIFIER LETTER APOSTROPHE
                '\u02BB', // MODIFIER LETTER TURNED COMMA
                '\uFF07', // FULLWIDTH APOSTROPHE
                '\u0060', // GRAVE ACCENT
                '\u00B4', // ACUTE ACCENT
                '\u2032', // PRIME
                '\u275B', // HEAVY SINGLE TURNED COMMA QUOTATION MARK ORNAMENT
                '\u275C', // HEAVY SINGLE COMMA QUOTATION MARK ORNAMENT
                '\u275F', // HEAVY LOW SINGLE COMMA QUOTATION MARK ORNAMENT
                '\u2760', // HEAVY LOW SINGLE TURNED COMMA QUOTATION MARK ORNAMENT
                '\u02C8', // MODIFIER LETTER VERTICAL LINE
                '\u02CA', // MODIFIER LETTER ACUTE ACCENT
                '\u02CB', // MODIFIER LETTER GRAVE ACCENT
                '\u02F4', // MODIFIER LETTER MIDDLE GRAVE ACCENT
                '\u02F5', // MODIFIER LETTER MIDDLE DOUBLE GRAVE ACCENT
                '\u02F6', // MODIFIER LETTER MIDDLE DOUBLE ACUTE ACCENT
                '\u1FEF', // GREEK VARIA
                '\u1FFD', // GREEK OXIA
                '\u1FBF', // GREEK PSILI
                '\u1FFE', // GREEK DASIA
                '\u0374', // GREEK NUMERAL SIGN
                '\u0384', // GREEK TONOS
                '\u055A', // ARMENIAN APOSTROPHE
                '\u07F4', // NKO HIGH TONE APOSTROPHE
                '\u07F5', // NKO LOW TONE APOSTROPHE
                '\u05F3', // HEBREW PUNCTUATION GERESH
                '\u05F4', // HEBREW PUNCTUATION GERSHAYIM
                '\u180B', // MONGOLIAN FREE VARIATION SELECTOR ONE
                '\u180C', // MONGOLIAN FREE VARIATION SELECTOR TWO
                '\u180D', // MONGOLIAN FREE VARIATION SELECTOR THREE
                '\u180E', // MONGOLIAN VOWEL SEPARATOR
                '\u2010', // HYPHEN
                '\u2011', // NON-BREAKING HYPHEN
                '\u2012', // FIGURE DASH
                '\u2013', // EN DASH
                '\u2014', // EM DASH
                '\u2015', // HORIZONTAL BAR
                '\u2E3A', // TWO-EM DASH
                '\u2E3B', // THREE-EM DASH
                '\u301C', // WAVE DASH
                '\u3030', // WAVY DASH
                '\u30A0', // KATAKANA-HIRAGANA DOUBLE HYPHEN
                '\uFE31', // PRESENTATION FORM FOR VERTICAL EM DASH
                '\uFE32', // PRESENTATION FORM FOR VERTICAL EN DASH
                '\uFE58', // SMALL EM DASH
                '\uFE63', // SMALL HYPHEN-MINUS
                '\uFF0D'  // FULLWIDTH HYPHEN-MINUS
            };

        public string ConvertNumbers(string input, string languageCode)
        {
            var converter = LingotionConverterFactory.GetNumberConverter(languageCode);
            return converter.ConvertNumbers(input);
        }


        public (string,string) ConvertAbbreviations(string input, string languageCode)
        {

            // string changelog;
            var converter = LingotionConverterFactory.GetAbbreviationConverter(languageCode);
            return (converter.ConvertAbbreviations(input));
        }


        public (string, string) CleanText(
            string input, 
            Dictionary<string, int> graphemeVocab, 
            bool isCustomPhonemized)
        {
            // If already phonemized, skip cleaning:
            if (isCustomPhonemized)
            {
                return (input, "");
            }

            // Holds information on all removed graphemes with their original indices
            List<string> removedGraphemes = new List<string>();

            // Regex to capture sequences of letters, combining marks, or digits
            Regex wordRegex = new Regex(@"[\p{L}\p{M}]+", RegexOptions.Compiled);

            // Use Regex.Replace with a "MatchEvaluator" lambda:
            // Only the matched "word" sections get processed.
            // Everything else (spaces, punctuation, etc.) is untouched.
            string cleaned = wordRegex.Replace(input, match =>
            {
                // We'll build a new substring for this match
                // keeping only characters that exist in the grapheme vocab.
                var sb = new System.Text.StringBuilder();

                // Check each character in the matched substring
                for (int i = 0; i < match.Value.Length; i++)
                {
                    char c = match.Value[i];
                    string cLower = c.ToString().ToLowerInvariant();
                    int originalIndex = match.Index + i;  // Where 'c' occurs in the full input

                    // If not in vocab, log it as removed
                    if (!graphemeVocab.ContainsKey(cLower))
                    {
                        removedGraphemes.Add($"'{c}' at index {originalIndex}");
                    }
                    else
                    {
                        // This character is allowed: keep it in the cleaned text
                        sb.Append(c);
                    }
                }

                // Return the newly built substring for this match region
                return sb.ToString();
            });

            // Build a feedback message if any characters were removed
            string feedback = "";
            if (removedGraphemes.Count > 0)
            {
                feedback = "Illegal graphemes removed: " + string.Join(", ", removedGraphemes);
            }

            return (cleaned, feedback);
        }


        public string RemoveExcessWhitespace(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            // This pattern \s+ matches one or more whitespace characters (including spaces, tabs, newlines).
            // Regex.Replace then replaces the entire match with a single space.
            // Finally, Trim() removes any leading or trailing spaces if they exist.
            string result = Regex.Replace(input, @"\s+", " ");

            return result;
        }
            
        /// <summary>
        /// Replaces various apostrophe-like characters in the input text with the standard apostrophe (U+0027).
        /// </summary>
        /// <param name="input">The input string potentially containing ambiguous apostrophe characters.</param>
        /// <returns>A string with all ambiguous apostrophe characters replaced by the standard apostrophe.</returns>
        public static string NormalizeApostrophes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;


            var builder = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                builder.Append(AmbiguousApostrophes.Contains(c) ? '\'' : c);
            }

            return builder.ToString();
        }

        public string ApplyAllFiltersOnSegmentPrePhonemize(UserSegment input, Dictionary<string, int> graphemeVocab, string defaultLanguageIso639_2)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            // If the segment is already custom phonemized, skip filtering
            if (input.isCustomPhonemized.GetValueOrDefault())
                return "";

            // Check if the segment has a language set; fall back to the default
            string languageToUse = input.languageObj?.iso639_2;

            // If no language is available at all, we must use the default
            if (string.IsNullOrWhiteSpace(languageToUse))
            {
                if (string.IsNullOrWhiteSpace(defaultLanguageIso639_2))
                    throw new ArgumentNullException(nameof(defaultLanguageIso639_2));

                languageToUse = defaultLanguageIso639_2;
            }

            string feedback = "";

            input.text = input.text.ToLower();

            if (languageToUse == "eng")
            {
                // For English, we need to "normalise" the apostrophes to a standard one

                input.text = NormalizeApostrophes(input.text);
            }

            // Apply all filters in sequence
            input.text = ConvertNumbers(input.text, languageToUse);
            string abbrevationFeedback;
            (input.text, abbrevationFeedback) = ConvertAbbreviations(input.text, languageToUse);
            if (!string.IsNullOrWhiteSpace(abbrevationFeedback))
            {
                feedback += abbrevationFeedback;
                feedback += "\n";
            }
            string textCleaningFeedback;
            (input.text, textCleaningFeedback) = CleanText(input.text, graphemeVocab, input.isCustomPhonemized ?? false);
            if (!string.IsNullOrWhiteSpace(textCleaningFeedback))
            {
                feedback += textCleaningFeedback;
                feedback += "\n";
            }
            input.text = RemoveExcessWhitespace(input.text);
            input.text = input.text.ToLower();
            return feedback;
        }
    }
}