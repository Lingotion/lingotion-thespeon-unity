// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;
using Lingotion.Thespeon.Core;

namespace Lingotion.Thespeon.LanguagePack
{
    /// <summary>
    /// Converts numbers in a string to their Swedish word representation in phonemes.
    /// </summary>
    public class NumberToWordsSwedish : NumberConverter
    {
        private static readonly string[] LessThanTwenty =
        {
            "nˈɔl",
            "ˈɛt",
            "tvˈoː",
            "trˈeː",
            "fˈyːra",
            "fˈɛm",
            "sˈɛks",
            "sxˈʉ",
            "ˈɔtːa",
            "nˈiːuː",
            "tˈiːuː",
            "ˈɛlva",
            "tˈɔlv",
            "trˈɛtːuːn",
            "fjˈuːtuːn",
            "fˈɛmtuːn",
            "sˈɛkstuːn",
            "sxˈʉtːuːn",
            "atˈuːn",
            "nˈɪtːuːn"
        };

        private static readonly string[] ThenthsLessThanHundred =
        {
            "nˈɔl",
            "tˈiːuː",
            "ɕˈʉɡuː",
            "trˈɛtːiːˌuː",
            "fˈytiːˌuː",
            "fˈɛmtiːˌuː",
            "sˈɛkstiːˌuː",
            "sxˈɵtːiːˌuː",
            "ˈɔtːiːˌuː",
            "nˈɪtːiːˌuː"
        };

        private static readonly (long Value, string Word)[] LargeNumbers =
        {
            (100,                 "hˈɵndra"),
            (1000,                "tˈʉsən"),
            (1000000,             "mˈɪljuːn"),
            (1000000000,          "mˈɪljard"),
            (1000000000000,       "bˈɪljuːn"),
            (1000000000000000,    "bˈɪljard")
        };

        /// <summary>
        /// Replaces all integers in the string with their spelled-out Swedish form.
        /// Example: "Vi har 1 katt och 1000 hundar." => "Vi har en katt och ett tusen hundar."
        /// </summary>
        public override string ConvertNumber(string number)
        {
            LingotionLogger.Debug($"Swedish! Converting number: {number}");

            if (string.IsNullOrWhiteSpace(number))
            {
                return number ?? string.Empty;
            }
            return ToWords(number);
        }

        /// <summary>
        /// Converts an integer string to Swedish words in phonemes, e.g., "1000" => "ett tusen", "19" => "nitton".
        /// Throws if not "safe" or not parseable.
        /// </summary>
        private static string ToWords(string number)
        {
            if (!long.TryParse(number, out long num))
            {
                throw new ArgumentException($"Not a finite integer: {number}. Swedish number conversion only supports integers at the moment.");
            }
            if (!IsSafeNumber(num))
            {
                throw new ArgumentOutOfRangeException(nameof(number), "Input is not a safe integer.");
            }

            if (num < 20)
                return LessThanTwenty[num];

            if (num < 100)
            {
                long tens = num / 10;
                long remainder = num % 10;
                string result = ThenthsLessThanHundred[tens];
                if (remainder > 0)
                    result += " " + LessThanTwenty[remainder];
                return result;
            }

            for (int i = LargeNumbers.Length - 1; i >= 0; i--)
            {
                long threshold = LargeNumbers[i].Value;
                string word = LargeNumbers[i].Word;

                if (num >= threshold)
                {
                    long basePart = num / threshold;
                    long remainder = num % threshold;

                    string baseWord = ToWords(basePart.ToString());

                    if (basePart == 1 && threshold >= 1000)
                    {
                        if (threshold == 1000)
                            baseWord = "ˈɛt";
                        else
                            baseWord = "ˈɛn";
                    }
                    string suffix = "";
                    if (basePart > 1 && threshold > 1000)
                    {
                        suffix = "ər";
                    }

                    string result = baseWord + " " + word + suffix;

                    if (remainder > 0)
                        result += " " + ToWords(remainder.ToString());

                    return result;
                }
            }

            return num.ToString();
        }

        private static bool IsSafeNumber(long value)
        {
            return Math.Abs(value) <= MAX_SAFE_INTEGER;
        }

    }

}