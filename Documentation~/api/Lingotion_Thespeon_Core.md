# Lingotion.Thespeon.Core API Documentation

## Class `CommonMetadataKeys`

Well-known metadata key constants used in `ThespeonDataPacket` metadata dictionaries.
### Properties

#### `string SessionID`

Metadata key for the synthesis session identifier.

## Class `ConfigFormatDetector`

Utility class for detecting and validating config formats.
### Properties

#### `string LARA_TYPE`

Type identifier for character configs (Lara model).
#### `string PHONEMIZER_TYPE`

Type identifier for language/phonemizer configs.
### Methods

#### `bool IsCharacterConfig(JObject config)`

Checks if the config is a character (lara) config.

**Parameters:**

- `config`: The parsed JSON config.

**Returns:** True if the config type is "lara".
#### `bool IsLanguageConfig(JObject config)`

Checks if the config is a language (phonemizer) config.

**Parameters:**

- `config`: The parsed JSON config.

**Returns:** True if the config type is "phonemizer".
#### `string GetConfigType(JObject config)`

Gets the type string from a config.

**Parameters:**

- `config`: The parsed JSON config.

**Returns:** The type string, or null if not present.
#### `void ValidateCharacterConfig(JObject config)`

Validates that the config is specifically a character (lara) config.

**Parameters:**

- `config`: The parsed JSON config.

**Exceptions:**

- `NotSupportedException`: Thrown when the config is not a lara config.
#### `void ValidateLanguageConfig(JObject config)`

Validates that the config is specifically a language (phonemizer) config.

**Parameters:**

- `config`: The parsed JSON config.

**Exceptions:**

- `NotSupportedException`: Thrown when the config is not a phonemizer config.

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

## Class `ManifestHandler`

Singleton that handles parsing and distributing information found in the manifest file.
### Properties

#### `ManifestHandler Instance`

Singleton reference.
#### `Dictionary<string, ModuleType> StringToModuleType`

Maps quality tag strings (e.g. "low", "high") to their corresponding `ModuleType` values.
### Methods

#### `void UpdateMappings()`

Forces the handler to re-parse the manifest and signal its update.
#### `List<ModuleLanguage> GetAllLanguageModuleLanguages()`

Fetches all languages in the parsed manifest.

**Returns:** A list of all languages found.
#### `List<string> GetAllCharacters()`

Fetches all character names in the parsed manifest.

**Returns:** A list of all characters found.
#### `List<ModuleType> GetAllModuleTypesForCharacter(string character)`

Fetches all unique available module types for a given character. Assumes there is only one module type per module.

**Parameters:**

- `character`: The name of the character to fetch module types for.
#### `Dictionary<string, ModuleLanguage> GetAllLanguagesForCharacterAndModuleType(string characterName, ModuleType type)`

Fetches all languages available for a given character and module type.

**Parameters:**

- `characterName`: The name of the character to fetch languages for.
- `type`: The module type to filter languages by.
#### `Dictionary<string, ModuleLanguage> GetAllDialectsInModuleLanguage(string characterName, ModuleType type, string iso639_2)`

Fetches all languages available for a given character and module type.

**Parameters:**

- `characterName`: The name of the character to fetch languages for.
- `type`: The module type to filter languages by.
#### `List<ModuleLanguage> GetAllSupportedLanguages(string characterName, ModuleType type)`

Fetches all supported and imported languages for a given character and module type.

**Parameters:**

- `characterName`: The name of the character to fetch languages for.
- `type`: The module type to fetch languages for.

**Returns:** A list of ModuleLanguage objects representing the supported languages.
#### `Dictionary<string, string> GetAllSupportedLanguageCodes(string characterName, ModuleType type)`

Fetches all supported language codes for a given character and module type.

**Parameters:**

- `characterName`: The name of the character to fetch languages for.
- `type`: The module type to fetch languages for.

**Returns:** A Dictionary mapping the English name of the language to the ISO639-2 language code.
#### `ModuleEntry GetCharacterModuleEntry(string characterName, ModuleType type)`

Finds a specific character module.

**Parameters:**

- `characterName`: Target character name.
- `type`: Target module type.

**Returns:** A module entry of the corresponding character.
#### `ModuleEntry GetLanguageModuleEntry(string moduleName)`

Finds a specific language module.

**Parameters:**

- `moduleName`: Target module name.

**Returns:** A module entry of the corresponding language.
#### `List<string> GetAllCharacterNames()`

Fetches all available character names.

**Returns:** List of all character names.
#### `string GetConfigFilename(string name)`

