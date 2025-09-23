# Lingotion.Thespeon.Core API Documentation

## Enum `DataPacketStatus`

Enum denoting if the inference succeeded or not.
### Members

#### `OK`
#### `FAILED`

## Enum `Emotion`

Enumeration representing various emotions that can be associated with a segment. Also contains a None as a special null-like value.
### Members

#### `None`

No emotion. Special null-like value.
#### `Ecstasy`

Delighted, giddy. Abundance of energy. Message: This is better than I imagined. Example: Feeling happiness beyond imagination, as if life is perfect at this moment.
#### `Admiration`

Connected, proud. Glowing sensation. Message: I want to support the person or thing. Example: Meeting your hero and wanting to express deep appreciation.
#### `Terror`

Alarmed, petrified. Hard to breathe. Message: There is big danger. Example: Feeling hunted and fearing for your life.
#### `Amazement`

Inspired, WOWed. Heart stopping sensation. Message: Something is totally unexpected. Example: Discovering a lost historical artifact in an abandoned building.
#### `Grief`

Heartbroken, distraught. Hard to get up. Message: Love is lost. Example: Losing a loved one in an accident.
#### `Loathing`

Disturbed, horrified. Bileous and vehement sensation. Message: Fundamental values are violated. Example: Seeing someone exploit others for personal gain.
#### `Rage`

Overwhelmed, furious. Pounding heart, seeing red. Message: I am blocked from something vital. Example: Being falsely accused and not believed by authorities.
#### `Vigilance`

Intense, focused. Highly focused sensation. Message: Something big is coming. Example: Watching over your child climbing a tree, ready to catch them if they fall.
#### `Joy`

Excited, pleased. Sense of energy and possibility. Message: Life is going well. Example: Feeling genuinely happy and optimistic in conversation.
#### `Trust`

Accepting, safe. Warm sensation. Message: This is safe. Example: Trusting someone to be loyal and supportive.
#### `Fear`

Stressed, scared. Agitated sensation. Message: Something I care about is at risk. Example: Realizing you forgot to prepare for a major presentation.
#### `Surprise`

Shocked, unexpected. Heart pounding. Message: Something new happened. Example: Walking into a surprise party.
#### `Sadness`

Bummed, loss. Heavy sensation. Message: Love is going away. Example: Feeling blue and unmotivated.
#### `Disgust`

Distrust, rejecting. Bitter and unwanted sensation. Message: Rules are violated. Example: Seeing someone put a cockroach in their food to avoid paying.
#### `Anger`

Mad, fierce. Strong and heated sensation. Message: Something is in the way. Example: Finding your car blocked by someone who left their car unattended.
#### `Anticipation`

Curious, considering. Alert and exploring. Message: Change is happening. Example: Waiting eagerly for a long-awaited promise to be fulfilled.
#### `Serenity`

Calm, peaceful. Relaxed, open-hearted. Message: Something essential or pure is happening. Example: Enjoying peaceful time with loved ones without stress.
#### `Acceptance`

Open, welcoming. Peaceful sensation. Message: We are in this together. Example: Welcoming a new person into your friend group.
#### `Apprehension`

Worried, anxious. Cannot relax. Message: There could be a problem. Example: Worrying about the outcome of an unexpected meeting.
#### `Distraction`

Scattered, uncertain. Unfocused sensation. Message: I don't know what to prioritize. Example: Struggling to focus during a conversation.
#### `Pensiveness`

Blue, unhappy. Slow and disconnected. Message: Love is distant. Example: Feeling uninterested in suggested activities.
#### `Boredom`

Tired, uninterested. Drained, low energy. Message: The potential for this situation is not being met. Example: Finding nothing enjoyable to do.
#### `Annoyance`

Frustrated, prickly. Slightly agitated. Message: Something is unresolved. Example: Being irritated by repetitive behavior.
#### `Interest`

Open, looking. Mild sense of curiosity. Message: Something useful might come. Example: Becoming curious when hearing unexpected news.
#### `Emotionless`

Detached, apathetic. No sensation or feeling at all. Message: This does not affect me. Example: Feeling nothing during a conversation about irrelevant topics.
#### `Contempt`

Distaste, scorn. Angry and sad at the same time. Message: This is beneath me. Example: Feeling disdain toward someone's dishonest behavior.
#### `Remorse`

Guilt, regret, shame. Disgusted and sad at the same time. Message: I regret my actions. Example: Wishing you could undo a hurtful action.
#### `Disapproval`

Dislike, displeasure. Sad and surprised. Message: This violates my values. Example: Rejecting a statement that contradicts your beliefs.
#### `Awe`

