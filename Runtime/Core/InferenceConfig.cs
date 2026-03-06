// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.
using Unity.InferenceEngine;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Configuration settings for the inference engine.
    /// </summary>
    public class InferenceConfig
    {
        public BackendType PreferredBackendType;
        public double TargetBudgetTime { get; set; }
        public double TargetFrameTime { get; set; }
        public float BufferSeconds { get; set; }
        public bool UseAdaptiveScheduling { get; set; }
        public float OvershootMargin { get; set; }
        public int MaxSkipLayers { get; set; }
        public ModuleType ModuleType { get; set; }
        public Emotion FallbackEmotion { get; set; }
        public ModuleLanguage FallbackLanguage { get; set; }
        public VerbosityLevel Verbosity { get; set; }

        public InferenceConfig()
        {
                ThespeonDefaultSettings defaults = ThespeonDefaultSettings.Instance;
                PreferredBackendType = defaults.PreferredBackendType.ToBackendType();
                TargetBudgetTime     = defaults.TargetBudgetTime;
                TargetFrameTime      = defaults.TargetFrameTime;
                BufferSeconds        = defaults.BufferSeconds;
                UseAdaptiveScheduling = defaults.UseAdaptiveScheduling;
                OvershootMargin      = defaults.OvershootMargin;
                MaxSkipLayers        = defaults.MaxSkipLayers;
                ModuleType           = defaults.ModuleType;
                FallbackEmotion      = defaults.FallbackEmotion;
                FallbackLanguage     = new ModuleLanguage(defaults.FallbackLanguageCode);
                Verbosity            = defaults.Verbosity;
        }
    }
}
