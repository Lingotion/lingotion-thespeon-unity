# Lingotion.Thespeon.Filters API Documentation

## Class `AbbreviationConverterSwedish`

> ### Method `string ConvertAbbreviationsOriginal(string text)`
> 
> Simple one-parameter version if you DON'T need the log.
> 
> 
> ### Method `string ReplaceAllowedToStart(string text, Dictionary<string,string> changesMap = null)`
> 
> Tuple-returning version: returns (convertedText, changesLog). Instead of counting how many times each abbreviation was replaced, we record each abbreviation + its translation in a dictionary.
> 

## Class `NumberToWordsConverter`

> ### Method `string ConvertNumbers(string input)`
> 
> Replaces numeric substrings with word equivalents, matching your JS logic: - Detects optional ordinal suffixes (st|nd|rd|th) - Replaces with spelled-out number words - If ordinal suffix was present, outputs ordinal words (e.g., "1st" -> "first")
> 
> 
> ### Method `string InsertSpaceBetweenNumbersAndLetters(string input)`
> 
> Check if the decimal is within JavaScript's "safe" integer/decimal range.
> 

## Class `AbbreviationConverter`


## Class `AbbrevEntry`

> ### Method `string ConvertAbbreviationsOriginal(string text)`
> 
> Original 1-parameter version: just returns the converted text, no log.
> 

## Class `NumberToWordsSwedish`

> ### Method `string ConvertNumbers(string input)`
> 
> Replaces all integers in the string with their spelled-out Swedish form. Example: "Vi har 1 katt och 1000 hundar." => "Vi har ett katt och ett tusen hundar."
> 
> 
> ### Method `string InsertSpaceBetweenNumbersAndLetters(string input)`
> 
> Converts an integer string to Swedish words, e.g., "1000" => "ett tusen", "19" => "nitton". Throws if not "safe" or not parseable.
> 

## Class `ConverterFilterService`
