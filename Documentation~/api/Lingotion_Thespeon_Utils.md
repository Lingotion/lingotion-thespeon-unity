# Lingotion.Thespeon.Utils API Documentation

## Class `ActorPack`

> ### Method `List<ActorPackModule> GetModules()`
> 
> Retrieves the list of modules in the actor pack
> 
> 
> ### Method `List<PhonemizerModule> GetLanguageModules(string moduleName = null)`
> 
> Retrieves the list of LanguageModules in the actor pack or optionally in a specific module.
> 
> 
> ### Method `List<Actor> GetActors(string moduleName=null)`
> 
> Retrieves a list of all available actors in the ActorPack or optionally all actors in a specific module.
> 
> 
> ### Method `List<Language> GetLanguages(Actor actor = null)`
> 
> Retrieves a list of languages for the given actor, or all languages in the ActorPack if no actor is specified.
> 
> 
> ### Method `List<Emotion> GetEmotions(Actor actor = null, Language language = null)`
> 
> Retrieves all emotions based on Actor and/or Language. If neither is specified, returns all emotions in the ActorPack. If only Actor is specified, returns all emotions associated with them. If only Language is specified, returns all emotions available for that language. If both are specified, returns the emotions available for that specific Actor-Language combination.
> 
> 
> ### Method `int GetLanguageKey(Language language)`
> 
> Given a Language object, return associated integer language key from LanguageOptions for the closes language match. Logs an error if the provided Language is null.
> **Parameters:**
> 
> - `language`: The Language object from which to retrieve the language key.
> 
> **Returns:** An integer representing the language key.
> 
> 
> 
> ### Method `int GetEmotionKey(string emotionSetName)`
> 
> Given an Emotion setname, return its associated integer emotion key from EmotionOptions Logs an error if the provided Emotion setname is null.
> **Parameters:**
> 
> - `emotionSetName`: The setname of the Emotion object from which to retrieve the emotion key.
> 
> **Returns:** An integer representing the emotion key.
> 
> 

## Class `BuildInfo`


## Class `ActorPackModule`


## Class `ModelOptions`

Retrieves the a dictionary of language codes and their associated language keys.


## Class `RecordingDataInfo`


## Class `ActorData`


## Class `Actor`


## Class `LanguageData`


## Class `Language`


## Class `LanguageCode`


## Class `CountryCode`


## Class `SubRegionCode`


## Class `EmotionData`


## Class `Emotion`


## Class `ActorOptions`


## Class `LanguageOptions`


## Class `EmotionOptions`


## Class `PhonemesTable`


## Class `WordSplitter`


## Class `PhonemizerSetup`


## Class `PhonemizerModule`


## Class `Config`


## Class `FileEntry`


## Class `LanguagePack`


## Class `Module`


## Class `Vocabularies`


## Class `Language`


## Class `ModuleFiles`


## Class `FileItem`


## Class `LanguagePackService`


## Class `Language`

> ### Constructor `Language()`
> 
> Initializes a new instance of the Language class.
> 
> 
> ### Constructor `Language(Language lang)`
> 
> Initializes a new instance of the Language class by copying properties from another instance.
> **Parameters:**
> 
> - `other`: The Language instance to copy from.
> 
> 
> 
> ### Method `string ToDisplay()`
> 
> Returns a display-friendly string representation of the language.
> **Returns:** A string summarizing the language and optional dialect.
> 
> 
> 
> ### Method `bool MatchLanguage(Language inputLanguage, Language candidateLanguage)`
> 
> Determines whether every non-empty property in the inputLanguage matches the corresponding property in candidateLanguage.
> **Parameters:**
> 
> - `obj`: The object to compare with this instance.
> - `inputLanguage`: The reference language to match against.
> - `candidateLanguage`: The candidate language to check.
> 
> **Returns:** True if all non-null properties in inputLanguage match those in candidateLanguage.
> 
> 

## Class `UserModelInput`

> ### Constructor `UserModelInput()`
> 
> Initializes a new instance of the UserModelInput class. Used duing deserialization.
> 
> 
> ### Constructor `UserModelInput(string actorName, List<UserSegment> textSegments)`
> 
> Initializes a new instance of the UserModelInput class with specified actor name and text segments. Will initialize moduleName defaultLanguage to the first module and language from the actor's available modules.
> **Parameters:**
> 
> - `actorName`: The username of the actor associated with this input.
> - `textSegments`: A list of user segments defining the input text data.
> 
> 
> 
> ### Method `NestedSummary MakeNestedSummary()`
> 
> Generates a NestedSummary object summarizing the effective languages and emotions used in the input segments.
> **Returns:** A NestedSummary containing grouped statistics on languages and emotions.
> 
> 

## Class `UserSegment`

A user-facing segment with only text-based fields.

> ### Constructor `UserSegment()`
> 
> Initializes a new instance of the UserSegment class.
> 
> 
> ### Constructor `UserSegment(UserSegment other)`
> 
> Initializes a new instance of the UserSegment class by copying properties from another instance. This is a shallow copy.
> **Parameters:**
> 
> - `other`: The UserSegment instance to copy from.
> 
> 
> 
> ### Method `bool EqualsIgnoringText(UserSegment other)`
> 
> Compares this segment's annotation with another, ignoring differences in text content.
> **Parameters:**
> 
> - `text`: The text content of the segment. Cannot be empty.
> - `language`: The optional language of the segment.
> - `emotion`: The optional emotion of this segment.
> - `style`: Not supported yet.
> - `isCustomPhonemized`: Specifies whether the text in the segment is pre-phonemized.
> - `heteronymDescription`: Not supported yet.
> - `extraData`: A dictionary capturing additional unknown properties. Will be ignored during inference.
> - `module`: An optional module that provides validation rules for emotions and languages.
> - `other`: The UserSegment instance to compare against.
> 
> **Returns:** True if all properties except text are equal; otherwise, false.
> 
> 

## Class `Version`

Gets or sets the value of a property dynamically by name.


## Class `NestedSummary`


## Class `LanguageSummary`


## Class `EmotionStat`
