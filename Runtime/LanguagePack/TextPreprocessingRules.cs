// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.Linq;
using Lingotion.Thespeon.Inputs;
using Lingotion.Thespeon.Language.Proto;

namespace Lingotion.Thespeon.Language
{
    /// <summary>
    /// The text preprocessing of one language, compiled from a language pack's text preprocessing file:
    /// the steps that normalize natural language text, how its numbers are spoken, and what a word is.
    /// </summary>
    /// <remarks>
    /// Everything that depends on Unicode comes from the file, so text is read the same way whatever Unicode
    /// data the runtime carries. See text_preprocessing.proto in model-meta-graph for the format.
    /// </remarks>
    public class TextPreprocessingRules
    {
        /// <summary>
        /// The major version of the text preprocessing format this version of Thespeon reads.
        /// </summary>
        public const uint SupportedMajorVersion = 3;

        private readonly List<Func<List<int>, List<int>>> _steps;
        private readonly Dictionary<string, CodePointSet> _classes = new();

        /// <summary>
        /// Gets the ISO 639-2 code of the language these rules are for.
        /// </summary>
        public string Iso639_2 { get; }

        /// <summary>
        /// Gets the numbers of the language: where they are in a text, and how they are spoken.
        /// </summary>
        public NumberExpander Numbers { get; }

        /// <summary>
        /// Gets what a word of the language is: the unit that is looked up in the lookup table and phonemized.
        /// </summary>
        public WordSplitter Words { get; }

        internal Lowercaser Lowercase { get; }
        internal IReadOnlyDictionary<string, CodePointSet> Classes => _classes;

        /// <summary>
        /// Compiles the rules read from a language pack's text preprocessing file.
        /// </summary>
        /// <param name="rules">The parsed text preprocessing file.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="rules"/> is null.</exception>
        /// <exception cref="NotSupportedException">Thrown if the file is of a format version this version of Thespeon does not read.</exception>
        /// <exception cref="ArgumentException">Thrown if a table, step, number form, rule set or the word definition is malformed.</exception>
        public TextPreprocessingRules(Proto.TextPreprocessing rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            if (rules.MajorVersion != SupportedMajorVersion)
            {
                throw new NotSupportedException($"Text preprocessing version {rules.MajorVersion}.{rules.MinorVersion}.{rules.PatchVersion} is not supported by this version of Thespeon, which reads major version {SupportedMajorVersion}.");
            }
            Iso639_2 = rules.Iso6392;
            foreach (CharacterClass characterClass in rules.CharacterClasses)
            {
                if (_classes.ContainsKey(characterClass.Name))
                {
                    throw new ArgumentException($"Character class '{characterClass.Name}' is defined twice.");
                }
                _classes[characterClass.Name] = new CodePointSet(characterClass.Name, characterClass.Ranges);
            }
            if (rules.Words == null)
            {
                throw new ArgumentException($"Text preprocessing rules for '{Iso639_2}' define no words.");
            }
            Words = new WordSplitter(rules.Words, _classes);
            _steps = rules.Steps.Select(CompileStep).ToList();
            Lowercase = rules.Steps.Where(step => step.KindCase == Step.KindOneofCase.Lowercase).Select(step => new Lowercaser(step.Lowercase, _classes)).FirstOrDefault();
            Numbers = new NumberExpander(rules.NumberForms, _classes, new RuleBasedNumberFormatter(rules.RuleSets));
        }

        /// <summary>
        /// Runs the normalization steps, in order, on natural language text. Not for custom pronunciation.
        /// </summary>
        /// <param name="text">The text to normalize.</param>
        /// <returns>The normalized text.</returns>
        public string ApplySteps(string text)
        {
            List<int> codePoints = CodePoints.From(text);
            foreach (Func<List<int>, List<int>> step in _steps)
            {
                codePoints = step(codePoints);
            }
            return CodePoints.ToText(codePoints);
        }

