// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using Newtonsoft.Json.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json.Converters;


namespace Lingotion.Thespeon.Utils
{
    [Serializable]
    public class ActorPack
    {
        [JsonProperty("build_info")]
        public BuildInfo build_info { get; set; }

        [JsonProperty("type")]
        public string type { get; set; }

        [JsonProperty("version")]
        public Version version { get; set; }

        [JsonProperty("platform")]
        public string platform { get; set; }

        [JsonProperty("modules")]
        public List<ActorPackModule> modules { get; set; }

        [JsonProperty("files")]
        public Dictionary<string, FileEntry> files { get; set; }
        [JsonProperty("name")]
        public string name { get; set; }
        [JsonProperty("createtime")]
        public string createtime { get; set; }  //fixthis so its some kind of date object instead.

        /// <summary>
        /// Retrieves the list of modules in the actor pack
        /// </summary>
        public List<ActorPackModule> GetModules() 
        {
            return modules;
        }

        /// <summary>
        /// Retrieves the list of LanguageModules in the actor pack or optionally in a specific module.
        /// </summary>
        public List<PhonemizerModule> GetLanguageModules(string moduleName = null)
        {
            List<PhonemizerModule> phonemizerModules = new List<PhonemizerModule>();

            if (modules == null) Debug.LogError("No modules have been imported. Import modules or visit portal.lingotion.com");

            if (moduleName != null)
            {
                //take the module with module.name==moduleName from modules
                ActorPackModule module = modules.FirstOrDefault(m => m.name == moduleName);
                if (module == null) Debug.LogError($"Module with module name {moduleName} is not present in this Actor Pack");
                if (module.phonemizer_setup?.modules != null)
                {
                    foreach (var phonemizerModule in module.phonemizer_setup.modules.Values)
                    {
                        phonemizerModules.Add(phonemizerModule);
                    }
                } else
                {
                    Debug.LogError($"No phonemizer modules found in module {moduleName}");
                }
            } else
            {
                foreach (var module in modules)
                {

                    if (module.phonemizer_setup?.modules != null)
                    {
                        foreach (var phonemizerModule in module.phonemizer_setup.modules.Values)
                        {
                            phonemizerModules.Add(phonemizerModule);
                        }
                    }
                }
            }


            return phonemizerModules;
        }
        /// <summary>
        /// Retrieves a list of all available actors in the ActorPack or optionally all actors in a specific module.
        /// </summary>
        public List<Actor> GetActors(string moduleName=null)        //QUESTION name or object?  object is large but name only works if it is unique. 
        {
            List<Actor> actors = new List<Actor>();

            if (modules == null) Debug.LogError("No modules have been imported. Import modules or visit portal.lingotion.com");
            if(moduleName==null)
            {
                foreach (var module in modules)
                {
                    if (module.model_options?.recording_data_info?.actors != null)
                    {
                        foreach (var actorEntry in module.model_options.recording_data_info.actors.Values)
                        {
                            actors.Add(actorEntry.actor);
                        }
                    }
                }
            } else 
            {
                //take the module with module.name==moduleName from modules
                ActorPackModule module = modules.FirstOrDefault(m => m.name == moduleName);
                if(module == null) Debug.LogError($"Module with module name {moduleName} is not present in this Actor Pack");
                if (module.model_options?.recording_data_info?.actors != null)
                {
                    foreach (var actorEntry in module.model_options.recording_data_info.actors.Values)
                    {
                        actors.Add(actorEntry.actor);
                    }
                }
            }

            return actors;
        }

        /// <summary>
        /// Retrieves a list of languages for the given actor, or all languages in the ActorPack if no actor is specified.
        /// </summary>
        public List<Language> GetLanguages(Actor actor = null)      // moduleName=null ska in här också
        {
            HashSet<Language> languages = new HashSet<Language>();

            foreach (var module in modules)
            {
                if (actor == null)
                {
                    // Collect all available languages from the ActorPack
                    if (module.language_options?.languages != null)
                    {
                        foreach (var lang in module.language_options.languages)
                        {
                            languages.Add(lang);
                        }
                    }
                }
                else
                {
                    // Find languages only for the specified actor
                    if (module.model_options?.recording_data_info?.actors != null &&
                        module.model_options.recording_data_info.actors.TryGetValue(actor.actorkey.ToString(), out var actorData))
                    {
                        foreach (var langEntry in actorData.languages.Values)
                        {
                            languages.Add(langEntry.language);
                        }
                    }
                }
            }

            return languages.ToList();
        }

