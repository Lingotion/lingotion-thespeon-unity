// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Base class for model input segments, providing common properties and methods for all model input segments.
    /// </summary>
    public abstract class ModelInputSegment
    {
        public string Text;

        /// <summary>
        /// Legacy single emotion. Kept for backwards compatibility; inference reads <see cref="StartEmotion"/> and <see cref="EndEmotion"/> instead.
        /// </summary>
        public Emotion Emotion;

        /// <summary>
        /// Emotion blend at the start of the segment. Keys are emotions, values are intensities that sum to 1.
        /// A blend containing only <see cref="Core.Emotion.None"/> (or an empty blend) means "no opinion" and contributes no keypoint to the emotion curve.
        /// </summary>
        public Dictionary<Emotion, float> StartEmotion = new();

        /// <summary>
        /// Emotion blend at the end of the segment. See <see cref="StartEmotion"/>.
        /// </summary>
        public Dictionary<Emotion, float> EndEmotion = new();

        /// <summary>
        /// Speed at the start of the segment.
        /// </summary>
        public float StartSpeed = 1f;

        /// <summary>
        /// Speed at the end of the segment.
        /// </summary>
        public float EndSpeed = 1f;

        /// <summary>
        /// Loudness at the start of the segment.
        /// </summary>
        public float StartLoudness = 1f;

        /// <summary>
        /// Loudness at the end of the segment.
        /// </summary>
        public float EndLoudness = 1f;
#nullable enable
        public ModuleLanguage? Language;
#nullable disable
        public bool IsCustomPronounced;

        /// <summary>
        /// Deep copy constructor for ModelInputSegment.
        /// </summary>
        /// <param name="other">The ModelInputSegment instance to copy from.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the provided ModelInputSegment instance is null.</exception>
        public ModelInputSegment(ModelInputSegment other)
        {
            if (other == null)
            {
                throw new System.ArgumentNullException(nameof(other), "Cannot copy from a null ModelInputSegment instance.");
            }
            Text = other.Text;
            Emotion = other.Emotion;
            StartEmotion = CopyBlend(other.StartEmotion);
            EndEmotion = CopyBlend(other.EndEmotion);
            StartSpeed = other.StartSpeed;
            EndSpeed = other.EndSpeed;
            StartLoudness = other.StartLoudness;
            EndLoudness = other.EndLoudness;
            Language = ModuleLanguage.CopyOrNull(other.Language);
            IsCustomPronounced = other.IsCustomPronounced;
        }

        /// <summary>
        /// Creates a shallow copy of an emotion blend, returning an empty blend when the source is null.
        /// </summary>
        /// <param name="blend">The blend to copy.</param>
        /// <returns>A new dictionary holding the same emotion weights.</returns>
        public static Dictionary<Emotion, float> CopyBlend(Dictionary<Emotion, float> blend)
        {
            return blend == null ? new Dictionary<Emotion, float>() : new Dictionary<Emotion, float>(blend);
        }
        /// <summary>
        /// Constructor for ModelInputSegment.
        /// Initializes a new instance of ModelInputSegment with the specified text, emotion, language, and custom pronunciation flag.
        /// </summary>
        /// <param name="text">The text of the segment.</param>
        /// <param name="language">The ISO-639 language code of the segment. Optional, can be null.</param>
        /// <param name="dialect">The ISO-3166 dialect code of the segment. Optional, can be null.</param>
        /// <param name="emotion">The emotion associated with the segment.</param>
        /// <param name="isCustomPronounced">Indicates whether the segment is custom pronounced.</param>
        /// <exception cref="System.ArgumentException">Thrown if the text is null or empty.</exception>
        public ModelInputSegment(string text, string language = null, string dialect = null, Emotion emotion = Emotion.None, bool isCustomPronounced = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new System.ArgumentException("Text cannot be null or empty. Please provide a valid text.");
            }
            Text = text;
            Emotion = emotion;
            StartEmotion = new Dictionary<Emotion, float> { { emotion, 1f } };
            EndEmotion = new Dictionary<Emotion, float> { { emotion, 1f } };
            IsCustomPronounced = isCustomPronounced;

            if (string.IsNullOrEmpty(language))
            {
                if (string.IsNullOrEmpty(dialect))
                {
                    Language = null;
                    return;
                }
                language = ModuleLanguage.NoLang;
            }
            Language = new ModuleLanguage(language, null, null, null, dialect, null);
        }

        public ModelInputSegment(string text, ModuleLanguage language, Emotion emotion = Emotion.None, bool isCustomPronounced = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new System.ArgumentException("Text cannot be null or empty. Please provide a valid text.");
            }
            Text = text;
            Emotion = emotion;
            StartEmotion = new Dictionary<Emotion, float> { { emotion, 1f } };
            EndEmotion = new Dictionary<Emotion, float> { { emotion, 1f } };
            Language = language;
            IsCustomPronounced = isCustomPronounced;
        }

        /// <summary>
        /// Constructor for ModelInputSegment taking emotion blends and speed/loudness boundary values.
        /// The blends and scalars are treated as keypoints on a piecewise-linear curve over the whole input, so values stay continuous across segment boundaries.
        /// </summary>
        /// <param name="text">The text of the segment.</param>
        /// <param name="startEmotion">The emotion blend at the start of the segment. Weights are clamped to [0,1] and normalized to sum to 1. Pass an empty blend (or one containing only <see cref="Core.Emotion.None"/>) to contribute no keypoint.</param>
        /// <param name="endEmotion">The emotion blend at the end of the segment.</param>
        /// <param name="language">The ISO-639 language code of the segment. Optional, can be null.</param>
        /// <param name="dialect">The ISO-3166 dialect code of the segment. Optional, can be null.</param>
        /// <param name="isCustomPronounced">Indicates whether the segment is custom pronounced.</param>
        /// <param name="startSpeed">Speed at the start of the segment.</param>
        /// <param name="endSpeed">Speed at the end of the segment.</param>
        /// <param name="startLoudness">Loudness at the start of the segment.</param>
        /// <param name="endLoudness">Loudness at the end of the segment.</param>
        /// <exception cref="System.ArgumentException">Thrown if the text is null or empty.</exception>
        public ModelInputSegment(string text, Dictionary<Emotion, float> startEmotion, Dictionary<Emotion, float> endEmotion, string language = null, string dialect = null, bool isCustomPronounced = false, float startSpeed = 1f, float endSpeed = 1f, float startLoudness = 1f, float endLoudness = 1f)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new System.ArgumentException("Text cannot be null or empty. Please provide a valid text.");
            }
            Text = text;
            StartEmotion = CopyBlend(startEmotion);
            EndEmotion = CopyBlend(endEmotion);
            Emotion = DominantEmotion(StartEmotion);
            StartSpeed = startSpeed;
            EndSpeed = endSpeed;
            StartLoudness = startLoudness;
            EndLoudness = endLoudness;
            IsCustomPronounced = isCustomPronounced;

            if (string.IsNullOrEmpty(language))
            {
                if (string.IsNullOrEmpty(dialect))
                {
                    Language = null;
                    return;
                }
                language = ModuleLanguage.NoLang;
            }
            Language = new ModuleLanguage(language, null, null, null, dialect, null);
        }

        public ModelInputSegment(string text, Dictionary<Emotion, float> startEmotion, Dictionary<Emotion, float> endEmotion, ModuleLanguage language, bool isCustomPronounced = false, float startSpeed = 1f, float endSpeed = 1f, float startLoudness = 1f, float endLoudness = 1f)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new System.ArgumentException("Text cannot be null or empty. Please provide a valid text.");
            }
            Text = text;
            StartEmotion = CopyBlend(startEmotion);
            EndEmotion = CopyBlend(endEmotion);
            Emotion = DominantEmotion(StartEmotion);
            StartSpeed = startSpeed;
            EndSpeed = endSpeed;
            StartLoudness = startLoudness;
            EndLoudness = endLoudness;
            Language = language;
            IsCustomPronounced = isCustomPronounced;
        }

        /// <summary>
        /// Sets a single emotion for the whole segment, pinning both the start and end emotion blends to it.
        /// </summary>
        /// <param name="emotion">The emotion to apply. <see cref="Core.Emotion.None"/> clears the segment's opinion, letting the surrounding segments' emotion curve pass through it.</param>
        public void SetEmotion(Emotion emotion)
        {
            Emotion = emotion;
            StartEmotion = new Dictionary<Emotion, float> { { emotion, 1f } };
            EndEmotion = new Dictionary<Emotion, float> { { emotion, 1f } };
        }

        /// <summary>
        /// Returns the highest weighted emotion of a blend, used to keep the legacy <see cref="Emotion"/> field meaningful.
        /// </summary>
        private static Emotion DominantEmotion(Dictionary<Emotion, float> blend)
        {
            if (blend == null || blend.Count == 0)
            {
                return Core.Emotion.None;
            }
            return blend.Aggregate((best, next) => next.Value > best.Value ? next : best).Key;
        }

        /// <summary>
        /// Returns a string representation of the ModelInputSegment in JSON format after filtering out any null or None elements and isCustomPronounced if set to false.
        /// </summary>
        /// <returns>A JSON string representing the ModelInputSegment.</returns>
        public virtual string ToJson()
        {
            JObject json = new()
            {
                ["text"] = Text,
                ["emotion"] = Emotion.ToString(),
                ["language"] = Language != null ? JToken.Parse(Language.ToJson()) : null,
                ["isCustomPronounced"] = IsCustomPronounced
            };
            List<string> keysToRemove = new();
            foreach (JProperty property in json.Properties())
            {
                if (string.IsNullOrEmpty(property.Value.ToString()) || (property.Name == "emotion" && property.Value.ToString() == Emotion.None.ToString()) || (property.Name == "isCustomPronounced" && property.Value.ToString() == "False"))
                {
                    keysToRemove.Add(property.Name);
                }
            }
            foreach (string key in keysToRemove)
            {
                json.Remove(key);
            }
            if (StartEmotion != null && StartEmotion.Count > 0)
            {
                json["startEmotion"] = BlendToJson(StartEmotion);
            }
            if (EndEmotion != null && EndEmotion.Count > 0)
            {
                json["endEmotion"] = BlendToJson(EndEmotion);
            }
            json["startSpeed"] = StartSpeed;
            json["endSpeed"] = EndSpeed;
            json["startLoudness"] = StartLoudness;
            json["endLoudness"] = EndLoudness;
            return json.ToString();
        }

        /// <summary>
        /// Serializes an emotion blend as a JSON object mapping emotion names to weights.
        /// </summary>
        protected static JObject BlendToJson(Dictionary<Emotion, float> blend)
        {
            JObject json = new();
            foreach (KeyValuePair<Emotion, float> pair in blend)
            {
                json[pair.Key.ToString()] = pair.Value;
            }
            return json;
        }

        /// <summary>
        /// Parses an emotion blend from a JSON object mapping emotion names to weights.
        /// </summary>
        /// <param name="token">The JSON token to parse. May be null.</param>
        /// <returns>The parsed blend, or null when the token is absent or not an object.</returns>
        protected static Dictionary<Emotion, float> BlendFromJson(JToken token)
        {
            if (token is not JObject blendJson)
            {
                return null;
            }
            Dictionary<Emotion, float> blend = new();
            foreach (JProperty property in blendJson.Properties())
            {
                if (System.Enum.TryParse(property.Name, true, out Emotion emotion))
                {
                    blend[emotion] = property.Value.ToObject<float>();
                }
                else
                {
                    LingotionLogger.Warning($"Unknown emotion '{property.Name}' in emotion blend. Ignoring it.");
                }
            }
            return blend;
        }

        public abstract ModelInputSegment DeepCopy();

    }

    /// <summary>
    /// Enumeration representing various emotions that can be associated with a segment. Also contains a None as a special null-like value.
    /// </summary>
    public enum Emotion
    {
        /// <summary>
        /// No emotion. Special null-like value.
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Delighted, giddy. Abundance of energy.
        /// Message: This is better than I imagined.
        /// Example: Feeling happiness beyond imagination, as if life is perfect at this moment.
        /// </summary>
        Ecstasy = 1,
        
        /// <summary>
        /// Connected, proud. Glowing sensation.
        /// Message: I want to support the person or thing.
        /// Example: Meeting your hero and wanting to express deep appreciation.
        /// </summary>
        Admiration = 2,
        
        /// <summary>
        /// Alarmed, petrified. Hard to breathe.
        /// Message: There is big danger.
        /// Example: Feeling hunted and fearing for your life.
        /// </summary>
        Terror = 3,
        
        /// <summary>
        /// Inspired, WOWed. Heart stopping sensation.
        /// Message: Something is totally unexpected.
        /// Example: Discovering a lost historical artifact in an abandoned building.
        /// </summary>
        Amazement = 4,
        
        /// <summary>
        /// Heartbroken, distraught. Hard to get up.
        /// Message: Love is lost.
        /// Example: Losing a loved one in an accident.
        /// </summary>
        Grief = 5,
        
        /// <summary>
        /// Disturbed, horrified. Bileous and vehement sensation.
        /// Message: Fundamental values are violated.
        /// Example: Seeing someone exploit others for personal gain.
        /// </summary>
        Loathing = 6,
        
        /// <summary>
        /// Overwhelmed, furious. Pounding heart, seeing red.
        /// Message: I am blocked from something vital.
        /// Example: Being falsely accused and not believed by authorities.
        /// </summary>
        Rage = 7,
        
        /// <summary>
        /// Intense, focused. Highly focused sensation.
        /// Message: Something big is coming.
        /// Example: Watching over your child climbing a tree, ready to catch them if they fall.
        /// </summary>
        Vigilance = 8,
        
        /// <summary>
        /// Excited, pleased. Sense of energy and possibility.
        /// Message: Life is going well.
        /// Example: Feeling genuinely happy and optimistic in conversation.
        /// </summary>
        Joy = 9,
        
        /// <summary>
        /// Accepting, safe. Warm sensation.
        /// Message: This is safe.
        /// Example: Trusting someone to be loyal and supportive.
        /// </summary>
        Trust = 10,
        
        /// <summary>
        /// Stressed, scared. Agitated sensation.
        /// Message: Something I care about is at risk.
        /// Example: Realizing you forgot to prepare for a major presentation.
        /// </summary>
        Fear = 11,
        
        /// <summary>
        /// Shocked, unexpected. Heart pounding.
        /// Message: Something new happened.
        /// Example: Walking into a surprise party.
        /// </summary>
        Surprise = 12,
        
        /// <summary>
        /// Bummed, loss. Heavy sensation.
        /// Message: Love is going away.
        /// Example: Feeling blue and unmotivated.
        /// </summary>
        Sadness = 13,
        
        /// <summary>
        /// Distrust, rejecting. Bitter and unwanted sensation.
        /// Message: Rules are violated.
        /// Example: Seeing someone put a cockroach in their food to avoid paying.
        /// </summary>
        Disgust = 14,
        
        /// <summary>
        /// Mad, fierce. Strong and heated sensation.
        /// Message: Something is in the way.
        /// Example: Finding your car blocked by someone who left their car unattended.
        /// </summary>
        Anger = 15,
        
        /// <summary>
        /// Curious, considering. Alert and exploring.
        /// Message: Change is happening.
        /// Example: Waiting eagerly for a long-awaited promise to be fulfilled.
        /// </summary>
        Anticipation = 16,
        
        /// <summary>
        /// Calm, peaceful. Relaxed, open-hearted.
        /// Message: Something essential or pure is happening.
        /// Example: Enjoying peaceful time with loved ones without stress.
        /// </summary>
        Serenity = 17,
        
        /// <summary>
        /// Open, welcoming. Peaceful sensation.
        /// Message: We are in this together.
        /// Example: Welcoming a new person into your friend group.
        /// </summary>
        Acceptance = 18,
        
        /// <summary>
        /// Worried, anxious. Cannot relax.
        /// Message: There could be a problem.
        /// Example: Worrying about the outcome of an unexpected meeting.
        /// </summary>
        Apprehension = 19,
        
        /// <summary>
        /// Scattered, uncertain. Unfocused sensation.
        /// Message: I don't know what to prioritize.
        /// Example: Struggling to focus during a conversation.
        /// </summary>
        Distraction = 20,
        
        /// <summary>
        /// Blue, unhappy. Slow and disconnected.
        /// Message: Love is distant.
        /// Example: Feeling uninterested in suggested activities.
        /// </summary>
        Pensiveness = 21,
        
        /// <summary>
        /// Tired, uninterested. Drained, low energy.
        /// Message: The potential for this situation is not being met.
        /// Example: Finding nothing enjoyable to do.
        /// </summary>
        Boredom = 22,
        
        /// <summary>
        /// Frustrated, prickly. Slightly agitated.
        /// Message: Something is unresolved.
        /// Example: Being irritated by repetitive behavior.
        /// </summary>
        Annoyance = 23,
        
        /// <summary>
        /// Open, looking. Mild sense of curiosity.
        /// Message: Something useful might come.
        /// Example: Becoming curious when hearing unexpected news.
        /// </summary>
        Interest = 24,
        
        /// <summary>
        /// Detached, apathetic. No sensation or feeling at all.
        /// Message: This does not affect me.
        /// Example: Feeling nothing during a conversation about irrelevant topics.
        /// </summary>
        Emotionless = 25,
        
        /// <summary>
        /// Distaste, scorn. Angry and sad at the same time.
        /// Message: This is beneath me.
        /// Example: Feeling disdain toward someone's dishonest behavior.
        /// </summary>
        Contempt = 26,
        
        /// <summary>
        /// Guilt, regret, shame. Disgusted and sad at the same time.
        /// Message: I regret my actions.
        /// Example: Wishing you could undo a hurtful action.
        /// </summary>
        Remorse = 27,
        
        /// <summary>
        /// Dislike, displeasure. Sad and surprised.
        /// Message: This violates my values.
        /// Example: Rejecting a statement that contradicts your beliefs.
        /// </summary>
        Disapproval = 28,
        
        /// <summary>
        /// Astonishment, wonder. Surprise with a hint of fear.
        /// Message: This is overwhelming.
        /// Example: Being speechless when meeting your idol.
        /// </summary>
        Awe = 29,
        
        /// <summary>
        /// Obedience, compliance. Fearful but trusting.
        /// Message: I must follow this authority.
        /// Example: Obeying a trusted figure's orders without question.
        /// </summary>
        Submission = 30,
        
        /// <summary>
        /// Cherish, treasure. Joy with trust.
        /// Message: I want to be with this person.
        /// Example: Feeling deep connection and joy with someone.
        /// </summary>
        Love = 31,
        
        /// <summary>
        /// Cheerfulness, hopeful. Joyful anticipation.
        /// Message: Things will work out.
        /// Example: Seeing the positive side of any situation.
        /// </summary>
        Optimism = 32,
        
        /// <summary>
        /// Pushy, self-assertive. Driven by anger.
        /// Message: I must remove obstacles.
        /// Example: Forcing your viewpoint aggressively.
        /// </summary>
        Aggressiveness = 33
    }
}


