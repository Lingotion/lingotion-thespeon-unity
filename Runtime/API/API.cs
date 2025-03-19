// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.


using UnityEngine;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Linq;
using Unity.Sentis;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Lingotion.Thespeon.Utils;
using Lingotion.Thespeon.Filters;
using Lingotion.Thespeon.FileLoader;
using Lingotion.Thespeon.ThespeonRunscripts;

//using Unity.Android.Gradle.Manifest;


namespace Lingotion.Thespeon.API
{
    
    /// <summary>
    /// A collection of static API methods for the Lingotion Thespeon package. This class provides methods for registering and unregistering ActorPacks, preloading and unloading ActorPack modules and creating a synthetization request.
    /// </summary>
    public static class ThespeonAPI//: MonoBehaviour        //MonoBehaviour for easier testing but later should be an object or a static class.
    {
        private static Dictionary<string, ActorPack> registeredActorPacks = new Dictionary<string, ActorPack>(); 
        public static  string MAPPINGS_PATH = RuntimeFileLoader.packMappingsPath; 
        private static JObject packMappings = Init();
        public static bool isRunning = false;

        
        private static JObject Init(object config=null)
        {             
            try
            {
                // Debug.Log($"Loading PackMappings from: " + MAPPINGS_PATH);
                string jsonData = RuntimeFileLoader.LoadFileAsString(MAPPINGS_PATH);
                packMappings = JObject.Parse(jsonData);
                if(packMappings==null) 
                {
                    Debug.LogWarning($"PackMappings file empty at {MAPPINGS_PATH}. Will not be able to preload models.");
                    throw new FileNotFoundException($"PackMappings file empty at {MAPPINGS_PATH}. Will not be able to preload models.");
                }
            } catch (Exception ex)
            {
                throw new Exception($"Error reading or parsing PackMappings JSON file: {ex.Message}");
            }
            return packMappings;
        }

#region Public API
        /// <summary>
        /// Gets a list of all Actors names in the actor packs that are on disk, registered or not.
        /// </summary>
        /// <returns>A list of actorUsername strings.</returns>
        public static List<string> GetActorsAvailabeOnDisk()
        {
            List<string> actorsOnDisk = new List<string>();
            foreach(var actorPack in (JObject) packMappings["actorpacks"])
            {
                actorPack.Value["actors"].Select(a => a.ToString()).ToList();
                actorsOnDisk.AddRange(actorPack.Value["actors"].Select(a => a.ToString()).ToList());
            }
            if(actorsOnDisk.Count == 0)
            {
                Debug.LogWarning("No actors found on disk.");
            }
            return new HashSet<string>(actorsOnDisk).ToList();
        }
        /// <summary>
        /// Registers an ActorPack module and returns it. This does not load the module into memory.
        /// </summary>
        /// <param name="actorUsername">The username of the actor you wish to load.</param>
        /// <returns>The registered ActorPack object</returns>
        public static ActorPack RegisterActorPack(string actorUsername)             //Assumes actor only appears in one pack.
        {
            if(registeredActorPacks.ContainsKey(actorUsername))
            {
                // Debug.Log("ActorPack already registered.");
                return registeredActorPacks[actorUsername];
            }
            string actorPackName = FindActorPackNameFromUsername(packMappings, actorUsername);
            string path = Path.Combine(RuntimeFileLoader.GetActorPacksPath(), actorPackName, actorPackName + ".json");

            string jsonContent = RuntimeFileLoader.LoadFileAsString(path);
            ActorPack actorPack = JsonConvert.DeserializeObject<ActorPack>(jsonContent);
            if(actorPack == null)
            {
                Debug.LogError("Error deserializing ActorPack.");
                return null;
            }

            // Debug.Log("Adding ActorPack to registeredActorPacks: " + actorUsername);
            if(registeredActorPacks.Values.FirstOrDefault(ap => ap.name == actorPack.name) != null)
            {
                // Debug.Log("ActorPack already registered under another user. Adding a reference to the same object.");
            }
            registeredActorPacks[actorUsername] = registeredActorPacks.Values.FirstOrDefault(ap => ap.name == actorPack.name) ?? actorPack;
            

            return actorPack;
        }

