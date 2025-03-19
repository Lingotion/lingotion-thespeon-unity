# Lingotion.Thespeon API Documentation

## Class `ActorData`

Only merges languages in the LanguageDataCache (from Language Packs). If an Actor's language has nameinenglish, we copy it if iso639_2 matches (or some other rule).


## Class `ModuleData`


## Class `LanguageData`


## Class `LanguageCode`


## Class `PackImporterEditor`


## Class `ExampleWindow`

> ### Method `void BuildGroupedLanguages()`
> 
> Builds a dictionary keyed by nameinenglish (or "Unknown"), with a list of LanguageData. We will later display only iso639_2 codes.
> 
> 
> ### Method `void CheckRequirementStatus()`
> 
> Gathers all iso639_2 codes needed by actors, compares to what we have in language packs. If any are missing, we store a warning message in 'requirementStatusMessage'. Otherwise we say everything is good.
> 
> 
> ### Method `void DrawRequirementStatus()`
> 
> Displays the requirement status if there's a problem, or success if everything is good. Also provides a selectable label so the user can copy the full message.
> 
> 
> ### Method `string FormatLanguage(LanguageData lang)`
> 
> Show just the minimal fields you want: iso639_2, iso3166_1, customdialect, plus nameinenglish (if present).
> 
> 
> ### Method `void DrawSelectableLabel(string text)`
> 
> Renders a single-line SelectableLabel so the user can copy the text.
> 