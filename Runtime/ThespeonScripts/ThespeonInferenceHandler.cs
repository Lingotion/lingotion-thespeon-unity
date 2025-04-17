// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.



using UnityEngine;
using Unity.Sentis;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEditor;
using System.Linq;
using System.Collections;
using Lingotion.Thespeon.API;
using Lingotion.Thespeon.Utils;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Data;
using UnityEngine.Profiling;
using Lingotion.Thespeon.FileLoader;


namespace Lingotion.Thespeon.ThespeonRunscripts
{
    public static class ThespeonInferenceHandler
    {
        #if UNITY_ANDROID                           //this BackendType should be CONFIGURABLE. 
            private static BackendType BACKEND_TYPE = BackendType.CPU; 
        #else
            private static BackendType BACKEND_TYPE = BackendType.GPUCompute;
        #endif
        
        // private static Dictionary<string, Model> phonemizerModels = new Dictionary<string, Model>();     //needed or can I just keep the workers?
        private static Dictionary<string, Model> encoderModels = new Dictionary<string, Model>();         // same as above
        private static Dictionary<(string,string), Worker> preloadedSynthWorkers = new Dictionary<(string, string), Worker>();
        private static Dictionary<string, Dictionary<Language, string>> globalLangIds = new Dictionary<string,  Dictionary<Language, string>>();    //not permanent solution. Should be coalesced.
        private static Dictionary<string, Worker> preloadedPhonemizerWorkers = new Dictionary<string,  Worker>();    //not permanent solution. Should be coalesced.
        private static Dictionary<string, Dictionary<string, string>> lookupTables = new Dictionary<string, Dictionary<string, string>>();
        private static Dictionary<string, Vocabularies> vocabs = new Dictionary<string, Vocabularies>();       //requires casting when fetched.
        private static Dictionary<string, Dictionary<string, int>> moduleSymbolTable = new Dictionary<string, Dictionary<string, int>>(); //for converting phonemized strings to ints for the model.
        private static readonly string EOS_TOKEN = "<eos>";
        private static Dictionary<string, InferenceStep[]> runningModules = new Dictionary<string, InferenceStep[]>();
        private static Dictionary<string, Tensor> tensors = new Dictionary<string, Tensor>();
        
        private static PackageConfig globalConfig = SetDefaultConfig();
        private static Dictionary<string, PackageConfig> localConfigs = new Dictionary<string, PackageConfig>();
        private static Dictionary<string, UserModelInput> synthIDToUserInput = new Dictionary<string, UserModelInput>(); 
        private static Dictionary<string, Config> moduleIdToConfig = new Dictionary<string, Config>();


        private static PackageConfig SetDefaultConfig(){
            return new PackageConfig() {
                useAdaptiveFrameBreakScheduling = true,
                targetFrameTime =  0.01d,
                overshootMargin =  1.4f,
            };
        }
        /// <summary>
        /// Sets the global configuration for the package by overriding all properties in the existing global config which are not null in configOverride.
        /// </summary>
        /// <param name="config"></param>
        public static void SetGlobalConfig(PackageConfig configOverride)
        {
            globalConfig = globalConfig.SetConfig(configOverride);
        }
        /// <summary>
        /// Gets a copy of current config. If a synthid is provided it will return the local config valid for that synthid. Otherwise it will return the global config.
        /// </summary>
        /// <param name="synthID">The synthid to get the local config for. If null or omitted, the global config is returned.</param>
        public static PackageConfig GetCurrentConfig(string synthID=null)
        {
            if (synthID == null)
            {
                if(!localConfigs.ContainsKey(synthID)) Debug.LogWarning("No local config found for synthID " + synthID + ". Returning global config.");
                return new PackageConfig(globalConfig);
            }
            else
            {
                return new PackageConfig(localConfigs[synthID]);
            }
        }

        /// <summary>
        /// Associates a synthRequestID with a PackageConfig object consisting of the global config overwritten by non-null properties in local config.
        /// </summary>
        /// <param name="synthRequestID">The ID of the synth request</param>
        /// <param name="config">The PackageConfig object to overwrite global configs with in the current synthRequestID</param>
        public static void SetLocalConfig(string synthRequestID, PackageConfig config = null)
        {            
            PackageConfig localConfig = new PackageConfig(globalConfig);
            if (config != null)
            {
                localConfig = localConfig.SetConfig(config);
            }
            if (localConfigs.ContainsKey(synthRequestID))
            {
                Debug.LogWarning($"Config for SynthRequestID {synthRequestID} already exists. Overwriting.");
                localConfigs[synthRequestID] = localConfig;
            }
            else
            {
                localConfigs.Add(synthRequestID, localConfig);
            }
        }
        
        /// <summary>
        /// Associates a synthRequestID with a UserModelInput object.
        /// </summary>
        /// <param name="synthRequestID">The ID of the synth request</param>
        /// <param name="annotatedInput">The UserModelInput object to associate with the synthRequestID</param>
        public static void SetSynthInput(string synthRequestId, UserModelInput annotatedInput)
        {
            if (synthIDToUserInput.ContainsKey(synthRequestId))
            {
                Debug.LogWarning($"Input for SynthRequestID {synthRequestId} already exists. Overwriting.");
                synthIDToUserInput[synthRequestId] = annotatedInput;
            }
            else
            {
                synthIDToUserInput.Add(synthRequestId, annotatedInput);
            }
        }
        static ThespeonInferenceHandler()
        {
            Application.quitting += Cleanup;

        }

        private static void Cleanup()
        {
            foreach (var worker in preloadedSynthWorkers.Values)
            {
                worker?.Dispose();
            }
            preloadedSynthWorkers.Clear();
            
            foreach (var worker in preloadedPhonemizerWorkers.Values)
            {
                worker?.Dispose();
            }
            preloadedPhonemizerWorkers.Clear();

            foreach (var tensor in tensors.Values)
            {
                tensor?.Dispose();
            }
            tensors.Clear();
            
            foreach (var module in runningModules.Values)
            {
                foreach (InferenceStep step in module)
                {
                    step?.Destroy();
                }
            }
            runningModules.Clear();


        }
        

        /// <summary>
        /// Preloads the module and its associated language packs into memory. Loads all relevant Models and creates workers for them.
        /// </summary>
        /// <param name="actorPack"></param>
        /// <param name="module"></param>
        /// <param name="packMappings"></param>
        /// <exception cref="Exception"></exception>
        public static void PreloadActorPackModule(ActorPack actorPack, ActorPackModule module, JObject packMappings, List<PhonemizerModule> phonemizerModules = null)
        {
            if(globalLangIds.ContainsKey(module.name))
            {
                Debug.LogWarning("ActorPack Module already preloaded.");
                return;
            }
            
            // Debug.Log("--------------------PreloadActorPackModule In Handler---------------------------");
            var token = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(module.sentisfiles));
            var filteredKeys = token.Properties()
                .Where(p => p.Name.StartsWith("vocoder"))
                .ToList();
            
            Dictionary<string, Model> vocoderParts = new Dictionary<string, Model>();
            foreach (JProperty item in filteredKeys)
            {
                string key = item.Name;
                string md5 = module.sentisfiles[key];
                // Extract the file name without extension to use as a key
                string fileName =  Path.Combine(RuntimeFileLoader.GetActorPacksPath(true), actorPack.name, actorPack.GetFile(md5));
                Model model = ModelLoader.Load(RuntimeFileLoader.LoadFileAsStream(fileName));
                vocoderParts.Add(key, model);
            }
            Lingotion.Thespeon.VocoderBuilder.SerializeVocoder(vocoderParts, ref encoderModels, module.config);
            if(moduleIdToConfig.ContainsKey(module.name)) moduleIdToConfig[module.name] = module.config;
            else moduleIdToConfig.Add(module.name, module.config);