Astonishment, wonder. Surprise with a hint of fear. Message: This is overwhelming. Example: Being speechless when meeting your idol.
#### `Submission`

Obedience, compliance. Fearful but trusting. Message: I must follow this authority. Example: Obeying a trusted figure's orders without question.
#### `Love`

Cherish, treasure. Joy with trust. Message: I want to be with this person. Example: Feeling deep connection and joy with someone.
#### `Optimism`

Cheerfulness, hopeful. Joyful anticipation. Message: Things will work out. Example: Seeing the positive side of any situation.
#### `Aggressiveness`

Pushy, self-assertive. Driven by anger. Message: I must remove obstacles. Example: Forcing your viewpoint aggressively.

## Class `InferenceConfig`

Configuration settings for the inference engine.

## Class `LingotionLogger`

Static class for logging messages with different verbosity levels.
### Properties

#### `VerbosityLevel CurrentLevel`

Current verbosity level for logging. Can be manually set for global logging control outside of Inference and Preload calls. Will be overridden by the InferenceConfig on inference or preload calls.

## Class `ModelInput<ModelInputType, InputSegmentType>`

Base class for model inputs, providing common properties and methods for all model inputs.
### Constructors

#### `ModelInput(ModelInput<ModelInputType, InputSegmentType> other)`

Deep copy constructor for ModelInput.

**Parameters:**

- `other`: The ModelInput instance to copy from.

**Exceptions:**

- `System.ArgumentNullException`: Thrown if the provided ModelInput instance is null.
#### `ModelInput(List<InputSegmentType> segments, string actorName, ModuleType moduleType = ModuleType.None, Emotion defaultEmotion = Emotion.None, string defaultLanguage = null, string defaultDialect = null)`

Constructor for ModelInput. Initializes a new instance of ModelInput with the specified actor name, module type, default emotion, and default language.

**Parameters:**

- `actorName`: The name of the actor.
- `segments`: A list of ModelInputSegment instances representing the segments of the input.
- `defaultLanguage`: The default language to be used.
- `defaultEmotion`: The default emotion to be used.
- `moduleType`: The type of the module.

**Exceptions:**

- `System.ArgumentException`: Thrown if the actor name is null or empty, or if the segments list is null or empty.

## Class `ModelInputSegment`

Base class for model input segments, providing common properties and methods for all model input segments.
### Constructors

#### `ModelInputSegment(ModelInputSegment other)`

Deep copy constructor for ModelInputSegment.

**Parameters:**

- `other`: The ModelInputSegment instance to copy from.

**Exceptions:**

- `System.ArgumentNullException`: Thrown if the provided ModelInputSegment instance is null.
#### `ModelInputSegment(string text, string language = null, string dialect = null, Emotion emotion = Emotion.None, bool isCustomPronounced = false)`

Constructor for ModelInputSegment. Initializes a new instance of ModelInputSegment with the specified text, emotion, language, and custom pronunciation flag.

**Parameters:**

- `text`: The text of the segment.
- `language`: The ISO-639 language code of the segment. Optional, can be null.
- `dialect`: The ISO-3166 dialect code of the segment. Optional, can be null.
- `emotion`: The emotion associated with the segment.
- `isCustomPronounced`: Indicates whether the segment is custom pronounced.

**Exceptions:**

- `System.ArgumentException`: Thrown if the text is null or empty.
### Methods

#### `string ToJson()`

Returns a string representation of the ModelInputSegment in JSON format after filtering out any null or None elements and isCustomPronounced if set to false.

**Returns:** A JSON string representing the ModelInputSegment.

## Struct `ModelRuntimeBinding`

Represents a runtime binding holding a worker and its model.

## Class `Module`

Class describing the common parameters of a module.
### Methods

#### `Dictionary<string, ModelRuntimeBinding> CreateRuntimeBindings(HashSet<string> md5s, BackendType preferredBackedType)`

Creates runtime bindings for the module's models, pairing them with their MD5s.

**Parameters:**

- `moduleInfo`: The module entry containing the ID, JSON path, and version.
- `md5s`: MD5 strings of already existing bindings.
- `preferredBackedType`: The preferred backend type for the models.

**Returns:** A dictionary mapping MD5 strings to their corresponding model runtime bindings.

**Exceptions:**

- `ArgumentNullException`: Thrown when the module ID or JSON path is null.
- `NotImplementedException`: Thrown if the method is not implemented in the derived class.
#### `IEnumerator CreateRuntimeBindingsCoroutine(HashSet<string> md5s, BackendType preferredBackendType, Action<Dictionary<string, ModelRuntimeBinding>> onComplete)`

