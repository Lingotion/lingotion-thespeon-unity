// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

// Assets/Converters/NumberToWordsConverterFactory.cs
using System;

namespace Lingotion.Thespeon.Filters
{
    public static class LingotionConverterFactory
    {
        public static INumberToWordsConverter GetNumberConverter(string languageCode)
        {
            switch (languageCode.ToLower())
            {
                case "eng":
                    return new NumberToWordsConverter();
                case "swe":
                    return new NumberToWordsSwedish();
                default:
                    throw new NotSupportedException($"Language '{languageCode}' is not supported.");
            }
        }


        public static IAbbrevationToWordsConverter GetAbbreviationConverter(string languageCode)
        {
            switch (languageCode.ToLower())
            {
                case "eng":
                    return new AbbreviationConverter();
                case "swe":
                    return new AbbreviationConverterSwedish();
                default:
                    throw new NotSupportedException($"Language '{languageCode}' is not supported.");
            }
        }

        
    }
}