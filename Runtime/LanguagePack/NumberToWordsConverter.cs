// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;

namespace Lingotion.Thespeon.LanguagePack
{
    /// <summary>
    /// Converts numbers in a string to their English word representation in phonemes.
    /// </summary>
    public class NumberToWordsConverter : NumberConverter
    {
        private static readonly string[] LessThanTwenty =
        {
            "zˈiəɹoʊ", "wˈʌn", "tˈuː", "θɹˈiː", "fˈoːɹ", "fˈaɪv", "sˈɪks", "sˈɛvən",
            "ˈeɪt", "nˈaɪn", "tˈɛn", "ᵻlˈɛvən", "twˈɛlv", "θˈɜːtiːn", "fˈoːɹtiːn",
            "fˈɪftiːn", "sˈɪkstiːn", "sˈɛvəntˌiːn", "ˈeɪtiːn", "nˈaɪntiːn"
        };
        private static readonly string[] Tens =
        {
            "zˈiəɹoʊ", "tˈɛn", "twˈɛnti", "θˈɜːɾi", "fˈɔːɹɾi", "fˈɪfti",
            "sˈɪksti", "sˈɛvɛnti", "ˈeɪɾi", "nˈaɪnti"
        };
        private static readonly List<string> OrdinalLessThanThirteen = new()
        {
            "zˈiəɹoʊθ", "fˈɜːst", "sˈɛkənd", "θˈɜːd", "fˈoːɹθ", "fˈɪfθ", "sˈɪksθ", "sˈɛvənθ", "ˈeɪtθ", "nˈaɪnθ", "tˈɛnθ", "ᵻlˈɛvənθ", "twˈɛlvθ"
        };
        private static readonly string[] ScaleNames =
        {
            "",             // for 10^0 (nothing spoken)
            "θˈaʊzənd",     // thousand
            "mˈɪliən",      // million
            "bˈɪliən",      // billion
            "tɹˈɪliən",     // trillion
            "kwɑːdɹˈɪliən", // quadrillion
            "kwɪntˈɪliən"   // quintillion
        };


        /// <summary>
        /// Replaces numeric substrings with word equivalents directly in phonemes.:
        ///   - Detects optional ordinal suffixes (st|nd|rd|th)
        ///   - Replaces with spelled-out number words
        ///   - If ordinal suffix was present, outputs ordinal words (e.g., "1st" -> "first")
        /// </summary>
        public override string ConvertNumber(string input)
        {
            LingotionLogger.Debug($"Converting number: {input}");
            string ordinalSuffix = "";
            string numberPart = input;

            if (input.Length >= 3 &&
                (input.EndsWith("st") || input.EndsWith("nd") || input.EndsWith("rd") || input.EndsWith("th")))
            {
                ordinalSuffix = input[^2..];
                numberPart = input[..^2];
            }

            return ToWords(numberPart, asOrdinal: !string.IsNullOrEmpty(ordinalSuffix));
        }

        /// <summary>
        /// Convert a numeric string to its word representation in phonemes.
        ///   - If asOrdinal = true, the spelled-out words are made ordinal ("first", "second", "third", etc.).
        ///   - Supports integers (including large ones) and decimals (e.g., "3.14" -> "three point one four").
        /// </summary>
        private static string ToWords(string number, bool asOrdinal = false)
        {
            if (!decimal.TryParse(number, out decimal numDecimal))
            {
                throw new ArgumentException($"Not a finite number: {number}");
            }
            if (!IsSafeNumber(numDecimal))
            {
                throw new ArgumentOutOfRangeException(nameof(number), "Input is not a safe number.");
            }

            if (decimal.Truncate(numDecimal) == numDecimal)
            {
                int n = (int) numDecimal;
                string asPhoneme = ConvertWholeNumberToWords(n);
                return asOrdinal ? MakeOrdinal(asPhoneme, n ) : asPhoneme;
            }
            else
            {
                // [DevComment] For decimal numbers, handle the "integer part" + "point" + digit-by-digit readout
                string[] parts = number.Split('.');
                string integerPartStr = parts[0];
                string decimalPartStr = parts[1];

                long integerPart = long.Parse(integerPartStr);
                string integerWords = ConvertWholeNumberToWords(integerPart);

                List<string> decimalWords = new();
                foreach (char digit in decimalPartStr)
                {
                    if (char.IsDigit(digit))
                    {
                        int d = digit - '0';
                        decimalWords.Add(LessThanTwenty[d]);
                    }
                }
                return $"{integerWords} pˈɔɪnt {string.Join(" ", decimalWords)}";
            }
        }

