# Lingotion.Thespeon.Core.IO API Documentation

## Class `RuntimeFileLoader`

Static class that fetches files for the Thespeon Package.
### Methods

#### `string TrimPackFilePath(string packFilePath)`

Trims away Unity-relative part of pack file path. Returns an empty string if the path is not a pack path.

**Parameters:**

- `subdirectory`: Specific subdirectory to path to.
- `packFilePath`: Filepath to the file inside the pack folders.

**Returns:** Relative path to the file inside the pack folders.
#### `string GetDirectoryPath(string filename)`

Fetches the subdirectory path of a file, regardless of directory separator.

**Parameters:**

- `filename`: A target file path.

**Returns:** A path pointing to the directory of the file.
#### `string GetActorPacksPath(bool relative = false)`

Fetches the runtime path of the actor packs.

**Parameters:**

- `relative`: Format the path as a Unity relative path

**Returns:** A path pointing to the actor pack location.
#### `string GetDirectory(string filePath)`

Gets the directory of a file.

**Parameters:**

- `filePath`: The absolute path to the file.

**Returns:** The directory of the file.
#### `string GetActorPackFile(string packRelativeFilePath, bool unityRelative = false)`

Creates a runtime path to a file inside an actor pack.

**Parameters:**

- `packRelativeFilePath`: Target filepath relative to actor pack .json file.
- `unityRelative`: If function should return a Unity relative path ("Assets/StreamingAssets/...").

**Returns:** A path pointing to the actor pack file.
#### `string GetLanguagePacksPath(bool relative = false)`

Fetches the runtime path of the language packs.

**Parameters:**

- `relative`: Format the path as a Unity relative path

**Returns:** A path pointing to the language pack location.
#### `string GetLanguagePackFile(string packRelativeFilePath, bool unityRelative = false)`

Creates a runtime path to a file inside a language pack.

**Parameters:**

- `packRelativeFilePath`: Target filepath relative to language pack .json file.
- `unityRelative`: If function should return a Unity relative path ("Assets/StreamingAssets/...").

**Returns:** A path pointing to the language pack file.
#### `Stream LoadFileAsStream(string filePath)`

Loads a file and returns it as a Stream.

**Parameters:**

- `filePath`: The absolute path to the file.

**Returns:** A Stream (FileStream or MemoryStream) if the file is successfully loaded; otherwise, null.
#### `string LoadFileAsString(string filePath)`

Loads a specifed file as a string.

**Parameters:**

- `filePath`: The path to the target file.

**Returns:** File contents as a string.
#### `IEnumerator LoadLookupTable(string filePath, Action<Dictionary<string, string>> onBatchComplete, Func<bool> yieldCondition, Action onYield)`

Loads a lookup table file line by line and yields whenever the condition is met, invoking the onBatchComplete callback with the current batch of entries and onYield callback.

**Parameters:**

- `filePath`: The path to the target lookup table file.
- `onBatchComplete`: Callback invoked with the current batch of entries when the yield condition is met.
- `yieldCondition`: A function that returns true when the coroutine should yield.
- `onYield`: Callback invoked after yielding.