            // See if removing vocoder parts destroys the vocoder chunks? Might lead to some issue whenever garbage collector kicks in?
            vocoderParts?.Clear();
            

            Dictionary<Language, string> GlobalLangIdsDict = new Dictionary<Language, string>();
            foreach(string filemd5sum in module.sentisfiles.Values.Except(filteredKeys.Select(p => p.Value.ToString())))
            {
                string fileName = Path.Combine(RuntimeFileLoader.GetActorPacksPath(true), actorPack.name, actorPack.GetFile(filemd5sum));;
                
                Model model = ModelLoader.Load(RuntimeFileLoader.LoadFileAsStream(fileName));
                if(!encoderModels.ContainsKey(fileName))
                {
                    encoderModels.Add(fileName, model);
                }
                if(actorPack.GetFile(filemd5sum).StartsWith("encoder")) preloadedSynthWorkers.Add((module.name, "encoder"), new Worker(model, BackendType.CPU)); 
                else if(actorPack.GetFile(filemd5sum).StartsWith("decoder_preprocess")) preloadedSynthWorkers.Add((module.name, "decoder_preprocess"), new Worker(model, BACKEND_TYPE));
                else if(actorPack.GetFile(filemd5sum).StartsWith("decoder_chunked")) preloadedSynthWorkers.Add((module.name, "decoder_chunked"), new Worker(model, BACKEND_TYPE));
                else if(actorPack.GetFile(filemd5sum).StartsWith("decoder_postprocess")) preloadedSynthWorkers.Add((module.name, "decoder_postprocess"), new Worker(model, BACKEND_TYPE));
                else throw new Exception("You're not supposed to be here\n" +"Unknown model type: " + fileName);                       
            }
            preloadedSynthWorkers.Add((module.name, "vocoder_first_chunk"), new Worker(encoderModels["vocoder_first_chunk"], BACKEND_TYPE));
            preloadedSynthWorkers.Add((module.name, "vocoder_middle_chunk"), new Worker(encoderModels["vocoder_middle_chunk"], BACKEND_TYPE));
            preloadedSynthWorkers.Add((module.name, "vocoder_last_chunk"), new Worker(encoderModels["vocoder_last_chunk"], BACKEND_TYPE));
            
            LanguagePackService languagePackService = new LanguagePackService();
            List<PhonemizerModule> langModules = actorPack.GetLanguageModules(module.name);
            if (phonemizerModules != null)
            {
                langModules = phonemizerModules;
            }
            foreach(var langModuleInActorModule in langModules)
            {
                string langPackName = FindLanguagePackName(packMappings, langModuleInActorModule.module_id);
                if(langPackName == null){
                    Debug.LogWarning("You lack the language pack for language(s): " + string.Join("\n ", langModuleInActorModule.languages.Select(l => l.ToDisplay())) + "\n which is used by actor pack" + actorPack.name + " in preloaded module " + module.name + ". This is okay but this language will not be supported.");
                    continue;
                }
                string LanguagePackBaseModuleId = langModuleInActorModule.module_id;
                var filesToLoad = languagePackService.GetModuleFileNames(langPackName, LanguagePackBaseModuleId);
                Vocabularies vocabularies = languagePackService.GetModuleVocabularies(langPackName, LanguagePackBaseModuleId);
                string guid = Guid.NewGuid().ToString();

                vocabs.Add(guid, vocabularies);     // assuming only one phonemizer model per module. Maybe add some catch on duplicate key for good measure
                foreach (string file in filesToLoad)
                {

                    if (file.EndsWith(".sentis"))
                    {
                        string filePath = Path.Combine(RuntimeFileLoader.GetLanguagePacksPath(true), langPackName, file);
                        Model model = ModelLoader.Load(RuntimeFileLoader.LoadFileAsStream(filePath));
                        if (model == null)
                        {
                            Debug.LogWarning("Error loading model: " + file);
                            throw new Exception("Error loading model: " + file + ". Could not preload language module.");
                        }
                        // if(!phonemizerModels.ContainsKey(file))
                        // {
                        //     phonemizerModels.Add(file, model);
                        // }
                        Worker worker = CreatePhonemizerWorker(model, vocabularies.phoneme_vocab);

                        languagePackService.GetLanguages(langPackName, LanguagePackBaseModuleId).ForEach(lang => GlobalLangIdsDict.Add(lang, guid));
                        preloadedPhonemizerWorkers.Add(guid, worker);   // assuming only one phonemizer model per module. Maybe add some catch on duplicate key for good measure
                    }
                    else if (file.EndsWith(".json"))
                    {
                        string filePath =  Path.Combine(RuntimeFileLoader.GetLanguagePacksPath(true), langPackName, file);
                        string jsonContent = RuntimeFileLoader.LoadFileAsString(filePath);
                        Dictionary<string, string> dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);
                        if (dict != null) lookupTables.Add(guid, dict);  // assuming only one phonemizer model per module. Maybe add some catch on duplicate key for good measure
                        else
                        {
                            Debug.LogWarning("Error loading lookup table: " + file);
                            throw new Exception("Error: could not load lookup table: " + file);
                        }
                    }
                    else Debug.LogWarning("Unknown file type: " + file + ". Continuing but this file will not be loaded.");
                }
            }
            globalLangIds.Add(module.name, GlobalLangIdsDict); 
            moduleSymbolTable.Add(module.name, module.phonemes_table.symbol_to_id);
        }

        /// <summary>
        /// Helper method which finds the language pack name corresponding to moduleId from the packMappings JSON object.
        /// </summary>
        /// <param name="packMappings">Represent the JSON object mapping Actor Packs to its Language Packs.</param>
        /// <param name="moduleId">ID of the module for which to find its parent Language Pack</param>
        /// <returns></returns>
        private static string FindLanguagePackName(JObject packMappings, string moduleId)
        {
            foreach (var actorPack in packMappings["actorpacks"])
            {
                foreach (var moduleName in actorPack.First)
                {
                    foreach (var module in moduleName.First)
                    {   
                        if (module.Path.Contains(moduleId))
                        {
                            return module.First.ToString();
                        }
                    }
                }
            }
            return null; // Return null if the moduleId is not found
        }

        /// <summary>
        /// Gets a list of all currently preloaded ActorPackModule names.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetLoadedActorPackModules()
        {
            return preloadedSynthWorkers.Keys.Select(k => k.Item1).Distinct().ToList();
        }

        /// <summary>
        /// Unloads the preloaded ActorPackModule and its associated language packs. If no module name is provided, all preloaded modules are unloaded. This will not unload modules which are currently running.
        /// </summary>
        /// <param name="actorPackModuleName">Optional name of specific ActorPackModule to unload. Otherwise unloads all</param>
        public static void UnloadActorPackModule(string actorPackModuleName=null)      // OBS fix so this also unloads the models? And maybe there is something else here. Investigate the matter.
        {
            if(actorPackModuleName == null)
            {
                // Debug.Log("Unloading all actorpacks");
                foreach(var module in GetLoadedActorPackModules())
                {
                    UnloadActorPackModule(module);
                }
                return;
            } 
            if(ThespeonAPI.isRunning && runningModules[actorPackModuleName] != null && runningModules[actorPackModuleName].Length > 0)
            {    
                Debug.LogWarning($"Cannot unload module {actorPackModuleName} while it is running. Unloading of this module will be skipped.");
                return;
            }
            
            if(!GetLoadedActorPackModules().Contains(actorPackModuleName))
            {
                // Debug.Log($"Module {actorPackModuleName} was not loaded. Nothing to unload. Skipping.");
                return;
            }
            
            List<(string, string)> keys = preloadedSynthWorkers.Keys.Where(k => k.Item1 == actorPackModuleName).ToList();
            foreach(var key in keys)
            {
                preloadedSynthWorkers[key].Dispose();     
                preloadedSynthWorkers.Remove(key);
            }
            List<string> GUIDS = globalLangIds[actorPackModuleName].Values.ToList();
            globalLangIds.Remove(actorPackModuleName);
            moduleSymbolTable.Remove(actorPackModuleName);
            foreach (string guid in GUIDS)
            {
                preloadedPhonemizerWorkers[guid].Dispose();     //objs not necessarily. Here we also need to filter for if its is used by any other module. OBS we need to fix this in insertion as well so as to only create worker if its needed, otherwise store reference.
                preloadedPhonemizerWorkers.Remove(guid);
                lookupTables.Remove(guid);
                vocabs.Remove(guid);
            }
            if(runningModules.ContainsKey(actorPackModuleName))
            {
                foreach (InferenceStep step in runningModules[actorPackModuleName])
                {
                    step?.Destroy();
                }
                runningModules.Remove(actorPackModuleName);
            }
            
        }

        /// <summary>
        ///  Returns true if the given moduleId is currently loaded in memory.
        /// </summary>
        /// <param name="moduleId"></param>
        /// <returns></returns>
        public static bool IsPreloaded(string moduleId)
        {
            return globalLangIds.ContainsKey(moduleId);
        }

        public static bool HasAnyLoadedLanguageModules(string moduleId){
            return globalLangIds[moduleId].Count != 0;  
        }
        
        // IEnumerator loadModelCoroutine()
        // {
        //     // Model and vocabulary 
        //     Debug.Log("Phonemizer loaded");
        //     charToIdphonemeVocab_tts = phonemeVocab_tts;

        //     string jsonData = Resources.Load<TextAsset>("emotiondata_input_example").text;
        //     root = JsonConvert.DeserializeObject<Root>(jsonData);
        // }
