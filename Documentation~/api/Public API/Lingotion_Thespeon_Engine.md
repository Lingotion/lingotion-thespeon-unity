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

## Class `ThespeonEngine`

The ThespeonEngine class is responsible for managing the Thespeon synthesis and acts as an API endpoint to the Thespeon user. It provides methods to synthesize audio from ThespeonInput, preload and unload actors. The engine supports various inference configurations which combine to control Thespeons performance and resource allocation.
### Properties

#### `Action<float[], PacketMetadata> OnAudioReceived`

Event triggered when audio data is received. Action takes the current audio packet as a float array and its associated metadata as arguments.
#### `Action<PacketMetadata> OnSynthesisComplete`

Event triggered when synthesis is complete. Action takes the final packet Metadata as an argument. This means OnAudioReceived has been called with the final packet.
#### `Action<bool> OnPreloadComplete`

Event triggered when TryPreloadCoroutine is complete. Action takes bool result of preload as an argument.
### Methods

#### `void Synthesize(ThespeonInput input, string sessionID = "", InferenceConfigOverride configOverride = null)`

Synthesize audio from the provided ThespeonInput using the specified inference configuration. If a synthesis is already running, the request will be queued and executed when the current synthesis is complete.

**Parameters:**

- `input`: The ThespeonInput containing the text to synthesize.
- `sessionID`: An optional session ID for tracking the synthesis session.
- `configOverride`: An optional InferenceConfigOverride where each provided property overrides the existing default.
#### `bool TryPreloadActor(string actorName, ModuleType moduleType, InferenceConfigOverride configOverride = null, bool runWarmup = true)`

Preload an actor module for inference.

**Parameters:**

- `actorName`: The name of the actor to preload.
- `moduleType`: The type of module to preload.
- `configOverride`: An optional InferenceConfigOverride to customize the inference behavior.
- `runWarmup`: Whether to run a warmup synthesis after preloading.

**Returns:** True if the actor was successfully preloaded, false otherwise.
#### `IEnumerator TryPreloadActorCoroutine(string actorName, ModuleType moduleType, InferenceConfigOverride configOverride = null, bool runWarmup = true)`

Preload an actor module for inference in a coroutine.

**Parameters:**

- `actorName`: The name of the actor to preload.
- `moduleType`: The type of module to preload.
- `configOverride`: An optional InferenceConfigOverride to customize preload behavior.
- `runWarmup`: Whether to run a warmup synthesis after preloading.

**Returns:** An IEnumerator for coroutine execution.
#### `bool TryUnloadActor(string actorName, ModuleType moduleType)`

Unload an actor module from inference.

**Parameters:**

- `actorName`: The name of the actor to unload.
- `moduleType`: The type of module to unload.

**Returns:** True if the actor was successfully unloaded, false otherwise.
#### `bool TryUnloadAll()`

Unload all actor modules from inference.

**Returns:** True if all actors were successfully unloaded, false otherwise.