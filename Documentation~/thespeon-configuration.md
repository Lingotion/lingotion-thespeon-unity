
# **Configuration and Performance Tuning Manual**

---

## Table of Contents

- [**Overview**](#overview)
- [**Using the InferenceConfigOverride Class**](#using-the-inferenceconfigoverride-class)
	- [Purpose and Overview](#purpose-and-overview)
	- [Properties and Use](#properties-and-usage)
- [**Example**](#example)


---
# Overview
This is an in-depth manual on configurable parameters for Thespeon, and how you can use it to optimize the engine according to the needs of your Unity Project. 
 
In broad strokes, the largest performance impact during synthesis comes from the choice of `ModuleType`, which serves as the main tradeoff between quality of acting and memory and computing performance. But once selected, the [**InferenceConfigOverride**](./api/Public%20API/Lingotion_Thespeon_Engine.md#class-inferenceconfigoverride) class is the tool used to tweak the performance of Thespeon.

---
## Using the InferenceConfigOverride Class

In this section you will find a detailed description of the `InferenceConfigOverride` class and its purpose in your Unity Project. For a lighter overview see the [API documentation](./api/Public%20API/Lingotion_Thespeon_Engine.md#class-inferenceconfigoverride).

### Purpose and Overview
In the Lingotion Thespeon Unity package, many default parameter decisions are hidden within the `InferenceConfig` class. These defaults are designed for general performance across platforms and may not suit your specific context.

The `InferenceConfigOverride` structure allows you to selectively override these defaults with your own tuned values, enabling better performance tuning tailored to your project. Typically, an `InferenceConfigOverride` instance is passed to the `Synthesize` and `Preload` methods in `ThespeonEngine`.
All fields are optional — you only need to supply the ones you want to change. Unspecified fields will retain their existing default values.
> [!NOTE]
> Thespeon synthesis is designed to run in a Coroutine at `EndOfFrame`. This ensures that most of your game logic executes before Thespeon, meaning Thespeon is often the operation dictating the frame time length during synthesis. By tuning the configuration parameters you may control how this happens.

---

### Properties and Use
In the current version of the Lingotion Thespeon Engine, the following fields are available in `InferenceConfigOverride` — all of them optional.
Only explicitly provided fields will override existing defaults; this means you do *not* need to re-specify values you are not changing.

#### `PreferredBackendType` (`Unity.InferenceEngine.BackendType`)

**Default**:

* `CPU`

Determines whether heavy computation executes on the CPU using Unity Burst or on the GPU using compute shaders. Offloading to GPUCompute alleviates CPU load and vice versa, letting you tailor Thespeon to your project's bottlenecks.

> [!NOTE]
> Certain critical Thespeon operations **always** run on the CPU, regardless of backend.
> The Unity Inference Engine _GPUPixel_ backend is **not supported**.


#### `TargetBudgetTime` (`double?`)

**Default**:

* `0.01` seconds on mobile
* `0.005` seconds otherwise

Allocates a soft time budget for how much time Thespeon is allowed to use per frame. This determines for how long Thespeon may execute before yielding control to the main thread.

The limit is enforced softly (i.e., it may occasionally be exceeded). An adaptive scheduling algorithm (if enabled) learns over time to keep execution within this budget.

> [!CAUTION]
> Setting this too low may significantly increase latency or cause generation slower than real-time.

#### `TargetFrameTime` (`double?`)
**Default**:
* `0.0333` seconds (\~30 FPS) on mobile
* `0.0167` seconds (\~60 FPS) otherwise
This is the total target time allowed for an entire frame, including everything from game logic to rendering. Even if the `TargetBudgetTime` has not been exceeded, Thespeon will voluntarily yield if this frame time threshold is close to being hit — ensuring it doesn't cause frame drops.
Internally, Thespeon uses **90% of this value** to determine if the time is exceeded. This is to give room for final operations to complete without breaching the target time.
> [!CAUTION]
> Excessively small values can cause frequent yields, resulting in increased generation latency and may cause generation slower than real-time.

#### `BufferSeconds` (`float?`)

**Default**:

* `0.5` seconds on mobile
* `0.1` seconds otherwise

Controls how much data (in seconds) Thespeon should generate before streaming packets to the consumer. This value is useful for real-time consumption such as streaming audio directly to the audio thread. A longer buffer improves stability in low-resource or jitter-prone conditions, while a shorter buffer reduces latency. For non real-time applications or situations where resources are abundant enough to enable continuous real-time generation this may be set to zero to start receiving data as soon as possible. 

> [!CAUTION]
> Too small a buffer can lead to stuttering in streamed audio, if audio is consumed faster than Thespeon can generate it.

#### `UseAdaptiveScheduling` (`bool?`)

**Default**: `true`

Enables or disables Thespeon’s adaptive Coroutine scheduling algorithm. When enabled, the engine uses runtime metrics to determine when to yield execution during synthesis in addition to the normal heuristic-based scheduler.

This behavior helps avoid overshooting the `TargetBudgetTime` and balances frame stability against latency. Its agressiveness can be controlled by tuning the `OvershootMargin` and `MaxSkipLayers`.

> [!CAUTION]
> Disabling the adaptive scheduling may make frame pacing less reliable under load, though it may reduce synthesis latency.

#### `OvershootMargin` (`float?`)

**Default**: `1.4`

A multiplier (minimum value of `1.0`) that determines how aggressively the adaptive scheduler avoids overshooting the `TargetBudgetTime`.

* A **larger** value makes the scheduler more tolerant of occasional spikes.
* A **smaller** value makes it more aggressive and inclined to yield more often.


#### `MaxSkipLayers` (`int?`)

**Default**: `20`

Defines a hard limit for the number of *extra Coroutine suspensions* (i.e., yields to the main thread) that the adaptive scheduler may introduce per subtask. This prevents overly fragmented execution.

> [!NOTE]
> Higher values gives the scheduler more freedom to break work into smaller units, over time lowering resources consumed per frame but increasing latency.

#### `Verbosity` (`VerbosityLevel`)

**Default**: `Error`

Controls the level of log output produced by Thespeon. This setting determines the amount of diagnostic and informational data emitted to logs, which can be helpful during development and for support case submissions. 
A method which takes a config object will log with the specified `VerbosityLevel` throughout its execution before resetting to the global default value on completion.

The available levels in order of increasing verbosity are:
* `None` — No logs will be emitted.
* `Error` — Only critical errors and faults are logged (default).
* `Warning` — Includes potential issues that don’t interrupt execution.
* `Info` — General status updates and high-level operations.
* `Debug` — Detailed updates on low level events.

> [!NOTE]
> A higher verbosity level can assist in debugging, but may slightly impact performance or clutter logs in production environments.

---

## Example
You can find a working usage example in the Configuration Example sample included in the package. Open Lingotion Thespeon in the Package Manager, import the sample and inspect the source code. An `InferenceConfigOverride` instance can also be created by parsing a JSON file, as shown in the example below. It can also be serialized back into a JSON string and saved to a file.

```csharp
using Newtonsoft.Json;


string jsonContent = System.IO.File.ReadAllText("path/to/file.json");
InferenceConfigOverride configOverride = JsonConvert.DeserializeObject<InferenceConfigOverride>(jsonContent);
```

with a file.json looking like this example
```
{
  "preferredBackendType": "CPU",
  "targetBudgetTime": 0.003,
  "targetFrameTime": 0.012,
  "verbosity": "Info"
}
```
