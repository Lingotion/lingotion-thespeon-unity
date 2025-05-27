# Lingotion.Thespeon.Filters API Documentation

> ## Class `AbbreviationConverter`
>
> This class is used to convert abbreviations in English text to their full forms.
> ### Methods
>
> #### `string ConvertAbbreviationsOriginal(string text)`
>
> Process input text to convert abbrevations to text based on lookup table (naive but useful), one-parameter version if you DON'T need the feedback log.
> #### `(string, string) ConvertAbbreviations(string text)`
>
> Tuple-returning version: returns (convertedText, changesLog). We record each occurred abbreviation + its translation in a dictionary.

> ## Class `AbbreviationConverterSwedish`
>
> This class is used to convert abbreviations in Swedish text to their full forms.
> ### Methods
>
> #### `string ConvertAbbreviationsOriginal(string text)`
>
> Process input text to convert abbreviations to text based on lookup table (naive but useful), one-parameter version if you DON'T need the feedback log.
> #### `(string, string) ConvertAbbreviations(string text)`
>
> Tuple-returning version: returns (convertedText, changesLog). We record each occurred abbreviation + its translation in a dictionary.

> ## Class `ConverterFilterService`
> ### Methods
>
> #### `string NormalizeApostrophes(string input)`
>
> Replaces various apostrophe-like characters in the input text with the standard apostrophe (U+0027).
>
> **Parameters:**
>
> - `input`: The input string potentially containing ambiguous apostrophe characters.
>
> **Returns:** A string with all ambiguous apostrophe characters replaced by the standard apostrophe.

> ## Class `NumberToWordsConverter`
>
> Converts numbers in a string to their English word representation.
> ### Methods
>
> #### `string ConvertNumbers(string input)`
>
> Replaces numeric substrings with word equivalents, matching your JS logic: - Detects optional ordinal suffixes (st|nd|rd|th) - Replaces with spelled-out number words - If ordinal suffix was present, outputs ordinal words (e.g., "1st" -> "first")

> ## Class `NumberToWordsSwedish`
>
> Converts numbers in a string to their Swedish word representation.
> ### Methods
>
> #### `string ConvertNumbers(string input)`
>
> Replaces all integers in the string with their spelled-out Swedish form. Example: "Vi har 1 katt och 1000 hundar." => "Vi har en katt och ett tusen hundar."