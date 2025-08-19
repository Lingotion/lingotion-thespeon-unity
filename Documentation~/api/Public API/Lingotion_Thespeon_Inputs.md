# Lingotion.Thespeon.Inputs API Documentation

## Class `ControlCharacters`

Collection of control characters used in Thespeon.

## Class `ThespeonCharacterAsset`

This class is used to define characters and their associated modules.

## Class `ThespeonCharacterHelper`

Helper class to manage Thespeon characters and their modules.
### Methods

#### `List<(string characterName, ModuleType moduleType)> GetAllCharactersAndModules()`

Retrieves all imported characters and their associated modules.

**Returns:** A list of tuples for each pair of character name and module type.
#### `List<ModuleType> GetAllModulesForCharacter(string characterName)`

Retrieves all module types for a specific character.

**Parameters:**

- `characterName`: The name of the character to retrieve modules for.

**Returns:** A list of ModuleType values available for the specified character.

## Class `ThespeonInput`

Represents a Thespeon input which specifies exactly what to synthesize.
### Constructors

#### `ThespeonInput(List<ThespeonInputSegment> segments, string actorName = null, ModuleType moduleType = ModuleType.None, Emotion defaultEmotion = Emotion.None, string defaultLanguage = null, string defaultDialect = null, AnimationCurve speed = null, AnimationCurve loudness = null)`

The main constructor for ThespeonInput. Initializes a new instance of ThespeonInput with the specified actor name, segments, model type, default emotion, default language, speed, and loudness.

**Parameters:**

- `segments`: A list of ModelInputSegment instances representing the segments of the input.
- `actorName`: The name of the actor to use.
- `moduleType`: An instance of the ModuleType enum representing the type of model to use.
- `defaultEmotion`: The default emotion to be used across segments.
- `defaultLanguage`: The default language to be used across segments.
- `defaultDialect`: The default dialect to be used across segments.
- `speed`: An AnimationCurve representing the speed of the input over its length.
- `loudness`: An AnimationCurve representing the loudness of the input over its length.

**Exceptions:**

- `System.ArgumentException`: Thrown if the actor name is null or empty or if the segments list is null or empty or contains empty text.
#### `ThespeonInput(ThespeonInput other)`

Deep copy constructor for ThespeonInput.

**Parameters:**

- `other`: The ThespeonInput instance to copy from.

**Exceptions:**

- `System.ArgumentNullException`: Thrown if the provided ThespeonInput instance is null.
### Methods

#### `ThespeonInput ParseFromJson(string jsonPath, InferenceConfig configOverride = null)`

Parses a ThespeonInput from a JSON file located at the specified path relative to Assets directory with optional config override settings.

**Parameters:**

- `jsonPath`: Relative path to json file to be read.
- `configOverride`: 

**Returns:** A ThespeonInput instance populated with the data from the JSON file.

**Exceptions:**

- `System.ArgumentException`: Thrown if the JSON file does not contain the required fields.
#### `ThespeonInput ParseFromJson(JObject json, InferenceConfig configOverride = null)`

Parses a ThespeonInput from a JSON object with optional config override settings.

**Parameters:**

- `json`: JSON object containing the input data.
- `configOverride`: Optional configuration override for default values.

**Returns:** A ThespeonInput instance populated with the data from the JSON object.

**Exceptions:**

- `System.ArgumentException`: Thrown if the JSON object is null or does not contain the required fields.
#### `string ToJson()`

Returns a string representation of the ThespeonInput in JSON format after filtering out any null or None elements.

**Returns:** A JSON string representing the ThespeonInput.

## Class `ThespeonInputSegment`

Represents a segment of input for Thespeon, containing text, language, dialect, emotion, and custom pronunciation flag.
### Constructors

#### `ThespeonInputSegment(string text, string language = null, string dialect = null, Emotion emotion = Emotion.None, bool isCustomPronounced = false)`

The main constructor for ThespeonInputSegment. Initializes a new instance of ThespeonInputSegment with the specified text, emotion, language, and custom pronunciation flag.

**Parameters:**

- `text`: The text of the segment. Cannot be null or empty
- `language`: The language of the segment. Optional.
- `dialect`: The dialect of the segment. Optional.
- `emotion`: The emotion associated with the segment. Optional
- `isCustomPronounced`: Indicates whether the segment consists of only IPA text. Optional, defaults to false.

**Exceptions:**

- `System.ArgumentException`: Thrown if the text is null or empty.
#### `ThespeonInputSegment(ThespeonInputSegment other)`

Deep copy constructor for ThespeonInputSegment.

**Parameters:**

- `other`: The ThespeonInputSegment instance to copy from.

**Exceptions:**

- `System.ArgumentNullException`: Thrown if the provided ThespeonInputSegment instance is null.
### Methods

#### `ThespeonInputSegment ParseFromJson(string jsonPath)`

Parses a ThespeonInputSegment from a JSON file located at the specified path relative to the project Assets directory.

**Parameters:**

- `jsonPath`: The path to the JSON file relative to the Assets directory.

**Returns:** A ThespeonInputSegment instance populated with data from the JSON file.

**Exceptions:**

- `System.ArgumentNullException`: Thrown if the provided JSON object is null or its required fields are missing.
#### `ThespeonInputSegment ParseFromJson(JObject json)`

Parses a ThespeonInputSegment from a JSON object.

**Parameters:**

- `json`: The JSON object containing the segment data.

**Returns:** A ThespeonInputSegment instance populated with data from the JSON object.

**Exceptions:**

- `System.ArgumentNullException`: Thrown if the provided JSON object is null or its required fields are missing.
#### `bool EqualsIgnoringText(ThespeonInputSegment other)`

Compares this ThespeonInputSegment with another for equality, ignoring the text field.

**Parameters:**

- `other`: The other ThespeonInputSegment to compare with.

**Returns:** True if the segments are equal ignoring the text field, otherwise false.
#### `ModelInputSegment DeepCopy()`

Creates a deep copy of the ThespeonInputSegment.

**Returns:** A new ThespeonInputSegment instance that is a deep copy of the current instance, including all relevant fields.