        /// <summary>
        /// Retrieves all emotions based on Actor and/or Language.
        /// If neither is specified, returns all emotions in the ActorPack.
        /// If only Actor is specified, returns all emotions associated with them.
        /// If only Language is specified, returns all emotions available for that language.
        /// If both are specified, returns the emotions available for that specific Actor-Language combination.
        /// </summary>
        public List<Emotion> GetEmotions(Actor actor = null, Language language = null) // moduleName=null ska in här också
        {
            HashSet<Emotion> emotions = new HashSet<Emotion>();

            foreach (var module in modules)
            {
                if (actor == null && language == null)
                {
                    // Return all emotions in the ActorPack
                    if (module.emotion_options?.emotions != null)
                    {
                        foreach (var emotion in module.emotion_options.emotions)
                        {
                            emotions.Add(emotion);
                        }
                    }
                }
                else if (actor != null && language == null)
                {
                    // Get all emotions associated with the Actor
                    if (module.model_options?.recording_data_info?.actors != null &&
                        module.model_options.recording_data_info.actors.TryGetValue(actor.actorkey.ToString(), out var actorData))
                    {
                        foreach (var langEntry in actorData.languages.Values)
                        {
                            foreach (var emotionEntry in langEntry.emotions.Values)
                            {
                                emotions.Add(emotionEntry.emotion);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError($"Error: Actor '{actor.firstname} {actor.lastname}' not found in ActorPack.");
                        return new List<Emotion>();
                    }
                }
                else if (actor == null && language != null)
                {
                    // Get all emotions associated with the specified Language
                    foreach (var actorEntry in module.model_options?.recording_data_info?.actors?.Values ?? Enumerable.Empty<ActorData>())
                    {
                        foreach (var langEntry in actorEntry.languages.Values)
                        {
                            if (langEntry.language.iso639_2 == language.iso639_2)
                            {
                                foreach (var emotionEntry in langEntry.emotions.Values)
                                {
                                    emotions.Add(emotionEntry.emotion);
                                }
                            }
                        }
                    }

                    if (emotions.Count == 0)
                    {
                        //Debug.LogError($"Error: No emotions found for language '{language.languagecode.nameinenglish}'.");
                        return new List<Emotion>();
                    }
                }
                else
                {
                    // Fix: Use correct key for actor's language lookup
                    if (module.model_options?.recording_data_info?.actors != null &&
                        module.model_options.recording_data_info.actors.TryGetValue(actor.actorkey.ToString(), out var actorData))
                    {
                        string languageKeyString = language.languageKey.ToString(); // Convert to match dictionary key
                        // Debug.Log($"Checking if Actor '{actor.firstname} {actor.lastname}' speaks '{language.iso639_2}' (Key: {languageKeyString})");

                        if (actorData.languages.ContainsKey(languageKeyString))
                        {
                            var langEntry = actorData.languages[languageKeyString];
                            foreach (var emotionEntry in langEntry.emotions.Values)
                            {
                                emotions.Add(emotionEntry.emotion);
                            }
                        }
                        else
                        {
                            Debug.LogError($"Error: Actor '{actor.firstname} {actor.lastname}' does not speak '{language.iso639_2}' (Key: {languageKeyString}).");
                            Debug.Log($"Available keys for this actor: {string.Join(", ", actorData.languages.Keys)}");
                            return new List<Emotion>();
                        }
                    }
                    else
                    {
                        Debug.LogError($"Error: Actor '{actor.firstname} {actor.lastname}' not found in ActorPack.");
                        return new List<Emotion>();
                    }
                }
            }
            return emotions.ToList();
        }

        public string GetFile(string md5sum)
        {
            if (files == null)
            {
                Debug.LogError("No files have been imported. Import files or visit portal.lingotion.com");
                return null;
            }

            if (files.TryGetValue(md5sum, out var fileEntry))
            {
                return fileEntry.filename;
            }
            else
            {
                Debug.LogError($"File with md5sum {md5sum} not found in this Actor Pack.");
                return null;
            }
        }


        /// <summary>
        /// Given a Language object, return associated integer language key from LanguageOptions for the closes language match.
        /// Logs an error if the provided Language is null.
        /// </summary>
        /// <param name="language">The Language object from which to retrieve the language key.</param>
        /// <returns>An integer representing the language key.</returns>
        public int GetLanguageKey(Language language)
        {
            if (language == null)
            {
                Debug.LogError("Cannot retrieve language key because the Language object is null.");
                return -1;  // or throw an exception if you prefer
            }

            foreach (var module in modules)
            {
                if (module.language_options?.languages != null)
                {
                    var candidates = module.language_options.languages;
                    
                    var (closest, _, _ )=   LanguageExtensions.FindClosestLanguage(language, candidates);
                    return closest.languageKey ?? -1;
                }
            }
            
            return language.languageKey ?? -1; // Provide a default value if null
        }

        /// <summary>
        /// Given an Emotion setname, return its associated integer emotion key from EmotionOptions
        /// Logs an error if the provided Emotion setname is null.
        /// </summary>
        /// <param name="emotionSetName">The setname of the Emotion object from which to retrieve the emotion key.</param>
        /// <returns>An integer representing the emotion key.</returns>
        public int GetEmotionKey(string emotionSetName)
        {
            if (emotionSetName == null)
            {
                Debug.LogError("Cannot retrieve emotion key because the Emotion setname is null.");
                return -1;  // or throw an exception if you prefer
            }

            foreach (var module in modules)
            {
                if (module.emotion_options?.emotions != null)
                {
                    foreach (var emotion in module.emotion_options.emotions)
                    {
                        if (emotion.emotionsetname == emotionSetName)
                        {
                            return emotion.emotionsetkey;
                        }
                    }
                }
            }

            Debug.LogError($"Emotion with setname {emotionSetName} not found in this Actor Pack.");
            return -1;
        }
        
    }

    [Serializable]
    public class BuildInfo
    {
        [JsonProperty("source_base_name")]
        public string SourceBaseName { get; set; }
    }

    // [Serializable]
    // public class Version
    // {
    //     [JsonProperty("major")]
    //     public int major { get; set; }
        
    //     [JsonProperty("minor")]
    //     public int minor { get; set; }
        
    //     [JsonProperty("patch")]
    //     public int patch { get; set; }
    // }

    [Serializable]
    public class ActorPackModule
    {
        [JsonProperty("type")]
        public string type { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("tags")]
        public ActorTags module_tags { get; set; }

        [JsonProperty("model_options")]
        public ModelOptions model_options { get; set; }

        [JsonProperty("actor_options")]
        public ActorOptions actor_options { get; set; }

        [JsonProperty("language_options")]
        public LanguageOptions language_options { get; set; }

        [JsonProperty("emotion_options")]
        public EmotionOptions emotion_options { get; set; }

        [JsonProperty("phonemes_table")]
        public PhonemesTable phonemes_table { get; set; }

        [JsonProperty("phonemizer_setup")]
        public PhonemizerSetup phonemizer_setup { get; set; }

        [JsonProperty("config")]
        public Config config { get; set; }

        [JsonProperty("sentisfiles")]
        public Dictionary<string, string> sentisfiles { get; set; }

        public List<Language> GetActorLanguages(Actor actor)
        {
            if (model_options?.recording_data_info?.actors != null &&
                model_options.recording_data_info.actors.TryGetValue(actor.actorkey.ToString(), out var actorData))
            {
                return actorData.languages.Values.Select(langData => langData.language).ToList();
            }
            else
            {
                Debug.LogError($"Error: Actor '{actor.firstname} {actor.lastname}' not found in ActorPack.");
                return new List<Language>();
            }   
        }
        public Actor GetActor(string actorUsername)
        {
            if (actor_options?.actors != null)
            {
                return actor_options.actors.FirstOrDefault(a => a.username == actorUsername);
            }
            else
            {
                Debug.LogError("No actors found in this module.");
                return null;
            }
        }

        /// <summary>
        /// Retrieves the a dictionary of emotion setnames and their associated emotion keys.
        /// </summary>
        public Dictionary<string, int> GetEmotionKeyDictionary()
        {
            Dictionary<string, int> emotionKeyDict = new Dictionary<string, int>();

            if (emotion_options?.emotions != null)
            {
                foreach (var emotion in emotion_options.emotions)
                {
                    emotionKeyDict.Add(emotion.emotionsetname, emotion.emotionsetkey);
                }
            }

            return emotionKeyDict;
        }

        /// <summary>
        /// Retrieves the a dictionary of language codes and their associated language keys.    
        /// </summary>
        /* public Dictionary<Language, int> GetLanguageKeyDictionary()
        {
            Dictionary<string, int> languageKeyDict = new Dictionary<string, int>();

            if (language_options?.languages != null)
            {
                foreach (var language in language_options.languages)
                {
                    languageKeyDict.Add(language, language.languageKey ?? -1);
                }
            }

            return languageKeyDict;
        } */



        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"ActorPackModule: {name}");
            sb.AppendLine($"  Type: {type}");
            sb.AppendLine($"  Model Options: {(model_options != null ? "Available" : "None")}");
            sb.AppendLine($"  Actor Options: {(actor_options != null ? $"Actors Count: {actor_options.actors?.Count ?? 0}" : "None")}");
            sb.AppendLine($"  Language Options: {(language_options != null ? $"Languages Count: {language_options.languages?.Count ?? 0}" : "None")}");
            sb.AppendLine($"  Emotion Options: {(emotion_options != null ? $"Emotions Count: {emotion_options.emotions?.Count ?? 0}" : "None")}");
            sb.AppendLine($"  Phonemes Table: {(phonemes_table != null ? "Available" : "None")}");
            sb.AppendLine($"  Phonemizer Setup: {(phonemizer_setup != null ? $"Modules Count: {phonemizer_setup.modules?.Count ?? 0}" : "None")}");
            sb.AppendLine($"  Config: {(config != null ? "Available" : "None")}");
            sb.AppendLine($"  Sentis Files: {(sentisfiles != null ? $"Count: {sentisfiles.Count}" : "None")}");
            return sb.ToString();
        }
    }

