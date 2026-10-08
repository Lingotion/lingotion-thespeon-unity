// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lingotion.Thespeon.Language.Proto;

namespace Lingotion.Thespeon.Language
{
    /// <summary>
    /// Finds the numbers in a text by a language pack's number forms, and speaks them out in phonemes with
    /// its rule sets.
    /// </summary>
    public class NumberExpander
    {
        private readonly Form[] _forms;
        private readonly RuleBasedNumberFormatter _formatter;

        internal NumberExpander(IEnumerable<NumberForm> forms, IReadOnlyDictionary<string, CodePointSet> classes, RuleBasedNumberFormatter formatter)
        {
            _formatter = formatter;
            _forms = forms.Select(form => new Form(form, classes, formatter)).ToArray();
        }

        /// <summary>
        /// Splits a text into alternating parts of text and numbers. At each position the forms are tried in
        /// order, and the first one that matches there makes a number part.
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <returns>The parts in order, each with whether it is a number.</returns>
        public List<(string Text, bool IsNumber)> Partition(string text)
        {
            List<int> codePoints = CodePoints.From(text);
            List<(string, bool)> parts = new();
            int last = 0;
            int position = 0;
            while (position < codePoints.Count)
            {
                int length = MatchLengthAt(codePoints, position);
                if (length == 0)
                {
                    position++;
                    continue;
                }
                if (position > last)
                {
                    parts.Add((CodePoints.ToText(codePoints, last, position - last), false));
                }
                parts.Add((CodePoints.ToText(codePoints, position, length), true));
                last = position = position + length;
            }
            if (last < codePoints.Count)
            {
                parts.Add((CodePoints.ToText(codePoints, last, codePoints.Count - last), false));
            }
            return parts;
        }

        /// <summary>
        /// Speaks out a number, written in one of the number forms, in phonemes.
        /// </summary>
        /// <param name="number">The number as written, e.g. "21st".</param>
        /// <returns>The number in phonemes.</returns>
        /// <exception cref="ArgumentException">Thrown if no number form matches the whole of <paramref name="number"/>, or its rule sets cannot format it.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the number is too large to format.</exception>
        public string Expand(string number)
        {
            List<int> codePoints = CodePoints.From(number);
            foreach (Form form in _forms)
            {
                List<(int Start, int Length)> matched = form.Match(codePoints, 0);
                if (matched != null && matched.Sum(element => element.Length) == codePoints.Count)
                {
                    return Render(form, codePoints, matched);
                }
            }
            throw new ArgumentException($"'{number}' is not written in any of the number forms.");
        }

        private int MatchLengthAt(List<int> codePoints, int position)
        {
            foreach (Form form in _forms)
            {
                if (!form.CanStartWith(codePoints[position]))
                {
                    continue;
                }
                List<(int Start, int Length)> matched = form.Match(codePoints, position);
                int length = matched?.Sum(element => element.Length) ?? 0;
                if (length > 0)
                {
                    return length;
                }
            }
            return 0;
        }

        private string Render(Form form, List<int> codePoints, List<(int Start, int Length)> matched)
        {
            StringBuilder output = new();
            foreach (object part in form.Parts)
            {
                if (part is string literal)
                {
                    output.Append(literal);
                    continue;
                }
                TemplateSubstitution substitution = (TemplateSubstitution)part;
                (int start, int length) = matched[substitution.Element - 1];
                if (length == 0)
                {
                    throw new ArgumentException($"Number form '{form.Template}': element {substitution.Element} matched nothing.");
                }
                CodePointSet digits = form.Elements[substitution.Element - 1].Class;
                if (substitution.EachDigit)
                {
                    List<string> spoken = new(length);
                    for (int i = start; i < start + length; i++)
                    {
                        spoken.Add(_formatter.Format(DigitValue(digits, codePoints[i]), substitution.RuleSet));
                    }
                    output.Append(string.Join(" ", spoken));
                }
                else
                {
                    long value = 0;
                    try
                    {
                        for (int i = start; i < start + length; i++)
                        {
                            value = checked(value * 10 + DigitValue(digits, codePoints[i]));
                        }
                    }
                    catch (OverflowException)
                    {
                        throw new ArgumentOutOfRangeException(nameof(codePoints), $"The number {CodePoints.ToText(codePoints, start, length)} is too large to speak out.");
                    }
                    output.Append(_formatter.Format(value, substitution.RuleSet));
                }
            }
            return output.ToString();
        }

        private static int DigitValue(CodePointSet digits, int codePoint)
        {
            int rangeStart = digits.RangeStart(codePoint);
            if (rangeStart < 0)
            {
                throw new ArgumentException($"U+{codePoint:X4} is not in the digit class '{digits.Name}'.");
            }
            return (codePoint - rangeStart) % 10;
        }

        private enum ElementKind
        {
            Digits,
            Literal,
            OneOf,
        }

        private sealed class Element
        {
            public ElementKind Kind;
            public CodePointSet Class;
            public List<List<int>> Texts;
            public bool Optional;
        }

        private sealed class TemplateSubstitution
        {
            public readonly int Element;
            public readonly string RuleSet;
            public readonly bool EachDigit;

            public TemplateSubstitution(int element, string ruleSet, bool eachDigit)
            {
                Element = element;
                RuleSet = ruleSet;
                EachDigit = eachDigit;
            }
        }

        private sealed class Form
        {
            public readonly string Template;
            public readonly List<Element> Elements = new();
            public readonly List<object> Parts = new();

