// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.IO;
using Lingotion.Thespeon.Utils;


namespace Lingotion.Thespeon.Filters
{
    public class ConverterFilterService
    {
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
            (input.text,textCleaningFeedback) = CleanText(input.text, graphemeVocab, input.isCustomPhonemized ?? false);
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