        private Func<List<int>, List<int>> CompileStep(Step step)
        {
            switch (step.KindCase)
            {
                case Step.KindOneofCase.Compose:
                    return new Composer(step.Compose).Apply;
                case Step.KindOneofCase.Lowercase:
                    return new Lowercaser(step.Lowercase, _classes).Apply;
                case Step.KindOneofCase.CollapseWhitespace:
                    return CompileCollapseWhitespace(CodePointSet.Find(_classes, step.CollapseWhitespace.SpaceClass));
                case Step.KindOneofCase.ReplaceChars:
                    return CompileReplaceChars(step.ReplaceChars);
                case Step.KindOneofCase.ReplaceText:
                    return CompileReplaceText(step.ReplaceText);
                case Step.KindOneofCase.KeepOnly:
                    return CompileKeepOnly(CodePointSet.Find(_classes, step.KeepOnly.CharacterClass));
                case Step.KindOneofCase.None:
                    throw new ArgumentException("A text preprocessing step is empty, or of a kind this version of Thespeon does not know.");
                default:
                    throw new NotSupportedException($"Text preprocessing step {step.KindCase} is not supported by this version of Thespeon.");
            }
        }

        private static Func<List<int>, List<int>> CompileCollapseWhitespace(CodePointSet space)
        {
            return text =>
            {
                List<int> output = new(text.Count);
                bool previousWasSpace = false;
                foreach (int codePoint in text)
                {
                    if (space.Contains(codePoint))
                    {
                        if (!previousWasSpace)
                        {
                            output.Add(' ');
                        }
                        previousWasSpace = true;
                    }
                    else
                    {
                        output.Add(codePoint);
                        previousWasSpace = false;
                    }
                }
                return output;
            };
        }

        private static Func<List<int>, List<int>> CompileKeepOnly(CodePointSet keep)
        {
            int marker = ControlCharacters.AudioSampleRequest;
            return text => text.Where(codePoint => keep.Contains(codePoint) || codePoint == marker).ToList();
        }

        private static Func<List<int>, List<int>> CompileReplaceChars(ReplaceChars replaceChars)
        {
            HashSet<int> characters = new(CodePoints.From(replaceChars.Characters));
            List<int> replacement = CodePoints.From(replaceChars.Replacement);
            return text =>
            {
                List<int> output = new(text.Count);
                foreach (int codePoint in text)
                {
                    if (characters.Contains(codePoint))
                    {
                        output.AddRange(replacement);
                    }
                    else
                    {
                        output.Add(codePoint);
                    }
                }
                return output;
            };
        }

        private Func<List<int>, List<int>> CompileReplaceText(ReplaceText replaceText)
        {
            List<int> target = CodePoints.From(replaceText.Text);
            List<int> replacement = CodePoints.From(replaceText.Replacement);
            if (target.Count == 0)
            {
                throw new ArgumentException("replace_text needs a text to replace.");
            }
            bool wholeWord = replaceText.WholeWord;
            WordSplitter words = Words;
            return text =>
            {
                List<int> output = new(text.Count);
                int i = 0;
                while (i < text.Count)
                {
                    if (Matches(text, i, target) && (!wholeWord || AtWordBoundaries(text, i, target.Count, words)))
                    {
                        output.AddRange(replacement);
                        i += target.Count;
                    }
                    else
                    {
                        output.Add(text[i]);
                        i++;
                    }
                }
                return output;
            };
        }

        private static bool AtWordBoundaries(List<int> text, int position, int length, WordSplitter words)
        {
            bool startsWord = position == 0 || !words.IsWordCharacter(text[position - 1]);
            bool endsWord = position + length == text.Count || !words.IsWordCharacter(text[position + length]);
            return startsWord && endsWord;
        }

        private static bool Matches(List<int> text, int position, List<int> target)
        {
            if (position + target.Count > text.Count)
            {
                return false;
            }
            for (int i = 0; i < target.Count; i++)
            {
                if (text[position + i] != target[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