    public class ActorTags
    {
        #nullable enable
        [JsonProperty("quality")]
        [JsonConverter(typeof(StringEnumConverter))]
        public Quality? quality { get; set; }
        #nullable disable
        
        public ActorTags(){} // Default constructor for JSON deserialization
        public ActorTags(string quality)
        {
            if (Enum.TryParse(typeof(Quality), quality, true, out var parsedQuality))
            {
                this.quality = (Quality)parsedQuality;
            }
            else
            {
                Debug.LogError($"Invalid quality value: {quality}");
                this.quality = null; // or set to a default value
            }
        } 
        public enum Quality
        {
            Ultralow,
            Low,
            Mid,
            High,
            Ultrahigh
        }

        public override string ToString()
        {
            return $"quality: {quality}";
        }

        public override bool Equals(object obj)
        {
            return obj is ActorTags tags &&
                   quality == tags.quality;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(quality);
        }
    }

    [Serializable]
    public class ModelOptions
    {

        [JsonProperty("version")]
        public Version version { get; set; }

        [JsonProperty("recording_data_info")]
        public RecordingDataInfo recording_data_info { get; set; }
    }

    [Serializable]
    public class RecordingDataInfo
    {
        [JsonProperty("actors")]
        public Dictionary<string, ActorData> actors { get; set; }