        /// <summary>   
        /// Unregisters an ActorPack. This also unloads the module from memory if it is loaded.
        /// </summary>
        /// <param name="actorUsername">The username of the actor you wish to unregister.</param>
        /// <returns></returns>
        public static void UnregisterActorPack(string actorUsername)
        {
            if(registeredActorPacks.ContainsKey(actorUsername))
            {
                ActorPack actorPack = registeredActorPacks[actorUsername];
                
                foreach( var module in actorPack.GetModules())
                {
                    UnloadActorPackModule(module.name);
                }
                registeredActorPacks.Remove(actorUsername);
            }
            else
            {
                Debug.LogError("ActorPack was not registered.");
                return;
            }
        }

        /// <summary>   
        /// Fetches all registered ActorPackModules containing the specific username.
        /// </summary>
        /// <param name="actorUsername">The username of the actor you wish to select.</param>
        /// <returns></returns>
        public static List<ActorPackModule> GetModulesWithActor(string actorUsername)
        {
            List<ActorPackModule> result = new List<ActorPackModule>();
            
            foreach (ActorPack pack in registeredActorPacks.Values)
            {
                foreach (ActorPackModule module in pack.GetModules())
                {
                    foreach ( Actor actor in module.actor_options.actors)
                    {
                        if(actor.username.Equals(actorUsername) && !result.Contains(module))
                            result.Add(module);
                    }
                }
            }
            
            if(result.Count == 0)
                throw new ArgumentException($"Username {actorUsername} not found in packMappings.");
            return result;
        }

        /// <summary>
        /// Gets a dictionary of all registered actor name - ActorPack pairs.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, ActorPack> GetRegisteredActorPacks()
        {
            return registeredActorPacks;
        }
        