Returns the config filename for a file by its display name.

**Parameters:**

- `name`: The specific display name to find.

**Returns:** The config filename, or null if not found.
#### `List<string> GetAllLanguageNames()`

Fetches all available language names.

**Returns:** List of all language names.
#### `List<string> GetAllModuleInfoInCharacter(string name)`

Summarizes all module info inside a character.

**Parameters:**

- `name`: The specific name to find.

**Returns:** A list of strings summarizing the modules inside the character.
#### `List<string> GetAllModuleInfoInLanguage(string name)`

Summarizes all module info inside an language module.

**Parameters:**

- `name`: The specific language name to find.

**Returns:** A list of strings summarizing the language modules.
#### `List<string> GetMissingLanguages()`

Fetches all missing languages that are required by the character. This is useful for identifying which languages need to be installed for the character to function correctly.
#### `List<string> GetAllModuleIDs()`

Fetches all module IDs from both characters and languages.

**Returns:** A combined list of all module IDs.
#### `bool IsFileShared(string md5)`

Checks if a file (by MD5) is shared across multiple modules.

**Parameters:**

- `md5`: The MD5 hash of the file to check.

**Returns:** True if the file is used by more than one module.

## Enum `MetadataType`

Specifies the data type stored in a `PacketMetadataValue`.

## Class `ModelInput<ModelInputType, InputSegmentType>`

Base class for model inputs, providing common properties and methods for all model inputs.
### Constructors

#### `ModelInput(ModelInput<ModelInputType, InputSegmentType> other)`

Deep copy constructor for ModelInput.

**Parameters:**

- `other`: The ModelInput instance to copy from.

**Exceptions:**

- `System.ArgumentNullException`: Thrown if the provided ModelInput instance is null.
#### `ModelInput(List<InputSegmentType> segments, string characterName, ModuleType moduleType = ModuleType.None, Emotion defaultEmotion = Emotion.None, string defaultLanguage = null, string defaultDialect = null)`

Constructor for ModelInput. Initializes a new instance of ModelInput with the specified character name, module type, default emotion, and default language.

**Parameters:**

- `characterName`: The name of the character.
- `segments`: A list of ModelInputSegment instances representing the segments of the input.
- `defaultLanguage`: The default language to be used.
- `defaultEmotion`: The default emotion to be used.
- `moduleType`: The type of the module.

**Exceptions:**

- `System.ArgumentException`: Thrown if the character name is null or empty, or if the segments list is null or empty.

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
### Properties

#### `Worker worker`

The inference engine worker executing this model.
#### `Model model`

The loaded model asset.

## Class `Module`

Class describing the common parameters of a module.
### Properties

#### `string ModuleID`

The unique identifier for this module.
#### `string JsonPath`

The path to the module's JSON configuration file.
#### `string Version`

The version string of this module.
### Methods

#### `Dictionary<string, ModelRuntimeBinding> CreateRuntimeBindings(HashSet<string> md5s, BackendType preferredBackedType)`

Creates runtime bindings for the module's models, pairing them with their MD5s.

**Parameters:**

- `moduleInfo`: The module entry containing the ID, JSON path, and version.
- `files`: The JSON "files" array from the config.
- `isModelFile`: Predicate that determines whether a file extension represents a model file for runtime binding purposes.
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
#### `bool IsIncludedIn(HashSet<string> workloadIDs, BackendType backend)`

Checks if the module is fully included in the provided set of MD5s.

**Parameters:**

- `workloadIDs`: A set of WorkloadIDs strings of already existing module bindings.
- `backend`: The backend type to check the module's workloads against.

**Returns:** True if the module is fully contained in the set of MD5s, false otherwise.
#### `HashSet<string> GetLoadedWorkloadIDs(BackendType backend)`

Gets all WorkloadIDs of the given module for a specific backend if it is loaded on that backend.

**Parameters:**

- `backend`: The backend type to get WorkloadIDs for.

**Returns:** A set of WorkloadID strings representing combinations of backendtype and file in this module.
#### `string GetInternalModelID(string internalName)`

Gets the file MD5 of a given internal name.

**Parameters:**

- `internalName`: The internal name of the model.

**Returns:** The md5 of the model with the provided internal name.

**Exceptions:**

- `KeyNotFoundException`: Thrown if the internal name does not exist in the internal model mappings.
#### `bool HasModelMD5(string md5)`

Checks if a given MD5 exists in the module's model mappings.

**Parameters:**

- `md5`: The MD5 hash to check.

