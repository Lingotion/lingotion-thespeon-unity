// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System;
using Newtonsoft.Json.Linq;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Utility class for detecting and validating config formats.
    /// </summary>
    public static class ConfigFormatDetector
    {
        /// <summary>
        /// Type identifier for character configs (Lara model).
        /// </summary>
        public const string LARA_TYPE = "lara";

        /// <summary>
        /// Type identifier for language/phonemizer configs.
        /// </summary>
        public const string PHONEMIZER_TYPE = "phonemizer";

        /// <summary>
        /// Checks if the config is a character (lara) config.
        /// </summary>
        /// <param name="config">The parsed JSON config.</param>
        /// <returns>True if the config type is "lara".</returns>
        public static bool IsCharacterConfig(JObject config)
        {
            return config?["type"]?.ToString() == LARA_TYPE;
        }

        /// <summary>
        /// Checks if the config is a language (phonemizer) config.
        /// </summary>
        /// <param name="config">The parsed JSON config.</param>
        /// <returns>True if the config type is "phonemizer".</returns>
        public static bool IsLanguageConfig(JObject config)
        {
            return config?["type"]?.ToString() == PHONEMIZER_TYPE;
        }

        /// <summary>
        /// Gets the type string from a config.
        /// </summary>
        /// <param name="config">The parsed JSON config.</param>
        /// <returns>The type string, or null if not present.</returns>
        public static string GetConfigType(JObject config)
        {
            return config?["type"]?.ToString();
        }

        /// <summary>
        /// Validates that the config is specifically a character (lara) config.
        /// </summary>
        /// <param name="config">The parsed JSON config.</param>
        /// <exception cref="NotSupportedException">Thrown when the config is not a lara config.</exception>
        public static void ValidateCharacterConfig(JObject config)
        {
            string type = GetConfigType(config);
            if (type != LARA_TYPE)
            {
                throw new NotSupportedException(
                    $"Expected character config type '{LARA_TYPE}', but got '{type}'. " +
                    "Please re-import your character using the latest format.");
            }
        }

        /// <summary>
        /// Validates that the config is specifically a language (phonemizer) config.
        /// </summary>
        /// <param name="config">The parsed JSON config.</param>
        /// <exception cref="NotSupportedException">Thrown when the config is not a phonemizer config.</exception>
        public static void ValidateLanguageConfig(JObject config)
        {
            string type = GetConfigType(config);
            if (type != PHONEMIZER_TYPE)
            {
                throw new NotSupportedException(
                    $"Expected language config type '{PHONEMIZER_TYPE}', but got '{type}'. " +
                    "Please re-import your language using the latest format.");
            }
        }
    }
}
