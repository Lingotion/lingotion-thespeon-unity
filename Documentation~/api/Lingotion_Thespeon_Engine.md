# Lingotion.Thespeon.Engine API Documentation

## Class `InferenceConfigOverride`

Represents a configuration for inference sessions. All provided non-null fields will override the existing default values. This class allows for customization of inference settings such as backend type, Thespeon frame budget time, and scheduling options.
### Properties

#### `BackendType PreferredBackendType`

Specifies the preferred backend for model inference. Default: CPU
#### `double? TargetBudgetTime`

Defines the target budget time in seconds per frame allocated to Thespeon. Default: 10ms on IOS/Android, 5ms otherwise.
#### `double? TargetFrameTime`

Defines the target maximum total frame time making Thespeon underutilize its allocated budget time if the current frame time exceeds this value. Default: 33.3 ms (30 fps) on IOS/Android, 16.7 ms (60 fps) otherwise
#### `float? BufferSeconds`

Specifies how many seconds of data to generate before releasing the data to the caller. Useful for real time streaming to the audio thread. Default: 0.5s on IOS/Android, 0.1s otherwise
#### `bool? UseAdaptiveScheduling`

Enables or disables adaptive scheduling to dynamically adjust computation load per frame over time. Default: True
#### `float? OvershootMargin`

Value larger than 1 which determines the aggressiveness of the adaptive scheduler. A larger value is more lenient with interfering Default: 1.4
#### `int? MaxSkipLayers`

Limits how many extra yields that can be added per subtask by the adaptive scheduler. Default: 20
#### `VerbosityLevel Verbosity`

Locally controls the verbosity level of the package logger called, LingotionLogger.
### Methods

#### `InferenceConfig GenerateConfig()`

Generates an `InferenceConfig` by applying all non-null override values on top of the defaults.

**Returns:** A new `InferenceConfig` with overridden values applied.

## Class `ThespeonComponent`

The ThespeonComponent class is responsible for managing the Thespeon synthesis and acts as an API endpoint to the Thespeon user. It provides methods to synthesize audio from ThespeonInput, preload and unload characters. The engine supports various inference configurations which combine to control Thespeons performance and resource allocation.
### Properties

#### `Action<string, float[]> OnAudioReceived`

Event triggered when audio data is received. Action takes synth ID string and the current audio packet as a float array.
#### `Action<string, long[]> OnAudioSampleRequestReceived`

Event triggered when the requested audio sample indices are ready. Action takes synth ID string and the requested sample indices as a long array. This is guaranteed to fire before the first OnAudioReceived event for a given synthesis session, allowing the user to prepare for the incoming audio data.
#### `Action<string> OnSynthesisComplete`

Event triggered when synthesis is complete. Action takes a string corresponding to the SessionID that was completed.
#### `Action<bool, string, ModuleType, BackendType> OnPreloadComplete`

Event triggered when TryPreloadCoroutine is complete. Action takes bool result of preload as an argument along with the characterName, moduleType and backend for which the preload was attempted.
#### `Action<string> OnSynthesisFailed`

Event triggered when synthesis has failed.
### Methods

#### `void Synthesize(ThespeonInput input, string sessionID = "", InferenceConfigOverride configOverride = null)`

Synthesize audio from the provided ThespeonInput using the specified inference configuration. If a synthesis is already running, the request will be queued and executed when the current synthesis is complete.

**Parameters:**

- `input`: The ThespeonInput containing the text to synthesize.
- `sessionID`: An optional session ID for tracking the synthesis session.
- `configOverride`: An optional InferenceConfigOverride where each provided property overrides the existing default.
#### `bool TryPreloadCharacter(string characterName, ModuleType moduleType, InferenceConfigOverride configOverride = null, bool runWarmup = true)`

Preload a character module for inference.

**Parameters:**

- `characterName`: The name of the character to preload.
- `moduleType`: The type of module to preload.
- `configOverride`: An optional InferenceConfigOverride to customize the inference behavior.
- `runWarmup`: Whether to run a warmup synthesis after preloading.

**Returns:** True if the character was successfully preloaded, false otherwise.
#### `IEnumerator TryPreloadCharacterCoroutine(string characterName, ModuleType moduleType, InferenceConfigOverride configOverride = null, bool runWarmup = true)`

Preload a character module for inference in a coroutine.

**Parameters:**

- `characterName`: The name of the character to preload.
- `moduleType`: The type of module to preload.
- `configOverride`: An optional InferenceConfigOverride to specify details such as which backend to load to.
- `runWarmup`: Whether to run a warmup synthesis after preloading.

**Returns:** An IEnumerator for coroutine execution.
#### `bool TryUnloadCharacter(string characterName, ModuleType moduleType, InferenceConfigOverride configOverride = null)`

Unload a character module from inference.

**Parameters:**

- `characterName`: The name of the character to unload.
- `moduleType`: The type of module to unload.
- `configOverride`: An optional InferenceConfigOverride to customize unload behavior. Setting preferredBackendType unload module on only that backend. Otherwise module will be unloaded on all backends.

**Returns:** True if the character was successfully unloaded, false otherwise.
#### `bool TryUnloadAll()`

Unload all character modules from inference.

**Returns:** True if all characters were successfully unloaded, false otherwise.