Creates runtime bindings for the module's models, pairing them with their MD5s and yielding between each binding creation.

**Parameters:**

- `md5s`: MD5 strings of already existing bindings.
- `preferredBackendType`: The preferred backend type for the models.
- `onComplete`: Callback to invoke when all bindings are created.

**Exceptions:**

- `NotImplementedException`: Thrown if the method is not implemented in the derived class.
#### `bool IsIncludedIn(HashSet<string> md5s)`

Checks if the module is fully included in the provided set of MD5s.

**Parameters:**

- `md5s`: A set of MD5 strings of already existing module bindings.

**Returns:** True if the module is fully contained in the set of MD5s, false otherwise.

**Exceptions:**

- `NotImplementedException`: Thrown if the method is not implemented in the derived class.
#### `HashSet<string> GetAllFileMD5s()`

Gets all MD5s of the files in this module.

**Returns:** A set of MD5 strings representing all files in the module.

**Exceptions:**

- `NotImplementedException`: Thrown if the method is not implemented in the derived class.
#### `string GetInternalModelID(string internalName)`

Gets the file MD5 of a given internal name.

**Parameters:**

- `internalName`: The internal name of the model.

**Returns:** The md5 of the model with the provided internal name.

**Exceptions:**

- `KeyNotFoundException`: Thrown if the internal name does not exist in the internal model mappings.

## Struct `ModuleEntry`

A simple representation of a module with its properties.
### Constructors

#### `ModuleEntry(string id, string path)`

Initializes a new instance of the ModuleEntry struct.

**Parameters:**

- `id`: The ID of the module.
- `path`: The path to the module's JSON file.
### Methods

#### `bool IsEmpty()`

Checks if the module entry is empty.

**Returns:** True if the ModuleID or JsonPath is empty, otherwise false.

## Struct `ModuleFile`

Represents a file associated with a module, including its path, MD5 hash, and type.

## Class `ModuleLanguage`

Class representing the language selection for a module.

## Enum `ModuleType`

Enum denoting the different actor module types.
### Members

#### `None`
#### `XS`
#### `S`
#### `M`
#### `L`
#### `XL`

## Class `NumberConverter`

Abstract class for converting numbers to a specific format. This class is intended to be extended for specific number conversion implementations.

## Class `PackManifestHandler`

Singleton that handles parsing and distributing information found in the pack manifest file.
### Properties

#### `PackManifestHandler Instance`

Singleton reference.
### Methods

#### `void UpdateMappings()`

Forces the handler to re-parse the manifest and signal its update.
#### `List<ModuleLanguage> GetAllLanguageModuleLanguages()`

Fetches all languages in the parsed manifest.

**Returns:** A list of all languages found.
#### `List<string> GetAllActors()`

Fetches all actor names in the parsed manifest.

**Returns:** A list of all actors found.
#### `List<ModuleType> GetAllModuleTypesForActor(string actor)`

Fetches all unique available module types for a given actor. Assumes there is only one module type per module.

**Parameters:**

- `actor`: The name of the actor to fetch module types for.
#### `Dictionary<string, ModuleLanguage> GetAllLanguagesForActorAndModuleType(string actorName, ModuleType type)`

Fetches all languages available for a given actor and module type.

**Parameters:**

- `actorName`: The name of the actor to fetch languages for.
- `type`: The module type to filter languages by.
#### `Dictionary<string, ModuleLanguage> GetAllDialectsInModuleLanguage(string actorName, ModuleType type, string iso639_2)`

Fetches all languages available for a given actor and module type.

**Parameters:**

- `actorName`: The name of the actor to fetch languages for.
- `type`: The module type to filter languages by.
#### `List<ModuleLanguage> GetAllSupportedLanguages(string actorName, ModuleType type)`

Fetches all supported languages for a given actor and module type that have an imported language pack.

**Parameters:**

- `actorName`: The name of the actor to fetch languages for.
- `type`: The module type to fetch languages for.

**Returns:** A list of ModuleLanguage objects representing the supported languages.
#### `Dictionary<string, string> GetAllSupportedLanguageCodes(string actorName, ModuleType type)`

Fetches all supported language codes for a given actor and module type.

**Parameters:**

- `actorName`: The name of the actor to fetch languages for.
- `type`: The module type to fetch languages for.

**Returns:** A Dictionary mapping the English name of the language to the ISO639-2 language code.
#### `IEnumerable<string> GetAllLanguageCodes()`

Fetches the language codes of all languages

**Returns:** An IEnumerator containing all installed language codes.
#### `ModuleEntry GetActorPackModuleEntry(string actorName, ModuleType type)`

