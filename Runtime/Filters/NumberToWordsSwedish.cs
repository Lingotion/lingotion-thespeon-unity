// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Text.RegularExpressions;


namespace Lingotion.Thespeon.Filters 
{
    public class NumberToWordsSwedish : INumberToWordsConverter
    {
        private const long MAX_SAFE_INTEGER = 9007199254740991;

        private static readonly string[] LESS_THAN_TWENTY =
        {
            "noll",    // 0
            "ett",     // 1
            "två",     // 2
            "tre",     // 3
            "fyra",    // 4
            "fem",     // 5
            "sex",     // 6
            "sju",     // 7
            "åtta",    // 8
            "nio",     // 9
            "tio",     // 10
            "elva",    // 11
            "tolv",    // 12
            "tretton", // 13
            "fjorton", // 14
            "femton",  // 15
            "sexton",  // 16
            "sjutton", // 17
            "arton",   // 18
            "nitton"   // 19
        };

        private static readonly string[] TENTHS_LESS_THAN_HUNDRED =
        {
            "noll",   // index=0 (unused for tens)
            "tio",    // 10
            "tjugo",  // 20
            "trettio", 
            "fyrtio", 
            "femtio", 
            "sextio", 
            "sjuttio", 
            "åttio", 
            "nittio"
        };

        // (Value, Word)
        // We'll handle logic for "1" specially below.
        private static readonly (long Value, string Word)[] LARGE_NUMBERS =
        {
            (100,                 "hundra"),
            (1000,                "tusen"),
            (1000000,             "miljon"),
            (1000000000,          "miljard"),
            (1000000000000,       "biljon"),
            (1000000000000000,    "biljard")
            // Extend further if needed
        };

        /// <summary>
        /// Replaces all integers in the string with their spelled-out Swedish form.
        /// Example: "Vi har 1 katt och 1000 hundar." => "Vi har ett katt och ett tusen hundar."
        /// </summary>
        public string ConvertNumbers(string input)
        {

            input = InsertSpaceBetweenNumbersAndLetters(input);
            if (string.IsNullOrWhiteSpace(input))
            {
                return input ?? string.Empty;
            }

            // Captures [non-digits] [digits] [non-digits]
            var pattern = new Regex(@"(\D*)(\d+)(\D*)");
            return pattern.Replace(input, match =>
            {
                string prefix = match.Groups[1].Value;
                string digits = match.Groups[2].Value;
                string suffix = match.Groups[3].Value;

                string converted = ToWords(digits);
                return prefix + converted + suffix;
            });
        }

        /// <summary>
        /// Converts an integer string to Swedish words, e.g., "1000" => "ett tusen", "19" => "nitton".
        /// Throws if not "safe" or not parseable.
        /// </summary>
        public static string ToWords(string number)
        {
            if (!long.TryParse(number, out long num))
            {
                throw new ArgumentException($"Not a finite integer: {number}");
            }

            if (!IsSafeNumber(num))
            {
                throw new ArgumentOutOfRangeException(nameof(number), "Input is not a safe integer.");
            }

            // 0..19 => direct index in LESS_THAN_TWENTY
            if (num < 20)
                return LESS_THAN_TWENTY[num];

            // 20..99 => tens plus possible remainder
            if (num < 100)
            {
                long tens = num / 10;
                long remainder = num % 10;
                string result = TENTHS_LESS_THAN_HUNDRED[tens];
                if (remainder > 0)
                    result += " " + LESS_THAN_TWENTY[remainder];
                return result;
            }

            // 100 and above => check from largest to smallest threshold
            for (int i = LARGE_NUMBERS.Length - 1; i >= 0; i--)
            {
                long threshold = LARGE_NUMBERS[i].Value; // e.g. 1000, 1000000...
                string word = LARGE_NUMBERS[i].Word;     // e.g. "tusen", "miljon"...

                if (num >= threshold)
                {
                    long basePart = num / threshold;       // e.g., 1234/1000=1
                    long remainder = num % threshold;     // e.g., 1234%1000=234

                    // Convert the base part to words
                    string baseWord = ToWords(basePart.ToString());

                    // Fix for base == 1, threshold >= 1000:
                    // Instead of just "1 tusen" or empty, we produce "ett tusen", "en miljon", etc.
                    // (Adjust exactly as desired for "tusen" vs. "ett tusen").
                    if (basePart == 1 && threshold >= 1000)
                    {
                        if (threshold == 1000)
                            baseWord = "ett";   // e.g. "ett tusen"
                        else
                            baseWord = "en";    // e.g. "en miljon", "en miljard", "en biljon", ...
                    }

                    string result = baseWord + " " + word;

                    if (remainder > 0)
                        result += " " + ToWords(remainder.ToString());

                    return result;
                }
            }

            // Fallback if none matched (should not happen for safe numbers)
            return num.ToString();
        }

        private static bool IsSafeNumber(long value)
        {
            return Math.Abs(value) <= MAX_SAFE_INTEGER;
        }

        public string InsertSpaceBetweenNumbersAndLetters(string input)
        {
            // This pattern uses lookbehind and lookahead to find a boundary:
            //  - (?<=[0-9])(?=[A-Za-z]) means "right after a digit, right before a letter"
            //  - (?<=[A-Za-z])(?=[0-9]) means "right after a letter, right before a digit"
            // Whenever the pattern matches, insert a space.
            return Regex.Replace(input, 
                                @"(?<=[\p{L}])(?=[\p{N}])|(?<=[\p{N}])(?=[\p{L}])", 
                                " ");
        }
    }

}