        /// <summary>
        /// Converts an integer (e.g., 1234567) into text (e.g., "one million two hundred thirty-four thousand five hundred and sixty-seven"), but in phonemes.
        /// </summary>
        private static string ConvertWholeNumberToWords(long number)
        {
            if (number == 0)
            {
                return "zˈiəɹoʊ";
            }

            // [DevComment] Currently we dont handle - signs in text. so this is unused.
            if (number < 0)
            {
                return "minus " + ConvertWholeNumberToWords(Math.Abs(number));
            }

            // [DevComment] Break the number into groups of three digits (e.g., for "1,234,567")
            List<string> words = new();
            int scaleIndex = 0;

            while (number > 0)
            {
                int chunk = (int)(number % 1000);
                if (chunk > 0)
                {
                    string chunkWords = ConvertHundreds(chunk);
                    string scaleName = ScaleNames[scaleIndex];
                    
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
                return LessThanTwenty[number];
            }
            else if (number < 100)
            {
                return ConvertTens(number);
            }
            else
            {
                int hundreds = number / 100;
                int remainder = number % 100;

                string hundredsPart = LessThanTwenty[hundreds] + " hˈʌndɹəd";
                if (remainder > 0)
                {
                    //  [DevComment] We can omit "and" if we prefer ("one hundred twenty-three")
                    return hundredsPart + " ˈænd " + ConvertTens(remainder);
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
                return LessThanTwenty[number];
            }
            else
            {
                int tensPart = number / 10;
                int onesPart = number % 10;

                string result = Tens[tensPart];
                if (onesPart > 0)
                {
                    result += " " + LessThanTwenty[onesPart];
                }
                return result;
            }
        }

        /// <summary>
        /// Converts a spelled-out cardinal word (e.g. "twenty") into its ordinal ("twentieth"), all in phonemes.
        /// </summary>
        private static string MakeOrdinal(string ordinal, int nbr)
        {
            int lastTwoDigits = nbr % 100;
            LingotionLogger.Debug($"NumberConverter Making ordinal of {nbr}: {0 < lastTwoDigits && lastTwoDigits < 13 || nbr == 0}");

            if (0 < lastTwoDigits && lastTwoDigits < 13 || nbr == 0)
            {
                string[] parts = ordinal.Split(' ');
                parts[^1] = OrdinalLessThanThirteen[lastTwoDigits];
                return string.Join(" ", parts);
            }

            if (ordinal.EndsWith("ti"))
            {
                // [DevComment] e.g., "twenty" -> "twentieth"
                return ordinal + "əθ";
            }
            int lastDigit = nbr % 10;
            if (lastDigit != 0)
            {
                string[] parts2 = ordinal.Split(' ');
                parts2[^1] = OrdinalLessThanThirteen[lastDigit];
                return string.Join(" ", parts2);
            }

            return ordinal + "θ";
        }

        /// <summary>
        /// Check if the decimal is within JavaScript's "safe" integer/decimal range.
        /// </summary>
        private static bool IsSafeNumber(decimal value)
        {
            decimal absValue = Math.Abs(value);
            return absValue <= MAX_SAFE_INTEGER;
        }
    }

}