**Returns:** True if the MD5 exists in the model mappings, false otherwise.
#### `HashSet<BackendType> GetLoadedBackends()`

Gets the set of backend types that this module is currently loaded on.
#### `void AddLoadedBackend(BackendType backend)`

Adds a backend type to the set of loaded backends for this module, indicating that the module is now loaded on that backend.

**Parameters:**

- `backend`: The backend type to add.
#### `void RemoveLoadedBackend(BackendType? backend)`

Removes a backend type from the set of loaded backends for this module, indicating that the module is no longer loaded on that backend. If the provided backend is null, removes all backends from the set, effectively marking the module as not loaded on any backend.

**Parameters:**

- `backend`: The backend type to remove, or null to remove all backends.
#### `string GetWorkloadID(string md5, BackendType backend)`

Generates a WorkloadID string based on the provided MD5 hash and backend type, following the format "{backend}_{md5}".

**Parameters:**

- `md5`: The MD5 hash of the model file.
- `backend`: The backend type for which the WorkloadID is being generated.

**Returns:** >A string representing the WorkloadID, formatted as "{backend}_{md5}".

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

Represents a file associated with a module, including its path, MD5 hash, and extension.
### Properties

#### `string filePath`

The runtime file path.
#### `string md5`

The MD5 hash identifying this file.
#### `string extension`

The file extension (e.g. "onnx", "bin").
### Constructors

#### `ModuleFile(string filePath, string md5, string extension = null)`

Initializes a new instance of the `ModuleFile` struct.

**Parameters:**

- `filePath`: The runtime file path.
- `md5`: The MD5 hash identifying this file.
- `extension`: The file extension, or null if not applicable.
### Methods

#### `string GetFilename()`

Gets the filename in the format "{md5}.{extension}".

## Class `ModuleLanguage`

Class representing the language selection for a module.

## Enum `ModuleType`

Enum denoting the different character module types.
### Members

#### `None`
#### `XS`
#### `S`
#### `M`
#### `L`
#### `XL`

## Class `NumberConverter`

Abstract class for converting numbers to a specific format. This class is intended to be extended for specific number conversion implementations.

## Struct `PacketMetadataValue`

A type-safe, immutable container for a single metadata value in a `ThespeonDataPacket`. Holds exactly one typed value (long, float, bool, or string) determined at creation time.
### Properties

#### `MetadataType Type`

The type of data stored in this metadata value.
### Methods

#### `PacketMetadataValue Create(long v)`

Creates a metadata value containing a long integer.

**Parameters:**

- `v`: The long value to store.
#### `PacketMetadataValue Create(float v)`

Creates a metadata value containing a float.

**Parameters:**

- `v`: The float value to store.
#### `PacketMetadataValue Create(bool v)`

Creates a metadata value containing a boolean.

**Parameters:**

- `v`: The boolean value to store.
#### `PacketMetadataValue Create(string v)`

Creates a metadata value containing a string.

**Parameters:**

- `v`: The string value to store.
#### `bool TryGet(out long v)`

Attempts to retrieve the value as a long integer.

**Parameters:**

- `v`: When this method returns, contains the long value if the type matches; otherwise, default.

**Returns:** True if the metadata type is `MetadataType.Int64`; otherwise, false.
#### `bool TryGet(out float v)`

Attempts to retrieve the value as a float.

**Parameters:**

- `v`: When this method returns, contains the float value if the type matches; otherwise, default.

**Returns:** True if the metadata type is `MetadataType.Float`; otherwise, false.
#### `bool TryGet(out bool v)`

Attempts to retrieve the value as a boolean.

**Parameters:**

- `v`: When this method returns, contains the boolean value if the type matches; otherwise, default.

**Returns:** True if the metadata type is `MetadataType.Bool`; otherwise, false.
#### `bool TryGet(out string v)`

Attempts to retrieve the value as a string.

**Parameters:**

- `v`: When this method returns, contains the string value if the type matches; otherwise, null.

**Returns:** True if the metadata type is `MetadataType.String`; otherwise, false.
#### `string ToString()`

Returns a string representation of the stored value.

## Struct `PacketPayload`

A type-safe, immutable payload container for `ThespeonDataPacket`. Holds exactly one typed value (float[], long[], or string) determined at creation time.
### Properties

#### `PacketPayloadType Type`

The type of data stored in this payload.
### Methods

#### `PacketPayload Create(float[] v)`

Creates a payload containing a float array.

**Parameters:**

- `v`: The float array to store.
#### `PacketPayload Create(long[] v)`

Creates a payload containing a long array.

