// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.Inference;

namespace Lingotion.Thespeon.Editor
{
    [InitializeOnLoad]
    /// <summary>
    /// Handles the cleanup of resources and disposing of tensors/workers when 
    /// the editor is set from play mode or quitting.
    /// </summary>
    public static class EditorResourceCleanup
    {
        static EditorResourceCleanup()
        {
            EditorApplication.quitting += CleanupResources;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupResources;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Get all active instances of ThespeonEngine
                var components = Object.FindObjectsByType<ThespeonEngine>(FindObjectsSortMode.None);
                foreach (var comp in components)
                {
                    comp.StopAllCoroutines();
                }
                CleanupResources();
            }
        }

        /// <summary>
        /// Cleans up and disposes all resources used by Thespeon.
        /// </summary>
        public static void CleanupResources()
        {
            InferenceResourceCleanup.CleanupResources();
        }
    }
}
#endif