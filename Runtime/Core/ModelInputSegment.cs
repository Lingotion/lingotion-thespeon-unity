// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Lingotion.Thespeon.Core
{
    /// <summary>
    /// Base class for model input segments, providing common properties and methods for all model input segments.
    /// </summary>
    public abstract class ModelInputSegment
    {
        public string Text;
        public Emotion Emotion;
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
            Language = ModuleLanguage.CopyOrNull(other.Language);
            IsCustomPronounced = other.IsCustomPronounced;
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
            if (string.IsNullOrEmpty(language))
            {
                if (!string.IsNullOrEmpty(dialect))
                {
                    LingotionLogger.Warning("Dialect provided without language in segment. Will set Language to default.");
                }
                Language = null;
            }
            else
            {
                LingotionLogger.Debug($"Creating ModelInputSegment with language: {language}, dialect: {dialect} and emotion: {emotion}");
                Language = new ModuleLanguage(language, null, null, null, dialect, null);
            }
            IsCustomPronounced = isCustomPronounced;
        }

        // [DevComment] No xml tag as ModuleLanguage is meant to be a non-front-facing class.
        public ModelInputSegment(string text, ModuleLanguage language, Emotion emotion = Emotion.None, bool isCustomPronounced = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new System.ArgumentException("Text cannot be null or empty. Please provide a valid text.");
            }
            Text = text;
            Emotion = emotion;
            Language = language;
            IsCustomPronounced = isCustomPronounced;
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
            return json.ToString();
        }

        public abstract ModelInputSegment DeepCopy();

    }

    /// <summary>
    /// Enumeration representing various emotions that can be associated with a segment. Also contains a None as a special null-like value.
    /// </summary>
    // [DevComment] move this into a file collecting all our core enums.
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


