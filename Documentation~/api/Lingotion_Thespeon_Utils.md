# Lingotion.Thespeon.Utils API Documentation

> ## Class `ActorPack`
> ### Methods
>
> #### `List<ActorPackModule> GetModules()`
>
> Retrieves the list of modules in the actor pack
>
> **Returns:** A list of ActorPackModule objects.
> #### `List<PhonemizerModule> GetLanguageModules(string moduleName = null)`
>
> Retrieves the list of LanguageModules in the actor pack or optionally in a specific module.
>
> **Parameters:**
>
> - `moduleName`: The name of the module to filter by (optional).
>
> **Returns:** A list of PhonemizerModule objects.
> #### `List<Actor> GetActors(string moduleName=null)`
>
> Retrieves a list of all available actors in the ActorPack or optionally all actors in a specific module.
>
> **Parameters:**
>
> - `moduleName`: The name of the module to filter by (optional).
>
> **Returns:** A list of Actor objects.
> #### `List<Language> GetLanguages(Actor actor = null)`
>
> Retrieves a list of languages for the given actor, or all languages in the ActorPack if no actor is specified.
>
> **Parameters:**
>
> - `actor`: The actor for which to retrieve languages (optional).
>
> **Returns:** A list of Language objects.
> #### `List<Emotion> GetEmotions(Actor actor = null, Language language = null)`
>
> Retrieves all emotions based on Actor and/or Language. If neither is specified, returns all emotions in the ActorPack. If only Actor is specified, returns all emotions associated with them. If only Language is specified, returns all emotions available for that language. If both are specified, returns the emotions available for that specific Actor-Language combination.
>
> **Parameters:**
>
> - `actor`: The actor for which to retrieve emotions (optional).
> - `language`: The language for which to retrieve emotions (optional).
>
> **Returns:** A list of Emotion objects.