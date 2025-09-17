# Lingotion.Thespeon.Inference API Documentation

## Class `InferenceResourceCleanup`

Handles the cleanup of resources and disposing of tensors/workers when Application.Quit() is called.
### Methods

#### `void CleanupResources()`

Cleans up and disposes all resources used by Thespeon.

## Class `InferenceSession<ModelInputType, InputSegmentType>`

Template for a new inference session.
### Methods

#### `void Dispose()`

Disposes the session and releases any resources.

## Class `InferenceWorkload`

Represents a workload for performing inference using a model runtime binding. This class manages the inputs and outputs of the model and handles the inference process.
### Constructors

#### `InferenceWorkload(ModelRuntimeBinding runtimeBinding)`

Initializes a new instance of the `InferenceWorkload` class with the specified model runtime binding.

**Parameters:**

- `runtimeBinding`: The model runtime binding containing the worker and model information.
### Methods

#### `void Dispose()`

Disposes of the worker and releases any resources it holds.
#### `IEnumerator InferAutoregressive(SessionTensorPool tensorPool, InferenceConfig config, Func<int, bool> doneCondition, string workloadmd5, bool skipFrames = true, string debugName = null, float budgetAdjustment = 1f)`

Runs this workload autoregressively until meeting a completion condition.

**Parameters:**

- `tensorPool`: The session tensor pool containing the tensors for inference.
- `config`: The inference configuration containing settings for the inference process.
- `doneCondition`: A function that returns true when the autoregressive process is done.
- `workloadmd5`: The MD5 hash of the workload, used for cleanup on fail.
- `skipFrames`: Whether to let the coroutine yield during inference or run in a single frame.
- `debugName`: Optional debug name for the workload, used for profiling.
- `budgetAdjustment`: Adjustment factor for the budget, used for adaptive scheduling and yielding logic.
#### `IEnumerator Infer(SessionTensorPool tensorPool, InferenceConfig config, Action<double> OnFinished, bool skipFrames = true, string debugName = null, bool fromAutoregessive = false, double budgetConsumed = 0d, float budgetAdjustment = 1f)`

Runs this workload.

**Parameters:**

- `tensorPool`: The session tensor pool containing the tensors for inference.
- `config`: The inference configuration containing settings for the inference process.
- `skipFrames`: Whether to let the coroutine yield during inference or run in a single frame.
- `debugName`: Optional debug name for the workload, used for profiling.
- `fromAutoregessive`: Indicates if this inference is part of an autoregressive process.
- `budgetAdjustment`: Adjustment factor for the budget, used for adaptive scheduling and yielding logic.
- `OnFinished`: Callback to invoke when the inference process is finished, providing the total time taken.

**Exceptions:**

- `Exception`: Thrown if an error occurs during the inference process.

## Class `InferenceWorkloadManager`

Singleton that keeps track of workloads. Responsible for disposing.
### Properties

#### `InferenceWorkloadManager Instance`

Singleton reference.
### Methods

#### `void RegisterModule(Module module, InferenceConfig config)`

Registers a module and its workloads if not already registered. If the module is already registered, it will not be registered again. This method is used to ensure that the module's workloads are available for inference.

**Parameters:**

- `module`: 
- `config`: 
#### `bool TryDeregisterModuleWorkloads(Module module)`

Deregisters a module and disposes of its workers if they are not in use.

**Parameters:**

- `module`: Module to deregister.

**Returns:** True if the module was successfully deregistered, false if it could not be deregistered.
#### `bool IsRegistered(Module module)`

Checks if a module is registered.

**Parameters:**

- `module`: Module to check.

**Returns:** True if the module is registered, false otherwise.
#### `InferenceWorkload AcquireWorkload(string md5)`

Flags workload as in use and returns a reference to it.

**Parameters:**

- `md5`: Target workload to request.
#### `void ReleaseWorkload(string md5)`

Flags worker as accessible.

**Parameters:**

- `md5`: Target worker to release.
#### `void ReleaseAllWorkloads()`

Releases all workloads, making them available for use again. This does not dispose of the workloads, only lets them be run again.
#### `void ReleaseAndDispose(string md5)`

Releases and disposes a workload with the given MD5. If the workload is not found, a warning will be issued to the LingotionLogger.

**Parameters:**

- `md5`: MD5 of the workload to release and dispose.

