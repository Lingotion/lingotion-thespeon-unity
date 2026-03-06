// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEngine;
using Unity.InferenceEngine;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// A ScriptableObject that holds default values for <see cref="InferenceConfig"/>.
    /// Users may edit these in the Inspector to override hard-coded defaults project-wide.
    /// At runtime, InferenceConfig's default constructor reads from this asset via Resources.Load.
    /// Place the asset at Assets/Lingotion Thespeon/Resources/ThespeonDefaultSettings.asset.
    /// </summary>
    public class ThespeonDefaultSettings : ScriptableObject
    {
        private const string ResourcePath = "ThespeonDefaultSettings";

        private static ThespeonDefaultSettings _instance;

        /// <summary>
        /// Returns the singleton instance loaded from Resources. Falls back to a transient
        /// instance with hard-coded defaults if no asset is found.
        /// </summary>
        public static ThespeonDefaultSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ThespeonDefaultSettings>(ResourcePath);
                    if (_instance == null)
                    {
                        // No asset found — create a transient instance with hard-coded defaults.
                        _instance = CreateInstance<ThespeonDefaultSettings>();
                        _instance.ResetToHardCodedDefaults();
                    }
                }
                return _instance;
            }
        }

        [Header("Backend")]
        [Tooltip("Enter your preferred default backend for synthesis. Applies when no local backend is specified in the synthesis config.")]
        public SupportedBackendType PreferredBackendType = SupportedBackendType.CPU;

        [Header("Scheduling")]
        [Tooltip("Target budget time per frame (seconds) on desktop platforms.")]
        public double DesktopTargetBudgetTime = 0.005;

        [Tooltip("Target frame time (seconds) on desktop platforms.")]
        public double DesktopTargetFrameTime = 0.0167d; // 60 FPS

        [Tooltip("Target budget time per frame (seconds) on mobile platforms.")]
        public double MobileTargetBudgetTime = 0.01;

        [Tooltip("Target frame time (seconds) on mobile platforms.")]
        public double MobileTargetFrameTime = 0.0333d; // 30 FPS

        // Runtime-resolved properties
        public double TargetBudgetTime => IsMobilePlatform
            ? MobileTargetBudgetTime
            : DesktopTargetBudgetTime;

        public double TargetFrameTime => IsMobilePlatform
            ? MobileTargetFrameTime
            : DesktopTargetFrameTime;

        [Tooltip("Seconds of audio to buffer before releasing. Higher values may increase latency but reduce risk of stuttering in constrained environments.")]
        public float BufferSeconds = 0.5f;

        [Tooltip("Enable adaptive scheduling for dynamic frame adjustment.")]
        public bool UseAdaptiveScheduling = true;

        [Tooltip("Determines how much the adaptive scheduler can overshoot the target budget time before taking corrective action (>1).")]
        public float OvershootMargin = 1.4f;

        [Tooltip("Maximum extra yields per subtask for the adaptive scheduler.")]
        public int MaxSkipLayers = 20;

        [Header("Defaults")]
        [Tooltip("Enter your preferred default module type for synthesis. Applies when no module type is specified in the synthesis config.")]
        public ModuleType ModuleType = ModuleType.L;

        [Tooltip("Enter your preferred default emotion for synthesis. Applies when no emotion is specified in the synthesis config.")]
        public Emotion FallbackEmotion = Emotion.Interest;

        [Tooltip("Enter your preferred default language for synthesis. Applies when no language is specified in the synthesis config.")]
        public string FallbackLanguageCode = "eng";

        [Header("Logging")]
        [Tooltip("Sets the verbosity level for Lingotion Thespeon logging. Messages with a more detailed level than this will be ignored.")]
        public VerbosityLevel Verbosity = VerbosityLevel.Error;

        /// <summary>
        /// Resets all fields to the original hard-coded defaults.
        /// </summary>
        public void ResetToHardCodedDefaults()
        {
            PreferredBackendType = SupportedBackendType.CPU;
            DesktopTargetFrameTime = 0.0167d;
            DesktopTargetBudgetTime = 0.005;
            MobileTargetFrameTime = 0.0333d;
            MobileTargetBudgetTime = 0.01;
            BufferSeconds = 0.5f;
            UseAdaptiveScheduling = true;
            OvershootMargin = 1.4f;
            MaxSkipLayers = 20;
            ModuleType = ModuleType.L;
            FallbackEmotion = Emotion.Interest;
            FallbackLanguageCode = "eng";
            Verbosity = VerbosityLevel.Error;
        }

        public static bool IsMobilePlatform
        {
            get
            {
        #if UNITY_EDITOR
                var target = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
                return target == UnityEditor.BuildTarget.Android ||
                    target == UnityEditor.BuildTarget.iOS;
        #else
                return Application.isMobilePlatform;
        #endif
            }
        }
    }

    public enum SupportedBackendType
    {
        CPU = BackendType.CPU,
        GPUCompute = BackendType.GPUCompute
    }

    public static class SupportedBackendTypeExtensions
    {
        public static BackendType ToBackendType(this SupportedBackendType supportedBackend)
        {
            return supportedBackend switch
            {
                SupportedBackendType.CPU => BackendType.CPU,
                SupportedBackendType.GPUCompute => BackendType.GPUCompute,
                _ => throw new System.ArgumentOutOfRangeException(nameof(supportedBackend))
            };
        }
    }
}