#region RunModelCoroutine
            public static IEnumerator RunModelCoroutine(string synthRequestID, Action<LingotionDataPacket<float>> callback, List<int>[] customSkipIndices = null) {
        
            ThespeonAPI.isRunning = true;
            // Debug.Log($"Trying to add layer {customSkipIndices} to heavylayers");
            yield return null;
            yield return new WaitForEndOfFrame();   
            Profiler.BeginSample("RunModel input");

            UserModelInput input = synthIDToUserInput[synthRequestID];

            (ActorPackModule selectedModule, _ ) = ThespeonAPI.GetActorPackModule(input);


            PopulateUserModelInput(input, selectedModule);
            

            // Debug.Log($"Registered ActorPacks: {string.Join(", ", ThespeonAPI.GetRegisteredActorPacks().Keys)}");
            
            int emotionKey =  GetEmotionSetKey(selectedModule, input.defaultEmotion);
            string emotion = input.defaultEmotion;           

            int inputTextLength = string.Join("", input.segments.Select(s => s.text)).Length;

            // clamp speed to always be larger than 0.1
            for(int i = 0; i < input.speed.Count; i++)
            {
                input.speed[i] = Math.Max(0.1, input.speed[i]);
            }
            input.speed = ResampleListAdvanced(input.speed, inputTextLength );

            //clamp loudness to always be non-negative
            for(int i = 0; i < input.loudness.Count; i++)
            {
                input.loudness[i] = Math.Max(0, input.loudness[i]);
            }
            input.loudness = ResampleListAdvanced(input.loudness, inputTextLength);

            string actorStageName = input.actorUsername;
            int actorID = selectedModule.GetActor(actorStageName).actorkey;
            string moduleName = input.moduleName;

            Profiler.EndSample();

            
            Profiler.BeginSample("RunModel extractWords");

            Dictionary<Language, HashSet<string>> wordsByLanguage = ExtractWordsByLanguage(input);

            Dictionary<Language, (ThespeonPhonemizer, Dictionary<string, string> , Vocabularies, Dictionary<string, int> , string)> tools  = new Dictionary<Language, (ThespeonPhonemizer, Dictionary<string, string> , Vocabularies, Dictionary<string, int>, string)>();
            Dictionary<Language, Dictionary<string, (List<int> phonemeKeys, string phonemeString)>> unkownLookups =  new Dictionary<Language, Dictionary<string, (List<int> phonemeKeys, string phonemeString)>>();
            Dictionary<Language, Dictionary<string, string>> knownLookups =  new Dictionary<Language, Dictionary<string, string>>();
            Profiler.EndSample();
            // Now iterate over the dictionary
            foreach (var kvp in wordsByLanguage)
            {
                
                Profiler.BeginSample("some iteration");
                Language language = kvp.Key;
                HashSet<string> wordSet = kvp.Value;

                tools[language] = GetPhonemizerTools(language, selectedModule, synthRequestID);

                var langPhonemizer = tools[language].Item1;
                var lookup = tools[language].Item2;
                var vocabularies = tools[language].Item3;
                var symbolTable = tools[language].Item4;
                var guid = tools[language].Item5;
                knownLookups[language]=lookup;

                // Filter out any words that appear as a key in the lookup
                var unknownWords =  FindUnknownWords(wordSet, lookup);


                // Make a list of emotionSetKeys as long as the corresponding inputTextGroups entry
                var langText= string.Join(" ", unknownWords);
                int segmentLength = langText.Count();
                Profiler.EndSample();
                TaskCompletionSource<Dictionary<string, (List<int>, string)>> phonemizeTask = new TaskCompletionSource<Dictionary<string, (List<int>, string)>>();
                yield return langPhonemizer.Infer(new PhonemizerInput(null, phonemizeTask, unknownWords));
                unkownLookups[language] = phonemizeTask.Task.Result;

                foreach (var p in unkownLookups[language])
                {
                    if (!lookup.ContainsKey(p.Key))
                    {
                       //Add the word to the lookup table
                        lookup.Add(p.Key, p.Value.phonemeString);
                    }
                }

                lookupTables[guid] = lookup;
            }


            // loop over the input text and get the phoneme keys for each word (and the corresponding emotionkeys and languagekeys), if CustomPhonemized is true, use the symbol table to get the phoneme keys
            // Otherwise use the lookup tables in UnkownLookups and knownLookups to get the phoneme keys for each word
            // Then concatenate the phoneme keys for each word to get the phoneme keys for the whole input text

            
            Dictionary<string, int> moduleEmotionKeys = selectedModule.GetEmotionKeyDictionary();


            List<int> phonemeKeys = new List<int>();
            List<int> alignedEmotionKeys = new List<int>();
            List<int> alignedLanguageKeys = new List<int>();

            List<double> alignedSpeed = new List<double>();
            List<double> alignedLoudness = new List<double>();

            int segmentCounter=0;
            int charsCounter=0;

            foreach (var segment in input.segments)
            {
                
                Profiler.BeginSample("Phonemizer apply");
                var lang = segment.languageObj ?? input.defaultLanguage;
                var segmentEmotion = segment.emotion ?? input.defaultEmotion;
                var isCustomPhonemized = segment.isCustomPhonemized;
                var text = segment.text;
                // Debug.Log($"Segment: {text} - {lang} - {segmentEmotion} - {isCustomPhonemized}");
                List<Language> candidateLanguages = selectedModule.language_options.languages;
                var (closest, distance, _) = LanguageExtensions.FindClosestLanguage(lang, candidateLanguages);
                var languageKey = closest.languageKey;
                


                
                


                if (isCustomPhonemized ?? false)
                {
                    List<int> segmentPhonemeKeys = PhonemeToKey(text.ToLower(), moduleSymbolTable[input.moduleName]);
                    int segmentLength = segmentPhonemeKeys.Count;
                    emotionKey = moduleEmotionKeys[segmentEmotion];
                    

                    phonemeKeys.AddRange(segmentPhonemeKeys);
                    alignedEmotionKeys.AddRange(Enumerable.Repeat(emotionKey, segmentLength));
                    alignedLanguageKeys.AddRange(Enumerable.Repeat(languageKey ?? 1, segmentLength));

                    //Debug.Log($"Member Language keys:   "+ string.Join("-",Enumerable.Repeat(languageKey ?? 1, segmentLength)));
                    //Debug.Log($"Member Emotion keys:  "+ string.Join("-",Enumerable.Repeat(emotionKey, segmentLength)));


                    var textLength= text.Length;

                    List<double> pieceSpeed = input.speed.GetRange(charsCounter, textLength);
                    List<double> pieceLoudness = input.loudness.GetRange(charsCounter, textLength);

                    List<double> editedSpeed = ResampleListAdvanced(pieceSpeed, segmentLength);
                    List<double> editedLoudness = ResampleListAdvanced(pieceLoudness, segmentLength);

                    alignedSpeed.AddRange(editedSpeed);
                    alignedLoudness.AddRange(editedLoudness);


                    //Make some logs here for speed and loudness and their counts
                    //Debug.Log($"piece Speed: {string.Join(", ", pieceSpeed)} - {pieceSpeed.Count}");
                    //Debug.Log($"piece Loudness: {string.Join(", ", pieceLoudness)} - {pieceLoudness.Count}");
                    //Debug.Log($"edited Speed: {string.Join(", ", editedSpeed)} - {editedSpeed.Count}");
                    //Debug.Log($"edited Loudness: {string.Join(", ", editedLoudness)} - {editedLoudness.Count}");
                    //Debug.Log($"number of chars counter: {charsCounter}");
                    charsCounter+=textLength;





                }
                else
                {
                    //Get text
                    //Split in words and keep track of delimeters
                    //For each word, check if it is in the knownLookups or unkownLookups
                    //If it is in knownLookups, get the PhonemeKeys and add them to the phonemeKeys list
                    //If it is in unkownLookups, get the PhonemeKeys and add them to the phonemeKeys list
                    //If it is a delimeter, add the corresponding phonemeId to the phonemeKeys list
                    //Add the corresponding emotionKey and languageKey to the alignedEmotionKeys and alignedLanguageKeys lists

                    List<string> words = new List<string>();
                    List<int> delimeters = new List<int>();

                    List<(string Text, bool IsWord)> result = ExtractWordsAndNonWords(text);
                    tools[lang] = GetPhonemizerTools(lang, selectedModule, synthRequestID);
                    ThespeonPhonemizer langPhonemizer = tools[lang].Item1;
                    Dictionary<string,string> lookup = tools[lang].Item2;
                    Vocabularies vocabularies = tools[lang].Item3;
                    Dictionary<string,int> symbolTable = tools[lang].Item4;

                    emotionKey = moduleEmotionKeys[segmentEmotion];
                    foreach (var (textPart, isWord) in result)
                    {
                        List<int> encoderIDs;
                        if (isWord)
                        {
                            Dictionary<string,string> correspondingKnownLookup =   knownLookups[lang] ;
                            Dictionary<string, (List<int>, string)> correspondingUnknownLookup =   unkownLookups[lang] ;

                            string phonemeString = "";
                            // Word, check if it is in knownLookups or unkownLookups
                            if (correspondingKnownLookup.ContainsKey(textPart))
                            {
                                // Found in knownLookups, get the PhonemeKeys
                                phonemeString = correspondingKnownLookup[textPart];
                            }
                            else if (correspondingUnknownLookup.ContainsKey(textPart))
                            {
                                // Found in unkownLookups, get the PhonemeKeys
                                (_, phonemeString) = correspondingUnknownLookup[textPart];
                            }else
                            {
                                // Not found in knownLookups or unkownLookups, use the phonemizer
                                continue;
                            }
                            encoderIDs = EncodePhoneme(phonemeString, symbolTable);

                        }
                        else
                        {
                            // Delimeter, encode it 
                            encoderIDs= EncodeDelimiter(textPart, symbolTable);
                        }
                        int segmentLength = encoderIDs.Count;
                        phonemeKeys.AddRange(encoderIDs);

                        alignedEmotionKeys.AddRange(Enumerable.Repeat(emotionKey, segmentLength));
                        alignedLanguageKeys.AddRange(Enumerable.Repeat(languageKey ?? 1, segmentLength));
                        int textLength= textPart.Length;

                        List<double> pieceSpeed = input.speed.GetRange(charsCounter, textLength);
                        List<double> pieceLoudness = input.loudness.GetRange(charsCounter, textLength);

                        List<double> editedSpeed = ResampleListAdvanced(pieceSpeed, segmentLength);
                        List<double> editedLoudness = ResampleListAdvanced(pieceLoudness, segmentLength);

                        alignedSpeed.AddRange(editedSpeed);
                        alignedLoudness.AddRange(editedLoudness);

                        //Debug.Log($"piece Loudness: {string.Join(", ", pieceLoudness)} - {pieceLoudness.Count}");
                        //Debug.Log($"edited Speed: {string.Join(", ", editedSpeed)} - {editedSpeed.Count}");
                        //Debug.Log($"piece Speed: {string.Join(", ", pieceSpeed)} - {pieceSpeed.Count}");
                        //Debug.Log($"edited Loudness: {string.Join(", ", editedLoudness)} - {editedLoudness.Count}");
                        //Debug.Log($"number of chars counter: {charsCounter}");
                        charsCounter+=textLength;

                    }
                }


                //Add a space between segment and it's corresponding language and emotion keys (also annotatinos). Skip if only one segment or the last segment of input or if the segment already end with a space or the next segment starts with a space
                if (segmentCounter < input.segments.Count - 1 && input.segments.Count != 1 && !text.EndsWith(" ") && input.segments[segmentCounter + 1].text.StartsWith(" "))
                {

                    List<int> spacePhonemeId = PhonemeToKey(" ", moduleSymbolTable[input.moduleName]);    
                    //Above should be a single value but we are using a list for convenience   
                    if (spacePhonemeId.Count != 1)
                    {
                        Debug.LogWarning("Space phoneme ID should be a single value");
                    }
                    Debug.LogWarning("Adding space phoneme ID");
                    phonemeKeys.AddRange(spacePhonemeId);
                    var spaceEmotionKey = moduleEmotionKeys[segmentEmotion];
                    alignedEmotionKeys.Add(spaceEmotionKey);
                    alignedLanguageKeys.Add(languageKey ?? 1);
                    //get the last speed and loudness values
                    int lastSpeedIndex = input.speed.Count - 1;
                    int lastLoudnessIndex = input.loudness.Count - 1;
                    alignedSpeed.Add(input.speed[lastSpeedIndex]);
                    alignedLoudness.Add(input.loudness[lastLoudnessIndex]);
                }
                segmentCounter++;
                Profiler.EndSample();
            }


            // Add SOS and EOS tokens to phoneme IDs and emotion keys
            Dictionary<string, int> symbolToID = moduleSymbolTable[input.moduleName];
            phonemeKeys.Insert(0, symbolToID["⏩"]);      //Yes or no?
            phonemeKeys.Add(symbolToID["⏪"]);            //Yes or no?

            alignedEmotionKeys.Insert(0, alignedEmotionKeys.FirstOrDefault()); // Assign first key for SOS
            alignedEmotionKeys.Add(alignedEmotionKeys.LastOrDefault()); // Assign last key for EOS

            alignedLanguageKeys.Insert(0, alignedLanguageKeys.FirstOrDefault()); // Assign first key for SOS
            alignedLanguageKeys.Add(alignedLanguageKeys.LastOrDefault()); // Assign last key for EOS


            alignedSpeed.Insert(0, alignedSpeed.FirstOrDefault()); // Assign first key for SOS
            alignedSpeed.Add(alignedSpeed.LastOrDefault()); // Assign last key for EOS

            alignedLoudness.Insert(0, alignedLoudness.FirstOrDefault()); // Assign first key for SOS
            alignedLoudness.Add(alignedLoudness.LastOrDefault()); // Assign last key for EOS


            // Get the phoneme text of the phonemeID through id_to_symbol in slected_module
            List<string> phonemeText = phonemeKeys.Select(id => symbolToID.FirstOrDefault(x => x.Value == id).Key).ToList();
            // Debug.Log($"PhonemeText and their length: {string.Join(", ", phonemeText)} - {phonemeText.Count}");



            var (encoder, decoder, vocoder) = BuildModels(synthRequestID); 

            if(customSkipIndices != null)
            {
                
                // Debug.Log($"Trying to add layer {customSkipIndices[1].Count} to heavylayers");
                encoder.AddCustomSkipIndices(customSkipIndices[0]);
                decoder.AddCustomSkipIndices(customSkipIndices[1]);
                vocoder.AddCustomSkipIndices(new[]{customSkipIndices[2], customSkipIndices[3], customSkipIndices[4]});
            }

            ////////////////////////////////////////////////////////////
            /// Encoder
            /// //////////////////////////////////////////////////////////
            
            tensors["phonemeTensor"] = new Tensor<int>(new TensorShape(1, phonemeKeys.Count), phonemeKeys.ToArray());
            tensors["emotionTensor"] = new Tensor<int>(new TensorShape(1, alignedEmotionKeys.Count), alignedEmotionKeys.ToArray());
            tensors["actorIDTensor"] = new Tensor<int>(new TensorShape(1, 1), new[]{actorID});
            tensors["languageTensor"] = new Tensor<int>(new TensorShape(1, alignedLanguageKeys.Count), alignedLanguageKeys.ToArray());
            tensors["speedTensor"] = new Tensor<float>(new TensorShape(1, alignedSpeed.Count), alignedSpeed.Select(d => (float)d).ToArray());
            tensors["loudnessTensor"] = new Tensor<float>(new TensorShape(1, alignedLoudness.Count), alignedLoudness.Select(d => (float)d).ToArray());
            
            Tensor[] inputsEncoder = new Tensor[6]
                {
                    tensors["phonemeTensor"],
                    tensors["emotionTensor"],
                    tensors["actorIDTensor"],
                    tensors["languageTensor"],
                    tensors["speedTensor"],
                    tensors["loudnessTensor"]
                };

            TaskCompletionSource<Tensor[]> tcs = new TaskCompletionSource<Tensor[]>();
            yield return encoder.Infer(new EncoderInput(inputsEncoder, tcs));
            Tensor[] encoderOutput = tcs.Task.Result;

            phonemeKeys.Clear();
            alignedEmotionKeys.Clear();
            alignedLanguageKeys.Clear();
            // Inputs: {Tensor<float> encoderMel, Tensor<int> encoder_mel_max_length}
            Tensor[] decoderPreprocessInput = new[]{encoderOutput[0], encoderOutput[2]};
            // Outputs: {Tensor<float> z, Tensor<int> nbr_of_chunks, Tensor<float> decoded_mel_overlap, Tensor<int> remainder, Tensor<int> trim_length}
            Tensor[] decoderPreprocessOutput = decoder.DecoderPreprocess(decoderPreprocessInput);

            

            yield return RunDecoderChunking(encoderOutput, decoderPreprocessOutput, decoder, vocoder, moduleIdToConfig[synthIDToUserInput[synthRequestID].moduleName], synthRequestID, callback);

            foreach (var t in inputsEncoder){ t.Dispose();}
            inputsEncoder = null;
            tensors["phonemeTensor"].Dispose();
            tensors["emotionTensor"].Dispose();
            tensors["actorIDTensor"].Dispose();
            tensors["languageTensor"].Dispose();
            tensors["speedTensor"].Dispose();
            tensors["loudnessTensor"].Dispose();
        }

