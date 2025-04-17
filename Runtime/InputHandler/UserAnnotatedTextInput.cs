// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Lingotion.Thespeon.API;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace Lingotion.Thespeon.Utils
{   
    public class UserModelInput
    {
        [JsonProperty("moduleName", Required = Required.Always)]
        public string moduleName { get; set; }
        [JsonProperty("actorUsername", Required = Required.Always)]
        public string actorUsername { get; set; } = "";
        #nullable enable
        [JsonProperty("defaultLanguage", NullValueHandling = NullValueHandling.Ignore)]
        public Language? defaultLanguage { get; set; }

        [JsonProperty("defaultEmotion", NullValueHandling = NullValueHandling.Ignore)]
        public string? defaultEmotion { get; set; }
        #nullable disable    
        [JsonProperty("segments", Required = Required.Always)]
        public List<UserSegment> segments { get; set; } = new();

        [JsonProperty("speed", NullValueHandling = NullValueHandling.Ignore)]
        public List<double> speed { get; set; }

        [JsonProperty("loudness", NullValueHandling = NullValueHandling.Ignore)]
        public List<double> loudness { get; set; }




        // --- Add this property for unknown/extra fields ---
        #nullable enable
        [JsonExtensionData]
        public Dictionary<string, JToken>? extraData { get; set; }
        #nullable disable

        // JSON serialization needs to create a new object and set the values individually, therefore no arguments in constructor.
        // TODO: Move JSON serialisation to some other class to prevent empty constructor for user.
        /// <summary>
        /// Initializes a new instance of the UserModelInput class. Used duing deserialization.
        /// </summary>
        public UserModelInput(){
            
        }




        /// <summary>
        /// Initializes a new instance of the UserModelInput class with specified actor name, match actor module with specified tags, and text segments. Will initialize moduleName defaultLanguage to the first module and language from the actor's available modules.
        /// </summary>
        /// <param name="actorName">The username of the actor associated with this input.</param>
        /// <param name="actorModuleTag">The tags of the actor module to be used, comes in a key-value pair dictionary.</param>
        /// <param name="textSegments">A list of user segments defining the input text data.</param>
        public UserModelInput(string actorName, ActorTags desiredTags,  List<UserSegment> textSegments)         //TUNI-88  -  Remove this constructor and move its content to ThespeonAPI.Synthesize
        {
            actorUsername = actorName;
            string selectedActorModuleName = ThespeonAPI.GetActorPackModuleName(actorUsername, desiredTags);

            if (string.IsNullOrEmpty(selectedActorModuleName))
            {
                throw new ArgumentException($"No module found for actor {actorName} with the specified tags.");
            }


            moduleName = selectedActorModuleName;

            
            this.segments = textSegments;
        }


        /// <summary>
        /// Initializes a new instance of the UserModelInput class with specified actor name and text segments. Will initialize moduleName defaultLanguage to the first module and language from the actor's available modules.
        /// </summary>
        /// <param name="actorName">The username of the actor associated with this input.</param>
        /// <param name="textSegments">A list of user segments defining the input text data.</param>
        public UserModelInput(string actorName, List<UserSegment> textSegments)
        {
            actorUsername = actorName;
            List<ActorPackModule> actorModules = ThespeonAPI.GetModulesWithActor(actorName);
            // TODO: Replace with tag of highest quality
            moduleName = actorModules[0].name;
            defaultLanguage = actorModules[0].language_options.languages[0];
            
            this.segments = textSegments;
        }

        // TODO: Add constructor 

        /// <summary>
        /// Validates the UserModelInput object and returns a tuple of lists containing errors and warnings.
        /// Will also print warnings to the console and throw an exception if validation fails.
        /// </summary>
        /// <returns>A tuple containing a list of error messages and a list of warning messages.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if any of the following conditions are met:
        /// - 'moduleName' is invalid or empty.
        /// - 'actorUsername' is invalid or unregistered.
        /// - No valid segments are provided.
        /// - A segment has empty text.
        /// </exception>
        public (List<string> errors, List<string> warnings) ValidateAndWarn()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            ActorPackModule module=null;
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                errors.Add("The 'moduleName' is required and cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(actorUsername))
            {
                errors.Add("The 'actorUsername' is required and cannot be empty.");
            } else
            {
               if(!ThespeonAPI.GetRegisteredActorPacks().ContainsKey(actorUsername))
                {
                    errors.Add($"The 'actorUsername' {actorUsername} does not match any registered actor.");
                } else {

                    List<ActorPackModule> actorModules = ThespeonAPI.GetModulesWithActor(actorUsername);
                    if(actorModules.FindIndex(module => module.name == moduleName) == -1)
                    {
                        errors.Add($"The 'moduleName' {moduleName} does not match any modules for the actor {actorUsername}.\nHere are the available modules for {actorUsername}:\n{string.Join(", ", actorModules.Select(module => module.name))}");
                    } else {
                        module = actorModules.Find(module => module.name == moduleName);
                    }
                } 
            }

            if (string.IsNullOrEmpty(defaultEmotion))
            {
                warnings.Add("DefaultEmotion => Optional field is missing; the fallback Emotion will be used instead.");
            } else if(module !=null && module.emotion_options.emotions.FindIndex(emotion => emotion.emotionsetname == defaultEmotion) == -1)
            {
                warnings.Add($"The 'defaultEmotion' {defaultEmotion} does not match any emotions for the module {moduleName}. The fallback Emotion will be used instead. \nHere are the available emotions for {moduleName}:\n{string.Join(", ", module.emotion_options.emotions.Select(emotion => emotion.emotionsetname))}");
            }
            if (defaultLanguage == null)
            {
                warnings.Add("DefaultLanguage => Optional field is missing; using fallback.");
            } else if(module !=null && module.language_options.languages.FindIndex(language => language.Equals(defaultLanguage)) == -1)
            {
                warnings.Add($"The 'defaultLanguage' {defaultLanguage} does not match any languages for the module {moduleName}. The fallback Language will be used instead. \nHere are the available languages for {moduleName}:\n{string.Join(", ", module.language_options.languages)}");
            }

            if (speed == null || speed.Count == 0)
            {
                warnings.Add("Speed => No speed values provided; default speed will apply.");
            } else 
            {
                if(speed.Any(val => val <=0.1))         //HARD LIMIT SET TO 0.1, This is then enforced ThespeonInferenceHandler.RunModelCoroutine
                {
                    warnings.Add("Speed => Some Speed multiplier(s) is less than or equal to 0.1 and will be clamped to 0.1.");
                }  
                if(speed.Any(val => val < 0.5 || val > 2))           //What limits?
                {
                    warnings.Add("Speed => Some Speed multiplier falls outside the interval 0.5 and 2. This may affect the quality of the output.");
                }
            }


            if (loudness == null || loudness.Count == 0)
            {
                warnings.Add("Loudness => No loudness values provided; default loudness will apply.");
            } else 
            {
                if(loudness.Any(val => val <=0))         //HARD LIMIT SET TO 0 (inclusive), This is then enforced ThespeonInferenceHandler.RunModelCoroutine
                {
                    warnings.Add("Loudness => Some Loudness multiplier(s) is less than or equal to 0 and will be clamped to 0.");
                } 
                if(loudness.Any(val => val > 2))           //What limits?
                {
                    warnings.Add("Loudness => Some Loudness multiplier is larger than 2. This may lead to audio distortion.");
                }
            }

            if (extraData != null && extraData.Count > 0)
            {
                string extraDataFeedback = "Some top-level properties were not recognized by the UserModelInput class:\n";
                foreach (var kvp in extraData)
                {
                    extraDataFeedback += $"Key = {kvp.Key}, Value = {kvp.Value}\n";
                }
                warnings.Add($"ExtraData => {extraDataFeedback}");
            }

            if (segments == null || segments.Count == 0)
            {
                errors.Add("No segments provided; 'segments' cannot be empty.");
            }
            else
            {
                for (int i = 0; i < segments.Count; i++)
                {

                    var segment = segments[i];

                    var (segErrors, segWarnings)  = segment.ValidateUserSegment(module);

                    foreach (var e in segErrors)
                    {
                        if (!string.IsNullOrEmpty(e))
                        {
                            errors.Add($"Segment {i + 1}: {e}");
                        }
                    }
                    foreach (var w in segWarnings)
                    {
                        if (!string.IsNullOrEmpty(w))
                        {
                            warnings.Add($"Segment {i + 1}: {w}");
                        }
                    }
                }
            }

            //complexityScore = segments.Count;
            if (errors.Count > 0)
            {
                UnityEngine.Debug.LogError($"Validation failed with {errors.Count} error(s):\n {string.Join("\n--------------------\n", errors)}\n-----------------------------------\n and {warnings.Count} warning(s):\n {string.Join("\n--------------------\n", warnings)}");
                throw new InvalidOperationException(
                $"Validation failed with {errors.Count} error(s):\n {string.Join("\n--------------------\n", errors)}\n-----------------------------------\n and {warnings.Count} warning(s):\n {string.Join("\n--------------------\n", warnings)}");
            }

            foreach (var w in warnings)
            {
                UnityEngine.Debug.LogWarning($"Warning: {w}");
            }
            

            return (errors, warnings);
        }

        /// <summary>
        /// Generates a NestedSummary object summarizing the effective languages and emotions used in the input segments.
        /// </summary>
        /// <returns>A NestedSummary containing grouped statistics on languages and emotions.</returns>
        public NestedSummary MakeNestedSummary()
        {
            // Create the result object
            var result = new NestedSummary();

            if (segments == null || segments.Count == 0)
            {
                // Just set the warning
                result.Warning = "No segments available to summarize.";
                return result;
            }

            // 1) Group by “effective language”
            var languageGroups = segments.GroupBy(seg => seg.languageObj ?? defaultLanguage);

            // We'll build the list of LanguageSummary objects
            var finalList = new List<LanguageSummary>();

            foreach (var languageGroup in languageGroups)
            {
                if (languageGroup.Key == null)
                    continue;

                // 2) Group by “effective emotion”
                var emotionGroups = languageGroup
                    .GroupBy(seg => string.IsNullOrEmpty(seg.emotion)
                                    ? defaultEmotion
                                    : seg.emotion);

                var emotionDataDict = new Dictionary<string, EmotionStat>();

                foreach (var emotionGroup in emotionGroups)
                {
                    string usedEmotion = string.IsNullOrEmpty(emotionGroup.Key)
                        ? "UnknownEmotion"
                        : emotionGroup.Key;

                    int totalChars = emotionGroup
                        .Where(s => !string.IsNullOrWhiteSpace(s.text))
                        .Sum(s => s.text.Length);

                    var emotionStat = new EmotionStat
                    {
                        EmotionName     = usedEmotion,
                        NumberOfChars   = totalChars,
                        // 'Quality' remains null unless you set it somewhere
                    };

                    emotionDataDict[usedEmotion] = emotionStat;
                }

                // 3) Add an entry for this language group
                finalList.Add(new LanguageSummary
                {
                    // If languageObj / defaultLanguage is not a string, you'll need to convert it
                    Language = languageGroup.Key, 
                    Emotions = emotionDataDict
                });
            }

            result.Summary = finalList;
            return result;
        }

                    
        /// <summary>
        /// Returns a JSON-formatted string representation of the UserModelInput instance.
        /// </summary>
        /// <returns>A JSON string representing the UserModelInput object.</returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }


    /// <summary>
    /// A user-facing segment with only text-based fields.
    /// </summary>
    public class UserSegment
    {
        #nullable enable
        [JsonProperty("text", Required = Required.Always)]
        public string text { get; set; }

        [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
        public Language? languageObj { get; set; }

        [JsonProperty("emotion", NullValueHandling = NullValueHandling.Ignore)]
        public string? emotion { get; set; }

        [JsonProperty("style", NullValueHandling = NullValueHandling.Ignore)]
        public string? style { get; set; }

        [JsonProperty("IsCustomPhonemized", NullValueHandling = NullValueHandling.Ignore)]
        public bool? isCustomPhonemized { get; set; }

        [JsonProperty("heteronymDescription", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> heteronymDescription { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JToken>? extraData { get; set; }
        #nullable disable
        /// <summary>
        /// Initializes a new instance of the UserSegment class.
        /// </summary>
        public UserSegment() 
        {
        }
        /// <summary>
        /// Initializes a new instance of the UserSegment class by copying properties from another instance. This is a shallow copy.
        /// </summary>
        /// /// <param name="other">The UserSegment instance to copy from.</param>
        public UserSegment(UserSegment other)
        {
            text = other.text;
            languageObj = other.languageObj; // Language is a reference type, so this is a shallow copy
            // languageObj = new Language(other.languageObj); // Change to this at some point for proper copying. OBS Untested but the copy constructor exists.
            emotion = other.emotion;
            style = other.style;
            isCustomPhonemized = other.isCustomPhonemized;
            heteronymDescription = other.heteronymDescription; // Heteronym description is a reference type, so this is a shallow copy
            // heteronymDescription = new Dictionary<string, string>(other.heteronymDescription); // Change to this at some point for proper copying. OBS Untested.
            extraData = other.extraData; // Extra data is not used in the synth request
        }

        /// <summary>
        /// Initializes a new instance of the UserSegment class with specified values.
        /// </summary>
        /// <param name="text">The text content of the segment. Cannot be empty.</param>
        /// <param name="language">The optional language of the segment.</param>
        /// <param name="emotion">The optional emotion of this segment.</param>
        /// <param name="style">Not supported yet.</param>
        /// <param name="isCustomPhonemized">Specifies whether the text in the segment is pre-phonemized.</param>
        /// <param name="heteronymDescription">Not supported yet.</param>
        /// <param name="extraData">A dictionary capturing additional unknown properties. Will be ignored during inference. </param>
        public UserSegment(
            string text, 
            Language language = null, 
            string emotion = null, 
            string style = null, 
            bool? isCustomPhonemized = null, 
            Dictionary<string, string> heteronymDescription = null, 
            Dictionary<string, JToken> extraData = null)
        {
            this.text = text;
            this.languageObj = language;
            this.emotion = emotion;
            this.style = style;
            this.isCustomPhonemized = isCustomPhonemized;
            this.heteronymDescription = heteronymDescription;
            this.extraData = extraData;  // Extra data is not used in the synth request 
        }

        /// <summary>
        /// Validates the UserSegment instance, checking for errors and warnings based on its properties.
        /// </summary>
        /// <param name="module">An optional module that provides validation rules for emotions and languages.</param>
        /// <returns>A tuple containing a list of errors and a list of warnings.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the 'text' property is empty or null.
        /// </exception>
        public (List<string> errors, List<string> warnings) ValidateUserSegment(ActorPackModule module = null)
        {

            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            string jsonData = JsonConvert.SerializeObject(extraData, Formatting.Indented);
            if (string.IsNullOrEmpty(text))
            {
                errors.Add("Text => Text cannot be empty.");
                throw new InvalidOperationException("Text cannot be empty.");
            }
            if(!string.IsNullOrEmpty(style)){
                warnings.Add("Style => The Style field is currently not supported by the Thespeon Engine and will be ignored.");
            }
            if(module!=null){
                if(!string.IsNullOrEmpty(emotion)){
                    if(module.emotion_options.emotions.FindIndex(emotion => emotion.emotionsetname == this.emotion) == -1)
                    {
                        warnings.Add($"Emotion => The 'emotion' {emotion} in segment with text: '{text}' does not match any emotions for the module {module.name}. \nHere are the available emotions for {module.name}:\n{string.Join(", ", module.emotion_options.emotions.Select(emotion => emotion.emotionsetname))}");
                    }
                }
                if(languageObj!=null){
                    if(module.language_options.languages.FindIndex(language => language.Equals(languageObj)) == -1)
                    {
                        warnings.Add($"Language => The 'language' {languageObj} in segment with text: '{text}' does not match any languages for the module {module.name}. \nHere are the available languages for {module.name}:\n{string.Join(", ", module.language_options.languages)}");
                    }
                }
            }

            //Some IPA check?
            if(isCustomPhonemized == true && module!=null)
            {
                if(text.Any(c => !module.phonemes_table.symbol_to_id.ContainsKey(c.ToString())))
                {
                    warnings.Add($"IsCustomPhonemized => The text {text} har been marked as custom phonemized but contains characters not supported byt this actorpack, which is not allowed for custom phonemization.\n These characters will be removed: {string.Join(", ", text.Where(c => !module.phonemes_table.symbol_to_id.ContainsKey(c.ToString())))} \nThese are all the supported characters: {string.Join(", ", module.phonemes_table.symbol_to_id.Keys)}");
                }
            }

            if (extraData!=null)
            {
               warnings.Add($"ExtraData => Extra data is ignored and not used in the synth request. The data ignored is: \n" + jsonData); 
            }

            return (errors, warnings);
        }
        /// <summary>
        /// Compares this segment's annotation with another, ignoring differences in text content.
        /// </summary>
        /// <param name="other">The UserSegment instance to compare against.</param>
        /// <returns>True if all properties except text are equal; otherwise, false.</returns>

        public bool EqualsIgnoringText(UserSegment other)
        {
            if (this == null || other == null) return this == other;
            bool langEqual = false;
            if (this.languageObj != null)
            {
                langEqual = this.languageObj.Equals(other.languageObj);
            }
            else
            {
                langEqual = other.languageObj == null;
            }
            
            return 
                langEqual &&
                string.Equals(this.emotion, other.emotion, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(this.style, other.style, StringComparison.OrdinalIgnoreCase) &&
                this.isCustomPhonemized == other.isCustomPhonemized &&
                (this.heteronymDescription?.SequenceEqual(other.heteronymDescription ?? new Dictionary<string, string>()) ?? true);
                // (segment1.extraData?.SequenceEqual(segment2.extraData ?? new Dictionary<string, JToken>()) ?? true);
        }
        
        /// <summary>
        /// Gets or sets the value of a property dynamically by name.
        /// </summary>
        /// <param name="propertyName">The name of the property to access.</param>
        /// <returns>The value of the specified property.</returns>
        public object this[string propertyName]
        {
            get
            {
                var property = typeof(UserSegment).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                return property?.GetValue(this);
            }
            set
            {
                var property = typeof(UserSegment).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(this, value);
                }
            }
        }

    }
    public class Version
    {
        [JsonProperty("major")] public int major { get; set; }
        [JsonProperty("minor")] public int minor { get; set; }
        [JsonProperty("patch")] public int patch { get; set; }
        public override string ToString() => $"{major}.{minor}.{patch}";
    }


    public class NestedSummary
    {
        [JsonProperty("warning")]
        public string Warning { get; set; }

        [JsonProperty("summary")]
        public List<LanguageSummary> Summary { get; set; }


        public override string ToString()
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Warning))
            {
                sb.AppendLine($"Warning: {Warning}");
                return sb.ToString();  // If there's a warning, no need to show further details.
            }

            if (Summary == null || Summary.Count == 0)
            {
                sb.AppendLine("No summary data available.");
                return sb.ToString();
            }

            sb.AppendLine("Summary:");

            foreach (var langSummary in Summary)
            {
                sb.AppendLine($"- Language: {langSummary.Language}");

                if (langSummary.Emotions == null || langSummary.Emotions.Count == 0)
                {
                    sb.AppendLine("  (No emotions recorded)");
                    continue;
                }

                foreach (var (emotionName, emotionStat) in langSummary.Emotions)
                {
                    string qualityText = string.IsNullOrEmpty(emotionStat.Quality) ? "N/A" : emotionStat.Quality;
                    sb.AppendLine($"  - Emotion: {emotionStat.EmotionName}, Chars: {emotionStat.NumberOfChars}, Quality: {qualityText}");
                }
            }

            return sb.ToString();
        }
    }

    public class LanguageSummary
    {
        // If you have a custom Language enum or class, change to that type instead of string
        [JsonProperty("language")]
        public Language Language { get; set; }

        [JsonProperty("emotions")]
        public Dictionary<string, EmotionStat> Emotions { get; set; }
    }

    public class EmotionStat
    {
        [JsonProperty("emotionName")]
        public string EmotionName { get; set; }

        [JsonProperty("number_of_chars")]
        public int NumberOfChars { get; set; }

        // Shown as null by default in your sample
        [JsonProperty("quality")]
        public string Quality { get; set; }
    }

}