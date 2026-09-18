// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using Lingotion.Thespeon.Inputs;
using Lingotion.Thespeon.Core;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;

namespace Lingotion.Thespeon.Language
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


        };

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

                string iso639_2 = segment.Language?.Iso639_2 ?? input.DefaultLanguage.Iso639_2;
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new ArgumentException("Segment text cannot be null or whitespace.");
                }
                string cleaned = CleanText(text);
                List<(string, bool)> parts = PartitionByNumberMatches(cleaned, iso639_2);
                List<(string, bool, List<float>)> mergedParts;
                if (segment.Text.Contains(ControlCharacters.AudioSampleRequest) && parts.Any(p => p.Item2))
                {
                    mergedParts = MergeMarkerSeparatedNumbers(parts);
                }
                else
                {
                    mergedParts = parts.Select(p => (p.Item1, p.Item2, (List<float>)null)).ToList();
                }
                if (!mergedParts.Any(p => p.Item2))
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

                // empty emotion blends. The original outer endpoints are restored by ApplySplitKeypoints below.
                List<ThespeonInputSegment> splitSegments = new();
                ThespeonInputSegment currentSegment = MakeSplitSegment(segment, string.Empty, false);


                foreach (var (part, isMatch, fractions) in mergedParts)
                {
                    string partText = isMatch ? converter.ConvertNumber(part) : part;
                    if (fractions != null)
                    {
                        var indices = fractions
                            .Select(f => UnityEngine.Mathf.RoundToInt(Math.Clamp(f, 0.0f, 1.0f) * partText.Length))
                            .OrderBy(x => x)
                            .ToList();
                        var sb = new StringBuilder(partText);
                        int added = 0;
                        foreach (var rawIdx in indices)
                        {
                            int idx = Math.Clamp(rawIdx, 0, partText.Length) + added;
                            sb.Insert(idx, ControlCharacters.AudioSampleRequest);
                            added++;
                        }

                        partText = sb.ToString();
                    }
                    if (currentSegment.Text.All(c => c == ControlCharacters.AudioSampleRequest)) currentSegment.IsCustomPronounced = isMatch;
                    if (currentSegment.IsCustomPronounced == isMatch || !WordRegex.IsMatch(part))
                    {
                        currentSegment.Text += partText;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(currentSegment.Text))
                        {
                            splitSegments.Add(currentSegment);
                        }
                        currentSegment = MakeSplitSegment(segment, partText, isMatch);
                    }
                }
                if (!string.IsNullOrEmpty(currentSegment.Text))
                {
                    splitSegments.Add(currentSegment);
                }
                if (splitSegments.Count == 0)
                {
                    throw new ArgumentException("Segment preprocessing unexpectedly produced no segments.");
                }
                ApplySplitKeypoints(segment, splitSegments);
                result.Segments.AddRange(splitSegments);
            }
            if (result.Segments.Count == 0)
            {
                throw new ArgumentException("Segment preprocessing unexpectedly produced no segments.");
            }
            ApplyGlobalScalarCurves(result);
            if (!KeypointUtils.PopulateEmotionKeypoints(result.Segments, result.DefaultEmotion))
            {
                throw new ArgumentException("Failed to populate emotion keypoints.");
            }
            if (!KeypointUtils.PopulateSpeedKeypoints(result.Segments))
            {
                throw new ArgumentException("Failed to populate speed keypoints.");
            }
            if (!KeypointUtils.PopulateLoudnessKeypoints(result.Segments))
            {
                throw new ArgumentException("Failed to populate loudness keypoints.");
            }
            LingotionLogger.Debug("Done processing: " + result.ToJson());
            return result;
        }

        /// <summary>
        /// Creates a sub-segment of an original segment with empty emotion blends, so splitting contributes no interior emotion keypoints.
        /// </summary>
        private static ThespeonInputSegment MakeSplitSegment(ThespeonInputSegment original, string text, bool isCustomPronounced)
        {
            ThespeonInputSegment segment = new(original)
            {
                Text = text,
                IsCustomPronounced = isCustomPronounced
            };
            segment.StartEmotion.Clear();
            segment.EndEmotion.Clear();
            return segment;
        }

        /// <summary>
        /// Restores the original segment's keypoints onto the sub-segments produced by number splitting.
        /// Emotion keeps only the outer endpoints (interior blends stay empty so the curve passes straight through), while
        /// speed and loudness - which have no unset representation - are resampled from the original linear ramp at each new boundary.
        /// </summary>
        private static void ApplySplitKeypoints(ThespeonInputSegment original, List<ThespeonInputSegment> splitSegments)
        {
            if (splitSegments.Count == 0)
            {
                return;
            }
            splitSegments[0].StartEmotion = ModelInputSegment.CopyBlend(original.StartEmotion);
            splitSegments[^1].EndEmotion = ModelInputSegment.CopyBlend(original.EndEmotion);

            int totalLength = splitSegments.Sum(s => s.Text.Length);
            if (splitSegments.Count == 1 || totalLength <= 1)
            {
                splitSegments[0].StartSpeed = original.StartSpeed;
                splitSegments[0].EndSpeed = original.EndSpeed;
                splitSegments[0].StartLoudness = original.StartLoudness;
                splitSegments[0].EndLoudness = original.EndLoudness;
                return;
            }
            int cursor = 0;
            foreach (ThespeonInputSegment segment in splitSegments)
            {
                int endPosition = cursor + segment.Text.Length - 1;
                float startAlpha = cursor / (float)(totalLength - 1);
                float endAlpha = endPosition / (float)(totalLength - 1);
                segment.StartSpeed = Lerp(original.StartSpeed, original.EndSpeed, startAlpha);
                segment.EndSpeed = Lerp(original.StartSpeed, original.EndSpeed, endAlpha);
                segment.StartLoudness = Lerp(original.StartLoudness, original.EndLoudness, startAlpha);
                segment.EndLoudness = Lerp(original.StartLoudness, original.EndLoudness, endAlpha);
                cursor = endPosition + 1;
            }
        }

        /// <summary>
        /// Overwrites every segment's speed and loudness boundary values by sampling the input level AnimationCurves
        /// at the segment's global character position. Curves that are flat at 1 are treated as "not supplied" and leave
        /// the per-segment values untouched.
        /// </summary>
        private static void ApplyGlobalScalarCurves(ThespeonInput input)
        {
            bool useSpeed = !IsDefaultCurve(input.Speed);
            bool useLoudness = !IsDefaultCurve(input.Loudness);
            if (!useSpeed && !useLoudness)
            {
                return;
            }
            int totalLength = input.Segments.Sum(segment => segment.Text.Length);
            if (totalLength <= 0)
            {
                return;
            }
            int globalPosition = 0;
            foreach (ThespeonInputSegment segment in input.Segments)
            {
                int endPosition = globalPosition + segment.Text.Length - 1;
                float startAlpha = totalLength > 1 ? globalPosition / (float)(totalLength - 1) : 0f;
                float endAlpha = totalLength > 1 ? endPosition / (float)(totalLength - 1) : 0f;
                if (useSpeed)
                {
                    segment.StartSpeed = input.Speed.Evaluate(startAlpha);
                    segment.EndSpeed = input.Speed.Evaluate(endAlpha);
                }
                if (useLoudness)
                {
                    segment.StartLoudness = input.Loudness.Evaluate(startAlpha);
                    segment.EndLoudness = input.Loudness.Evaluate(endAlpha);
                }
                globalPosition += segment.Text.Length;
            }
        }

        /// <summary>
        /// Returns true when the curve is missing or effectively constant at 1, meaning the caller did not supply a curve.
        /// </summary>
        public static bool IsDefaultCurve(UnityEngine.AnimationCurve curve)
        {
            if (curve == null || curve.keys.Length == 0)
            {
                return true;
            }

            const int sampleCount = 50;
            float t0 = curve.keys[0].time;
            float t1 = curve.keys[^1].time;

            if (t1 - t0 < 0.0001f)
            {
                return Math.Abs(curve.Evaluate(t0) - 1f) < 0.001f;
            }

            float dt = (t1 - t0) / sampleCount;
            float sum = 0f;
            for (int i = 0; i <= sampleCount; i++)
            {
                float diff = curve.Evaluate(t0 + i * dt) - 1f;
                sum += diff * diff;
            }
            return UnityEngine.Mathf.Sqrt(sum * dt / (t1 - t0)) < 0.01f;
        }

        private static float Lerp(float start, float end, float alpha)
        {
            return start + (end - start) * Math.Clamp(alpha, 0f, 1f);
        }

        private static string CleanText(string input)
        {
            input = input.ToLowerInvariant();
            input = Regex.Replace(input, @"\s+", " ");
            char ASRChar = ControlCharacters.AudioSampleRequest;
            input = Regex.Replace(input, $"{Regex.Escape(ASRChar.ToString())}+", ASRChar.ToString());
            var builder = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                builder.Append(ambiguousApostrophes.Contains(c) ? '\'' : c);
            }
            return builder.ToString();
        }

        private static List<(string, bool)> PartitionByNumberMatches(string text, string iso639_2)
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

        /// <summary>
        /// Merges consecutive number parts separated by only markers into a single segment with associated fractions representing original marker positions.
        /// </summary>
        /// <param name="parts">A partition of text into numbers and non numbers (alternating)</param>
        /// <returns></returns>
        private static List<(string, bool, List<float>)> MergeMarkerSeparatedNumbers(List<(string Text, bool IsMatch)> parts)
        {
            var result = new List<(string, bool, List<float>)>();

            int i = 0;
            while (i < parts.Count)
            {

                var (text, isMatch) = parts[i];

                if (!isMatch)
                {
                    result.Add((text, false, null));
                    i++;
                    continue;
                }

                int j = i + 1;
                bool canMerge = false;

                int totalDigitsLen = text.Length;
                while (j < parts.Count)
                {
                    var (t, isM) = parts[j];

                    if (isM)
                    {
                        canMerge = true;
                        totalDigitsLen += t.Length;
                        j++;
                    }
                    else
                    {
                        bool markersOnly = t.Length > 0 && t.All(c => c == ControlCharacters.AudioSampleRequest);

                        if (!markersOnly) break;
                        j++;
                    }
                }

                if (!canMerge)
                {
                    result.Add((text, true, null));
                    i++;
                    continue;
                }

                var sb = new StringBuilder(text);
                var fractions = new List<float>();
                int digitOffset = text.Length;

                for (int k = i + 1; k < j; k++)
                {
                    var (t, isM) = parts[k];
                    if (isM)
                    {
                        sb.Append(t);
                        digitOffset += t.Length;
                    }
                    else
                    {
                        if (t.Length > 0 && t.All(c => c == ControlCharacters.AudioSampleRequest))
                        {
                            float frac = totalDigitsLen == 0 ? 0.0f : Math.Clamp((float)(digitOffset - 0) / totalDigitsLen, 0.0f, 1.0f);
                            for (int m = 0; m < t.Length; m++)
                                fractions.Add(frac);
                        }
                    }
                }
                LingotionLogger.Debug($"Merged number: {sb} with fractions: {string.Join(", ", fractions)}");
                result.Add((sb.ToString(), true, fractions));
                i = j;
            }

            return result;
        }



    }

    
}