## Class `ModuleHandler`

Handles the registration and management of modules used in inference.
### Properties

#### `ModuleHandler Instance`

Singleton reference.
### Methods

#### `void RegisterModule<T>(ModuleEntry moduleEntry)`

Registers a new module of type T with the provided module entry.

**Parameters:**

- `moduleEntry`: The module entry containing the module information.
#### `T AcquireModule<T>(ModuleEntry moduleEntry)`

Acquires a module of type T using the provided module entry. If the module is not already registered, it will be created and registered.

**Parameters:**

- `moduleEntry`: The module entry containing the module information.

**Returns:** The acquired module of type T.

**Exceptions:**

- `InvalidCastException`: Thrown when a module with the same ID exists but has a different type.
#### `T DeregisterModule<T>(ModuleEntry moduleEntry)`

Deregisters and removes a module instance from the available modules.

**Parameters:**

- `moduleEntry`: The entry containing the ID of the module to be removed.

**Returns:** The deregistered module cast to type <typeparamref name="T"/> if found and the type matches; otherwise, returns `default(T)`.

**Exceptions:**

- `InvalidCastException`: Thrown if a module with the specified ID is found, but its actual type does not match the requested type <typeparamref name="T"/>.
#### `HashSet<string> GetNonOverlappingModelMD5s<T>(T module)`

Returns a set of MD5 hashes of all model files that are not used by any other module of the same type.

**Parameters:**

- `module`: The module to check for overlapping model MD5s.
#### `HashSet<string> GetNonOverlappingLangModules(ActorModule actorModule)`

Returns a set of language module IDs used by the provided actorModule that are not used by any other actor module.

**Parameters:**

- `actorModule`: The actor module to check for unused language modules.

**Returns:** A set of unused language module IDs.
#### `void Clear()`

Clears all registered modules.

## Class `SessionTensorPool`

A collection of Tensor objects passed around to Workloads during an InferenceSession. Handles all tensors as copies.
### Methods

#### `Tensor GetTensor(string identifier)`

Gets a Tensor by its identifier.

**Parameters:**

- `identifier`: The identifier of the tensor.

**Returns:** The Tensor associated with the identifier.

**Exceptions:**

- `InvalidOperationException`: Thrown if the tensor is not found.
#### `void SetTensor(string identifier, Tensor targetValue)`

Sets a Tensor in the pool, replacing any existing tensor with the same identifier.

**Parameters:**

- `identifier`: The identifier for the tensor.
- `targetValue`: The Tensor to set.

**Exceptions:**

- `InvalidOperationException`: Thrown if an error occurs while setting the tensor.
#### `void Dispose()`

Disposes of all tensors in the pool and clears the collection. This method releases all resources associated with the tensors.
#### `bool IsDisposed()`

Checks if the tensor pool has been disposed.

**Returns:** True if the pool is disposed, otherwise false.

## Class `ThespeonInference`

The ThespeonInference class handles the inference process for ThespeonInput, managing the setup of modules, input processing, and synthesized data distribution.
### Methods

#### `bool TrySetupModules(string actorName, ModuleType moduleType, InferenceConfig config)`

Sets up the modules required for inference based on the actor name and module type.

**Parameters:**

- `actorName`: The name of the actor to set up modules for.
- `moduleType`: The type of module to set up.
- `config`: The inference configuration to use.
#### `bool TryUnloadModule(string actorName, ModuleType moduleType)`

Unloads the specified module for the given actor.

**Parameters:**

- `actorName`: The name of the actor whose module should be unloaded.
- `moduleType`: The type of module to unload.
#### `IEnumerator Infer<T>(ThespeonInput input, InferenceConfig config, Action<ThespeonDataPacket<T>> callback, string sessionID, bool asyncDownload = true)`

Performs inference on the given ThespeonInput, processing the input and invoking the callback with the result.

**Parameters:**

- `input`: The ThespeonInput to process.
- `config`: The InferenceConfig to use for inference.
- `callback`: The callback to invoke with the result of the inference.
- `sessionID`: The session ID for the inference.
- `asyncDownload`: Whether to download tensors asynchronously.

**Returns:** An IEnumerator for coroutine execution.
#### `IEnumerator SetupModulesCoroutine(string actorName, ModuleType moduleType, InferenceConfig config)`

Coroutine to set up modules for inference in a coroutine.