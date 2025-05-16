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

        private static Dictionary<string, List<ActorPack>> registeredActorPacks = new Dictionary<string, List<ActorPack>>(); 
        public static  string MAPPINGS_PATH = RuntimeFileLoader.packMappingsPath; 
        private static JObject packMappings = Init();

        
        private static JObject Init()
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
        /// Sets the global config variables. These are applied whenever no local config is supplied for a synthesis. Non-null properties in the argument will permanently override the default config.
        /// </summary>
        /// <param name="config">The config object to set. Any non-null properties will override the current global config.</param>
        public static void SetGlobalConfig(PackageConfig config)
        {
            ThespeonInferenceHandler.SetGlobalConfig(config);
        }

        /// <summary>
        /// Gets a copy of current config. If a synthid is provided it will return the local config valid for that synthid. Otherwise it will return the global config.
        /// </summary>
        /// <param name="synthID">The synthid to get the local config for. If null or omitted, the global config is returned.</param>
        public static PackageConfig GetConfig(string synthID=null)
        {
            return ThespeonInferenceHandler.GetCurrentConfig(synthID);
        }

        /// <summary>
        /// Gets a list of all available actor name - tag pairs in the actor packs that are on disk, registered or not.
        /// </summary>
        /// <returns>A list of pairs actorUsername strings, ActorTags.</returns>
        public static List<(string, ActorTags)> GetActorsAvailabeOnDisk()
        {
            List<(string, ActorTags)> actorsOnDisk = new List<(string, ActorTags)>();
            foreach(var module in (JObject) packMappings["tagMapping"])
            {
                foreach(var actor in (JArray) module.Value["actors"])
                {
                    actorsOnDisk.Add((actor.ToString(), JsonConvert.DeserializeObject<ActorTags>(module.Value["tags"].ToString())));
                }
            }
            if(actorsOnDisk.Count == 0)
            {
                Debug.LogWarning("No actors found on disk.");
            }
            return new HashSet<(string, ActorTags)>(actorsOnDisk).ToList();
        }

        /// <summary>
        /// Registers ActorPacks modules and returns it, note that it registers all actorpacks given an actorUsername This does not load the module into memory.
        /// </summary>
        /// <param name="actorUsername">The username of the actor you wish to load.</param>
        /// <returns>The registered ActorPack object</returns>
        public static void RegisterActorPacks(string actorUsername)                  
        //TUNI-87 Add optional Tags here. Now we register all packs for the actor. But we might want to register only one of them.      
        {

            List<ActorPack> actorPacks = new List<ActorPack>();
            if(registeredActorPacks.ContainsKey(actorUsername))
            {
                actorPacks = registeredActorPacks[actorUsername];
            }
            List<string> actorPackNames  = FindAllActorPackNamesFromUsername(actorUsername);
            if (actorPackNames.Count == 0)
            {
            throw new ArgumentException($"No actor packs found for username {actorUsername}.");
            }


            foreach (string actorPackName in actorPackNames)
            {
            string path = Path.Combine(RuntimeFileLoader.GetActorPacksPath(), actorPackName, actorPackName + ".json");

            string jsonContent = RuntimeFileLoader.LoadFileAsString(path);
            ActorPack actorPack = JsonConvert.DeserializeObject<ActorPack>(jsonContent);
            if(actorPack == null)
            {
                Debug.LogError("Error deserializing ActorPack.");
                return;
            }

            if(registeredActorPacks.Values.SelectMany(list => list).FirstOrDefault(ap => ap.name == actorPack.name) != null)
            {
                Debug.LogWarning($"ActorPack {actorPack.name} already registered under another user. Adding a reference to the same object.");
                actorPack = registeredActorPacks.Values.SelectMany(list => list).FirstOrDefault(ap => ap.name == actorPack.name);
            }

            if (registeredActorPacks.ContainsKey(actorUsername))
            {
                if (registeredActorPacks[actorUsername].Any(ap => ap.name == actorPack.name))
                {
                Debug.LogWarning($"Actor pack {actorPack.name} is already registered for actor {actorUsername}. Skipping.");
                continue;
                }

                registeredActorPacks[actorUsername].Add(actorPack);
            }
            else
            {
                registeredActorPacks[actorUsername] = new List<ActorPack> { actorPack };
            }
            }
        }

        

        /// <summary>   
        /// Unregisters an ActorPack. This also unloads the module from memory if it is loaded.
        /// </summary>
        /// <param name="actorUsername">The username of the actor you wish to unregister.</param>
        /// <returns></returns>
        public static void UnregisterActorPack(string actorUsername)
        //TUNI-87 Add optional Tags here. Now we unregister all packs for the actor. But we might want to unregister only one of them.      
        {
            if(registeredActorPacks.ContainsKey(actorUsername))
            {
                List<ActorPack> actorPacks = registeredActorPacks[actorUsername];
                
                foreach (var actorPack in actorPacks)
                {
                    foreach( var module in actorPack.GetModules())
                    {
                        UnloadActorPackModule(module.name);
                    }
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
        //TUNI-88  -  This should not be part of API and not be exposed to user. Only used internally.
        {
            List<ActorPackModule> result = new List<ActorPackModule>();

            if (!registeredActorPacks.ContainsKey(actorUsername))
            {
                Debug.LogError($"Actor {actorUsername} not found in registered packs.");
                throw new ArgumentException($"Actor {actorUsername} not found in registered packs.");
            }
            foreach (ActorPack pack in registeredActorPacks[actorUsername])
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
        /// Gets a dictionary of all registered actor name - ActorPack lists.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, List<ActorPack>> GetRegisteredActorPacks()    
        //TUNI-88  -  This should not be part of API and not be exposed to user. Is now used by engine and sample. Rework Later.
        {
            return registeredActorPacks;
        }



        /// <summary>
        /// Retrieves an <see cref="ActorPackModule"/> from the specified actor's packs based on the provided module name. 
        /// If the actor pack or specified module is not found, an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <param name="annotatedInput"> A <see cref="UserModelInput"/> instance containing the actor's username and the desired module name.</param>
        /// <returns> The first matching <see cref="ActorPackModule"/> for the specified actor and module name. </returns>
        /// <exception cref="ArgumentException"> Thrown if the actor username does not exist in the registered packs, or if no module matching the requested name is found for the specified actor. </exception>
        public static (ActorPackModule, ActorPack) GetActorPackModule(UserModelInput annotatedInput)
        {

            Dictionary<string, List<ActorPack>> registeredActorPacks = GetRegisteredActorPacks();

            if (registeredActorPacks.ContainsKey(annotatedInput.actorUsername))
            {
                List<ActorPack> selectedPacks = registeredActorPacks[annotatedInput.actorUsername];


                foreach (var pack in selectedPacks)
                {

                    // loop over modules for pack
                    foreach (var module in pack.GetModules())
                    {
                        if (module.name == annotatedInput.moduleName)
                        {
                            return (module, pack); // Return immediately if found
                        }
                    }
                }   

                // If we reach this point, the module wasn't found in any pack
                Debug.LogError($"Module '{annotatedInput.moduleName}' not found in ActorPack '{annotatedInput.actorUsername}'.");
                throw new ArgumentException($"Module '{annotatedInput.moduleName}' not found in ActorPack '{annotatedInput.actorUsername}'.");
            }
            else
            {
                Debug.LogError($"ActorPack '{annotatedInput.actorUsername}' not found.");
                throw new ArgumentException($"ActorPack '{annotatedInput.actorUsername}' not found.");
            }
        }

        /// <summary>
        /// Preloads an ActorPack module and the language modules necessary for full functionality. the parameter languages can be optionally used to filter which language modules to preload for restricted functionality.
        /// </summary>
        /// <param name="actorPackModuleName">Which Actor Pack Module to preload.</param>
        /// <param name="languages">Optional. If provided only Language Packs for given languages are preloaded. Otherwise all Language Packs necessary for full functionality are preloaded.</param>
        /// <returns></returns>
        // public static void PreloadActorPackModule(ActorPackModule actorPackModule,  List<Language> languages=null)
        public static void PreloadActorPackModule(string actorUsername, ActorTags tags,  List<Language> languages=null)         //TUNI-87
        {
            string actorPackModuleName = GetActorPackModuleName(actorUsername, tags); //actorUsername and tags are used to find the module name in the packMappings. 
            //find actorpack that contains this module
            ActorPack actorPack = registeredActorPacks.Values.SelectMany(apList => apList).FirstOrDefault(ap => ap.GetModules().Any(m => m.name == actorPackModuleName));       //what if you have several actorPacks with the same module in it - investigate later.
            if(ThespeonInferenceHandler.IsPreloaded(actorPackModuleName))
            {
                //Debug.LogWarning("ActorPack Module already preloaded.");        //This method is called in synthesizer so it will be called multiple times in anyone preloads and should not really warn the user in that case.
                return; 
            }

            ActorPackModule module = actorPack.GetModules().FirstOrDefault(m => m.name == actorPackModuleName); 
            if (module == null)
            {
                Debug.LogError($"Module not found: {actorPackModuleName}");
                throw new ArgumentException($"Module not found: {actorPackModuleName}");
            }
            
            List<PhonemizerModule> langModules = actorPack.GetLanguageModules(actorPackModuleName);
            if (languages != null)
            {
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
        /// <param name="dataStreamCallback">An Action callback to receive synthesized data as a stream.</param>
        /// <param name="config">A synthetization instance specific config override.</param>
        /// <returns>A LingotionSynthRequest object containing a unique Synth Request ID, feedback object and warnings/errors raised as well as a copy of the processed input and callback to be used by this synthesis.</returns>
        public static LingotionSynthRequest Synthesize(UserModelInput annotatedInput, Action<float[]> dataStreamCallback = null, PackageConfig config=null)
        {
            UserModelInput inputCopy = new UserModelInput(annotatedInput);
            string synthRequestID = Guid.NewGuid().ToString();
            ThespeonInferenceHandler.SetLocalConfig(synthRequestID, config);


            var (errors, warnings) = inputCopy.ValidateAndWarn();

            foreach (var error in errors)
            {
                Debug.LogError(error);
            }

            (ActorPackModule selectedModule, _ )= GetActorPackModule(inputCopy);

                // TUNI-87
            PreloadActorPackModule(packMappings["tagMapping"][inputCopy.moduleName]["actors"][0].ToString(), JsonConvert.DeserializeObject<ActorTags>(packMappings["tagMapping"][inputCopy.moduleName]["tags"].ToString())); //if already loaded this will retun immediately. -> OBS we should move ValidateAndWarn() to above this and make it return languages so we can send that into this. On the other side we cannot return of not all languages are loaded.
            if(!ThespeonInferenceHandler.HasAnyLoadedLanguageModules(inputCopy.moduleName))
            {
                throw new ArgumentException("No suitable Language Packs imported, aborting synthesis.");
            }
            //parse and TPP the annotated input.

            PopulateUserModelInput(inputCopy, selectedModule);

            NestedSummary summaryJObject = inputCopy.MakeNestedSummary();
            FillSummaryQualities(selectedModule, inputCopy.actorUsername, summaryJObject);


            HashSet<Language> inputLangs = GetInputLanguages(inputCopy);
            Dictionary<Language, Vocabularies> vocabsByLanguage = new Dictionary<Language, Vocabularies>(); 

            foreach (var lang in inputLangs)
            {
                Vocabularies vocab= ThespeonInferenceHandler.GetVocabs(lang, selectedModule);
                vocabsByLanguage[lang] = vocab;
            }

            Dictionary<string, int> moduleSymbolTable = ThespeonInferenceHandler.GetSymbolToID(inputCopy.moduleName);
            


            
            string feedback= TPP(inputCopy, vocabsByLanguage, moduleSymbolTable);           
            warnings.Add(feedback);

            //hide LanguageKey from user
            UserModelInput feedbackInput = new UserModelInput(inputCopy);
            feedbackInput.defaultLanguage.languageKey = null;
            foreach(var segment in feedbackInput.segments)
            {
                if(segment.languageObj != null) segment.languageObj.languageKey = null;
            }


            return new LingotionSynthRequest(synthRequestID, summaryJObject, errors, warnings, feedbackInput, dataStreamCallback);
        }

        /// <summary>
        /// Sets the backend type for all modules loaded in the future. 
        /// </summary>
        /// <param name="backendType"></param>
        public static void SetBackend(BackendType backendType)
        //Becomes obsolete after TUNI-75 is handled.
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


        
        /// <summary>
        /// Returns a dictionary keyed by actorUsername.
        /// Each entry is a list of ModuleTagInfo objects describing
        /// (packName, moduleName, moduleId, tags).
        /// </summary>
        public static Dictionary<string, List<ModuleTagInfo>> GetActorsAndTagsOverview()
        {
            var result = new Dictionary<string, List<ModuleTagInfo>>();

            JObject tagOverview = packMappings["tagOverview"] as JObject;
            if (tagOverview == null)
            {
                Debug.LogWarning("No 'tagOverview' in packMappings.");
                return result;
            }

            // For each actorpack
            foreach (var packKvp in tagOverview)
            {
                string packName = packKvp.Key;
                JObject packObj = packKvp.Value as JObject;
                if (packObj == null) 
                    continue;

                // find "actors"
                JArray actorArr = packObj["actors"] as JArray;
                if (actorArr == null) 
                    continue;

                // For each actor in that array
                foreach (var actorToken in actorArr)
                {
                    string actorName = actorToken.ToString();
                    if (!result.ContainsKey(actorName))
                        result[actorName] = new List<ModuleTagInfo>();

                    // Now look at each moduleName
                    foreach (var moduleKvp in packObj)
                    {
                        if (moduleKvp.Key == "actors")
                            continue;

                        string moduleName = moduleKvp.Key;
                        JObject moduleNode = moduleKvp.Value as JObject;
                        if (moduleNode == null)
                            continue;

                        JObject tagsObj = moduleNode["tags"] as JObject;
                        Dictionary<string,string> tagsDict = null;
                        if (tagsObj != null)
                        {
                            tagsDict = tagsObj.Properties()
                                .ToDictionary(p => p.Name, p => p.Value.ToString());
                        }
                        // We treat "moduleName" as the unique ID
                        var info = new ModuleTagInfo
                        {
                            PackName   = packName,
                            ModuleName = moduleName,
                            // We'll just store moduleName in ModuleId if we want
                            ModuleId   = moduleName,
                            Tags       = tagsDict
                        };
                        result[actorName].Add(info);
                    }
                }
            }

            return result;
        }


        /// <summary>
        /// Find the modulename in packMappings which has the the actorUsername and tags.
        /// </summary>
        /// <param name="actorUsername"></param>
        /// <param name="tags"></param>
        /// <returns></returns>
        public static string GetActorPackModuleName(string actorUsername, ActorTags tags)          
        //TUNI-87 - function headder somewhat correct, content might need revision. Also make this private when doing TUNI-88.
        {
            List<string> moduleNames = new List<string>();
            JObject modules = (JObject) packMappings["tagMapping"];
            foreach (var module in modules.Properties())
            {
                JObject moduleObj = (JObject) module.Value;
                JArray actorsArray = (JArray) moduleObj["actors"];

                if (actorsArray != null && actorsArray.Any(a => a.ToString() == actorUsername))
                {
                    // Check if the tags match
                    ActorTags tagsObj = JsonConvert.DeserializeObject<ActorTags>(moduleObj["tags"].ToString());
                    if (tagsObj != null && tagsObj.Equals(tags))
                    {
                        moduleNames.Add(module.Name); // Return the module name
                    }
                }
            }
            if(moduleNames.Count==0) throw new ArgumentException($"Module not found for actor {actorUsername} with tags {tags}.");
            if(moduleNames.Count > 1){
                Debug.LogWarning("Found multiple modules for actor " + actorUsername + " with tags " + tags + ". Returning the first one found.");
            }
            return moduleNames[0]; // Return the first module name found if several
        }

        public static void PrintActorsAndTagsOverview(Dictionary<string, List<ModuleTagInfo> >  overview) {

            // Debug.Log how many actors we found
            Debug.Log($"Found {overview.Count} actor(s) in 'tagOverview' data.");

            // Let's dump out each actor's modules
            foreach (var kvp in overview)
            {
                string actorName = kvp.Key;
                List<ModuleTagInfo> modules = kvp.Value;

                Debug.Log($"Actor: {actorName} has {modules.Count} modules total.");

                // For each module, print some info
                foreach (var modInfo in modules)
                {
                    // If modInfo.Tags is null, it means no tags
                    string tagText = (modInfo.Tags == null)
                        ? "No Tags"
                        : string.Join(", ", modInfo.Tags.Select(x => $"{x.Key}={x.Value}"));

                    Debug.Log($"  -> Pack: {modInfo.PackName}, ModuleName: {modInfo.ModuleName}, " +
                            $"ModuleID: {modInfo.ModuleId}, Tags: {tagText}");
                }
            }

        }
    
#endregion

#region Private helpers
        private static List<string> FindAllActorPackNamesFromUsername(
            string actorUsername, 
            bool useTagOverview = false
        )
        {
            // Decide which top-level field to search in: "actorpacks" or "tagOverview"
            var lookupField = useTagOverview ? "tagOverview" : "actorpacks";            //remove in the future - unnecessary. Only need actorpacks. See TUNI-87
            
            // Grab the chosen object from the input JSON
            JObject actorPacks = (JObject)packMappings[lookupField];
            
            List<string> foundPackNames = new List<string>();
            
            // Iterate through each actorpack (or tagOverview) entry
            foreach (var actorPack in actorPacks.Properties())
            {
                JToken actorPackValue = actorPack.Value;

                // If we have an array of "actors", check if our username is among them
                if (actorPackValue["actors"] is JArray actorsArray)
                {
                    if (actorsArray.Any(a => a.ToString() == actorUsername))
                    {
                        foundPackNames.Add(actorPack.Name);
                    }
                }
            }

            // If no matches were found, throw the same error as before
            if (!foundPackNames.Any())
            {
                Debug.LogError($"Username [{actorUsername}] not found in {lookupField} of packMappings.");
                throw new ArgumentException(
                    $"Username [{actorUsername}] not found in {lookupField} of packMappings."
                );
            }

            return foundPackNames;
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
            

            // Print every detail of langVocabularies
            if (langVocabularies == null || langVocabularies.Count == 0)
            {
                Debug.LogError("No vocabularies found.");
                return "No vocabularies found.";
            }

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


                    // check if segmentVocabularies is null
                    if (segmentVocabularies == null)
                    {
                        Debug.LogError($"No vocabularies found for segment {segment.text}.");
                    }


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

        private static void FillSummaryQualities(
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
                string emotionsetname= "Interest";          // hard coded here for now but should later be selected from Config or if it does not exist: from module.
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
            

        private static (string,string) PhonemeValidation(string phonemeText, Dictionary<string, int> symbol_to_id)
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

        public string Type { get; private set; }            //Make an Enum of this instead of string
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
        public UserModelInput usedInput { get; private set; }
        public Action<float[]> onDataCallback { get; private set; }
        public LingotionSynthRequest(string synthRequestID, NestedSummary estimatedQuality, List<string> errors, List<string> warnings, UserModelInput usedInput, Action<float[]> onDataCallback = null) 
        {
            this.synthRequestID = synthRequestID;
            this.estimatedQuality = estimatedQuality;
            this.errors = errors;
            this.warnings = warnings;
            this.usedInput = usedInput;
            this.onDataCallback = onDataCallback;
        }

        public string GetSynthId()
        {
            return synthRequestID;
        }
        public NestedSummary GetEstimatedQuality()
        {
            return estimatedQuality;
        }
        public List<string> GetErrorsAndWarnings()
        {
            return errors.Concat(warnings).ToList();
        }
        public UserModelInput GetUsedInput()
        {
            return usedInput;
        }


    }

    /// <summary>
    /// Simple container for (packName, moduleName, moduleId, tags).
    /// You can adapt it or remove it if you prefer just raw dictionary structures.
    /// </summary>
    public class ModuleTagInfo
    {
        public string PackName   { get; set; }
        public string ModuleName { get; set; }
        public string ModuleId   { get; set; }

        // If the module had a non-null "tags" object, these go here
        // e.g. { "quality of sound": "ultra ultra high", "style": "pirate" }
        public Dictionary<string,string> Tags { get; set; }
    }

}