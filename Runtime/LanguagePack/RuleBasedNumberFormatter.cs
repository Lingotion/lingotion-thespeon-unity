// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lingotion.Thespeon.Language.Proto;

namespace Lingotion.Thespeon.Language
{
    /// <summary>
    /// Spells out numbers with the rule-based number format (RBNF) rule sets of a language pack's text
    /// preprocessing file. The rule syntax is described in text_preprocessing.proto.
    /// </summary>
    public class RuleBasedNumberFormatter
    {
        private const int MaxDepth = 64;

        private readonly Dictionary<string, Rule[]> _ruleSets = new();

        /// <summary>
        /// Compiles and validates the rule sets of a text preprocessing file.
        /// </summary>
        /// <param name="ruleSets">The rule sets to compile.</param>
        /// <exception cref="ArgumentException">Thrown if a rule set is malformed or refers to one that does not exist.</exception>
        public RuleBasedNumberFormatter(IEnumerable<NumberRuleSet> ruleSets)
        {
            foreach (NumberRuleSet ruleSet in ruleSets)
            {
                if (!ruleSet.Name.StartsWith("%"))
                {
                    throw new ArgumentException($"Rule set name '{ruleSet.Name}' must start with '%'.");
                }
                if (_ruleSets.ContainsKey(ruleSet.Name))
                {
                    throw new ArgumentException($"Rule set '{ruleSet.Name}' is defined twice.");
                }
                if (ruleSet.Rules.Count == 0)
                {
                    throw new ArgumentException($"Rule set '{ruleSet.Name}' has no rules.");
                }
                Rule[] rules = ruleSet.Rules.Select(rule => new Rule(rule.BaseValue, rule.Text)).ToArray();
                if (rules[0].BaseValue < 0)
                {
                    throw new ArgumentException($"Rule set '{ruleSet.Name}': negative base values are not supported.");
                }
                for (int i = 1; i < rules.Length; i++)
                {
                    if (rules[i].BaseValue <= rules[i - 1].BaseValue)
                    {
                        throw new ArgumentException($"Rule set '{ruleSet.Name}': rules must be in ascending order of base value ({rules[i - 1].BaseValue} then {rules[i].BaseValue}).");
                    }
                }
                _ruleSets[ruleSet.Name] = rules;
            }
            foreach ((string name, Rule[] rules) in _ruleSets)
            {
                foreach (Rule rule in rules)
                {
                    foreach (Substitution substitution in rule.Substitutions())
                    {
                        if (substitution.RuleSet != null && !_ruleSets.ContainsKey(substitution.RuleSet))
                        {
                            throw new ArgumentException($"Rule set '{name}', rule {rule.BaseValue}: unknown rule set '{substitution.RuleSet}'.");
                        }
                        if (substitution.Kind == SubstitutionKind.Self && substitution.RuleSet == name)
                        {
                            throw new ArgumentException($"Rule set '{name}', rule {rule.BaseValue}: '=' names its own rule set and would never end.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets whether a rule set of the given name exists.
        /// </summary>
        /// <param name="name">The rule set name, including its leading '%'.</param>
        /// <returns>True if the rule set exists.</returns>
        public bool HasRuleSet(string name)
        {
            return _ruleSets.ContainsKey(name);
        }

        /// <summary>
        /// Spells out a non-negative integer with the named rule set.
        /// </summary>
        /// <param name="number">The number to spell out.</param>
        /// <param name="ruleSet">The name of the rule set to use.</param>
        /// <returns>The number in words, as the rule set writes them.</returns>
        /// <exception cref="ArgumentException">Thrown if the number is negative, the rule set is unknown or has no rule for the number, or the rules recurse without end.</exception>
        public string Format(long number, string ruleSet)
        {
            if (!_ruleSets.ContainsKey(ruleSet))
            {
                throw new ArgumentException($"Unknown rule set '{ruleSet}'.");
            }
            StringBuilder output = new();
            Format(number, ruleSet, 0, output);
            return output.ToString();
        }

        private void Format(long number, string ruleSet, int depth, StringBuilder output)
        {
            if (depth > MaxDepth)
            {
                throw new ArgumentException($"Rule set '{ruleSet}' recursed more than {MaxDepth} times formatting {number}.");
            }
            if (number < 0)
            {
                throw new ArgumentException($"Cannot format the negative number {number}.");
            }
            Rule rule = null;
            foreach (Rule candidate in _ruleSets[ruleSet])
            {
                if (candidate.BaseValue > number)
                {
                    break;
                }
                rule = candidate;
            }
            if (rule == null)
            {
                throw new ArgumentException($"Rule set '{ruleSet}' has no rule for {number}.");
            }
            long quotient = number / rule.Divisor;
            long remainder = number % rule.Divisor;
            Render(rule.Tokens, number, quotient, remainder, ruleSet, depth, output);
        }

        private void Render(List<Token> tokens, long number, long quotient, long remainder, string ruleSet, int depth, StringBuilder output)
        {
            foreach (Token token in tokens)
            {
                switch (token)
                {
                    case Literal literal:
                        output.Append(literal.Text);
                        break;
                    case Optional optional:
                        if (remainder != 0)
                        {
                            Render(optional.Tokens, number, quotient, remainder, ruleSet, depth, output);
                        }
                        break;
                    case Substitution substitution:
                        long value = substitution.Kind switch
                        {
                            SubstitutionKind.Quotient => quotient,
                            SubstitutionKind.Remainder => remainder,
                            _ => number,
                        };
                        Format(value, substitution.RuleSet ?? ruleSet, depth + 1, output);
                        break;
                }
            }
        }

        private enum SubstitutionKind
        {
            Quotient,
            Remainder,
            Self,
        }

        private abstract class Token
        {
        }

        private sealed class Literal : Token
        {
            public readonly string Text;

            public Literal(string text)
            {
                Text = text;
            }
        }

        private sealed class Substitution : Token
        {
            public readonly SubstitutionKind Kind;

            public readonly string RuleSet;

            public Substitution(SubstitutionKind kind, string ruleSet)
            {
                Kind = kind;
                RuleSet = ruleSet;
            }
        }

        private sealed class Optional : Token
        {
            public readonly List<Token> Tokens;

            public Optional(List<Token> tokens)
            {
                Tokens = tokens;
            }
        }

        private sealed class Rule
        {
            public readonly long BaseValue;
            public readonly long Divisor;
            public readonly List<Token> Tokens;

            public Rule(long baseValue, string text)
            {
                BaseValue = baseValue;
                Divisor = 1;
                while (Divisor <= baseValue / 10)
                {
                    Divisor *= 10;
                }
                Tokens = Parse(text);
            }

            public IEnumerable<Substitution> Substitutions()
            {
                foreach (Token token in Tokens)
                {
                    if (token is Substitution substitution)
                    {
                        yield return substitution;
                    }
                    else if (token is Optional optional)
                    {
                        foreach (Substitution inner in optional.Tokens.OfType<Substitution>())
                        {
                            yield return inner;
                        }
                    }
                }
            }

            private static List<Token> Parse(string text)
            {
                List<Token> tokens = new();
                List<Token> section = null;
                StringBuilder literal = new();

                void Flush()
                {
                    if (literal.Length > 0)
                    {
                        (section ?? tokens).Add(new Literal(literal.ToString()));
                        literal.Clear();
                    }
                }

                int i = 0;
                while (i < text.Length)
                {
                    char c = text[i];
                    if (c == '<' || c == '>' || c == '=')
                    {
                        int end = text.IndexOf(c, i + 1);
                        if (end < 0)
                        {
                            throw new ArgumentException($"Rule '{text}': unclosed '{c}' substitution.");
                        }
                        string name = end == i + 1 ? null : text.Substring(i + 1, end - i - 1);
                        if (name != null && !name.StartsWith("%"))
                        {
                            throw new ArgumentException($"Rule '{text}': '{name}' is not a rule set name.");
                        }
                        if (c == '=' && name == null)
                        {
                            throw new ArgumentException($"Rule '{text}': '==' would format the number with its own rule and never end.");
                        }
                        Flush();
                        SubstitutionKind kind = c == '<' ? SubstitutionKind.Quotient : c == '>' ? SubstitutionKind.Remainder : SubstitutionKind.Self;
                        (section ?? tokens).Add(new Substitution(kind, name));
                        i = end + 1;
                    }
                    else if (c == '[')
                    {
                        if (section != null)
                        {
                            throw new ArgumentException($"Rule '{text}': optional sections cannot be nested.");
                        }
                        Flush();
                        section = new List<Token>();
                        i++;
                    }
                    else if (c == ']')
                    {
                        if (section == null)
                        {
                            throw new ArgumentException($"Rule '{text}': ']' without '['.");
                        }
                        Flush();
                        tokens.Add(new Optional(section));
                        section = null;
                        i++;
                    }
                    else
                    {
                        literal.Append(c);
                        i++;
                    }
                }
                if (section != null)
                {
                    throw new ArgumentException($"Rule '{text}': unclosed '['.");
                }
                Flush();
                return tokens;
            }
        }
    }
}