Finds a specific actor module.

**Parameters:**

- `actorName`: Target actor name.
- `type`: Target module type.

**Returns:** A module entry of the corresponding actor pack.
#### `ModuleEntry GetLanguagePackModuleEntry(string moduleName)`

Finds a specific language module.

**Parameters:**

- `moduleName`: Target module name.

**Returns:** A module entry of the corresponding language pack.
#### `List<ModuleLanguage> GetAccentsForActorAndLanguage(string actorName, string iso639_2)`

Fetches the information about a language spoken by an actor.

**Parameters:**

- `actorName`: Target actor name.
- `iso639_2`: Language of which to find parameters for.

**Returns:** A list of ModuleLanguages for that specific actor and language.
#### `List<string> GetAllActorPackNames()`

Fetches all available actor pack names.

**Returns:** List of all actor pack names.
#### `string GetPackDirectory(string packName)`

Returns the location of a pack

**Parameters:**

- `packName`: The specific pack to find

**Returns:** A string containing the unity-relative path to the pack
#### `List<string> GetAllLanguagePackNames()`

Fetches all available language pack names.

**Returns:** List of all language pack names.
#### `List<string> GetAllPackNames()`

Fetches all available pack names.

**Returns:** List of all pack names.
#### `List<string> GetAllModuleInfoInActorPack(string packName)`

Summarizes all module info inside an actor pack.

**Parameters:**

- `packName`: The specific pack to find.

**Returns:** A list of strings summarizing the modules inside the actor pack.
#### `List<string> GetAllModuleInfoInLanguagePack(string packName)`

Summarizes all module info inside an language pack.

**Parameters:**

- `packName`: The specific pack to find.

**Returns:** A list of strings summarizing the modules inside the language pack.
#### `List<string> GetMissingLanguagePacks()`

Fetches all missing language packs that are required by the actor packs. This is useful for identifying which language packs need to be installed for the actor packs to function correctly.
#### `IReadOnlyDictionary<string, List<string>> GetAllLanguagesPerModule()`

Fetches all installed language modules and their languages.

**Returns:** A dictionary of module name to language names.
#### `Dictionary<string, (string packName, Version version)> GetAllLanguagesAndTheirPacks()`

Scans the manifest for all installed languages and returns a map of the language code to its pack information.

**Returns:** A dictionary mapping a language's ISO 639-2 code to a tuple containing the pack name and version.

## Struct `PacketMetadata`

Metadata for Thespeon data packets. This includes its origin session ID, and which character name and module type was used, and any eventual requested audio indices.
### Properties

#### `string sessionID`

The session ID associated with the session the packet originates from.
#### `DataPacketStatus status`

Status symbol indicating if the synthesis succeeded or not.
#### `string characterName`

The name of the character/actor used during the session.
#### `ModuleType moduleType`

The module type used during the session.
#### `Queue<int> requestedAudioIndices`

A queue of audio indices corresponding to requested positions in input text in chronological order. Is null if none were requested.
### Constructors

#### `PacketMetadata(string sessionID, DataPacketStatus status, string characterName = null, ModuleType moduleType = ModuleType.None, Queue<int> requestedAudioIndices = null)`

Initializes a new instance of the `PacketMetadata` struct.

## Struct `ThespeonDataPacket<T>`

Represents a data packet from Thespeon synthesis containing raw data, metadata and a flag for if it is the final packet of a synthesis.
### Properties

#### `T[] data`

The raw data contained in the packet.
#### `bool isFinalPacket`

Indicates if this is the final packet of a synthesis.
#### `PacketMetadata metadata`

Metadata associated with the packet, including session ID, character name, module type and any eventual requested audio indices.
### Constructors

#### `ThespeonDataPacket(T[] data, string sessionID, DataPacketStatus status = DataPacketStatus.OK, bool isFinalPacket = false, string characterName = null, ModuleType moduleType = ModuleType.None, Queue<int> requestedAudioIndices = null)`

Initializes a new instance of the `ThespeonDataPacket{T}` struct.

## Enum `VerbosityLevel`

Enumeration representing the verbosity level for logging and debugging. This can be used to control the amount of information logged during the synthesis process. None - turns off all logging. Error - logs only error messages. Warning - logs warning messages. These typically indicate potential issues that limit functionality but do not stop execution. Info - logs informational messages. These provide general information about the synthesis process. Debug - logs detailed debug messages. These are useful for troubleshooting and issue reporting to the Lingotion team.
### Members

#### `None`
#### `Error`
#### `Warning`
#### `Info`
#### `Debug`