# Lingotion.Thespeon.ActorPack API Documentation

## Class `ActorModule`

Virtual character module, containing character-specific information and specifications.
### Constructors

#### `ActorModule(ModuleEntry moduleInfo)`

Creates a new ActorModule instance.

**Parameters:**

- `moduleInfo`: Module entry information.
### Methods

#### `Dictionary<string, ModelRuntimeBinding> CreateRuntimeBindings(HashSet<string> md5s, BackendType preferredBackendType)`

Creates runtime bindings for this specific module setup.

**Parameters:**

- `md5s`: List of model MD5 strings that are *already loaded*, thus should be skipped.

**Returns:** A dictionary of model MD5 strings to corresponding runtime binding.
#### `IEnumerator CreateRuntimeBindingsCoroutine(HashSet<string> md5s, BackendType preferredBackendType, Action<Dictionary<string, ModelRuntimeBinding>> onComplete)`

Creates runtime bindings for this specific module setup, yielding between each binding creation to avoid blocking the main thread.

**Parameters:**

- `md5s`: List of model MD5 strings that are *already loaded*, thus should be skipped.
- `preferredBackendType`: The preferred backend type for the models.
- `onComplete`: Callback to invoke when all bindings are created.

**Returns:** A dictionary of model MD5 strings to corresponding runtime binding.
#### `bool IsIncludedIn(HashSet<string> md5s)`

Checks if the module is fully included in the provided MD5 set.

**Parameters:**

- `md5s`: Set of MD5 strings to check against.

**Returns:** True if the module is included, false otherwise.
#### `HashSet<string> GetAllFileMD5s()`

Gets all MD5s of the files in this module.

**Returns:** A set of MD5 strings representing all files in the module.
#### `(List<int>, List<int>) EncodePhonemes(string phonemes)`

Encodes phonemes into their corresponding IDs based on the encoder id vocabulary.

**Parameters:**

- `phonemes`: String of phonemes to encode.

**Returns:** A tuple containing a list of encoded phoneme IDs and a list of indices for not found phonemes.
#### `int GetLanguageKey(ModuleLanguage language)`

Gets the language key for a given language. If the language is not found, it tries to find the first matching ISO639-2 code.

**Parameters:**

- `language`: The language to get the key for.

**Returns:** The language key if found, otherwise -1.
#### `int GetActorKey()`

Gets the actor key for this module.

**Returns:** The actor key if found, otherwise -1.