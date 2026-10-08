// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using Lingotion.Thespeon.Core;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Unity.InferenceEngine;

namespace Lingotion.Thespeon.Inference
{
    /// <summary>
    /// Singleton that keeps track of workloads. Responsible for disposing.
    /// </summary>
    public class InferenceWorkloadManager
    {
        private static InferenceWorkloadManager _instance;
        /// <summary>
        /// Singleton reference.
        /// </summary>
        public static InferenceWorkloadManager Instance => _instance ??= new InferenceWorkloadManager();

        private Dictionary<string, InferenceWorkload> _availableWorkers;
        private HashSet<string> workersInUse;

        private InferenceWorkloadManager()
        {
            _availableWorkers = new Dictionary<string, InferenceWorkload>();
            workersInUse = new HashSet<string>();
        }

        /// <summary>
        /// Registers a module and its workloads if not already registered.
        /// If the module is already registered, it will not be registered again.
        /// This method is used to ensure that the module's workloads are available for inference.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="config"></param>
        public void RegisterModule(Module module, InferenceConfig config)
        {
            if (IsRegistered(module, config.PreferredBackendType))
            {
                LingotionLogger.Debug($"Module {module.ModuleID} is already registered, skipping registration.");
                return;
            }
            Dictionary<string, ModelRuntimeBinding> models = module.CreateRuntimeBindings(_availableWorkers.Keys.ToHashSet(), config.PreferredBackendType);
            foreach ((string md5, ModelRuntimeBinding binding) in models)
            {
                _availableWorkers[md5] = new InferenceWorkload(binding);
                LingotionLogger.Info($"Creating Workload {md5}, inputs=[{string.Join(", ", binding.model.inputs.Select(modelInput => $"{modelInput.name}:{modelInput.dataType}"))}]");
            }
            module.AddLoadedBackend(config.PreferredBackendType);
        }

        public IEnumerator RegisterModuleCoroutine(Module module, InferenceConfig config)
        {
            if (IsRegistered(module, config.PreferredBackendType))
            {
                yield break;
            }
            Dictionary<string, ModelRuntimeBinding> models = new();
            var createBindings = module.CreateRuntimeBindingsCoroutine(_availableWorkers.Keys.ToHashSet(), config.PreferredBackendType, (result) => models = result);
            while (createBindings.MoveNext()) { yield return createBindings.Current; }
            UnityEngine.Profiling.Profiler.BeginSample("Thespeon Done creating runtime bindings");
            foreach ((string md5, ModelRuntimeBinding binding) in models)
            {
                _availableWorkers[md5] = new InferenceWorkload(binding);
                LingotionLogger.Info($"Creating Workload {md5}, inputs=[{string.Join(", ", binding.model.inputs.Select(modelInput => $"{modelInput.name}:{modelInput.dataType}"))}]");
            }
            module.AddLoadedBackend(config.PreferredBackendType);
            UnityEngine.Profiling.Profiler.EndSample();
        }

        /// <summary>
        /// Deregisters a module and disposes of its workers if they are not in use.
        /// </summary>
        /// <param name="module">Module to deregister.</param>
        /// <param name="backend">Optional backend to deregister for. If null, will attempt to deregister for all backends.</param>
        /// <returns>True if the module was successfully deregistered, false if it could not be deregistered.</returns>
        public bool TryDeregisterModuleWorkloads(Module module, BackendType? backend = null)
        {
            if (!IsRegistered(module, backend))
            {
                return true;
            }
            if(backend.HasValue && !module.GetLoadedBackends().Contains(backend.Value))
            {
                LingotionLogger.Warning($"Module {module.ModuleID} is not loaded on backend {backend.Value}, skipping deregistration for this backend.");
                return true;
            }
            List<BackendType> backendTypesToCheck = backend.HasValue ? new List<BackendType> { backend.Value } : module.GetLoadedBackends().ToList();
            foreach (BackendType backendType in backendTypesToCheck)
            { 
                HashSet<string> workersToClear = ModuleHandler.Instance.GetWorkloadIDsToRemove(module, backendType);
                if (workersToClear.Any(workersInUse.Contains))
                {
                    LingotionLogger.Error($"Cannot deregister workloads from module {module.ModuleID} as it is still in use.");
                    return false;
                }
                foreach (string md5 in workersToClear)
                {
                    if (!TryDispose(md5)) return false;
                    LingotionLogger.Info($"Deregistering workload {md5} from module {module.ModuleID} on backend {backendType}.");
                }
                module.RemoveLoadedBackend(backendType);
            }
            return true;
        }

