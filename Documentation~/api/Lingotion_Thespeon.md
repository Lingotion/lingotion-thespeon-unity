# Lingotion.Thespeon API Documentation

> ## Class `PackImporterEditor`
> ### Methods
>
> #### `void DeleteActorPack()`
>
> Lets the user pick an Actor-Pack folder (limited to the Actor-Packs root) and—after confirmation—permanently deletes it together with its meta file.
> #### `void DeleteLanguagePack()`
>
> Same workflow as `DeleteActorPack` but for Language Packs.

> ## Class `StreamingAssetsExtension`
> ### Methods
>
> #### `List<string> GetPathsRecursively(string path, ref List<string> paths)`
>
> Recursively traverses each folder under `path` and returns the list of file paths. It will only work in Editor mode.
>
> **Parameters:**
>
> - `path`: Relative to Application.streamingAssetsPath.
> - `paths`: List of file path strings.
>
> **Returns:** List of file path strings.