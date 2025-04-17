// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Lingotion.Thespeon.Filters
{

    public class NumberToWordsConverter : INumberToWordsConverter
    {
        private const long MAX_SAFE_INTEGER = 9007199254740991;

        // For numbers < 20
        private static readonly string[] LESS_THAN_TWENTY =
        {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
        };

        // For multiples of ten < 100
        private static readonly string[] TENS =
        {
            "zero", "ten", "twenty", "thirty", "forty", "fifty",
            "sixty", "seventy", "eighty", "ninety"
        };

        // For ordinals < 13
        private static readonly Dictionary<string, string> OrdinalLessThanThirteen = new Dictionary<string, string>
        {
            ["zero"] = "zeroth",
            ["one"] = "first",
            ["two"] = "second",
            ["three"] = "third",
            ["four"] = "fourth",
            ["five"] = "fifth",
            ["six"] = "sixth",
            ["seven"] = "seventh",
            ["eight"] = "eighth",
            ["nine"] = "ninth",
            ["ten"] = "tenth",
            ["eleven"] = "eleventh",
            ["twelve"] = "twelfth"
        };

        // Large scale names, up to quintillion
        // (Enough to cover entire "safe" range ~9.007e15)
        private static readonly string[] ScaleNames = 
        {
            "",         // 10^0
            "thousand", // 10^3
            "million",  // 10^6
            "billion",  // 10^9
            "trillion", // 10^12
            "quadrillion", // 10^15
            "quintillion"  // 10^18 - beyond JS safe range, but we include for completeness
        };

        /// <summary>
        /// Replaces numeric substrings with word equivalents, matching your JS logic:
        ///   - Detects optional ordinal suffixes (st|nd|rd|th)
        ///   - Replaces with spelled-out number words
        ///   - If ordinal suffix was present, outputs ordinal words (e.g., "1st" -> "first")
        /// </summary>
        public string ConvertNumbers(string input)
        {

            input = InsertSpaceBetweenNumbersAndLetters(input);
            if (string.IsNullOrWhiteSpace(input))
            {
                return input ?? string.Empty;
            }

            // This pattern captures:
            //   1) Non-digits (prefix)
            //   2) A number (integer or decimal)
            //   3) Optional ordinal suffix (st|nd|rd|th)
            //   4) Non-digits (suffix)
            var pattern = new Regex(@"(\D*)(\d+(\.\d+)?)(st|nd|rd|th)?(\D*)");
            
            return pattern.Replace(input, match =>
            {
                string prefix = match.Groups[1].Value;
                string number = match.Groups[2].Value; // integer or decimal
                bool hasOrdinalSuffix = !string.IsNullOrEmpty(match.Groups[4].Value);
                string suffix = match.Groups[5].Value;

                // Convert the matched number to words
                string converted = ToWords(number, hasOrdinalSuffix);
                return prefix + converted + suffix;
            });
        }

        /// <summary>
        /// Convert a numeric string to its word representation.
        ///   - If asOrdinal = true, the spelled-out words are made ordinal ("first", "second", "third", etc.).
        ///   - Supports integers (including large ones) and decimals (e.g., "3.14" -> "three point one four").
        /// </summary>
        public static string ToWords(string number, bool asOrdinal = false)
        {
            // Validate numeric input
            if (!decimal.TryParse(number, out decimal numDecimal))
            {
                throw new ArgumentException($"Not a finite number: {number}");
            }
            if (!IsSafeNumber(numDecimal))
            {
                throw new ArgumentOutOfRangeException(nameof(number), "Input is not a safe number.");
            }

            // If decimal is effectively an integer
            if (decimal.Truncate(numDecimal) == numDecimal)
            {
                // Just handle as a long for spelling out
                long n = (long)numDecimal;
                string words = ConvertWholeNumberToWords(n);
                return asOrdinal ? MakeOrdinal(words) : words;
            }
            else
            {
                // For decimal numbers, handle the "integer part" + "point" + digit-by-digit readout
                string[] parts = number.Split('.');
                string integerPartStr = parts[0];
                string decimalPartStr = parts[1];

                // Convert the integer part
                long integerPart = long.Parse(integerPartStr);
                string integerWords = ConvertWholeNumberToWords(integerPart);

                // Convert each digit in decimal part
                List<string> decimalWords = new List<string>();
                foreach (char digit in decimalPartStr)
                {
                    if (char.IsDigit(digit))
                    {
                        int d = digit - '0';
                        decimalWords.Add(LESS_THAN_TWENTY[d]);
                    }
                }
                return $"{integerWords} point {string.Join(" ", decimalWords)}";
            }
        }

        /// <summary>
        /// Converts an integer (e.g., 1234567) into comprehensive words (e.g., "one million two hundred thirty-four thousand five hundred sixty-seven").
        /// </summary>
        private static string ConvertWholeNumberToWords(long number)
        {
            if (number == 0)
            {
                return "zero";
            }

            // Handle negatives
            if (number < 0)
            {
                return "minus " + ConvertWholeNumberToWords(Math.Abs(number));
            }

            // Break the number into groups of three digits (e.g., for "1,234,567")
            // Then convert each chunk and add the scale name.
            List<string> words = new List<string>();
            int scaleIndex = 0;

            while (number > 0)
            {
                int chunk = (int)(number % 1000);  // last three digits
                if (chunk > 0)
                {
                    string chunkWords = ConvertHundreds(chunk);
                    string scaleName = ScaleNames[scaleIndex];
                    
                    // If there's a scale name (thousand, million, etc.), append after chunk words
                    if (!string.IsNullOrEmpty(scaleName))
                    {
                        chunkWords += " " + scaleName;
                    }

                    words.Insert(0, chunkWords);
                }
                
                number /= 1000;
                scaleIndex++;
            }

            return string.Join(" ", words).Trim();
        }

        /// <summary>
        /// Convert a number from 0..999 into words.
        /// </summary>
        private static string ConvertHundreds(int number)
        {
            if (number < 20)
            {
                return LESS_THAN_TWENTY[number];
            }
            else if (number < 100)
            {
                return ConvertTens(number);
            }
            else
            {
                int hundreds = number / 100;
                int remainder = number % 100;

                string hundredsPart = LESS_THAN_TWENTY[hundreds] + " hundred";
                if (remainder > 0)
                {
                    // You can include "and" if you prefer ("one hundred and twenty-three")
                    // For US style, it's often omitted: "one hundred twenty-three"
                    return hundredsPart + " " + ConvertTens(remainder);
                }
                else
                {
                    return hundredsPart;
                }
            }
        }

        /// <summary>
        /// Convert 0..99 to words.
        /// </summary>
        private static string ConvertTens(int number)
        {
            if (number < 20)
            {
                return LESS_THAN_TWENTY[number];
            }
            else
            {
                int tensPart = number / 10;
                int onesPart = number % 10;

                string result = TENS[tensPart];
                if (onesPart > 0)
                {
                    result += " " + LESS_THAN_TWENTY[onesPart];
                }
                return result;
            }
        }

        /// <summary>
        /// Converts a spelled-out cardinal word (e.g. "twenty") into its ordinal ("twentieth").
        /// </summary>
        public static string MakeOrdinal(string words)
        {
            // If the spelled-out word is in our known dictionary for < 13
            if (OrdinalLessThanThirteen.ContainsKey(words))
            {
                return OrdinalLessThanThirteen[words];
            }

            // If the spelled-out word ends with 'y'
            if (words.EndsWith("y", StringComparison.OrdinalIgnoreCase))
            {
                return words.Substring(0, words.Length - 1) + "ieth";
            }

            return words + "th";
        }

        /// <summary>
        /// Converts a numeric string to ordinal with a suffix (1 => 1st, 2 => 2nd, 3 => 3rd, etc.).
        /// Unlike MakeOrdinal(), this is for numeric digits directly (e.g. "123" => "123rd").
        /// </summary>
        public static string ToOrdinal(string number)
        {
            if (!long.TryParse(number, out long num))
            {
                throw new ArgumentException($"Not a finite number: {number}");
            }
            if (!IsSafeNumber(num))
            {
                throw new ArgumentOutOfRangeException(nameof(number), "Input is not a safe number.");
            }

            string str = num.ToString();
            long lastTwoDigits = Math.Abs(num % 100);
            char lastChar = str[str.Length - 1];

            string suffix;
            if (lastTwoDigits >= 11 && lastTwoDigits <= 13)
            {
                suffix = "th";
            }
            else
            {
                switch (lastChar)
                {
                    case '1': suffix = "st"; break;
                    case '2': suffix = "nd"; break;
                    case '3': suffix = "rd"; break;
                    default: suffix = "th"; break;
                }
            }

            return str + suffix;
        }

        /// <summary>
        /// Check if the decimal is within JavaScript's "safe" integer/decimal range.
        /// </summary>
        private static bool IsSafeNumber(decimal value)
        {
            decimal absValue = Math.Abs(value);
            return absValue <= MAX_SAFE_INTEGER;
        }

        public string InsertSpaceBetweenNumbersAndLetters(string input)
        {
            // This pattern uses lookbehind and lookahead to find a boundary:
            //  - (?<=[0-9])(?=[A-Za-z]) means "right after a digit, right before a letter"
            //  - (?<=[A-Za-z])(?=[0-9]) means "right after a letter, right before a digit"
            // Whenever the pattern matches, insert a space.
            return Regex.Replace(input, 
                                @"(?<=[0-9])(?=[A-Za-z])|(?<=[A-Za-z])(?=[0-9])", 
                                " ");
        }
    }

}