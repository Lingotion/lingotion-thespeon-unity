// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using Lingotion.Thespeon.Core;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

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
            if (IsRegistered(module))
            {
                LingotionLogger.Debug($"Module {module.ModuleID} is already registered, skipping registration.");
                return;
            }
            Dictionary<string, ModelRuntimeBinding> models = module.CreateRuntimeBindings(_availableWorkers.Keys.ToHashSet(), config.PreferredBackendType);
            foreach ((string md5, ModelRuntimeBinding binding) in models)
            {
                _availableWorkers[md5] = new InferenceWorkload(binding);
                LingotionLogger.Debug($"Creating Workload {md5}, {binding.model.inputs}");
            }
        }

        public IEnumerator RegisterModuleCoroutine(Module module, InferenceConfig config)
        {
            if (IsRegistered(module))
            {
                yield break;
            }
            Dictionary<string, ModelRuntimeBinding> models = new();
            yield return module.CreateRuntimeBindingsCoroutine(_availableWorkers.Keys.ToHashSet(), config.PreferredBackendType, (result) => models = result);
            UnityEngine.Profiling.Profiler.BeginSample("Thespeon Done creating runtime bindings");
            foreach ((string md5, ModelRuntimeBinding binding) in models)
            {
                _availableWorkers[md5] = new InferenceWorkload(binding);
                LingotionLogger.Debug($"Creating Workload {md5}, {binding.model.inputs}");
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }

        /// <summary>
        /// Deregisters a module and disposes of its workers if they are not in use.
        /// </summary>
        /// <param name="module">Module to deregister.</param>
        /// <returns>True if the module was successfully deregistered, false if it could not be deregistered.</returns>
        public bool TryDeregisterModuleWorkloads(Module module)
        {
            if (!IsRegistered(module))
            {
                return true;
            }
            HashSet<string> workersToClear = ModuleHandler.Instance.GetNonOverlappingModelMD5s(module);
            HashSet<string> currentMD5s = module.GetAllFileMD5s();
            if (workersToClear.Any(md5 => workersInUse.Contains(md5)))
            {
                LingotionLogger.Warning($"Cannot deregister workloads from module {module.ModuleID} as it is still in use.");
                return false;
            }
            foreach (string md5 in workersToClear)
            {


                if (!TryDispose(md5)) return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if a module is registered.
        /// </summary>
        /// <param name="module">Module to check.</param>
        /// <returns>True if the module is registered, false otherwise.</returns>
        public bool IsRegistered(Module module)
        {
            return module.IsIncludedIn(_availableWorkers.Keys.ToHashSet());
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