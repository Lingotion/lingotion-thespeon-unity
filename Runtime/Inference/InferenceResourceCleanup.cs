// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.LanguagePack;

namespace Lingotion.Thespeon.Inference
{
    /// <summary>
    /// Handles the cleanup of resources and disposing of tensors/workers when 
    /// Application.Quit() is called.
    /// </summary>
    public static class InferenceResourceCleanup
    {
        [RuntimeInitializeOnLoadMethod]
        static void RegisterCleanup()
        {
            LingotionLogger.Debug("Initializing runtime resource cleanup hook");
            Application.quitting += CleanupResources;
        }


        /// <summary>
        /// Cleans up and disposes all resources used by Thespeon.
        /// </summary>
        public static void CleanupResources()
        {
            LingotionLogger.Debug("Runtime resource cleanup starting...");
            InferenceWorkloadManager.Instance.ReleaseAllWorkloads();
            InferenceWorkloadManager.Instance.DisposeAndClearAll();
            ModuleHandler.Instance.Clear();
            LookupTableHandler.Instance.DisposeAndClear();
            LingotionLogger.Debug("Runtime resource cleanup finished.");
        }
    }
}