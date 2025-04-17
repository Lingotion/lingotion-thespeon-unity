// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using Newtonsoft.Json;
using UnityEngine;
namespace Lingotion.Thespeon.API
{
    public class PackageConfig
    {

        #nullable enable
        // [JsonProperty("verbosity", NullValueHandling = NullValueHandling.Ignore)]
        // public VerbosityLevel verbosity { get; set; }
        [JsonProperty("useAdaptiveFrameBreakScheduling", NullValueHandling = NullValueHandling.Ignore)]
        public bool? useAdaptiveFrameBreakScheduling { get; set; }
        // [JsonProperty("heavyLayerCullingType", NullValueHandling = NullValueHandling.Ignore)]
        // public string heavyLayerCullingType { get; set; }
        [JsonProperty("targetFrameTime", NullValueHandling = NullValueHandling.Ignore)]
        public double? targetFrameTime { get; set; }

        [JsonProperty("overshootMargin", NullValueHandling = NullValueHandling.Ignore)]
        public float? overshootMargin { get; set; }
    
        // [JsonProperty("defaultEmotion", NullValueHandling = NullValueHandling.Ignore)]
        // public string defaultEmotion { get; set; }        
        // [JsonProperty("defaultSpeed", NullValueHandling = NullValueHandling.Ignore)]
        // public string defaultSpeed { get; set; }
        // [JsonProperty("defaultLoudness", NullValueHandling = NullValueHandling.Ignore)]
        // public string defaultLoudness { get; set; }
        // [JsonProperty("backendTypes", NullValueHandling = NullValueHandling.Ignore)]
        // public Dictionary<string,BackendType> backendTypes { get; set; }
        #nullable disable


        
        /// <summary> 
        /// Initializes a new empty instance of the PackageConfig class.
        /// </summary>
        public PackageConfig() {}

        /// <summary>
        /// Copy constructor for PackageConfig.
        /// </summary>
        /// <param name="config">The PackageConfig instance to copy from.</param>
        public PackageConfig(PackageConfig config)
        {
            useAdaptiveFrameBreakScheduling = config.useAdaptiveFrameBreakScheduling;                
            targetFrameTime = config.targetFrameTime;
            overshootMargin = config.overshootMargin;
        }

        /// <summary>
        /// Sets the configuration values from another PackageConfig instance by overwriting all non-null values in overrideConfig and returns the new instance. A validation of config values with eventual revision also takes place.
        /// </summary>
        /// <param name="overrideConfig">The PackageConfig instance to override values from.</param>
        /// <returns>A new PackageConfig instance with the overridden values.</returns>
        public PackageConfig SetConfig(PackageConfig overrideConfig)
        {
            if (overrideConfig == null)
                return this;
            //oldConfig = null;
            PackageConfig newConfig = new PackageConfig(this);

            var type = typeof(PackageConfig);
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                var overrideValue = prop.GetValue(overrideConfig);
                if (overrideValue != null)
                {
                    prop.SetValue(newConfig, overrideValue);
                }
            }

            newConfig.ValidateAndRevise();
            return newConfig;
        }


        
        /// <summary>
        /// Validates the configuration values and revises them in place if invalid.
        /// </summary>
        private void ValidateAndRevise(){
            if(targetFrameTime <= 0) 
            {
                Debug.LogWarning("targetFrameTime cannot be non-positive. Setting it to 5ms.");
                targetFrameTime = 0.005f;
            }
            if(overshootMargin <= 1) 
            {
                Debug.LogWarning("overshootMargin cannot be less than 1. Setting it to 1.");
                overshootMargin = 1f;
            }

        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }



        // public enum VerbosityLevel
        // {
        //     None = 0,
        //     Error = 1,
        //     Warning = 2,
        //     Info = 3,
        //     Debug = 4
        // }

    }
}