**Parameters:**

- `v`: The long array to store.
#### `PacketPayload Create(string v)`

Creates a payload containing a string.

**Parameters:**

- `v`: The string to store.
#### `PacketPayload CreateEmpty()`

Creates an empty payload with no data.
#### `bool TryGet(out float[] v)`

Attempts to retrieve the payload as a float array.

**Parameters:**

- `v`: When this method returns, contains the float array if the payload type is `PacketPayloadType.FloatArray`; otherwise, null.

**Returns:** True if the payload contains a float array; otherwise, false.
#### `bool TryGet(out long[] v)`

Attempts to retrieve the payload as a long array.

**Parameters:**

- `v`: When this method returns, contains the long array if the payload type is `PacketPayloadType.Int64Array`; otherwise, null.

**Returns:** True if the payload contains a long array; otherwise, false.
#### `bool TryGet(out string v)`

Attempts to retrieve the payload as a string.

**Parameters:**

- `v`: When this method returns, contains the string if the payload type is `PacketPayloadType.String`; otherwise, null.

**Returns:** True if the payload contains a string; otherwise, false.

## Enum `PacketPayloadType`

Specifies the data type stored in a `PacketPayload`.
### Members

#### `None`

No payload data.
#### `FloatArray`

Payload contains a float array (e.g. audio samples).
#### `Int64Array`

Payload contains a long array (e.g. trigger sample indices).
#### `String`

Payload contains a string (e.g. error message).

## Enum `SupportedBackendType`
### Members

#### `CPU`
#### `GPUCompute`

## Enum `SynthCallbackType`

Specifies the type of synthesis callback a data packet represents.
### Members

#### `CB_UNSPECIFIED`

Unspecified callback type.
#### `CB_ERROR`

Error callback indicating synthesis failure.
#### `CB_AUDIO`

Audio data callback containing synthesized audio samples.
#### `CB_TRIGGERSAMPLE`

Trigger sample callback containing requested audio sample indices.

## Class `ThespeonDataPacket`

Represents a data packet from Thespeon synthesis containing a single data payload and its metadata.
### Properties

#### `SynthCallbackType CallbackType`

The type of synthesis callback this packet represents.
#### `PacketPayload Payload`

The data payload of the packet.
#### `Dictionary<string, PacketMetadataValue> Metadata`

Key-value metadata associated with this packet.
### Constructors

#### `ThespeonDataPacket(SynthCallbackType callbackType, PacketPayload payload)`

Initializes a new data packet with the specified callback type and payload. Metadata dictionary is initialized empty.

**Parameters:**

- `callbackType`: The type of synthesis callback.
- `payload`: The data payload.
#### `ThespeonDataPacket(SynthCallbackType callbackType, PacketPayload payload, Dictionary<string, PacketMetadataValue> metadata)`

Initializes a new data packet with the specified callback type, payload, and metadata.

**Parameters:**

- `callbackType`: The type of synthesis callback.
- `payload`: The data payload.
- `metadata`: Key-value metadata to associate with this packet.
### Methods

#### `ThespeonDataPacket CreateErrorPacket(string errorMessage)`

Creates an error packet with the specified error message as its string payload.

**Parameters:**

- `errorMessage`: The error message to include in the packet.

**Returns:** A new `ThespeonDataPacket` with `SynthCallbackType.CB_ERROR` callback type.

## Class `ThespeonDefaultSettings`

A ScriptableObject that holds default values for `InferenceConfig`. Users may edit these in the Inspector to override hard-coded defaults project-wide. At runtime, InferenceConfig's default constructor reads from this asset via Resources.Load. Place the asset at Assets/Lingotion Thespeon/Resources/ThespeonDefaultSettings.asset.
### Properties

#### `SupportedBackendType PreferredBackendType`

Returns the singleton instance loaded from Resources. Falls back to a transient instance with hard-coded defaults if no asset is found.
### Methods

#### `void ResetToHardCodedDefaults()`

Resets all fields to the original hard-coded defaults.

## Enum `VerbosityLevel`

Enumeration representing the verbosity level for logging and debugging. This can be used to control the amount of information logged during the synthesis process. None - turns off all logging. Error - logs only error messages. Warning - logs warning messages. These typically indicate potential issues that limit functionality but do not stop execution. Info - logs informational messages. These provide general information about the synthesis process. Debug - logs detailed debug messages. These are useful for troubleshooting and issue reporting to the Lingotion team.
### Members

#### `None`
#### `Error`
#### `Warning`
#### `Info`
#### `Debug`