        /// <summary>
        /// Checks if a module's workloads are registered with the given backend. If BackendType is None, checks if registered for any backend.
        /// </summary>
        /// <param name="module">Module to check.</param>
        /// <param name="backend">Backend to check for. If null, checks if registered for any backend.</param>
        /// <returns>True if the module is registered, false otherwise.</returns>
        public bool IsRegistered(Module module, BackendType? backend = null)
        {
            HashSet<BackendType> backendsToCheck = backend.HasValue ? new HashSet<BackendType> { backend.Value } : module.GetLoadedBackends();
            HashSet<string> availableKeys = _availableWorkers.Keys.ToHashSet();
            foreach (BackendType backendType in backendsToCheck)
            {
                if (!module.GetLoadedBackends().Contains(backendType))
                {
                    LingotionLogger.Debug($"Module {module.ModuleID} is not loaded on backend {backendType}.");
                    continue;
                }
                if (module.IsIncludedIn(availableKeys, backendType))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks whether a workload has been registered, regardless of whether it is in use.
        /// </summary>
        /// <param name="workloadID">The WorkloadID to look for.</param>
        /// <returns>True if a workload with that ID is registered, false otherwise.</returns>
        public bool HasWorkload(string workloadID)
        {
            return _availableWorkers.ContainsKey(workloadID);
        }

        /// <summary>
        /// Flags workload as in use and returns a reference to it.
        /// </summary>
        /// <param name="md5">Target workload to request.</param>
        /// <returns></returns>
        public bool AcquireWorkload(string md5, ref InferenceWorkload acquiredWorkload)
        {
            if (workersInUse.Contains(md5))
            {
                LingotionLogger.Debug($"Workload {md5} already in use.");
                acquiredWorkload = null;
                return false;
            }
            workersInUse.Add(md5);
            acquiredWorkload = _availableWorkers[md5];
            return true;
        }

        /// <summary>
        /// Flags worker as accessible.
        /// </summary>
        /// <param name="md5">Target worker to release.</param>
        public void ReleaseWorkload(string md5)
        {
            workersInUse.Remove(md5);
        }

        /// <summary>
        /// Releases all workloads, making them available for use again.
        /// This does not dispose of the workloads, only lets them be run again.
        /// </summary>
        public void ReleaseAllWorkloads()
        {
            workersInUse.Clear();
        }

        public void DisposeAndClearAll()
        {
            if (workersInUse.Count > 0)
            {
                LingotionLogger.Error("Workloads are still in use, release them before calling DisposeAndClearAll.");
            }

            foreach (InferenceWorkload step in _availableWorkers.Values)
            {
                step.Dispose();
            }

            _availableWorkers.Clear();
        }

        /// <summary>
        /// Releases and disposes a workload with the given MD5.
        /// If the workload is not found, a warning will be issued to the LingotionLogger.
        /// </summary>
        /// <param name="md5">MD5 of the workload to release and dispose.</param>
        public void ReleaseAndDispose(string md5)
        {
            if (_availableWorkers.TryGetValue(md5, out InferenceWorkload workload))
            {
                workload.Dispose();
                _availableWorkers.Remove(md5);
                workersInUse.Remove(md5);
            }
            else
            {
                LingotionLogger.Warning($"Workload with MD5: {md5} not found.");
            }
        }

        private bool TryDispose(string md5)
        {
            if (workersInUse.Contains(md5))
            {
                LingotionLogger.Warning($"Cannot dispose workload with MD5: {md5} as it is currently in use.");
                return false;
            }
            if (_availableWorkers.TryGetValue(md5, out InferenceWorkload workload))
            {
                workload.Dispose();
                _availableWorkers.Remove(md5);
            }
            return true;
        }
    }
}