// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Language.Proto;

namespace Lingotion.Thespeon.Language
{
    /// <summary>
    /// A word found in a text: the unit that is looked up in the lookup table and phonemized.
    /// </summary>
    public readonly struct Word
    {
        /// <summary>
        /// Gets the index in the text, in chars, where the word starts.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the length of the word, in chars.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the word.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Initializes a new word.
        /// </summary>
        /// <param name="index">The index in the text, in chars, where the word starts.</param>
        /// <param name="text">The word.</param>
        public Word(int index, string text)
        {
            Index = index;
            Length = text.Length;
            Text = text;
        }
    }

    /// <summary>
    /// Splits text into words by a language pack's word definition.
    /// </summary>
    public class WordSplitter
    {
        private readonly CodePointSet _start;
        private readonly CodePointSet _continuation;
        private readonly CodePointSet _joiner;
        private readonly bool _joinerMayEnd;

        internal WordSplitter(WordDefinition definition, IReadOnlyDictionary<string, CodePointSet> classes)
        {
            _start = CodePointSet.Find(classes, definition.StartClass);
            _continuation = CodePointSet.Find(classes, definition.ContinueClass);
            _joiner = CodePointSet.Find(classes, definition.JoinerClass);
            _joinerMayEnd = definition.JoinerMayEnd;
        }

        /// <summary>
        /// Splits a text into its words.
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <param name="isKnown">Whether a word is in the lookup table, which decides whether joiners right after a word stay part of it when the definition allows that.</param>
        /// <returns>The words in order.</returns>
        public List<Word> Split(string text, Func<string, bool> isKnown)
        {
            List<int> codePoints = CodePoints.From(text);
            int count = codePoints.Count;
            int[] charIndex = new int[count + 1];
            for (int k = 0; k < count; k++)
            {
                charIndex[k + 1] = charIndex[k] + CodePoints.Utf16Length(codePoints[k]);
            }

            List<Word> words = new();
            int i = 0;
            while (i < count)
            {
                if (!InWord(codePoints, i))
                {
                    i++;
                    continue;
                }
                int start = i;
                while (i < count && InWord(codePoints, i))
                {
                    i++;
                }
                while (start < i && _continuation.Contains(codePoints[start]) && !_start.Contains(codePoints[start]))
                {
                    start++;
                }
                int end = i;
                if (_joinerMayEnd && isKnown != null)
                {
                    int after = end;
                    while (after < count && _joiner.Contains(codePoints[after]))
                    {
                        after++;
                    }
                    if (after > end && isKnown(CodePoints.ToText(codePoints, start, after - start)))
                    {
                        end = after;
                    }
                }
                if (end > start)
                {
                    words.Add(new Word(charIndex[start], CodePoints.ToText(codePoints, start, end - start)));
                }
                i = Math.Max(i, end);
            }
            return words;
        }

        private bool InWord(List<int> codePoints, int index)
        {
            int codePoint = codePoints[index];
            if (_start.Contains(codePoint) || _continuation.Contains(codePoint))
            {
                return true;
            }
            return _joiner.Contains(codePoint) && index > 0 && index < codePoints.Count - 1
                && IsWordCharacter(codePoints[index - 1]) && IsWordCharacter(codePoints[index + 1]);
        }

        /// <summary>
        /// Gets whether a text holds any word.
        /// </summary>
        /// <param name="text">The text to look in.</param>
        /// <returns>True if some character of the text starts a word.</returns>
        public bool ContainsWord(string text)
        {
            foreach (int codePoint in CodePoints.From(text))
            {
                if (_start.Contains(codePoint))
                {
                    return true;
                }
            }
            return false;
        }

        internal bool IsWordCharacter(int codePoint)
        {
            return _start.Contains(codePoint) || _continuation.Contains(codePoint) || _joiner.Contains(codePoint);
        }
    }
}
