// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Protobuf.Collections;
using Lingotion.Thespeon.Language.Proto;

namespace Lingotion.Thespeon.Language
{

    /// <summary>
    /// Converts between strings and Unicode code points. .NET strings are UTF-16, so a code point outside the
    /// Basic Multilingual Plane takes two chars.
    /// </summary>
    internal static class CodePoints
    {
        public static List<int> From(string text)
        {
            List<int> codePoints = new(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    codePoints.Add(char.ConvertToUtf32(text[i], text[i + 1]));
                    i++;
                }
                else
                {
                    codePoints.Add(text[i]);
                }
            }
            return codePoints;
        }

        public static string ToText(IReadOnlyList<int> codePoints, int start, int count)
        {
            StringBuilder text = new(count);
            for (int i = start; i < start + count; i++)
            {
                Append(text, codePoints[i]);
            }
            return text.ToString();
        }

        public static string ToText(IReadOnlyList<int> codePoints)
        {
            return ToText(codePoints, 0, codePoints.Count);
        }

        public static void Append(StringBuilder text, int codePoint)
        {
            if (codePoint > 0xFFFF)
            {
                text.Append(char.ConvertFromUtf32(codePoint));
            }
            else
            {
                text.Append((char)codePoint);
            }
        }

        public static int Utf16Length(int codePoint)
        {
            return codePoint > 0xFFFF ? 2 : 1;
        }
    }

    /// <summary>
    /// A character class of a text preprocessing file: ascending, disjoint inclusive ranges of code points.
    /// </summary>
    internal sealed class CodePointSet
    {
        private readonly int[] _firsts;
        private readonly int[] _lasts;
        private readonly bool[] _ascii = new bool[128];

        public string Name { get; }

        public CodePointSet(string name, RepeatedField<uint> ranges)
        {
            Name = name;
            if (ranges.Count % 2 != 0)
            {
                throw new ArgumentException($"Character class '{name}' must list its ranges as pairs, got {ranges.Count} values.");
            }
            _firsts = new int[ranges.Count / 2];
            _lasts = new int[ranges.Count / 2];
            long previousLast = -1;
            for (int i = 0; i < _firsts.Length; i++)
            {
                uint first = ranges[2 * i];
                uint last = ranges[2 * i + 1];
                if (first > last || first <= previousLast || last > 0x10FFFF)
                {
                    throw new ArgumentException($"Character class '{name}': ranges must be ascending, disjoint and within U+10FFFF.");
                }
                _firsts[i] = (int)first;
                _lasts[i] = (int)last;
                previousLast = last;
            }
            for (int codePoint = 0; codePoint < _ascii.Length; codePoint++)
            {
                _ascii[codePoint] = RangeStart(codePoint) >= 0;
            }
        }

        public bool Contains(int codePoint)
        {
            if (codePoint >= 0 && codePoint < _ascii.Length)
            {
                return _ascii[codePoint];
            }
            return RangeStart(codePoint) >= 0;
        }

        /// <summary>
        /// The first code point of the range holding codePoint, or -1 if it is not in the class.
        /// </summary>
        public int RangeStart(int codePoint)
        {
            int index = Array.BinarySearch(_firsts, codePoint);
            if (index < 0)
            {
                index = ~index - 1;
            }
            if (index >= 0 && codePoint <= _lasts[index])
            {
                return _firsts[index];
            }
            return -1;
        }

        public static CodePointSet Find(IReadOnlyDictionary<string, CodePointSet> classes, string name)
        {
            if (!classes.TryGetValue(name, out CodePointSet found))
            {
                throw new ArgumentException($"Unknown character class '{name}'.");
            }
            return found;
        }
    }

    /// <summary>
    /// The lowercase step: lowercasing by the file's tables, as Python's str.lower() does it.
    /// </summary>
    internal sealed class Lowercaser
    {
        private readonly int[] _firsts;
        private readonly int[] _lasts;
        private readonly int[] _strides;
        private readonly int[] _deltas;
        private readonly Dictionary<int, int[]> _special = new();
        private readonly Dictionary<int, (int FinalForm, CodePointSet Cased, CodePointSet Ignorable)> _contextual = new();

        public Lowercaser(Lowercase lowercase, IReadOnlyDictionary<string, CodePointSet> classes)
        {
            if (lowercase.Runs.Count % 4 != 0)
            {
                throw new ArgumentException($"Lowercase runs must come in groups of four, got {lowercase.Runs.Count} values.");
            }
            int count = lowercase.Runs.Count / 4;
            _firsts = new int[count];
            _lasts = new int[count];
            _strides = new int[count];
            _deltas = new int[count];
            int previousLast = -1;
            for (int i = 0; i < count; i++)
            {
                _firsts[i] = lowercase.Runs[4 * i];
                _lasts[i] = lowercase.Runs[4 * i + 1];
                _strides[i] = lowercase.Runs[4 * i + 2];
                _deltas[i] = lowercase.Runs[4 * i + 3];
                if (_strides[i] < 1 || _firsts[i] > _lasts[i] || _firsts[i] <= previousLast)
                {
                    throw new ArgumentException("Lowercase runs must be ascending and disjoint, with a positive stride.");
                }
                previousLast = _lasts[i];
            }
            foreach (SpecialLowercase special in lowercase.Special)
            {
                _special[(int)special.CodePoint] = CodePoints.From(special.Lowercase).ToArray();
            }
            foreach (ContextualLowercase contextual in lowercase.Contextual)
            {
                _contextual[(int)contextual.CodePoint] = ((int)contextual.FinalForm, CodePointSet.Find(classes, contextual.CasedClass), CodePointSet.Find(classes, contextual.IgnorableClass));
            }
        }

        /// <summary>
        /// The lowercase of one code point on its own, without context.
        /// </summary>
        public int[] Lower(int codePoint)
        {
            if (_special.TryGetValue(codePoint, out int[] special))
            {
                return special;
            }
            int index = Array.BinarySearch(_firsts, codePoint);
            if (index < 0)
            {
                index = ~index - 1;
            }
            if (index >= 0 && codePoint <= _lasts[index] && (codePoint - _firsts[index]) % _strides[index] == 0)
            {
                return new[] { codePoint + _deltas[index] };
            }
            return new[] { codePoint };
        }

        public List<int> Apply(List<int> text)
        {
            List<int> output = new(text.Count);
            for (int i = 0; i < text.Count; i++)
            {
                if (_contextual.TryGetValue(text[i], out var context) && IsFinal(text, i, context.Cased, context.Ignorable))
                {
                    output.Add(context.FinalForm);
                }
                else
                {
                    output.AddRange(Lower(text[i]));
                }
            }
            return output;
        }

        private static bool IsFinal(List<int> text, int index, CodePointSet cased, CodePointSet ignorable)
        {
            int before = index - 1;
            while (before >= 0 && ignorable.Contains(text[before]))
            {
                before--;
            }
            if (before < 0 || !cased.Contains(text[before]))
            {
                return false;
            }
            int after = index + 1;
            while (after < text.Count && ignorable.Contains(text[after]))
            {
                after++;
            }
            return after == text.Count || !cased.Contains(text[after]);
        }
    }

    /// <summary>
    /// The compose step: canonical composition by the file's tables.
    /// </summary>
    internal sealed class Composer
    {
        private readonly Dictionary<long, int> _pairs = new();
        private readonly int[] _firsts;
        private readonly int[] _lasts;
        private readonly int[] _classes;

        public Composer(Compose compose)
        {
            if (compose.Pairs.Count % 3 != 0 || compose.CombiningClasses.Count % 3 != 0)
            {
                throw new ArgumentException("Compose pairs and combining classes must come in groups of three.");
            }
            for (int i = 0; i < compose.Pairs.Count; i += 3)
            {
                _pairs[Key((int)compose.Pairs[i], (int)compose.Pairs[i + 1])] = (int)compose.Pairs[i + 2];
            }
            int count = compose.CombiningClasses.Count / 3;
            _firsts = new int[count];
            _lasts = new int[count];
            _classes = new int[count];
            int previousLast = -1;
            for (int i = 0; i < count; i++)
            {
                _firsts[i] = (int)compose.CombiningClasses[3 * i];
                _lasts[i] = (int)compose.CombiningClasses[3 * i + 1];
                _classes[i] = (int)compose.CombiningClasses[3 * i + 2];
                if (_firsts[i] > _lasts[i] || _firsts[i] <= previousLast)
                {
                    throw new ArgumentException("Combining classes must be ascending and disjoint.");
                }
                previousLast = _lasts[i];
            }
        }

        public List<int> Apply(List<int> text)
        {
            List<int> output = new(text.Count);
            int starter = -1;
            int lastClass = -1;
            foreach (int codePoint in text)
            {
                int combiningClass = CombiningClass(codePoint);
                if (starter >= 0)
                {
                    bool blocked = lastClass >= 0 && (lastClass == 0 || lastClass >= combiningClass);
                    if (!blocked && _pairs.TryGetValue(Key(output[starter], codePoint), out int composed))
                    {
                        output[starter] = composed;
                        continue;
                    }
                }
                output.Add(codePoint);
                if (combiningClass == 0)
                {
                    starter = output.Count - 1;
                    lastClass = -1;
                }
                else
                {
                    lastClass = combiningClass;
                }
            }
            return output;
        }

        private int CombiningClass(int codePoint)
        {
            int index = Array.BinarySearch(_firsts, codePoint);
            if (index < 0)
            {
                index = ~index - 1;
            }
            if (index >= 0 && codePoint <= _lasts[index])
            {
                return _classes[index];
            }
            return 0;
        }

        private static long Key(int first, int second)
        {
            return ((long)first << 21) | (uint)second;
        }
    }
}
