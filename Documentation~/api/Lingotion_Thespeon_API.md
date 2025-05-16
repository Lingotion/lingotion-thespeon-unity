# Lingotion.Thespeon.API API Documentation

> ## Class `ActorTags`
>
> Represents the tags associated with an actor.
> ### Constructors
>
> #### `ActorTags()`
>
> Initializes a new instance of the ActorTags class.
> #### `ActorTags(string quality)`
>
> Initializes a new instance of the ActorTags class with the specified quality.
>
> **Parameters:**
>
> - `quality`: The quality of the model.
> ### Methods
>
> #### `string ToString()`
>
> Returns a string representation of the ActorTags object.
> #### `bool Equals(object obj)`
>
> Compares this ActorTags object with another object for equality.
> #### `int GetHashCode()`
>
> Returns the hash code for this ActorTags object.
>
> **Returns:** A hash code for the current ActorTags object.

> ## Class `Language`
>
> Represents a language with various properties such as ISO codes, glottocode, and custom dialect.
> ### Properties
>
> #### `string iso639_2`
>
> The [ISO 639-2](https://en.wikipedia.org/wiki/List_of_ISO_639-2_codes) code for the language. This is a required property.
> #### `string? iso639_3`
>
> The [ISO 639-3](https://en.wikipedia.org/wiki/List_of_ISO_639-3_codes) code for the language for more specific identification. This property is currently unused.
> #### `string? glottoCode`
>
> The [Glottocode](https://glottolog.org/glottolog/language) for the language, which is a unique high resolution identifier for languages. This property is currently unused.
> #### `string? iso3166_1`
>
> The [ISO 3166-1](https://en.wikipedia.org/wiki/ISO_3166-1#Codes) code for the country associated with the language. This property is optional and specifies country based dialects.
> #### `string? iso3166_2`
>
> The [ISO 3166-2](https://en.wikipedia.org/wiki/ISO_3166-2#Current_codes) code for the region associated with the language. This property is optional and specifies region based dialects.
> #### `string? customDialect`
>
> A custom dialect name separate from other standards. This property is currently unused.
> ### Constructors
>
> #### `Language()`
>
> Initializes a new instance of the Language class.
> #### `Language(Language lang)`
>
> Initializes a new instance of the Language class by copying properties from another instance.
>
> **Parameters:**
>
> - `other`: The Language instance to copy from.
>
> **Exceptions:**
>
> - `ArgumentNullException`: Thrown when the provided Language instance is null or lacks an iso639-2 code.
> ### Methods
>
> #### `string ToString()`
>
> Returns a string representation of the Language object.
>
> **Returns:** A formatted string containing all language properties.
> #### `string ToDisplay()`
>
> Returns a display-friendly string representation of the language.
>
> **Returns:** A string summarizing the language and optional dialect.
> #### `bool Equals(object obj)`
>
> Determines whether the specified object is equal to the current Language instance.
>
> **Parameters:**
>
> - `obj`: The object to compare with this instance.
>
> **Returns:** True if the specified object properties are equal to this instance's; otherwise, false.
> #### `int GetHashCode()`
>
> Returns a hash code for this instance.
>
> **Returns:** A hash code based on the properties of this instance.
> #### `bool MatchLanguage(Language inputLanguage, Language candidateLanguage)`
>
> Determines whether every non-empty property in the inputLanguage matches the corresponding property in candidateLanguage.
>
> **Parameters:**
>
> - `inputLanguage`: The reference language to match against.
> - `candidateLanguage`: The candidate language to check.
>
> **Returns:** True if all non-null properties in inputLanguage match those in candidateLanguage.

> ## Class `PackageConfig`
> ### Constructors
>
> #### `PackageConfig()`
>
> Initializes a new empty instance of the PackageConfig class.
> #### `PackageConfig(PackageConfig config)`
>
> Copy constructor for PackageConfig.
>
> **Parameters:**
>
> - `config`: The PackageConfig instance to copy from.
> ### Methods
>
> #### `PackageConfig SetConfig(PackageConfig overrideConfig)`
>
> Sets the configuration values from another PackageConfig instance by overwriting all non-null values in overrideConfig and returns the new instance. A validation of config values with eventual revision also takes place.
>
> **Parameters:**
>
> - `overrideConfig`: The PackageConfig instance to override values from.
>
> **Returns:** A new PackageConfig instance with the overridden values.
> #### `string ToString()`
>
> Converts the PackageConfig instance to a JSON string.

> ## Class `ThespeonAPI`
>
> A collection of static API methods for the Lingotion Thespeon package. This class provides methods for registering and unregistering ActorPacks, preloading and unloading ActorPack modules and creating a synthetization request.
> ### Methods
>
> #### `void SetGlobalConfig(PackageConfig config)`
>
> Sets the global config variables. These are applied whenever no local config is supplied for a synthesis. Non-null properties in the argument will permanently override the default config.
>
> **Parameters:**
>
> - `config`: The config object to set. Any non-null properties will override the current global config.
> #### `PackageConfig GetConfig(string synthID=null)`
>
> Gets a copy of current config. If a synthid is provided it will return the local config valid for that synthid. Otherwise it will return the global config.
>
> **Parameters:**
>
> - `synthID`: The synthid to get the local config for. If null or omitted, the global config is returned.
> #### `List<(string, ActorTags)> GetActorsAvailabeOnDisk()`
>
> Gets a list of all available actor name - tag pairs in the actor packs that are on disk, registered or not.
>
> **Returns:** A list of pairs actorUsername strings, ActorTags.
> #### `void RegisterActorPacks(string actorUsername)`
>
> Registers ActorPacks modules and returns it, note that it registers all actorpacks given an actorUsername This does not load the module into memory.
>
> **Parameters:**
>
> - `actorUsername`: The username of the actor you wish to load.
>
> **Returns:** The registered ActorPack object
> #### `void UnregisterActorPack(string actorUsername)`
>
> Unregisters an ActorPack. This also unloads the module from memory if it is loaded.
>
> **Parameters:**
>
> - `actorUsername`: The username of the actor you wish to unregister.
> #### `List<ActorPackModule> GetModulesWithActor(string actorUsername)`
>
> Fetches all registered ActorPackModules containing the specific username.
>
> **Parameters:**
>
> - `actorUsername`: The username of the actor you wish to select.
> #### `Dictionary<string, List<ActorPack>> GetRegisteredActorPacks()`
>
> Gets a dictionary of all registered actor name - ActorPack lists.
> #### `(ActorPackModule, ActorPack) GetActorPackModule(UserModelInput annotatedInput)`
>
> Retrieves an `ActorPackModule` from the specified actor's packs based on the provided module name. If the actor pack or specified module is not found, an `ArgumentException` is thrown.
>
> **Parameters:**
>
> - `annotatedInput`: A `UserModelInput` instance containing the actor's username and the desired module name.
>
> **Returns:** The first matching `ActorPackModule` for the specified actor and module name.
>
> **Exceptions:**
>
> - `ArgumentException`: Thrown if the actor username does not exist in the registered packs, or if no module matching the requested name is found for the specified actor.
> #### `void PreloadActorPackModule(string actorUsername, ActorTags tags,  List<Language> languages=null)`
>
> Preloads an ActorPack module and the language modules necessary for full functionality. the parameter languages can be optionally used to filter which language modules to preload for restricted functionality.
>
> **Parameters:**
>
> - `actorPackModuleName`: Which Actor Pack Module to preload.
> - `languages`: Optional. If provided only Language Packs for given languages are preloaded. Otherwise all Language Packs necessary for full functionality are preloaded.
> #### `void UnloadActorPackModule(string actorPackModuleName=null)`
>
> Unloads the preloaded ActorPackModule and its associated Language Packs.
>
> **Parameters:**
>
> - `actorPackModuleName`: Optional name of specific ActorPackModule to unload. Otherwise unloads all
> #### `LingotionSynthRequest Synthesize(UserModelInput annotatedInput, Action<float[]> dataStreamCallback = null, PackageConfig config=null)`
>
> Creates a LingotionSynthRequest object with a unique ID for the synthetization and an estimated quality of the synthesized data. Warnings and metadata can be empty or contain information about the synthetization.
>
> **Parameters:**
>
> - `annotatedInput`: Model input. See Annotated Input Format Guide for details.
> - `dataStreamCallback`: An Action callback to receive synthesized data as a stream.
> - `config`: A synthetization instance specific config override.
>
> **Returns:** A LingotionSynthRequest object containing a unique Synth Request ID, feedback object and warnings/errors raised as well as a copy of the processed input and callback to be used by this synthesis.
> #### `void SetBackend(BackendType backendType)`
>
> Sets the backend type for all modules loaded in the future.
>
> **Parameters:**
>
> - `backendType`: 
> #### `List<string> GetLoadedActorPackModules()`
>
> returns a list of actor names that are currently loaded in memory.
> #### `Dictionary<string, List<ModuleTagInfo>> GetActorsAndTagsOverview()`
>
> Returns a dictionary keyed by actorUsername. Each entry is a list of ModuleTagInfo objects describing (packName, moduleName, moduleId, tags).
> #### `string GetActorPackModuleName(string actorUsername, ActorTags tags)`
>
> Find the modulename in packMappings which has the the actorUsername and tags.
>
> **Parameters:**
>
> - `actorUsername`: 
> - `tags`: 

> ## Class `ThespeonEngine`
>
> ThespeonEngine is the main game component for interfacing with the Thespeon API from your scene. It is responsible for loading and managing actors and modules, and for running inference jobs.
> ### Methods
>
> #### `UserModelInput CreateModelInput(string actorName, List<UserSegment> textSegments)`
>
> Creates a new UserModelInput object with the specified actor name and text segments.
>
> **Parameters:**
>
> - `actorName`: The name of the actor to use for inference.
> - `textSegments`: A list of UserSegment objects representing the text to synthesize.
>
> **Returns:** A new UserModelInput object.
> #### `void SetCustomSkipIndices(List<int>[] customSkipIndices)`
>
> Sets the custom skip indices for the decoder.
>
> **Parameters:**
>
> - `customSkipIndices`: A list of lists of integers representing the custom skip indices for each layer of the decoder.
> #### `LingotionSynthRequest Synthesize(UserModelInput input, Action<float[]> audioStreamCallback = null, PackageConfig config = null)`
>
> Starts a Thespeon inference job with the specified input. An Action callback can be otionally provided to receive audio data as it is synthesized. A PackageConfig object can also be optionally provided to override the global configuration.
>
> **Parameters:**
>
> - `input`: The UserModelInput object containing the actor name and text segments.
> - `audioStreamCallback`: An Action callback to receive audio data as it is synthesized.
> - `config`: A PackageConfig object to override the global configuration.
>
> **Returns:** A LingotionSynthRequest object representing the synthesis request.

> ## Class `UserModelInput`
>
> A class representing the users input data for a model in the Thespeon API. It includes properties for module name, actor username, default language, default emotion, segments, speed and loudness. The class is deserializable from JSON using Newtonsoft.Json as follows: UserModelInput modelInput = JsonConvert.DeserializeObject<UserModelInput>(myJsonString);
> ### Properties
>
> #### `string moduleName`
>
> The name of the module to be used for inference. This is a required property but can be selected with actorUsername and ActorTags using the class constructors.
> #### `string actorUsername`
>
> The username of the actor associated with this input. This is a required property and must match a valid option (case sensitive).
> #### `Language? defaultLanguage`
>
> The default language to be used across segments. This is an optional, nullable property.
> #### `string? defaultEmotion`
>
> The default emotion to be used across segments. This is an optional, nullable property.
> #### `List<UserSegment> segments`
>
> A list of UserSegment objects representing the text segments with local parameters to be synthesized. This is a required property.
> #### `List<double> speed`
>
> A list of speed multipliers for the segments. Default value is 1 meaning no change whereas 2 means double speed. This is an optional property of arbitrary length.
> #### `List<double> loudness`
>
> A list of loudness multipliers for the segments. Default value is 1 meaning no change whereas 2 means double loudness. This is an optional property of arbitrary length.
> ### Constructors
>
> #### `UserModelInput()`
>
> Initializes a new instance of the UserModelInput class. Used duing deserialization.
> #### `UserModelInput(string actorName, ActorTags desiredTags,  List<UserSegment> textSegments)`
>
> Initializes a new instance of the UserModelInput class with specified actor name, tags, and text segments.
>
> **Parameters:**
>
> - `actorName`: The username of the actor associated with this input.
> - `actorModuleTag`: The tags of the actor module to be used, comes in a key-value pair dictionary.
> - `textSegments`: A list of user segments defining the input text data.
> #### `UserModelInput(string actorName, List<UserSegment> textSegments)`
>
> Initializes a new instance of the UserModelInput class with specified actor name and text segments.
>
> **Parameters:**
>
> - `actorName`: The username of the actor associated with this input.
> - `textSegments`: A list of user segments defining the input text data.
> #### `UserModelInput(UserModelInput other)`
>
> Copy constructor for UserModelInput. This is a deep copy except for reference copy of extraData.
>
> **Parameters:**
>
> - `other`: The UserModelInput instance to copy from.
>
> **Exceptions:**
>
> - `ArgumentNullException`: Thrown when the provided instance is null.
> - `InvalidOperationException`: Thrown when the provided instance has an empty module name, actor username or segments list.
> ### Methods
>
> #### `(List<string> errors, List<string> warnings) ValidateAndWarn()`
>
> Validates the UserModelInput object and returns a tuple of lists containing errors and warnings. Will also print warnings to the console and throw an exception if validation fails.
>
> **Returns:** A tuple containing a list of error messages and a list of warning messages.
> #### `NestedSummary MakeNestedSummary()`
>
> Generates a NestedSummary object summarizing the effective languages and emotions used in the input segments.
>
> **Returns:** A NestedSummary containing grouped statistics on languages and emotions.
> #### `string ToString()`
>
> Returns a JSON-formatted string representation of the UserModelInput instance.
>
> **Returns:** A JSON string representing the UserModelInput object.

> ## Class `UserSegment`
>
> A class representing a text segment of UserModelInput. Is annotated with a single language, emotion and/or flagged as custom phonemized.
> ### Properties
>
> #### `string text`
>
> The text content of the segment. This is a required property and cannot be empty.
> #### `Language? languageObj`
>
> The language of the segment. This is an optional, nullable property.
> #### `string? emotion`
>
> The emotion of the segment. This is an optional, nullable property.
> #### `string? style`
>
> The style of the segment. This is an optional, nullable property and is currently unused.
> #### `bool? isCustomPhonemized`
>
> Specifies whether the entire text in the segment is pre-phonemized. This is an optional, nullable property.
> #### `Dictionary<string, string> heteronymDescription`
>
> A dictionary capturing additional heteronym descriptions. This is an optional, nullable property and is currently unused.
> ### Constructors
>
> #### `UserSegment()`
>
> Initializes a new instance of the UserSegment class.
> #### `UserSegment(UserSegment other)`
>
> Initializes a new instance of the UserSegment class by copying properties from another instance. This is a deep copy except for reference copy of extraData.
>
> **Parameters:**
>
> - `other`: The UserSegment instance to copy from.
>
> **Exceptions:**
>
> - `ArgumentNullException`: Thrown when the provided instance is null or has null or empty text.
> ### Methods
>
> #### `(List<string> errors, List<string> warnings) ValidateUserSegment(ActorPackModule module = null)`
>
> Validates the UserSegment instance, checking for errors and warnings based on its properties.
>
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
>
> **Returns:** A tuple containing a list of errors and a list of warnings.
>
> **Exceptions:**
>
> - `InvalidOperationException`: Thrown if the 'text' property is empty or null.
> #### `bool EqualsIgnoringText(UserSegment other)`
>
> Compares this segment's annotation with another, ignoring differences in text content.
>
> **Parameters:**
>
> - `other`: The UserSegment instance to compare against.
>
> **Returns:** True if all properties except text are equal; otherwise, false.