#endregion
#region Public getters and setters
        public static void SetBackend(BackendType backendType)
        {
            BACKEND_TYPE = backendType;
        }
        public static Vocabularies GetVocabs(Language language, ActorPackModule module)
        {
            
            List <Language> candidateLanguages = globalLangIds[module.name].Keys.ToList();

                
            var (closest, distance, _) = LanguageExtensions.FindClosestLanguage(language, candidateLanguages);
            var guidForGroup = globalLangIds[module.name][closest];

            Vocabularies vocabularies = vocabs[guidForGroup];

            // Create and store a phonemizer for this segment
            return vocabularies;
        

        }

        public static Dictionary<string, int> GetSymbolToID(string moduleName)
        {
            return moduleSymbolTable[moduleName];
        }

#endregion
#region Private Helpers 
        // TODO: Refactor whole function, way too messy and obscure. Investigate easier way of keeping track of inputs/outputs
        private static IEnumerator RunDecoderChunking(Tensor[] encoderOutputs, Tensor[] decoderPreprocessOutputs, ThespeonDecoder decoder, ThespeonVocoder vocoder, Config config, string synthRequestID, Action<LingotionDataPacket<float>> callback){
            int current_chunk_idx = 0;
            int chunk_idx = 0;

            tensors["chunk_idx"] = new Tensor<int>(new TensorShape(1), new int[]{chunk_idx});
            tensors["chunk_length"] = new Tensor<int>(new TensorShape(1), new int[]{config.decoder_chunk_length});
            tensors["boundary_clone_alpha"] = new Tensor<float>(new TensorShape(1), new []{0.0f});
            tensors["actor"] = new Tensor<int>(new TensorShape(1,1), new int[]{1});
            // Inputs DecoderChunking: {int chunk_index, int chunk_length, float z, float encoder_mel, int encoder_mel_mask, float boundary_clone_alpha, int actors, flaot prev_decoded_overlap}
            Tensor[] decoderChunkedInput = new Tensor[8]
            {
                tensors["chunk_idx"],
                tensors["chunk_length"],
                decoderPreprocessOutputs[0],
                encoderOutputs[0],
                encoderOutputs[1],
                tensors["boundary_clone_alpha"],
                tensors["actor"],   //change later
                decoderPreprocessOutputs[2]
            };

            // Read back number of chunks 
            Tensor<int> num_chunks_tensor = decoderPreprocessOutputs[1] as Tensor<int>;
            int nbr_of_chunks = num_chunks_tensor.DownloadToArray()[0];
            num_chunks_tensor.Dispose();
            float[] outputAudio = null;
            while (chunk_idx < nbr_of_chunks)
            {
                
                if (current_chunk_idx == 0)
                {
                    TaskCompletionSource<Tensor<float>[]> tcs = new TaskCompletionSource<Tensor<float>[]>();
                    yield return decoder.Infer(new DecoderInput(decoderChunkedInput, tcs));
                    decoderChunkedInput[7]?.Dispose();
                    decoderChunkedInput[7] = tcs.Task.Result[1];
                    
                    TaskCompletionSource<float[]> vocoderTask = new TaskCompletionSource<float[]>();
                    yield return vocoder.Infer(new VocoderInput(new Tensor[]{tcs.Task.Result[0], encoderOutputs[5]}, vocoderTask, ChunkType.First));
                    outputAudio = vocoderTask.Task.Result;
                    callback?.Invoke(new LingotionDataPacket<float>("Audio", new Dictionary<string, object> {{"length", outputAudio.Length}, {"sample rate", 44100}, {"finalDataPackage", false}}, outputAudio));

                }
                
                else if (chunk_idx == nbr_of_chunks - 1)
                {
                    // last chunk
                    decoderChunkedInput[0]?.Dispose();
                    decoderChunkedInput[0] = new Tensor<int>(new TensorShape(1), new int[]{chunk_idx}); 
                    TaskCompletionSource<Tensor<float>[]> tcs = new TaskCompletionSource<Tensor<float>[]>();
                    yield return decoder.Infer(new DecoderInput(decoderChunkedInput, tcs));

                    // Make adjustments of final chunk
                    Tensor[] decoderFinalChunkOutput = tcs.Task.Result;
                    
                    Tensor[] decoderPostProcessInput = new[]{decoderFinalChunkOutput[0], decoderFinalChunkOutput[1], decoderPreprocessOutputs[3], encoderOutputs[1], decoderPreprocessOutputs[4]};

                    Tensor<float> finalVocoderInput = decoder.DecoderPostprocess(decoderPostProcessInput) as Tensor<float>;

                    TaskCompletionSource<float[]> vocoderTask = new TaskCompletionSource<float[]>();
                    yield return vocoder.Infer(new VocoderInput(new Tensor[]{finalVocoderInput}, vocoderTask, ChunkType.Last));
                    finalVocoderInput.Dispose();
                    outputAudio = vocoderTask.Task.Result;
                    callback?.Invoke(new LingotionDataPacket<float>("Audio", new Dictionary<string, object> {{"length", outputAudio.Length}, {"sample rate", 44100}, {"finalDataPackage", true}}, outputAudio));
                    break;
                    
                }
                else
                {
                    // middle chunk
                    decoderChunkedInput[0]?.Dispose();
                    decoderChunkedInput[0] = new Tensor<int>(new TensorShape(1), new int[]{chunk_idx}); 
                    decoderChunkedInput[5]?.Dispose();
                    decoderChunkedInput[5] = new Tensor<float>(new TensorShape(1), new float[]{1f});
                    TaskCompletionSource<Tensor<float>[]> tcs = new TaskCompletionSource<Tensor<float>[]>();
                    yield return decoder.Infer(new DecoderInput(decoderChunkedInput, tcs));
                    decoderChunkedInput[7]?.Dispose();
                    decoderChunkedInput[7] = tcs.Task.Result[1];

                    TaskCompletionSource<float[]> vocoderTask = new TaskCompletionSource<float[]>();
                    yield return vocoder.Infer(new VocoderInput(new Tensor[]{tcs.Task.Result[0]}, vocoderTask, ChunkType.Middle));
                    outputAudio = vocoderTask.Task.Result;
                    callback?.Invoke(new LingotionDataPacket<float>("Audio", new Dictionary<string, object> {{"length", outputAudio.Length}, {"sample rate", 44100}, {"finalDataPackage", false}}, outputAudio));
                }
                // Move to the next chunk
                int chunk_size = config.decoder_chunk_length;
                int boundary_length_ratio = config.chunk_boundary_length_ratio;
                int boundary_length = (int) Mathf.Round((float)chunk_size/(float)boundary_length_ratio);
                int chunk_hop_length = chunk_size - boundary_length;
                current_chunk_idx += chunk_hop_length;
                chunk_idx++;
            }

            for (int i = 0; i < decoderChunkedInput.Length; i++)
            {
                decoderChunkedInput[i]?.Dispose();
            }
            ThespeonAPI.isRunning = false;
        }
        private static Worker CreatePhonemizerWorker(Model phonemizerModel, Dictionary<string, int> phonemeVocab)
        {
            var graph = new FunctionalGraph();

            var target_shape = DynamicTensorShape.DynamicOfRank(2);

            var phonemizer_input = graph.AddInput<int>(target_shape);
            
            var previous_output = graph.AddInput<int>(target_shape);
            var mask = graph.AddInput<int>(target_shape);
            var finished_indices = graph.AddInput<int>(DynamicTensorShape.DynamicOfRank(1));
            //throw new NotImplementedException();
            // perform phonemizer inference
            var phonemizer_output = Functional.ForwardWithCopy(phonemizerModel, new[] { phonemizer_input, previous_output, finished_indices < 1});
            // if next predicted value is EOS or has been previously. Returns 1 if any are true, else 0
            var cond = Functional.Equals(phonemizer_output[0][.., ^1], phonemeVocab[EOS_TOKEN]) | (finished_indices > 0);
            // concat result to the "counter" mask
            mask = Functional.Concat(new[] { mask, Functional.Unsqueeze(1 - cond, 1) }, 1);
            // if eos or has been, calculate stop index, else 0
            finished_indices = Functional.Where(cond, Functional.ReduceSum(mask, 1), Functional.Constant(0));
            // count the finished batches
            var num_finished = Functional.ReduceSum(cond, 0);
            FunctionalTensor[] outputs = { phonemizer_output[0], mask, finished_indices, num_finished, phonemizer_output[1] };
            var wrapper_model = graph.Compile(outputs);
            // TODO: Investigate running phonemizer on GPU without significant stutters
            return new Worker(wrapper_model, BackendType.CPU);

        }
        private static int GetEmotionSetKey(ActorPackModule actorPackModule, string emotionSetName)
        {
            if (actorPackModule?.emotion_options?.emotions == null)
            {
                Debug.LogWarning($"Module '{actorPackModule.name}' or its emotion options not found.");
                return -1;
            }
            var emotion = actorPackModule.emotion_options.emotions
                .FirstOrDefault(e => e.emotionsetname.Equals(emotionSetName, StringComparison.OrdinalIgnoreCase));

            if (emotion == null)
            {
                Debug.LogWarning($"Emotion set name '{emotionSetName}' not found in '{actorPackModule.name}'.");
                return -1;
            }
            return emotion.emotionsetkey;
        }

        private static List<int> PhonemeToKey(string phonemeText, Dictionary<string, int> symbol_to_id)
        {
            List<int> phonemeIds = new List<int>();
            foreach (char phonemeChar in phonemeText)
            {
                if (symbol_to_id.TryGetValue(phonemeChar.ToString(), out int id))
                {
                    phonemeIds.Add(id);
                }
                else
                {
                    Debug.LogWarning($"Phoneme '{phonemeChar}' is a not a valid phoneme charachter in segment \"{phonemeText}\". Therefore it was removed from the phoneme sequence."); 
                }
            }
            return phonemeIds;
        }


        
        private static Dictionary<Language, HashSet<string>> ExtractWordsByLanguage(UserModelInput modelInput)
        {
            Regex WordRegex = new Regex(@"[\p{L}\p{M}\p{N}]+", RegexOptions.Compiled);
            var result = new Dictionary<Language, HashSet<string>>();
            if (modelInput == null)
            {
                throw new ArgumentNullException(nameof(modelInput));
            }
            // If no default language was set, we can still handle it safely
            // or throw an exception if your logic requires a guaranteed default.
            var defaultLanguageIso = modelInput.defaultLanguage;
            foreach (var segment in modelInput.segments)
            {
                // Determine which language to use for this segment
                var segmentLanguageIso = segment.languageObj ?? defaultLanguageIso;

                // If there's still no language, skip or handle as needed
                if (segmentLanguageIso == null)
                {
                    continue; // or throw an exception, depending on requirements
                }

                // Split text into words. You can change the separators or use a regex if desired.
                var text = segment.text ?? string.Empty;
                //var words = text
                //    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                //    .Select(w => w.Trim());
                var matches = WordRegex.Matches(text);
                foreach (Match match in matches)
                //foreach (var word in words)
                {
                    if (!result.TryGetValue(segmentLanguageIso, out var wordSet))
                    {
                        wordSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        result[segmentLanguageIso] = wordSet;
                    }
                    wordSet.Add(match.Value);
                }
            }
            return result;
        }
        private static (ThespeonPhonemizer, Dictionary<string, string> , Vocabularies, Dictionary<string, int>, string guid) GetPhonemizerTools(Language language, ActorPackModule module, string synthRequestID)
        {
            List <Language> candidateLanguages = globalLangIds[module.name].Keys.ToList();
            var (closest, distance, _) = LanguageExtensions.FindClosestLanguage(language, candidateLanguages);      
            var guidForGroup = globalLangIds[module.name][closest];

            Worker phonemizerWorker = preloadedPhonemizerWorkers[guidForGroup];
            Dictionary<string, string> lookupTable = lookupTables[guidForGroup];
            Vocabularies vocabularies = vocabs[guidForGroup];
            Dictionary<string, int> symbolTable = moduleSymbolTable[module.name];
            PackageConfig localPackageConfig = localConfigs[synthRequestID];

            // Create and store a phonemizer for this segment
            ThespeonPhonemizer phonemizer = null;
            if(localPackageConfig.targetFrameTime.HasValue && localPackageConfig.useAdaptiveFrameBreakScheduling.HasValue && localPackageConfig.overshootMargin.HasValue)
            {
                phonemizer = new ThespeonPhonemizer(ref phonemizerWorker, ref vocabularies, localPackageConfig.targetFrameTime.Value, localPackageConfig.useAdaptiveFrameBreakScheduling.Value, localPackageConfig.overshootMargin.Value);
            }
            else
            {
                Debug.LogError($"Local config entries for synth {synthRequestID} are null.");
            }
            return (phonemizer, lookupTable, vocabularies, symbolTable, guidForGroup);


        }
        private static List<string> FindUnknownWords(HashSet<string> words, Dictionary<string, string> lookupDictionary)
        {
            if (words == null)
                throw new ArgumentNullException(nameof(words));
            if (lookupDictionary == null)
                throw new ArgumentNullException(nameof(lookupDictionary));

            // Filter out any words that appear as a key in the lookup
            var unknownWords = new List<string>();
            foreach (var word in words)
            {
                // If the dictionary does not contain the word as a key, it's considered "unknown"
                if (!lookupDictionary.ContainsKey(word))
                {
                    unknownWords.Add(word);
                }
            }

            return unknownWords;
        }
        private static List<(string Text, bool IsWord)> ExtractWordsAndNonWords(string text)
        {

            Regex WordRegex = new Regex(@"[\p{L}\p{M}\p{N}]+", RegexOptions.Compiled);

            var result = new List<(string Text, bool IsWord)>();
            if (string.IsNullOrEmpty(text)) return result;

            int lastIndex = 0;
            var matches = WordRegex.Matches(text);

            foreach (Match match in matches)
            {
                // Capture the non-matching part before the match
                if (match.Index > lastIndex)
                {
                    result.Add((text.Substring(lastIndex, match.Index - lastIndex), false));
                }

                // Capture the matched word
                result.Add((match.Value, true));

                // Move lastIndex forward
                lastIndex = match.Index + match.Length;
            }

            // Capture any trailing non-matching part
            if (lastIndex < text.Length)
            {
                result.Add((text.Substring(lastIndex), false));
            }

            return result;
        }
        private static List<int> EncodeDelimiter(string seq, Dictionary<string, int> symbolToID)
        {
            // Implement encoding logic based on your vocabulary
            List<int> encoded = new List<int>();
            foreach (var token in seq)
            {
                if (symbolToID.ContainsKey(token.ToString()))
                {
                    encoded.Add(symbolToID[token.ToString()]);
                }
                else
                {
                    Debug.LogWarning($"Delimiter '{token}' is not valid symbol in model. Ignoring.");
                }
            }
            return encoded;

        }
        private static List<int> EncodePhoneme(string phonemes, Dictionary<string, int> symbolToID)
        {
            List<int> encoded = new List<int>();
            foreach (char token in phonemes)
            {
            if (symbolToID.ContainsKey(token.ToString()))
            {
                encoded.Add(symbolToID[token.ToString()]);
            }
            else
            {
                Debug.LogWarning($"Phoneme '{token}' is not valid symbol in model. Ignoring.");
            }
            }
            return encoded;
        }

        private static (ThespeonEncoder, ThespeonDecoder, ThespeonVocoder) BuildModels(string synthRequestID)
        {
            PackageConfig localPackageConfig = localConfigs[synthRequestID];
            //Due to global config having default values, no element in it should ever be null except if someone tampered with the code.
            if(!localPackageConfig.targetFrameTime.HasValue || !localPackageConfig.useAdaptiveFrameBreakScheduling.HasValue || !localPackageConfig.overshootMargin.HasValue) Debug.LogError($"Local config entries for synth {synthRequestID} are null.");
        

            string moduleId = synthIDToUserInput[synthRequestID].moduleName;
            // Find previous Inference step objects
            if(runningModules.ContainsKey(moduleId) && runningModules[moduleId][0].GetFirstWorkerHash() == preloadedSynthWorkers[(moduleId, "encoder")].GetHashCode()) 
            {
                var stepsObjects = runningModules[moduleId];
                foreach (var step in stepsObjects)
                {
                    step.ApplyConfigChange(localPackageConfig);                                                                             //This will need revision once Backend is added to the config. See TUNI-75.
                }

                return (
                    (ThespeonEncoder)stepsObjects[0], 
                    (ThespeonDecoder)stepsObjects[1], 
                    (ThespeonVocoder)stepsObjects[2]
                );
            }
            // Debug.Log("TARGET FRAME TIME: "+targetFrameTime);
            Worker encoderWorker = preloadedSynthWorkers[(moduleId, "encoder")];

            ThespeonEncoder encoder = new ThespeonEncoder(encoderWorker, localPackageConfig.targetFrameTime.Value, localPackageConfig.useAdaptiveFrameBreakScheduling.Value, localPackageConfig.overshootMargin.Value);   

            

            Worker[] decoderWorkers = new Worker[3];
            decoderWorkers[0] = preloadedSynthWorkers[(moduleId, "decoder_preprocess")];
            decoderWorkers[1] = preloadedSynthWorkers[(moduleId, "decoder_chunked")];
            decoderWorkers[2] = preloadedSynthWorkers[(moduleId, "decoder_postprocess")];
            ThespeonDecoder decoder = new ThespeonDecoder(decoderWorkers, localPackageConfig.targetFrameTime.Value, localPackageConfig.useAdaptiveFrameBreakScheduling.Value, localPackageConfig.overshootMargin.Value); //Take true from localConfigs[synthRequestID].useAdaptiveScheduling

            Worker[] vocoderWorkers = new Worker[3];
            vocoderWorkers[0] = preloadedSynthWorkers[(moduleId, "vocoder_first_chunk")];
            vocoderWorkers[1] = preloadedSynthWorkers[(moduleId, "vocoder_middle_chunk")];
            vocoderWorkers[2] = preloadedSynthWorkers[(moduleId, "vocoder_last_chunk")];
            Config config = moduleIdToConfig[synthIDToUserInput[synthRequestID].moduleName];

            int resblock_dilations = config.resblock_dilation_sizes.Count;
            int kernel_sizes = config.resblock_kernel_sizes.Count;
            int upsamples = config.upsample_kernel_sizes.Count;

            int resblk_rest_count = 2 * resblock_dilations * kernel_sizes * 4;
            int add_rest_count = upsamples * (kernel_sizes - 1);
            int chunk_rests = 1 + 1 + 4 + resblk_rest_count + add_rest_count + 1 + 1;
            int copy_outs = 1 + 1 + 4 + resblk_rest_count + add_rest_count + 1 + 1;

            ThespeonVocoder vocoder = new ThespeonVocoder(vocoderWorkers, localPackageConfig.targetFrameTime.Value, localPackageConfig.useAdaptiveFrameBreakScheduling.Value, chunk_rests, localPackageConfig.overshootMargin.Value);   //OBS! Do we want clamp?
            InferenceStep[] models = new InferenceStep[3];
            models[0] = encoder;
            models[1] = decoder;
            models[2] = vocoder;
            if (runningModules.ContainsKey(moduleId))
            {
                runningModules[moduleId] = models;
            }
            else
            {
                runningModules.Add(moduleId, models);
            }
            return (encoder, decoder, vocoder);
        }
        /// <summary>
        /// Resamples a list of double values to a new size.
        /// - If newSize == originalValues.Count, returns the same data (unchanged).
        /// - If newSize > originalValues.Count, uses linear interpolation to upscale.
        /// - If newSize < originalValues.Count, uses box averaging to downsample.
        /// </summary>
        private static List<double> ResampleListAdvanced(List<double> originalValues, int newSize)
        {
            // Basic validation
            if (originalValues == null || originalValues.Count == 0 || newSize < 1)
            {
                // Return empty list for invalid parameters
                return new List<double>();
            }

            int oldSize = originalValues.Count;

            // If the size is the same, just return a copy (or the same reference if you're okay with that).
            if (newSize == oldSize)
            {
                return new List<double>(originalValues);
            }
            // If we're upscaling (newSize > oldSize), do linear interpolation
            else if (newSize > oldSize)
            {
                return UpsampleLinear(originalValues, newSize);
            }
            // If we're downscaling (newSize < oldSize), do box/area averaging
            else
            {
                return CubicSplineResampler.ResampleCubicSpline(originalValues, newSize);
            }
        }

        /// <summary>
        /// Upsamples a list to a larger size using linear interpolation.
        /// </summary>
        private static List<double> UpsampleLinear(List<double> originalValues, int newSize)
        {
            List<double> result = new List<double>(newSize);

            int oldSize = originalValues.Count;
            
            // If the new size is 1, just pick the first value or some fallback
            if (newSize == 1)
            {
                result.Add(originalValues[0]);
                return result;
            }

            // Linear interpolation formula:
            //   oldIndexF = i * (oldSize - 1) / (newSize - 1)
            for (int i = 0; i < newSize; i++)
            {
                double oldIndexF = (double) i * (oldSize - 1) / (newSize - 1);

                int i0 = (int)Math.Floor(oldIndexF);
                int i1 = Math.Min(i0 + 1, oldSize - 1);

                double t = oldIndexF - i0;

                double interpolatedValue = (1 - t) * originalValues[i0] + t * originalValues[i1];
                result.Add(interpolatedValue);
            }

            return result;
        }

        // populate user model input with defaultLanguage, defaultEmotion, speed, loudness if null
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
        }
            

