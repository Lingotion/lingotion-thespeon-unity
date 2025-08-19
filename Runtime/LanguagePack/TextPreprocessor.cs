// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using Lingotion.Thespeon.Inputs;
using Lingotion.Thespeon.Core;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;

namespace Lingotion.Thespeon.LanguagePack
{
    /// <summary>
    /// Provides methods for preprocessing text inputs, including cleaning and partitioning text segments.
    /// </summary>
    public static class TextPreprocessor
    {
        private static readonly HashSet<char> ambiguousApostrophes = new()
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
            '\u02C8', // MODIFIER LETTER VERTICAL LINE
            '\u02CA', // MODIFIER LETTER ACUTE ACCENT
            '\u02CB', // MODIFIER LETTER GRAVE ACCENT
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
            '\uFE32' // PRESENTATION FORM FOR VERTICAL EN DASH
        };

        private static readonly Dictionary<string, Regex> NumberPatterns = new()
        {
            { "eng", new(@"(\d+(\.\d+)?)(st|nd|rd|th)?", RegexOptions.Compiled) },
            { "swe", new(@"(\d+)", RegexOptions.Compiled) }
            // [DevComment] (\.\d+)?(:a|:e)? for Swedish if we remove : as a delimiter and want to parse decimals too.
            // [DevComment] old swedish pattern was new(@"(\D*)(\d+)(\D*)") This new one is untested but should capture decimal numbers as well
        };
        // [DevComment] Obs this regexp is used again in the NumberConverter
        public static readonly Regex WordRegex = new(@"[\p{L}\p{M}\p{N}]+(?:['’][\p{L}\p{M}\p{N}]+)*", RegexOptions.Compiled);

        /// <summary>
        /// Preprocesses the input by cleaning text segments and converting numbers to words (phonemes) where applicable.
        /// </summary>
        /// <param name="input">The ThespeonInput containing text segments to preprocess.</param>
        /// <returns>A new ThespeonInput with processed segments.</returns>
        /// <exception cref="ArgumentException">Thrown if a segment's text is null or whitespace.</exception>
        /// <exception cref="NotSupportedException">Thrown if the language is not supported for number conversion.</exception>
        public static ThespeonInput PreprocessInput(ThespeonInput input)
        {
            LingotionLogger.Debug("ProcessingInput: " + input.ToJson());
            ThespeonInput result = new(input);
            result.Segments.Clear();
            foreach (var segment in input.Segments)
            {
                string text = segment.Text;
                // [DevComment] assumes ISO639-2 is always present. langpacks are curretly identified by it and only it.
                string iso639_2 = segment.Language?.Iso639_2 ?? input.DefaultLanguage.Iso639_2;
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new ArgumentException("Segment text cannot be null or whitespace.");
                }
                string cleaned = CleanText(text);
                List<(string Text, bool IsMatch)> parts = PartitionByNumberMatches(cleaned, iso639_2);
                if (!parts.Any(p => p.IsMatch))
                {
                    ThespeonInputSegment segmentCopy = new(segment)
                    {
                        Text = cleaned,
                    };
                    result.Segments.Add(segmentCopy);
                    continue;
                }
                NumberConverter converter = null;
                converter = iso639_2.ToLower() switch
                {
                    "eng" => new NumberToWordsConverter(),
                    "swe" => new NumberToWordsSwedish(),
                    _ => throw new NotSupportedException($"Language '{iso639_2}' is not supported."),
                };
                ThespeonInputSegment currentSegment = new(segment)
                {
                    Text = string.Empty,
                    IsCustomPronounced = false
                };
                foreach (var (part, isMatch) in parts)
                {
                    string partText = isMatch ? converter.ConvertNumber(part) : part;
                    if (currentSegment.IsCustomPronounced == isMatch || !WordRegex.IsMatch(part))
                    {
                        currentSegment.Text += partText;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(currentSegment.Text))
                        {
                            result.Segments.Add(currentSegment);
                        }
                        currentSegment = new(segment)
                        {
                            Text = partText,
                            IsCustomPronounced = isMatch
                        };
                    }
                }
                result.Segments.Add(currentSegment);
            }
            LingotionLogger.Debug("Done processing: " + result.ToJson());
            return result;
        }

        private static string CleanText(string input)
        {
            input = Regex.Replace(input, @"<<<\d+>>>", string.Empty);
            input = input.ToLowerInvariant();
            input = Regex.Replace(input, @"\s+", " ");
            var builder = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                builder.Append(ambiguousApostrophes.Contains(c) ? '\'' : c);
            }
            return builder.ToString();
        }

        private static List<(string Text, bool IsMatch)> PartitionByNumberMatches(string text, string iso639_2)
        {
            var parts = new List<(string, bool)>();
            int index = 0;
            foreach (Match match in NumberPatterns[iso639_2].Matches(text))
            {
                if (match.Index > index)
                {
                    string before = text[index..match.Index];
                    parts.Add((before, false));
                }
                parts.Add((match.Value, true));
                index = match.Index + match.Length;
            }
            if (index < text.Length)
            {
                parts.Add((text.Substring(index), false));
            }
            return parts;
        }


    }

    
}