        public override string ToString()
        {
            if (actors == null || actors.Count == 0)
            {
                return "No recording data available.";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Recording Data Info ===");

            foreach (var actorEntry in actors)
            {
                ActorData actorData = actorEntry.Value;
                sb.AppendLine($"  - Actor: {actorData.actor.firstname} {actorData.actor.lastname} ({actorData.actor.username})");
                
                if (actorData.languages != null && actorData.languages.Count > 0)
                {
                    sb.AppendLine("    ↳ Languages:");
                    foreach (var languageEntry in actorData.languages.Values)
                    {
                        Language lang = languageEntry.language;
                        sb.AppendLine($"      • {lang.iso639_2} ({lang.iso639_2}) [{lang.iso639_2}]");

                        if (languageEntry.emotions != null && languageEntry.emotions.Count > 0)
                        {
                            sb.AppendLine("        ↳ Emotions:");
                            foreach (var emotionEntry in languageEntry.emotions.Values)
                            {
                                Emotion emotion = emotionEntry.emotion;
                                sb.AppendLine($"          - {emotion.emotionsetname} (Quality:  {emotionEntry.quality})");
                            }
                        }
                        else
                        {
                            sb.AppendLine("        ↳ No emotions recorded.");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("    ↳ No languages available.");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    [Serializable]
    public class ActorData
    {
        [JsonProperty("actor")]
        public Actor actor { get; set; }

        [JsonProperty("languages")]
        public Dictionary<string, LanguageData> languages { get; set; }
    }

    [Serializable]
    public class Actor
    {
        [JsonProperty("actoruserkey")]
        public string actoruserkey { get; set; }

        [JsonProperty("email")]
        public string email { get; set; }

        [JsonProperty("username")]
        public string username { get; set; }

        [JsonProperty("firstname")]
        public string firstname { get; set; }

        [JsonProperty("lastname")]
        public string lastname { get; set; }

        [JsonProperty("actorkey")]
        public int actorkey { get; set; }

        public override string ToString()
        {
            return $"Actor: actoruserkey: {actoruserkey}, {firstname} {lastname} ({username}), actorkey: {actorkey}";
        }
        public string GetUsername()
        {
            return username;
        }



    }

    [Serializable]
    public class LanguageData
    {
        [JsonProperty("language")]
        public Language language { get; set; }

        [JsonProperty("emotions")]
        public Dictionary<string, EmotionData> emotions { get; set; }
    }

    /* [Serializable]
    public class Language       
    {
        [JsonProperty("iso639_2")]
        public string iso639_2 { get; set; }

        [JsonProperty("iso639_3")]
        public string iso639_3 { get; set; }

        [JsonProperty("glottocode")]
        public string glottocode { get; set; }

        [JsonProperty("iso3166_1")]
        public string iso3166_1 { get; set; }

        [JsonProperty("iso3166_2")]
        public string iso3166_2 { get; set; }

        [JsonProperty("customdialect")]
        public string customdialect { get; set; }

        [JsonProperty("languagekey")]
        public int languagekey { get; set; }

        public List<string> GetValues()
        {
            return new List<string> { iso639_2, iso639_3, glottocode, iso3166_1, iso3166_2, customdialect };
        }
        public Dictionary<string,string> GetItems()
        {
            return new Dictionary<string, string>
            {
                {"iso639_2", iso639_2},
                {"iso639_3", iso639_3},
                {"glottocode", glottocode},
                {"iso3166_1", iso3166_1},
                {"iso3166_2", iso3166_2},
                {"customdialect", customdialect}
            };
        }

        public override string ToString()
        {
            string res = $"{iso639_2}";
            if(iso3166_1 != null && customdialect!=null)
            {
                res += $" ({iso3166_1} - {customdialect})";
            }
            else if(iso3166_1!=null)
            {
                res += $" ({iso3166_1})";
            }
            else if(customdialect!=null)
            {
                res += $" ({customdialect})";
            }
            return res;
        }
        public string toString()        //Eyo dont be removing my methods man. This guy be acting kinda sus ngl frfr.
        {
            return "iso639_2: " + iso639_2 + " iso639_3: " + iso639_3 + " glottocode: " + glottocode + " iso3166_1: " + iso3166_1 + " iso3166_2: " + iso3166_2 + " customdialect: " + customdialect;
        }
        public override int GetHashCode()
        {
            return toString().GetHashCode();
        }

    } */


    [Serializable]
    public class LanguageCode
    {

        [JsonProperty("languagecodekey")]
        public int languagecodekey { get; set; }

        [JsonProperty("iso639_1")]
        public string iso639_1 { get; set; }

        [JsonProperty("iso639_2")]
        public string iso639_2 { get; set; }

        [JsonProperty("nameinenglish")]
        public string nameinenglish { get; set; }

        [JsonProperty("autonym")]
        public string autonym { get; set; }
    }

    [Serializable]
    public class CountryCode
    {

        [JsonProperty("iso3166_1")]
        public string iso3166_1 { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }
    }

    [Serializable]
    public class SubRegionCode
    {

        [JsonProperty("iso3166_2")]
        public string iso3166_2 { get; set; }

        [JsonProperty("iso3166_1")]
        public string iso3166_1 { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }
    }

    [Serializable]
    public class EmotionData
    {
        [JsonProperty("emotion")]
        public Emotion emotion { get; set; }

        [JsonProperty("quality")]
        public string quality { get; set; }


    }

    [Serializable]
    public class Emotion
    {
        [JsonProperty("emotionsetkey")]
        public int emotionsetkey { get; set; }

        [JsonProperty("emotionsetname")]
        public string emotionsetname { get; set; }
        public override string ToString()
        {
            return $"{emotionsetname} (ID: {emotionsetkey})";
        }
    }

    [Serializable]
    public class ActorOptions
    {

        [JsonProperty("version")] 
        public Version version { get; set; }

        [JsonProperty("actors")]
        public List<Actor> actors { get; set; }

        [JsonProperty("actors_size")]
        public int actors_size { get; set; }
    }

    [Serializable]
    public class LanguageOptions
    {

        [JsonProperty("version")]
        public Version version { get; set; }

        [JsonProperty("languages")]
        public List<Language> languages { get; set; }

        [JsonProperty("languages_size")]
        public int languages_size { get; set; }
    }

    [Serializable]
    public class EmotionOptions
    {

        [JsonProperty("version")]
        public Version version { get; set; }

        [JsonProperty("emotions")]
        public List<Emotion> emotions { get; set; }

        [JsonProperty("emotions_size")]
        public int emotions_size { get; set; }
    }

    [Serializable]
    public class PhonemesTable
    {

        [JsonProperty("symbol_to_id")]   
        public Dictionary<string, int> symbol_to_id { get; set; }

        [JsonProperty("id_to_symbol")]
        public Dictionary<string, string> id_to_symbol { get; set; }

        [JsonProperty("word_splitters")]
        public List<WordSplitter> word_splitters { get; set; }

        [JsonProperty("vocab_size")]
        public int vocab_size { get; set; }
    }

    [Serializable]
    public class WordSplitter
    {

        [JsonProperty("symbol")]
        public string symbol { get; set; }

        [JsonProperty("id")]
        public int id { get; set; }
    }

    [Serializable]
    public class PhonemizerSetup
    {

        [JsonProperty("modules")]
        public Dictionary<string, PhonemizerModule> modules { get; set; }
    }

    [Serializable]
    public class PhonemizerModule
    {

        [JsonProperty("type")]
        public string type { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("module_id")]
        public string module_id { get; set; }

        [JsonProperty("languages")]
        public List<Language> languages { get; set; }

        [JsonProperty("properties")]
        public Dictionary<string, string> properties { get; set; }

        [JsonProperty("files")]
        public Dictionary<string, string> files { get; set; }

        [JsonProperty("languagepack")]
        public string languagepack { get; set; }
    }

    [Serializable]
    public class Config
    {

        [JsonProperty("sampling_rate")]
        public int sampling_rate{ get; set; }

        [JsonProperty("n_mel_channels")]
        public int n_mel_channels{ get; set; }

        [JsonProperty("resblock")]
        public int resblock{ get; set; }

        [JsonProperty("decoder_chunk_length")]
        public int decoder_chunk_length{ get; set; }

        [JsonProperty("chunk_boundary_length_ratio")]
        public int chunk_boundary_length_ratio{ get; set; }

        [JsonProperty("upsample_rates")]
        public List<int> upsample_rates{ get; set; }

        [JsonProperty("upsample_kernel_sizes")]
        public List<int> upsample_kernel_sizes{ get; set; }

        [JsonProperty("upsample_initial_channel")]
        public int upsample_initial_channel{ get; set; }

        [JsonProperty("resblock_kernel_sizes")]
        public List<int> resblock_kernel_sizes{ get; set; }

        [JsonProperty("resblock_dilation_sizes")]
        public List<List<int>> resblock_dilation_sizes{ get; set; }

        [JsonProperty("pre_conv_kernel")]
        public int pre_conv_kernel{ get; set; }

        [JsonProperty("post_conv_kernel")]
        public int post_conv_kernel{ get; set; }
    }

    [Serializable]
    public class FileEntry
    {

        [JsonProperty("type")]
        public string type{ get; set; }

        [JsonProperty("md5sum")]
        public string md5sum{ get; set; }

        [JsonProperty("filename")]
        public string filename{ get; set; }
    }


}