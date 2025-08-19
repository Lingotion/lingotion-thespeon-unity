// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using Lingotion.Thespeon.Core;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEditor;
using Lingotion.Thespeon.Core.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Lingotion.Thespeon.Inputs
{
    /// <summary>
    /// Represents a Thespeon input which specifies exactly what to synthesize.
    /// </summary>
    public class ThespeonInput : ModelInput<ThespeonInput, ThespeonInputSegment>
    {
        public AnimationCurve Speed;
        public AnimationCurve Loudness;

        /// <summary>
        /// The main constructor for ThespeonInput.
        /// Initializes a new instance of ThespeonInput with the specified actor name, segments, model type, default emotion, default language, speed, and loudness.
        /// </summary>
        /// <param name="segments">A list of ModelInputSegment instances representing the segments of the input.</param>
        /// <param name="actorName">The name of the actor to use.</param>
        /// <param name="moduleType">An instance of the ModuleType enum representing the type of model to use.</param>
        /// <param name="defaultEmotion">The default emotion to be used across segments.</param>
        /// <param name="defaultLanguage">The default language to be used across segments.</param>
        /// <param name="defaultDialect">The default dialect to be used across segments.</param>
        /// <param name="speed">An AnimationCurve representing the speed of the input over its length.</param>
        /// <param name="loudness">An AnimationCurve representing the loudness of the input over its length.</param>
        /// <exception cref="System.ArgumentException">Thrown if the actor name is null or empty or if the segments list is null or empty or contains empty text.</exception>
        public ThespeonInput(List<ThespeonInputSegment> segments, string actorName = null, ModuleType moduleType = ModuleType.None, Emotion defaultEmotion = Emotion.None, string defaultLanguage = null, string defaultDialect = null, AnimationCurve speed = null, AnimationCurve loudness = null)
            : base(segments, actorName, moduleType, defaultEmotion, defaultLanguage, defaultDialect)
        {
            Speed = new AnimationCurve();
            if (speed != null)
            {
                // [DevComment] Speed.CopyFrom(speed); for some reason this has broken hence the for loops with AddKey.
                foreach(Keyframe key in speed.keys)
                {
                    Speed.AddKey(key);
                }
            } else
            {
                Speed.AddKey(new Keyframe(0, 1));
            }
            Loudness = new AnimationCurve();
            if (loudness != null)
            {
                foreach(Keyframe key in loudness.keys)
                {
                    Loudness.AddKey(key);
                }
            } else
            {
                Loudness.AddKey(new Keyframe(0,1));
            }
        }


        /// <summary>
        /// Deep copy constructor for ThespeonInput.
        /// </summary>
        /// <param name="other">The ThespeonInput instance to copy from.</param> 
        /// <exception cref="System.ArgumentNullException">Thrown if the provided ThespeonInput instance is null.</exception>
        public ThespeonInput(ThespeonInput other) : base(other)
        {
            if (other == null)
            {
                throw new System.ArgumentNullException(nameof(other), "Cannot copy from a null ThespeonInput instance.");
            }
            Speed = new AnimationCurve();
            foreach(Keyframe key in other.Speed.keys)
            {
                Speed.AddKey(key);
            }
            Loudness = new AnimationCurve();
            foreach(Keyframe key in other.Loudness.keys)
            {
                Loudness.AddKey(key);
            }
        }


        // [DevComment] No xml tag as ModuleLanguage is meant to be a non-front-facing class.
        public ThespeonInput(string actorName, List<ThespeonInputSegment> segments, ModuleLanguage defaultLanguage, Emotion defaultEmotion = Emotion.None, ModuleType moduleType = ModuleType.None, AnimationCurve speed = null, AnimationCurve loudness = null)
            : base(segments, actorName, defaultEmotion, moduleType, defaultLanguage)
        {
            Speed = new AnimationCurve();
            if (speed != null)
            {
                foreach(Keyframe key in speed.keys)
                {
                    Speed.AddKey(key);
                }
            } else
            {
                Speed.AddKey(new Keyframe(0, 1));
            }
            Loudness = new AnimationCurve();
            if (loudness != null)
            {
                foreach(Keyframe key in loudness.keys)
                {
                    Loudness.AddKey(key);
                }
            } else
            {
                Loudness.AddKey(new Keyframe(0,1));
            }
        }

        /// <summary>
        /// Parses a ThespeonInput from a JSON file located at the specified path relative to Assets directory with optional config override settings.
        /// </summary>
        /// <param name="jsonPath">Relative path to json file to be read.</param>
        /// <param name="configOverride"></param>
        /// <returns>A ThespeonInput instance populated with the data from the JSON file.</returns>
        /// <exception cref="System.ArgumentException">Thrown if the JSON file does not contain the required fields.</exception>
        public static ThespeonInput ParseFromJson(string jsonPath, InferenceConfig configOverride = null)
        {
            JObject json = JObject.Parse(RuntimeFileLoader.LoadFileAsString(jsonPath));
            return ParseFromJson(json, configOverride);
        }
        /// <summary>
        /// Parses a ThespeonInput from a JSON object with optional config override settings.
        /// </summary>
        /// <param name="json">JSON object containing the input data.</param>
        /// <param name="configOverride">Optional configuration override for default values.</param>
        /// <returns>A ThespeonInput instance populated with the data from the JSON object.</returns>
        /// <exception cref="System.ArgumentException">Thrown if the JSON object is null or does not contain the required fields.</exception>
        public static ThespeonInput ParseFromJson(JObject json, InferenceConfig configOverride = null)
        {
            configOverride ??= new InferenceConfig();
            string actorName = string.IsNullOrEmpty(json["actorName"]?.ToString()) ? PackManifestHandler.Instance.GetAllActors()[0] : json["actorName"]?.ToString();
            List<ThespeonInputSegment> segments = new();
            if (json["segments"] is JArray segmentsArray)
            {
                foreach (var segmentJson in segmentsArray)
                {
                    ThespeonInputSegment segment = ThespeonInputSegment.ParseFromJson(segmentJson.ToObject<JObject>());
                    segments.Add(segment);
                }
            }
            else
            {
                throw new System.ArgumentException("JSON does not contain valid 'segments' array.");
            }
            string moduleTypesString = string.IsNullOrEmpty(json["moduleType"]?.ToString()) ? configOverride?.ModuleType.ToString() : json["moduleType"]?.ToString();
            ModuleType moduleType = (ModuleType)System.Enum.Parse(typeof(ModuleType), moduleTypesString);
            string defaultEmotionString = string.IsNullOrEmpty(json["defaultEmotion"]?.ToString()) ? configOverride?.FallbackEmotion.ToString() : json["defaultEmotion"]?.ToString();
            Emotion defaultEmotion = (Emotion)System.Enum.Parse(typeof(Emotion), defaultEmotionString);
            ModuleLanguage defaultLanguage = string.IsNullOrEmpty(json["defaultLanguage"]?.ToString()) ? configOverride?.FallbackLanguage : json["defaultLanguage"]?.ToObject<ModuleLanguage>();
            AnimationCurve speed = new AnimationCurve();
            AnimationCurve loudness = new AnimationCurve();
            List<double> speedValues = json["speed"]?.ToObject<List<double>>() ?? new List<double> { 1 };
            for (int i = 0; i < speedValues.Count; i++)
            {
                speed.AddKey(i / (float)(speedValues.Count - 1), (float)speedValues[i]);
            }
            List<double> loudnessValues = json["loudness"]?.ToObject<List<double>>() ?? new List<double> { 1 };
            for (int i = 0; i < loudnessValues.Count; i++)
            {
                loudness.AddKey(i / (float)(loudnessValues.Count - 1), (float)loudnessValues[i]);
            }
            return new ThespeonInput(actorName, segments, defaultLanguage, defaultEmotion, moduleType, speed, loudness);
        }


        /// <summary>
        /// Returns a string representation of the ThespeonInput in JSON format after filtering out any null or None elements.
        /// </summary>
        /// <returns>A JSON string representing the ThespeonInput.</returns>
        public override string ToJson()
        {
            JObject json = new()
            {
                ["actorName"] = ActorName,
                ["moduleType"] = ModuleType.ToString(),
                ["defaultEmotion"] = DefaultEmotion.ToString(),
                ["defaultLanguage"] = DefaultLanguage != null ? JToken.Parse(DefaultLanguage.ToJson()) : null,
            };
            json["segments"] = new JArray(Segments.Where(segment => segment != null && !string.IsNullOrEmpty(segment.Text)).Select(segment => JObject.Parse(segment.ToJson())));
            List<string> keysToRemove = new();
            foreach (JProperty property in json.Properties())
            {
                if (string.IsNullOrEmpty(property.Value.ToString()) || (property.Name == "defaultEmotion" && property.Value.ToString() == Emotion.None.ToString()) || (property.Name == "moduleType" && property.Value.ToString() == ModuleType.None.ToString()))
                {
                    keysToRemove.Add(property.Name);
                }
            }
            foreach (string key in keysToRemove)
            {
                json.Remove(key);
            }
            if (Speed != null && Speed.keys.Count() > 0)
            {
                // [DevComment] these lose the tangent information though.
                json["speed"] = new JArray(Speed.keys.ToList().Select(key => key.value));
            }
            if (Loudness != null && Loudness.keys.Count() > 0)
            {
                json["loudness"] = new JArray(Loudness.keys.ToList().Select(key => key.value));
            }
            return JsonConvert.SerializeObject(json, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            });
        }
    }
}