            public Form(NumberForm form, IReadOnlyDictionary<string, CodePointSet> classes, RuleBasedNumberFormatter formatter)
            {
                Template = form.Template;
                foreach (FormElement element in form.Elements)
                {
                    Elements.Add(CompileElement(element, classes));
                }
                if (Elements.Count == 0)
                {
                    throw new ArgumentException($"Number form '{Template}' has no elements.");
                }
                ParseTemplate(formatter);
            }

            /// <summary>
            /// Whether a match could start with codePoint: false only when no match of the form can.
            /// </summary>
            public bool CanStartWith(int codePoint)
            {
                foreach (Element element in Elements)
                {
                    bool startsElement = element.Kind == ElementKind.Digits
                        ? element.Class.Contains(codePoint)
                        : element.Texts.Any(text => text[0] == codePoint);
                    if (startsElement)
                    {
                        return true;
                    }
                    if (!element.Optional)
                    {
                        return false;
                    }
                }
                return false;
            }

            /// <summary>
            /// The start and length each element matched, or null if the form does not match at position.
            /// </summary>
            public List<(int Start, int Length)> Match(List<int> text, int position)
            {
                List<(int, int)> matched = new(Elements.Count);
                foreach (Element element in Elements)
                {
                    int length = 0;
                    switch (element.Kind)
                    {
                        case ElementKind.Digits:
                            while (position + length < text.Count && element.Class.Contains(text[position + length]))
                            {
                                length++;
                            }
                            break;
                        default:
                            foreach (List<int> option in element.Texts)
                            {
                                if (StartsWith(text, position, option))
                                {
                                    length = option.Count;
                                    break;
                                }
                            }
                            break;
                    }
                    if (length == 0 && !element.Optional)
                    {
                        return null;
                    }
                    matched.Add((position, length));
                    position += length;
                }
                return matched;
            }

            private static bool StartsWith(List<int> text, int position, List<int> prefix)
            {
                if (position + prefix.Count > text.Count)
                {
                    return false;
                }
                for (int i = 0; i < prefix.Count; i++)
                {
                    if (text[position + i] != prefix[i])
                    {
                        return false;
                    }
                }
                return true;
            }

            private Element CompileElement(FormElement element, IReadOnlyDictionary<string, CodePointSet> classes)
            {
                Element compiled = new() { Optional = element.Optional };
                switch (element.KindCase)
                {
                    case FormElement.KindOneofCase.Digits:
                        compiled.Kind = ElementKind.Digits;
                        compiled.Class = CodePointSet.Find(classes, element.Digits);
                        break;
                    case FormElement.KindOneofCase.Literal:
                        if (string.IsNullOrEmpty(element.Literal))
                        {
                            throw new ArgumentException($"Number form '{Template}': empty literal.");
                        }
                        compiled.Kind = ElementKind.Literal;
                        compiled.Texts = new List<List<int>> { CodePoints.From(element.Literal) };
                        break;
                    case FormElement.KindOneofCase.OneOf:
                        if (element.OneOf.Options.Count == 0 || element.OneOf.Options.Any(string.IsNullOrEmpty))
                        {
                            throw new ArgumentException($"Number form '{Template}': one_of needs non-empty options.");
                        }
                        compiled.Kind = ElementKind.OneOf;
                        compiled.Texts = element.OneOf.Options.Select(CodePoints.From).ToList();
                        break;
                    default:
                        throw new ArgumentException($"Number form '{Template}': empty element.");
                }
                return compiled;
            }

            private void ParseTemplate(RuleBasedNumberFormatter formatter)
            {
                StringBuilder literal = new();
                int i = 0;
                while (i < Template.Length)
                {
                    char c = Template[i];
                    if (c == '}')
                    {
                        throw new ArgumentException($"Template '{Template}': '}}' without '{{'.");
                    }
                    if (c != '{')
                    {
                        literal.Append(c);
                        i++;
                        continue;
                    }
                    int end = Template.IndexOf('}', i + 1);
                    if (end < 0)
                    {
                        throw new ArgumentException($"Template '{Template}': unclosed '{{'.");
                    }
                    string[] fields = Template.Substring(i + 1, end - i - 1).Split(':');
                    bool validFields = (fields.Length == 2 || fields.Length == 3 && fields[2] == "each")
                        && fields[0].Length > 0 && fields[0].All(digit => digit >= '0' && digit <= '9');
                    if (!validFields)
                    {
                        throw new ArgumentException($"Template '{Template}': malformed substitution '{Template.Substring(i, end - i + 1)}'.");
                    }
                    int element = int.Parse(fields[0], System.Globalization.CultureInfo.InvariantCulture);
                    if (element < 1 || element > Elements.Count)
                    {
                        throw new ArgumentException($"Template '{Template}': element {element} does not exist; the form has {Elements.Count}.");
                    }
                    if (Elements[element - 1].Kind != ElementKind.Digits)
                    {
                        throw new ArgumentException($"Template '{Template}': element {element} is not a digit run.");
                    }
                    if (!formatter.HasRuleSet(fields[1]))
                    {
                        throw new ArgumentException($"Template '{Template}': unknown rule set '{fields[1]}'.");
                    }
                    if (literal.Length > 0)
                    {
                        Parts.Add(literal.ToString());
                        literal.Clear();
                    }
                    Parts.Add(new TemplateSubstitution(element, fields[1], fields.Length == 3));
                    i = end + 1;
                }
                if (literal.Length > 0)
                {
                    Parts.Add(literal.ToString());
                }
            }
        }
    }
}