        /// <summary>
        /// Preloads an ActorPack module and the language modules necessary for full functionality. the parameter languages can be optionally used to filter which language modules to preload for restricted functionality.
        /// </summary>
        /// <param name="actorPackModuleName">Which Actor Pack Module to preload.</param>
        /// <param name="languages">Optional. If provided only Language Packs for given languages are preloaded. Otherwise all Language Packs necessary for full functionality are preloaded.</param>
        /// <returns></returns>
        public static void PreloadActorPackModule(string actorPackModuleName,  List<Language> languages=null)
        {

            if(ThespeonInferenceHandler.IsPreloaded(actorPackModuleName))
            {
                // Debug.Log("ActorPack Module already preloaded.");        //This method is called in synthesizer so it will be called multiple times in anyone preloads and should not really warn the user in that case.
                return; 
            }

            //try to find and return the registered actor pack with this actorpachmodule in it.
            ActorPack actorPack = registeredActorPacks.Values.FirstOrDefault(ap => ap.GetModules().Any(m => m.name == actorPackModuleName));
            if(actorPack == null)
            {
                Debug.LogError($"ActorPack {actorPackModuleName} has not been registered. Register it first.");
                throw new ArgumentException($"ActorPack {actorPackModuleName} has not been registered. Register it first.");

                // Fix so instead of error, we register the module and then preloading continues. Means we need to find the username corresponding to the actorPackModuleName and then register it ? 
                // How the user ended up here without registring is beyond me tho. Registring is what gives them the ActorPack object which in turn gives them the modulename to preload.
                // Only way to end up here is by manually entering the actorPackModuleName by finding it in the json file.
            } 

            ActorPackModule module = actorPack.GetModules().FirstOrDefault(m => m.name == actorPackModuleName); // actorPack from somewhere else.
            if (module == null)
            {
                Debug.LogError($"Module not found: {actorPackModuleName}");
                throw new ArgumentException($"Module not found: {actorPackModuleName}");
            }

            List<PhonemizerModule> langModules = actorPack.GetLanguageModules(actorPackModuleName); //actorPack from somewhere else.
            if (languages != null)
            {
                // Debug.Log($"Preload language pack filter provided: {string.Join(", ", languages)}");
                langModules = langModules.Where(langModule => 
                    langModule.languages.Any(lang => languages.Any(filterLang => 
                        MatchLanguage(filterLang, lang)
                    ))
                ).ToList();
            }
            if(langModules.Count == 0)
            {
                Debug.LogWarning("No language modules found for actor pack module: " + actorPackModuleName);
                throw new ArgumentException("No language modules found for actor pack module: " + actorPackModuleName);
            }

            if(!GetLoadedActorPackModules().Contains(module.name)){
                ThespeonInferenceHandler.PreloadActorPackModule(actorPack, module, packMappings);
            
            }


        }
        /// <summary>
        /// Unloads the preloaded ActorPackModule and its associated Language Packs.
        /// </summary>
        /// <param name="actorPackModuleName">Optional name of specific ActorPackModule to unload. Otherwise unloads all</param>
        public static void UnloadActorPackModule(string actorPackModuleName=null)
        {
            ThespeonInferenceHandler.UnloadActorPackModule(actorPackModuleName);
        }
        

        
        /// <summary>
        /// Creates a LingotionSynthRequest object with a unique ID for the synthetization and an estimated quality of the synthesized data. 
        /// Warnings and metadata can be empty or contain information about the synthetization.    
        /// </summary>
        /// <param name="annotatedInput">Model input. See Annotated Input Format Guide for details.</param>
        /// <param name="config">A synthetization instance specific config override.</param>
        /// <returns></returns>
        /// 
        public static LingotionSynthRequest Synthesize(UserModelInput annotatedInput, object config=null) //replace object with some type
        {
            if(isRunning){
                Debug.LogWarning("Already running a model. Please wait for the current model to finish before starting a new one.");
                return null;
            }
            if(config != null)
            {
                // Debug.Log("Config provided: " + config); 
                // Override the default config with the provided one for this synthetization.
                // Possibilities so far: BackendType, JitterSize, PollingInterval.
            }
            // QUESTION: if modules are preloaded but we have provided a new config (new backend type) should we reload them?
                    // (will be slow and thus info must be added to the LingotionSynthRequest object)
                    // or is there some way of changing the backend type just for this synthetization? How do we then change back for other runs? 
            // config override will be connected to this synthRequestId.
            var (errors, warnings) = annotatedInput.ValidateAndWarn();

            foreach (var error in errors)
            {
                Debug.LogError(error);
            }
            foreach (var warning in warnings)
            {
                Debug.LogWarning(warning);
            }

            if(!GetRegisteredActorPacks().SelectMany(pack => pack.Value.GetModules()).Select(module => module.name).Contains(annotatedInput.moduleName))
            {
                annotatedInput.moduleName = GetRegisteredActorPacks().SelectMany(pack => pack.Value.GetModules()).First().name;
                if(annotatedInput.moduleName == null)
                {
                    throw new ArgumentException("No ActorPack Modules registered. Register an ActorPack Module first.");
                }
                Debug.LogWarning($"ActorPack Module {annotatedInput.moduleName} not found in registered ActorPacks. The first available module {annotatedInput.moduleName} will be registered, preloaded and used in its stead.");
            }
            PreloadActorPackModule(annotatedInput.moduleName); //if already loaded this will retun immediately. -> OBS we should move ValidateAndWarn() to above this and make it return languages so we can send that into this. On the other side we cannot return of not all languages are loaded.
            if(!ThespeonInferenceHandler.HasAnyLoadedLanguageModules(annotatedInput.moduleName))
            {
                throw new ArgumentException("No Language Packs on disk, aborting synthesis.");
            }
            //parse and TPP the annotated input.
            ActorPackModule selectedModule = GetRegisteredActorPacks()[annotatedInput.actorUsername].GetModules().Where(m => m.name == annotatedInput.moduleName).First();
            HashSet<Language> inputLangs = GetInputLanguages(annotatedInput);
            Dictionary<Language, Vocabularies> vocabsByLanguage = new Dictionary<Language, Vocabularies>(); 

            foreach (var lang in inputLangs)
            {
                Vocabularies vocab= ThespeonInferenceHandler.GetVocabs(lang, selectedModule);
                vocabsByLanguage[lang] = vocab;
            }

            Dictionary<string, int> moduleSymbolTable = ThespeonInferenceHandler.GetSymbolToID(annotatedInput.moduleName);
            



            NestedSummary summaryJObject = annotatedInput.MakeNestedSummary();

            
            PopulateUserModelInput(annotatedInput, selectedModule);

            FillSummaryQualities(selectedModule, annotatedInput.actorUsername, summaryJObject);
            
            string feedback= TPP(annotatedInput, vocabsByLanguage, moduleSymbolTable);           
            warnings.Add(feedback);


            string synthRequestId = Guid.NewGuid().ToString();  

            ThespeonInferenceHandler.AssociateIDToInput(synthRequestId, annotatedInput);


            return new LingotionSynthRequest(synthRequestId, summaryJObject, errors, warnings, annotatedInput);
        }

