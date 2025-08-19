// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.
using Lingotion.Thespeon.Core;
using Newtonsoft.Json;
using Unity.InferenceEngine;

namespace Lingotion.Thespeon.Engine
{
    /// <summary>
    /// Represents a configuration for inference sessions. All provided non-null fields will override the existing default values.
    /// This class allows for customization of inference settings such as backend type, Thespeon frame budget time, and scheduling options.
    /// </summary>
    public class InferenceConfigOverride
    {
        /// <summary>
        /// Specifies the preferred backend for model inference. 
        /// Default: CPU
        /// </summary>
        /// <remarks>
        /// Determines which execution backend Thespeon will use. 
        /// Currently supported options are GPUCompute and CPU.
        /// Note that parts of Thespeon will always run on CPU, regardless of preferred backend.
        /// </remarks>
        [JsonProperty("preferredBackendType")]
        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public BackendType PreferredBackendType { get; set; }

        /// <summary>
        /// Defines the target budget time in seconds per frame allocated to Thespeon. 
        /// Default: 10ms on IOS/Android, 5ms otherwise.
        /// </summary>
        /// <remarks>
        /// This value dictates how much of each frame Thespeon aims to use during synthesis, letting you control the tradeoff between latency and performance impact.
        /// </remarks>
        [JsonProperty("targetBudgetTime")]
        public double? TargetBudgetTime { get; set; }

        /// <summary>
        ///  Defines the target maximum total frame time making Thespeon underutilize its allocated budget time if the current frame time exceeds this value.
        /// Default: 33.3 ms (30 fps) on IOS/Android, 16.7 ms (60 fps) otherwise
        /// </summary>
        /// <remarks>
        /// Helps enforce the set frame rate of your game. Note that this is a soft limit and may be violated.
        /// </remarks>
        [JsonProperty("targetFrameTime")]
        public double? TargetFrameTime { get; set; }

        /// <summary>
        /// Specifies how many seconds of data to generate before releasing the data to the caller. Useful for real time streaming to the audio thread.
        /// Default: 0.5s on IOS/Android, 0.1s otherwise
        /// </summary>
        /// <remarks>
        /// This buffer can help midigate impact of resource variation and prevent stuttering in audio. Larger value means longer latency to start of stream but lower risk of stuttering.
        /// </remarks>
        [JsonProperty("bufferSeconds")]
        public float? BufferSeconds { get; set; }

        /// <summary>
        /// Enables or disables adaptive scheduling to dynamically adjust computation load per frame over time.
        /// Default: True
        /// </summary>
        /// <remarks>
        /// When true, the engine will adjust computation load per frame based on real-time performance metrics,
        /// potentially yielding to a frame before meeting the budget or frame time constraints.
        /// </remarks>
        [JsonProperty("useAdaptiveScheduling")]
        public bool? UseAdaptiveScheduling { get; set; }

        /// <summary>
        /// Value larger than 1 which determines the aggressiveness of the adaptive scheduler. A larger value is more lenient with interfering
        /// Default: 1.4
        /// </summary>
        [JsonProperty("overshootMargin")]
        public float? OvershootMargin { get; set; }

        /// <summary>
        /// Limits how many extra yields that can be added per subtask by the adaptive scheduler.
        /// Default: 20
        /// </summary>
        /// <remarks>
        /// Used to set hard limit on the adaptive scheduler's agressiveness. A large value can make synthesis latency increase over time.
        /// </remarks>
        [JsonProperty("maxSkipLayers")]
        public int? MaxSkipLayers { get; set; }

        /// <summary>
        /// Locally controls the verbosity level of the package logger called, LingotionLogger.
        /// </summary>
        /// <remarks>
        /// Higher verbosity levels provide more detailed logs for debugging or profiling.
        /// Set to 0 (None) for production.
        /// </remarks>
        [JsonProperty("verbosity")]
        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public VerbosityLevel Verbosity { get; set; }

        public InferenceConfig GenerateConfig()
        {
            InferenceConfig resultingConfig = new();
            resultingConfig.PreferredBackendType = PreferredBackendType != 0 ? PreferredBackendType : resultingConfig.PreferredBackendType;
            resultingConfig.TargetBudgetTime = TargetBudgetTime ?? resultingConfig.TargetBudgetTime;
            resultingConfig.TargetFrameTime = TargetFrameTime ?? resultingConfig.TargetFrameTime;
            resultingConfig.BufferSeconds = BufferSeconds ?? resultingConfig.BufferSeconds;
            resultingConfig.UseAdaptiveScheduling = UseAdaptiveScheduling ?? resultingConfig.UseAdaptiveScheduling;
            resultingConfig.OvershootMargin = OvershootMargin ?? resultingConfig.OvershootMargin;
            resultingConfig.MaxSkipLayers = MaxSkipLayers ?? resultingConfig.MaxSkipLayers;
            resultingConfig.Verbosity = Verbosity != 0 ? Verbosity : resultingConfig.Verbosity;

            return resultingConfig;
        }
    }

}
