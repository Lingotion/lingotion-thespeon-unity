// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using Unity.InferenceEngine;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Configuration settings for the inference engine.
    /// </summary>
    public class InferenceConfig
    {
        public BackendType PreferredBackendType = BackendType.CPU;
#if UNITY_IOS || UNITY_ANDROID
        public double TargetBudgetTime { get; set; } = 0.01;
        public double TargetFrameTime { get; set; } = 0.0333d; // 30 FPS
        public float BufferSeconds { get; set; } = 0.5f; // 500 ms
#else
        public double TargetBudgetTime { get; set; } = 0.005;
        public double TargetFrameTime { get; set; } = 0.0167d; // 60 FPS
        public float BufferSeconds { get; set; } = 0.5f; // 500 ms
#endif
        public bool UseAdaptiveScheduling { get; set; } = true;
        public float OvershootMargin { get; set; } = 1.4f;
        public int MaxSkipLayers { get; set; } = 20;
        public ModuleType ModuleType { get; set; } = ModuleType.L;
        public Emotion FallbackEmotion { get; set; } = Emotion.Interest;
        public ModuleLanguage FallbackLanguage { get; set; } = new ModuleLanguage("eng");
        public VerbosityLevel Verbosity { get; set; } = VerbosityLevel.Error;
    }
}