        /// <summary>
        /// Sets the backend type for all modules loaded in the future. 
        /// </summary>
        /// <param name="backendType"></param>
        public static void SetBackend(BackendType backendType)
        {
            ThespeonInferenceHandler.SetBackend(backendType);
        }

        /// <summary>
        /// returns a list of actor names that are currently loaded in memory.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetLoadedActorPackModules()
        {
            return ThespeonInferenceHandler.GetLoadedActorPackModules();
        }
    
#endregion

#region Private helpers
        /// <summary>
        /// Helper method which finds the actor pack name corresponding to the username from the packMappings JSON object.
        /// </summary>
        /// <param name="packMappings"></param>
        /// <param name="actorUsername"></param>
        /// <returns></returns>
        private static string FindActorPackNameFromUsername(JObject packMappings, string actorUsername)
        {
            JObject actorPacks = (JObject) packMappings["actorpacks"];
            foreach (var actorPack in actorPacks.Properties()) // Iterate through actor pack names
            {
                JToken actorPackValue = actorPack.Value;

                if (actorPackValue["actors"] is JArray actorsArray) // Check if actors array exists
                {
                    if (actorsArray.Any(a => a.ToString() == actorUsername)) // Check if actorUsername exists
                    {
                        return actorPack.Name; // Return the actor pack name  OBS the first encountered one so assumes no duplicates of actorUsername across packs.
                    }
                }
            }
            throw new ArgumentException("Username not found in packMappings.");
        }

        
        #nullable enable
        private static bool MatchLanguage(Language inputLanguage, Language candidateLanguage)
        {
            if(inputLanguage==null)
            {
                return false;
            }
            var inputItems = inputLanguage.GetItems();
            var candidateItems = candidateLanguage.GetItems();
            foreach (var kvp in inputItems)
            {
                string key = kvp.Key;
                string? inputValue = kvp.Value;

                // Skip if null or empty => we don't enforce it
                if (string.IsNullOrWhiteSpace(inputValue))
                    continue;

                if (!candidateItems.TryGetValue(key, out string? candidateValue))
                    return false;

                // Case-insensitive compare
                if (!string.Equals(inputValue, candidateValue, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
        #nullable disable

        

        private static string TPP(UserModelInput userInput, Dictionary<Language, Vocabularies> langVocabularies, Dictionary<string, int> moduleSymbolTable ){

            ConverterFilterService converterFilterService = new ConverterFilterService();

            string feedback = "";


            int segmentIndex = 1;

            foreach (var segment in userInput.segments)
            {
                if(!(segment.isCustomPhonemized ?? false))
                {
                    //(_, Vocabularies segmentVocabularies, _) = GetMatchingToolsRelaxed(userInput, segment);

                    // Get the vocab for the segment's language, if it exists othherwise get the default language's vocab.
                    Vocabularies segmentVocabularies = langVocabularies.FirstOrDefault(lv => MatchLanguage(segment.languageObj, lv.Key)).Value ?? langVocabularies.FirstOrDefault(lv => MatchLanguage(userInput.defaultLanguage, lv.Key)).Value;


                    string graphmemeFeedback = converterFilterService.ApplyAllFiltersOnSegmentPrePhonemize(segment, segmentVocabularies.grapheme_vocab, userInput.defaultLanguage.iso639_2);
                    if (!string.IsNullOrEmpty(graphmemeFeedback))
                    {
                        feedback += "Pre-Proccessing for segment " + segmentIndex + ": \n";
                        feedback += graphmemeFeedback;
                    }
                    segmentIndex++;
                }
                else 
                {
                    // If the segment is custom phonemized, we still need to validate the phonemes
                    // and remove any illegal phonemes.
                    (string validPhonemeText, string PhonmeFeedback) = PhonemeValidation(segment.text, moduleSymbolTable);
                    segment.text = validPhonemeText;
                    if (!string.IsNullOrEmpty(PhonmeFeedback))
                    {
                        feedback += "PhonmeFeedback for segment " + segmentIndex + ": \n";
                        feedback += PhonmeFeedback;
                    }

                    segmentIndex++;
                }
            } 

            return feedback;
        }


                // Extract a HashSet of Languages for all input segments
        private static HashSet<Language> GetInputLanguages(UserModelInput modelInput)
        {

            var result = new HashSet<Language>();

            if (modelInput == null)
            {
                throw new ArgumentNullException(nameof(modelInput));
            }

            if (modelInput.defaultLanguage != null)
            {
                result.Add(modelInput.defaultLanguage);
            }

            foreach (var segment in modelInput.segments)
            {
                if (segment.languageObj != null)
                {
                    result.Add(segment.languageObj);
                }
            }

            return result;

        }

        /// <summary>
        /// Fills the "quality" attributes in the given summary object by matching 
        /// languages and emotions with the data from the specified module and actor.
        /// 
        /// 1) Finds the actor by username in module.model_options?.recording_data_info?.actors.
        /// 2) For each item in summary.Summary, fuzzy-match the language using 
        ///    LanguageExtensions.FindClosestLanguage().
        /// 3) For each emotion in that summary item, set "quality" to the matching 
        ///    ActorPack data (if found).
        /// </summary>

        public static void FillSummaryQualities(
            ActorPackModule module,
            string actorUsername,
            NestedSummary summary)
        {
            if (module == null)
            {
                UnityEngine.Debug.LogError("Module is null. Cannot fill summary qualities.");
                return;
            }

            if (module.model_options?.recording_data_info?.actors == null)
            {
                UnityEngine.Debug.LogError("No actor data found in module.model_options.recording_data_info.actors");
                return;
            }

            // 1) Find the actor entry by username
            var actorEntry = module.model_options.recording_data_info.actors
                .Values
                .FirstOrDefault(a => a.actor.username == actorUsername);

            if (actorEntry == null)
            {
                UnityEngine.Debug.LogError($"No actor with username '{actorUsername}' found in this module.");
                return;
            }

            // The actor’s languages
            var actorLanguageData = actorEntry.languages?.Values;
            if (actorLanguageData == null || actorLanguageData.Count == 0)
            {
                UnityEngine.Debug.LogError($"Actor '{actorUsername}' has no languages in this module.");
                return;
            }

            // If there's a warning instead of a summary, or if summary is null, nothing to do
            if (summary == null || summary.Summary == null)
            {
                UnityEngine.Debug.LogWarning("Summary is null or does not have any items.");
                return;
            }

            // 2) For each language summary in the typed object
            foreach (var langGroup in summary.Summary)
            {
                // langGroup.Language is presumably a string like "en" or "eng" etc.
                if (langGroup.Language == null)
                    continue;

                // Prepare a “Language” object from your string, if needed for fuzzy match
                // e.g. new Language(langGroup.Language). Or maybe you pass the string directly.
                var summaryLangObj = new Language { iso639_2 = langGroup.Language.iso639_2 };

                // Collect the actor's known Language objects
                var candidateLanguages = actorLanguageData
                    .Select(ld => ld.language)
                    .Where(l => l != null)
                    .ToList();

                // 2A) Fuzzy-match with the actor’s known languages
                // Suppose this call returns (closestLang, distance, similarity)
                // Adjust to how your LanguageExtensions.FindClosestLanguage is actually defined.
                var (closestLang, _, _) = LanguageExtensions.FindClosestLanguage(summaryLangObj, candidateLanguages);

                if (closestLang == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"No fuzzy-match found for language {langGroup.Language} for actor '{actorUsername}'. Skipping."
                    );
                    continue;
                }

                // Find the corresponding LanguageData from actorEntry
                var matchedLangData = actorEntry.languages.Values.FirstOrDefault(ld => ld.language == closestLang);
                if (matchedLangData == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"No LanguageData found for fuzzy matched language {closestLang.iso639_2} for actor '{actorUsername}'."
                    );
                    continue;
                }

                if (matchedLangData.emotions == null || matchedLangData.emotions.Count == 0)
                    continue;

                // 3) For each emotion in the summary, try to match & fill “quality”
                foreach (var emotionKV in langGroup.Emotions)
                {
                    var emotionKey = emotionKV.Key;        // e.g. "happy"
                    var emotionStat = emotionKV.Value;     // e.g. { EmotionName="happy", NumberOfChars=123, Quality=null }

                    if (emotionStat == null)
                        continue;

                    string summaryEmotionName = emotionStat.EmotionName;
                    if (string.IsNullOrEmpty(summaryEmotionName))
                        continue;

                    // Attempt to find a matching emotion in matchedLangData.emotions
                    // The dictionary key might be "happy", or might be an ID. 
                    // Often we compare summaryEmotionName to .emotionsetname in the ActorPack data:
                    var foundKey = matchedLangData.emotions
                        .Keys
                        .FirstOrDefault(k =>
                        {
                            var eData = matchedLangData.emotions[k];
                            return eData?.emotion?.emotionsetname == summaryEmotionName;
                        });

                    if (foundKey == null)
                    {
                        // We didn’t find a match for this emotion; skip
                        continue;
                    }

                    var actorEmotionData = matchedLangData.emotions[foundKey];
                    if (actorEmotionData == null)
                        continue;

                    // The ActorPack’s EmotionData has a .quality string
                    string actorQuality = actorEmotionData.quality;
                    if (!string.IsNullOrEmpty(actorQuality))
                    {
                        // UnityEngine.Debug.Log(
                        //     $"Filling quality for emotion '{summaryEmotionName}' with '{actorQuality}'"
                        // );
                        emotionStat.Quality = actorQuality;  // <--- Fill the typed property
                    }
                }
            }
        }

        private static void PopulateUserModelInput(UserModelInput input, ActorPackModule module)
        {
        
            // Retrieve the first Actor's username
            // string firstActorUsername = module.actor_options?
            //    .actors?
            //    .FirstOrDefault()?
            //    .username;

            if (input.defaultLanguage == null)
            {
                Language firstLanguage = module.language_options.languages.FirstOrDefault();
                
                input.defaultLanguage = firstLanguage;

                Debug.LogWarning($"Default language not set. Using first language '{firstLanguage}' from module '{module.name}'.");
            }
            if (input.defaultEmotion == null)
            {
                string emotionsetname= "Emotionless";
                input.defaultEmotion = emotionsetname;

                Debug.LogWarning($"Default emotion not set. Using first emotion '{emotionsetname}' from module '{module.name}'.");
            }
            if (input.speed == null)
            {
                input.speed = new List<double> { 1.0 };

                Debug.LogWarning($"Speed not set. Using default speed 1.0.");
            }
            if (input.loudness == null)
            {
                input.loudness = new List<double> { 1.0 };

                Debug.LogWarning($"Loudness not set. Using default loudness 1.0.");
            }

            if (input.extraData != null)
            {
                Debug.LogWarning("Extra data exists in input. Removing : \n" + input.extraData);
                input.extraData = null;
            }

            foreach (UserSegment segment in input.segments) {

                if (segment.extraData != null)
                {
                    Debug.LogWarning("Extra data exists in segment. Removing : \n" + segment.extraData);
                    segment.extraData = null;
                }
                
            }
        }
            

        public static (string,string) PhonemeValidation(string phonemeText, Dictionary<string, int> symbol_to_id)
        {
            phonemeText= phonemeText.ToLower();
            string validPhonemeText = "";
            string feedback = "";
            List <string> illegalPhonemes = new List<string>();
            foreach (char phonemeChar in phonemeText)
            {
                if (symbol_to_id.TryGetValue(phonemeChar.ToString(), out int id))
                {
                    validPhonemeText += phonemeChar;
                }
                else
                {
                    illegalPhonemes.Add(phonemeChar.ToString());
                    Debug.LogWarning($"Phoneme '{phonemeChar}' is a not a valid phoneme charachter in segment \"{phonemeText}\". Therefore it was removed from the phoneme sequence."); 
                }
            }
            if (illegalPhonemes.Count > 0)
            {
                feedback += "Illegal phonemes removed: "  +  string.Join(", ", illegalPhonemes);
            }
            return (validPhonemeText, feedback);
        }

}

#endregion


    public class LingotionData<T>{
        public string SynthRequestID  { get; private set; }
        public Queue<LingotionDataPacket<T>> PacketBuffer  { get; private set; }
        public LingotionData(string synthRequestID)
        {
            SynthRequestID = synthRequestID;
            PacketBuffer = new Queue<LingotionDataPacket<T>>(); // Non-thread-safe but fine if accessed from one thread
        }

    }

    /// <summary>
    /// A Lingotion data packet containing the type of data, metadata and the data itself.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LingotionDataPacket<T>
    {

        public string Type { get; private set; }
        public Dictionary<string, object> Metadata { get; private set; }
        public T[] Data { get; private set; } //or float? depends on if we ever want to return something else.

        public LingotionDataPacket(string type, Dictionary<string, object> metadata, T[] data)
        {
            Type = type;
            Metadata = metadata;
            Data = data;
        }
    }

    /// <summary>
    /// A Lingotion synthetization request containing a unique ID, estimated quality, errors, warnings and metadata.
    /// Is used to initiate data synthesis.
    /// </summary>
    public class LingotionSynthRequest
    {
        public string synthRequestID { get; private set; }
        public NestedSummary estimatedQuality { get; private set; }
        public List<string> errors { get; private set; }
        public List<string> warnings { get; private set; }
        public UserModelInput metaData { get; private set; }

        // public ThespeonInferenceHandler handler;
        public LingotionSynthRequest(string synthRequestID, NestedSummary estimatedQuality, List<string> errors, List<string> warnings, UserModelInput metaData)
        {
            this.synthRequestID = synthRequestID;
            this.estimatedQuality = estimatedQuality;
            this.errors = errors;
            this.warnings = warnings;
            this.metaData = metaData;
            // this.handler = handler;
        }


    }

}