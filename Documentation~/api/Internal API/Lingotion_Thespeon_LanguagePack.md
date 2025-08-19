# Lingotion.Thespeon.LanguagePack API Documentation

## Class `LanguageModule`

Virtual language module.
### Constructors

#### `LanguageModule(ModuleEntry moduleInfo)`

Initializes a new instance of the `LanguageModule` class with the specified module information.

**Parameters:**

- `moduleInfo`: Module entry containing module information.

**Exceptions:**

- `System.Exception`: Thrown if the module is not a valid language pack config file.
- `System.Exception`: Thrown if the module is not found in the config file.
- `System.Exception`: Thrown if grapheme or phoneme vocabularies are not defined in the module.
### Methods

#### `Dictionary<string, ModelRuntimeBinding> CreateRuntimeBindings(HashSet<string> md5s, BackendType preferredBackendType)`

Creates runtime bindings for this specific module setup.

**Parameters:**

- `md5s`: List of model MD5 strings that are *already loaded*, thus should be skipped.

**Returns:** A dictionary of model MD5 strings to corresponding runtime binding.
#### `bool IsIncludedIn(HashSet<string> md5s)`

Checks if the module is fully included in the provided set of existing MD5 strings.

**Parameters:**

- `md5s`: Set of MD5 strings to check against.

**Returns:** True if the module is included, false otherwise.
#### `HashSet<string> GetAllFileMD5s()`

Gets all MD5s of the files in this module.

**Returns:** A set of MD5 strings representing all files in the module.
#### `List<int> EncodeGraphemes(string graphemes)`

Encodes graphemes into their corresponding IDs based on the grapheme vocabulary.

**Parameters:**

- `graphemes`: String of graphemes to encode.

**Returns:** A list of encoded grapheme IDs.
#### `List<int> EncodePhonemes(string phonemes)`

Encodes phonemes into their corresponding IDs based on the phoneme vocabulary.

**Parameters:**

- `phonemes`: String of phonemes to encode.

**Returns:** A list of encoded phoneme IDs and a list of indices for not found phonemes.
#### `string DecodePhonemes(List<int> ids)`

Decodes phoneme IDs back into their corresponding phoneme strings based on the phoneme vocabulary.

**Parameters:**

- `ids`: List of phoneme IDs to decode.

**Returns:** A string representation of the decoded phonemes.
#### `void InsertStringBoundaries(List<int> wordIDs)`

Inserts start-of-sequence and end-of-sequence tokens into the provided list of word IDs.

**Parameters:**

- `wordIDs`: List of word IDs to modify.
#### `Dictionary<string, string> GetLookupTable()`

Get lookup table as Dictionary for language modules.

**Returns:** Lookup table as Dictionary

**Exceptions:**

- `KeyNotFoundException`: Thrown if the lookup table is not found in the internal file mappings.
#### `IEnumerator GetLookupTableCoroutine(Action<Dictionary<string, string>> onComplete, Func<bool> yieldCondition, Action onYield)`

Get lookup table as Dictionary for language modules batchwise, yielding whenever necessary.

**Exceptions:**

- `KeyNotFoundException`: Thrown if the lookup table is not found in the internal file mappings.
#### `string GetLookupTableID()`

Gets the ID of the lookup table file.

**Returns:** The MD5 of the lookup table file.

## Class `LookupTableHandler`

Singleton that manages and registers runtime lookup tables for language modules.
### Methods

#### `void RegisterLookupTable(LanguageModule module)`

Registers a language module's lookup table if not already registered.

**Parameters:**

- `module`: The language module to register lookup table for.
#### `IEnumerator RegisterLookupTableCoroutine(LanguageModule module, Func<bool> yieldCondition, Action onYield)`

Registers a language module's lookup table if not already registered, yielding whenever necessary.

**Parameters:**

- `module`: The language module to register lookup table for.
#### `void DeregisterTable(LanguageModule module)`

Deregisters a language module's lookup table by its MD5 identifier.

**Parameters:**

- `module`: The language module to deregister lookup table for.
#### `RuntimeLookupTable GetLookupTable(string md5)`

Retrieves a registered lookup table by its MD5 identifier.

**Parameters:**

- `md5`: The MD5 identifier of the lookup table.

**Returns:** The RuntimeLookupTable associated with the MD5 identifier, or null if not found.
#### `void DisposeAndClear()`

Clears all registered lookup tables for garbage collection.

## Class `NumberToWordsConverter`

Converts numbers in a string to their English word representation in phonemes.
### Methods

#### `string ConvertNumber(string input)`

Replaces numeric substrings with word equivalents directly in phonemes.: - Detects optional ordinal suffixes (st|nd|rd|th) - Replaces with spelled-out number words - If ordinal suffix was present, outputs ordinal words (e.g., "1st" -> "first")

## Class `NumberToWordsSwedish`

Converts numbers in a string to their Swedish word representation in phonemes.
### Methods

#### `string ConvertNumber(string number)`

Replaces all integers in the string with their spelled-out Swedish form. Example: "Vi har 1 katt och 1000 hundar." => "Vi har en katt och ett tusen hundar."

## Class `RuntimeLookupTable`

Represents a runtime lookup table that can dynamically add entries and check for existing keys.
### Constructors

#### `RuntimeLookupTable(Dictionary<string, string> staticLookupTable)`

Initializes a new instance of the RuntimeLookupTable with a static lookup table.

**Parameters:**

- `staticLookupTable`: A dictionary containing static key-value pairs.
### Methods

#### `bool TryGetValue(string key, out string value)`

Tries to get the value associated with the specified key from the lookup tables.

**Parameters:**

- `key`: The key to look up.
- `value`: The value associated with the key if found.

**Returns:** True if the key exists in either table, otherwise false.
#### `bool ContainsKey(string key)`

Checks if the specified key exists in either the static or dynamic lookup tables.

**Parameters:**

- `key`: The key to check for existence.

**Returns:** True if the key exists in either table, otherwise false.
#### `void AddOrUpdateDynamicEntry(string key, string value)`

Adds or updates an entry in the dynamic lookup table. If the key already exists, its value is updated; otherwise, a new entry is added.

**Parameters:**

- `key`: The key to add or update.
- `value`: The value to associate with the key.

## Class `TextPreprocessor`

Provides methods for preprocessing text inputs, including cleaning and partitioning text segments.
### Methods

#### `ThespeonInput PreprocessInput(ThespeonInput input)`

Preprocesses the input by cleaning text segments and converting numbers to words (phonemes) where applicable.

**Parameters:**

- `input`: The ThespeonInput containing text segments to preprocess.

**Returns:** A new ThespeonInput with processed segments.

**Exceptions:**

- `ArgumentException`: Thrown if a segment's text is null or whitespace.
- `NotSupportedException`: Thrown if the language is not supported for number conversion.