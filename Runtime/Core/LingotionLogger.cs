// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Static class for logging messages with different verbosity levels.
    /// </summary>
    public static class LingotionLogger
    {
        /// <summary>
        /// Current verbosity level for logging. Can be manually set for global logging control outside of Inference and Preload calls.
        /// Will be overridden by the InferenceConfig on inference or preload calls.
        /// </summary>
        public static VerbosityLevel CurrentLevel = new InferenceConfig().Verbosity;
        public static Action<string> Error = message => Log(message, VerbosityLevel.Error);
        public static Action<string> Warning = message => Log(message, VerbosityLevel.Warning);
        public static Action<string> Info = message => Log(message, VerbosityLevel.Info);
        public static Action<string> Debug = message => Log(message, VerbosityLevel.Debug);

        private static void Log(string message, VerbosityLevel level)
        {
            if (CurrentLevel >= level)
            {
                if (level == VerbosityLevel.Error)
                    UnityEngine.Debug.LogError($"[{level}] {message}");
                else if (level == VerbosityLevel.Warning)
                    UnityEngine.Debug.LogWarning($"[{level}] {message}");
                else
                    UnityEngine.Debug.Log($"[{level}] {message}");
            }
        }
    }

    
    /// <summary>
    /// Enumeration representing the verbosity level for logging and debugging.
    /// This can be used to control the amount of information logged during the synthesis process.
    /// None - turns off all logging.
    /// Error - logs only error messages.
    /// Warning - logs warning messages. These typically indicate potential issues that limit functionality but do not stop execution.
    /// Info - logs informational messages. These provide general information about the synthesis process.
    /// Debug - logs detailed debug messages. These are useful for troubleshooting and issue reporting to the Lingotion team.
    /// </summary>
    public enum VerbosityLevel
    {
        None = 1,
        Error = 2,
        Warning = 3,
        Info = 4,
        Debug = 5
    }
}