#endregion


    }




    public static class CubicSplineResampler
    {
        // We will store the spline coefficients (a, b, c, d) for each interval [i..i+1]
        // so that S(x) = a[i] + b[i]*(x - i) + c[i]*(x - i)^2 + d[i]*(x - i)^3 for x in [i, i+1].
        private class SplineCoefficients
        {
            public double[] A;  // a[i]
            public double[] B;  // b[i]
            public double[] C;  // c[i]
            public double[] D;  // d[i]
            public int Count;   // number of intervals (n - 1)
        }

        /// <summary>
        /// Main method to resample a list of y-values to newSize using a Natural Cubic Spline.
        /// If newSize == originalValues.Count, returns a copy unmodified.
        /// </summary>
        public static List<double> ResampleCubicSpline(List<double> originalValues, int newSize)
        {
            if (originalValues == null || originalValues.Count == 0 || newSize < 1)
                return new List<double>(); // empty on invalid input

            int n = originalValues.Count;
            if (newSize == n)
                return new List<double>(originalValues);

            // 1) Compute the spline coefficients for the given set of y-values (x = i)
            var spline = ComputeNaturalCubicSpline(originalValues);

            // 2) Now sample that spline at newSize equally spaced positions from x=0 to x=(n-1).
            double xMax = n - 1;  // the last x index
            var result = new List<double>(newSize);

            // If newSize=1, we might just pick the midpoint or average, but let's pick x=0.
            if (newSize == 1)
            {
                double val = EvaluateSpline(spline, 0.0);
                result.Add(val);
                return result;
            }

            for (int i = 0; i < newSize; i++)
            {
                // We want i in [0..newSize-1] mapped to [0..xMax].
                double x = xMax * i / (newSize - 1);

                double y = EvaluateSpline(spline, x);
                result.Add(y);
            }

            return result;
        }

        /// <summary>
        /// Compute the natural cubic spline coefficients for the dataset y[0..n-1], x = 0..n-1.
        /// Returns the arrays a,b,c,d for intervals [i, i+1].
        /// </summary>
        private static SplineCoefficients ComputeNaturalCubicSpline(List<double> y)
        {
            int n = y.Count;
            int n1 = n - 1; // number of intervals

            // We'll follow a standard approach to solve for second derivatives, from which we get c[].
            double[] a = new double[n]; // We often use 'a' for y-values in some references
            double[] b = new double[n1];
            double[] c = new double[n];
            double[] d = new double[n1];

            // Here let's store y in 'a' for convenience (some references do that)
            for (int i = 0; i < n; i++)
                a[i] = y[i];

            // Step 1: alpha and l, mu, z arrays for the tridiagonal system
            double[] alpha = new double[n];
            double[] l = new double[n];
            double[] mu = new double[n];
            double[] z = new double[n];

            // Build the alpha array
            // alpha[i] = 3*(a[i+1]-a[i]) - 3*(a[i]-a[i-1]) but in simplified form for x spacing=1
            // Actually: alpha[i] = 3*(a[i+1]-2a[i]+a[i-1]) for x-spacing=1
            for (int i = 1; i < n1; i++)
            {
                alpha[i] = 3.0 * (a[i + 1] - 2.0 * a[i] + a[i - 1]);
            }

            // Natural spline boundary conditions => alpha[0] = alpha[n-1] = 0
            alpha[0] = 0.0;
            alpha[n - 1] = 0.0;

            // Step 2: Solve the tridiagonal system for c[i]
            // l[0], mu[0], z[0] initialization
            l[0] = 1.0;
            mu[0] = 0.0;
            z[0] = 0.0;

            for (int i = 1; i < n; i++)
            {
                l[i] = 4.0 - mu[i - 1];
                mu[i] = 1.0 / l[i];
                z[i] = (alpha[i] - z[i - 1]) / l[i];
            }

            // Step 3: Back-substitute for c[i]
            c[n - 1] = z[n - 1];
            for (int j = n - 2; j >= 0; j--)
            {
                c[j] = z[j] - mu[j] * c[j + 1];
            }

            // Step 4: Now compute b[i], d[i]
            for (int i = 0; i < n1; i++)
            {
                // spacing = 1, so
                b[i] = a[i + 1] - a[i] - (c[i + 1] + 2.0 * c[i]) / 3.0;
                d[i] = (c[i + 1] - c[i]) / 3.0;
            }

            // 'a' array is effectively the y[i], so we keep that as is
            // We'll store them in a SplineCoefficients object:
            return new SplineCoefficients
            {
                A = a,  // a[i] = y[i]
                B = b,
                C = c,
                D = d,
                Count = n1
            };
        }

        /// <summary>
        /// Evaluate the spline at a floating x, 0 <= x <= n-1, using the precomputed coefficients.
        /// </summary>
        private static double EvaluateSpline(SplineCoefficients spline, double x)
        {
            // Figure out which segment we're in. Segment i covers x in [i, i+1].
            int i = (int)Math.Floor(x);
            if (i < 0)
            {
                i = 0;
                x = 0.0;
            }
            else if (i >= spline.Count)
            {
                // if x is at or beyond the last segment, clamp to the last segment
                i = spline.Count - 1;
                x = spline.Count; // e.g. if n=4, then last segment is i=2, which covers x in [2..3].
            }

            double dx = x - i; // offset from the start of the segment
            double A = spline.A[i];
            double B = spline.B[i];
            double C = spline.C[i];
            double D = spline.D[i];

            // S(x) = a[i] + b[i] * dx + c[i] * dx^2 + d[i] * dx^3
            return  Math.Max(0.00001, A + B * dx + C * dx * dx + D * dx * dx * dx);
        }
    }

    
    
}




            
