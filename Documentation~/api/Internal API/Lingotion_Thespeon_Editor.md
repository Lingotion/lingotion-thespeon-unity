# Lingotion.Thespeon.Editor API Documentation

## Class `EditorInfoWindow`

Allows user to import, delete, and see an overview of imported packs.
### Methods

#### `void ShowWindow()`

Reveals the Editor Info window.
#### `void CreateGUI()`

Creates the GUI skeleton.

## Class `EditorInputContainer`

Scriptable object for Thespeon inputs. For use in Editor Window.

## Class `EditorPackImporter`

Allows for importing and verification of packs.
### Methods

#### `void ImportThespeonPack()`

Extracts, verifies and imports a Lingotion Pack.
#### `void DeletePack(string packName)`

Deletes a pack by its name, including its meta file.

**Parameters:**

- `packName`: The name of the pack to delete.

## Class `EditorPackWatcher`

Watches for changes to the pack directories and updates the manifest.
### Methods

#### `void OnPreprocessBuild(BuildReport report)`

Called before build. Performs final write to pack manifest.

**Parameters:**

- `report`: 

## Class `EditorResourceCleanup`

Handles the cleanup of resources and disposing of tensors/workers when the editor is set from play mode or quitting.
### Methods

#### `void CleanupResources()`

Cleans up and disposes all resources used by Thespeon.

## Class `LingotionCharacterAssetGenerator`

Automatically generates character assets for all imported actors and module types. The assets are stored in your Project under Assets/Lingotion Thespeon/CharacterAssets and can be used to easily select the desired actor in your scene.