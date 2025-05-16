# Lingotion.Thespeon.ThespeonRunscripts API Documentation

> ## Class `ThespeonInferenceHandler`
>
> The ThespeonInferenceHandler class is responsible for managing the inference process of the Thespeon API. It handles the loading and unloading of models, as well as the execution of inference jobs.
> ### Methods
>
> #### `void SetGlobalConfig(PackageConfig configOverride)`
>
> Sets the global configuration for the package by overriding all properties in the existing global config which are not null in configOverride.
>
> **Parameters:**
>
> - `configOverride`: 
> #### `PackageConfig GetCurrentConfig(string synthID=null)`
>
> Gets a copy of current config. If a synthid is provided it will return the local config valid for that synthid. Otherwise it will return the global config.
>
> **Parameters:**
>
> - `synthID`: The synthid to get the local config for. If null or omitted, the global config is returned.
> #### `void SetLocalConfig(string synthRequestID, PackageConfig config = null)`
>
> Associates a synthRequestID with a PackageConfig object consisting of the global config overwritten by non-null properties in local config.
>
> **Parameters:**
>
> - `synthRequestID`: The ID of the synth request
> - `config`: The PackageConfig object to overwrite global configs with in the current synthRequestID
> #### `void PreloadActorPackModule(ActorPack actorPack, ActorPackModule module, JObject packMappings, List<PhonemizerModule> phonemizerModules = null)`
>
> Preloads the module and its associated language packs into memory. Loads all relevant Models and creates workers for them.
>
> **Parameters:**
>
> - `actorPack`: 
> - `module`: 
> - `packMappings`: 
>
> **Exceptions:**
>
> - `Exception`: 
> #### `List<string> GetLoadedActorPackModules()`
>
> Gets a list of all currently preloaded ActorPackModule names.
>
> **Parameters:**
>
> - `packMappings`: Represent the JSON object mapping Actor Packs to its Language Packs.
> - `moduleId`: ID of the module for which to find its parent Language Pack
> #### `void UnloadActorPackModule(string actorPackModuleName=null)`
>
> Unloads the preloaded ActorPackModule and its associated language packs. If no module name is provided, all preloaded modules are unloaded. This will not unload modules which are currently running.
>
> **Parameters:**
>
> - `actorPackModuleName`: Optional name of specific ActorPackModule to unload. Otherwise unloads all
> #### `bool IsPreloaded(string moduleId)`
>
> Returns true if the given moduleId is currently loaded in memory.
>
> **Parameters:**
>
> - `moduleId`: 