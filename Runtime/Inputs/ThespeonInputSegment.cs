// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Core.IO;
using Newtonsoft.Json.Linq;

namespace Lingotion.Thespeon.Inputs
{

    /// <summary>
    /// Represents a segment of input for Thespeon, containing text, language, dialect, emotion, and custom pronunciation flag.
    /// </summary>
    public class ThespeonInputSegment : ModelInputSegment
    {

        /// <summary>
        /// The main constructor for ThespeonInputSegment.
        /// Initializes a new instance of ThespeonInputSegment with the specified text, emotion, language, and custom pronunciation flag.
        /// </summary>
        /// <param name="text">The text of the segment. Cannot be null or empty</param>
        /// <param name="language">The language of the segment. Optional.</param>
        /// <param name="dialect">The dialect of the segment. Optional.</param>
        /// <param name="emotion">The emotion associated with the segment. Optional</param>
        /// <param name="isCustomPronounced">Indicates whether the segment consists of only IPA text. Optional, defaults to false.</param>
        /// <exception cref="System.ArgumentException">Thrown if the text is null or empty.</exception>
        public ThespeonInputSegment(string text, string language = null, string dialect = null, Emotion emotion = Emotion.None, bool isCustomPronounced = false) : base(text, language, dialect, emotion, isCustomPronounced)
        {

        }

        /// <summary>
        /// Deep copy constructor for ThespeonInputSegment.
        /// </summary>
        /// <param name="other">The ThespeonInputSegment instance to copy from.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the provided ThespeonInputSegment instance is null.</exception>
        public ThespeonInputSegment(ThespeonInputSegment other) : base(other)
        {

        }


        public ThespeonInputSegment(string text, ModuleLanguage language, Emotion emotion = Emotion.None, bool isCustomPronounced = false) : base(text, language, emotion, isCustomPronounced)
        {

        }

        /// <summary>
        /// Constructor for ThespeonInputSegment taking emotion blends and speed/loudness boundary values.
        /// The boundary values are keypoints on a piecewise-linear curve spanning the whole input, so they stay continuous across segment boundaries.
        /// </summary>
        /// <param name="text">The text of the segment. Cannot be null or empty.</param>
        /// <param name="startEmotion">The emotion blend at the start of the segment. Weights are clamped to [0,1] and normalized to sum to 1. Pass an empty blend to contribute no keypoint.</param>
        /// <param name="endEmotion">The emotion blend at the end of the segment.</param>
        /// <param name="language">The language of the segment. Optional.</param>
        /// <param name="dialect">The dialect of the segment. Optional.</param>
        /// <param name="isCustomPronounced">Indicates whether the segment consists of only IPA text. Optional, defaults to false.</param>
        /// <param name="startSpeed">Speed at the start of the segment.</param>
        /// <param name="endSpeed">Speed at the end of the segment.</param>
        /// <param name="startLoudness">Loudness at the start of the segment.</param>
        /// <param name="endLoudness">Loudness at the end of the segment.</param>
        /// <exception cref="System.ArgumentException">Thrown if the text is null or empty.</exception>
        public ThespeonInputSegment(string text, Dictionary<Emotion, float> startEmotion, Dictionary<Emotion, float> endEmotion, string language = null, string dialect = null, bool isCustomPronounced = false, float startSpeed = 1f, float endSpeed = 1f, float startLoudness = 1f, float endLoudness = 1f)
            : base(text, startEmotion, endEmotion, language, dialect, isCustomPronounced, startSpeed, endSpeed, startLoudness, endLoudness)
        {

        }


        public ThespeonInputSegment(string text, Dictionary<Emotion, float> startEmotion, Dictionary<Emotion, float> endEmotion, ModuleLanguage language, bool isCustomPronounced = false, float startSpeed = 1f, float endSpeed = 1f, float startLoudness = 1f, float endLoudness = 1f)
            : base(text, startEmotion, endEmotion, language, isCustomPronounced, startSpeed, endSpeed, startLoudness, endLoudness)
        {

        }

        /// <summary>
        /// Parses a ThespeonInputSegment from a JSON file located at the specified path relative to the project Assets directory.
        /// </summary>
        /// <param name="jsonPath">The path to the JSON file relative to the Assets directory.</param>
        /// <returns>A ThespeonInputSegment instance populated with data from the JSON file.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if the provided JSON object is null or its required fields are missing.</exception>
        public static ThespeonInputSegment ParseFromJson(string jsonPath)
        {
            JObject json = JObject.Parse(RuntimeFileLoader.LoadFileAsString(jsonPath));
            return ParseFromJson(json);
        }

        /// <summary>
        /// Parses a ThespeonInputSegment from a JSON object.
        /// </summary>
        /// <param name="json">The JSON object containing the segment data.</param>
        /// <returns>A ThespeonInputSegment instance populated with data from the JSON object.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if the provided JSON object is null or its required fields are missing.</exception>
        public static ThespeonInputSegment ParseFromJson(JObject json)
        {
            if (json == null)
            {
                throw new System.ArgumentNullException(nameof(json), "JSON object cannot be null.");
            }

            string text = json["text"]?.ToString() ?? string.Empty;
            Emotion emotion = json["emotion"]?.ToObject<Emotion>() ?? Emotion.None;
            string language = json["language"]?.ToString();
            string dialect = json["dialect"]?.ToString();
            bool isCustomPronounced = json["isCustomPronounced"]?.ToObject<bool>() ?? false;
            Dictionary<Emotion, float> startEmotion = BlendFromJson(json["startEmotion"]);
            Dictionary<Emotion, float> endEmotion = BlendFromJson(json["endEmotion"]);
            float startSpeed = json["startSpeed"]?.ToObject<float>() ?? 1f;
            float endSpeed = json["endSpeed"]?.ToObject<float>() ?? 1f;
            float startLoudness = json["startLoudness"]?.ToObject<float>() ?? 1f;
            float endLoudness = json["endLoudness"]?.ToObject<float>() ?? 1f;

            if (startEmotion == null && endEmotion == null)
            {

                return new ThespeonInputSegment(text, language, dialect, emotion, isCustomPronounced)
                {
                    StartSpeed = startSpeed,
                    EndSpeed = endSpeed,
                    StartLoudness = startLoudness,
                    EndLoudness = endLoudness
                };
            }

            return new ThespeonInputSegment(text, startEmotion, endEmotion, language, dialect, isCustomPronounced, startSpeed, endSpeed, startLoudness, endLoudness);
        }

        /// <summary>
        /// Compares this ThespeonInputSegment with another for equality, ignoring the text field.
        /// </summary>
        /// <param name="other">The other ThespeonInputSegment to compare with.</param>
        /// <returns>True if the segments are equal ignoring the text field, otherwise false.</returns>
        public bool EqualsIgnoringText(ThespeonInputSegment other)
        {
            bool areLanguagesEqual = (Language == null && other.Language == null) || (Language != null && Language.Equals(other.Language));
            return other.Emotion == Emotion && areLanguagesEqual && other.IsCustomPronounced == IsCustomPronounced;
        }

        /// <summary>
        /// Creates a deep copy of the ThespeonInputSegment.
        /// </summary>
        /// <returns>A new ThespeonInputSegment instance that is a deep copy of the current instance, including all relevant fields.</returns>
        public override ModelInputSegment DeepCopy()
        {

            return new ThespeonInputSegment(this);
        }
    }
}
