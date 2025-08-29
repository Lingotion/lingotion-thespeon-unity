using System;
using Lingotion.Thespeon.CurvesToUI;
using UnityEngine;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.Inputs;
using System.Collections.Generic;
using System.Linq;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Core.IO;
using System.IO;
using System.Collections;
using Unity.InferenceEngine;


/// <summary>
/// UIManager is responsible for managing the UI elements in the Thespeon sample scene.
/// It handles dropdown selections for model, quality, language, and input files,
/// and updates the visualizer and text annotator accordingly.
/// It also manages the backend selection and preloads actors for the Thespeon engine.
/// </summary>
public class UIManager : MonoBehaviour
{
    private DropdownHandler modelSelectorHandler;
    private DropdownHandler qualitySelectorHandler;
    private DropdownHandler languageSelectorHandler;
    private DropdownHandler loadPredefinedInputHandler;
    private DropdownHandler BackendSelectorHandler;
    public VisualizerManager visualizerManager;
    public RLETextAnnotator textAnnotator;
    public CurvesToUI curvesToUI;
    private ThespeonInput currentInput;
    private NPCActor npc;
    private ThespeonEngine engine;
    private BackendType currentBackend;
    public float targetFrameTimeMs { get; set; } = 0.005f;

    void Start()
    {
        npc = GameObject.Find("NPC Object").GetComponent<NPCActor>();
        modelSelectorHandler = GameObject.Find("Model Selector").GetComponent<DropdownHandler>();
        qualitySelectorHandler = GameObject.Find("Quality Selector").GetComponent<DropdownHandler>();
        languageSelectorHandler = GameObject.Find("Language Selector").GetComponent<DropdownHandler>();
        loadPredefinedInputHandler = GameObject.Find("Input Selector").GetComponent<DropdownHandler>();
        BackendSelectorHandler = GameObject.Find("Backend Selector").GetComponent<DropdownHandler>();

        List<string> allActorNames = PackManifestHandler.Instance.GetAllActors();

        if (allActorNames.Count == 0)
        {
            LingotionLogger.Error("You have not imported any Actor Packs. See Window >> Lingotion >> Thespeon Info for details.");
            return;
        }
        modelSelectorHandler.SetOptions(allActorNames);

        loadPredefinedInputHandler.SetOptions(GetJsonFileList());

        modelSelectorHandler.DropdownValueChanged += OnModelSelected;

        qualitySelectorHandler.DropdownValueChanged += OnQualitySelected;

        languageSelectorHandler.DropdownValueChanged += OnLanguageSelected;

        loadPredefinedInputHandler.DropdownValueChanged += OnLoadInputSelected;

        BackendSelectorHandler.DropdownValueChanged += OnBackendSelected;

        List<BackendType> availableBackends = Enum.GetValues(typeof(BackendType)).Cast<BackendType>().Where(b => b != BackendType.GPUPixel).ToList();
        BackendSelectorHandler.SetOptions(availableBackends.Select(b => b.ToString()).ToList());
#if UNITY_ANDROID
        currentBackend = BackendType.CPU;
#else
        currentBackend = BackendType.CPU;
#endif

        curvesToUI.OnCurvesChanged += () =>
        {
            if (curvesToUI == null || string.IsNullOrEmpty(textAnnotator.GetPureText()))
                return;
            if(curvesToUI.speedCurve!=null)
            {
                AnimationCurve newSpeed = new();
                foreach(Keyframe kf in curvesToUI.speedCurve.keys)
                {
                    newSpeed.AddKey(kf);
                }
                currentInput.Speed=newSpeed;
            }
            if(curvesToUI.loudnessCurve!=null)
            {
                AnimationCurve newLoudness = new();
                foreach(Keyframe kf in curvesToUI.loudnessCurve.keys)
                {
                    newLoudness.AddKey(kf);
                }
                currentInput.Loudness=newLoudness;
            }
            visualizerManager.UpdateVisualizer(currentInput.ToJson());
        };
        textAnnotator.OnTextChangedAndCleaned += OnTextChangedAndCleaned;
        textAnnotator.OnChangeEmotion += (emotion) =>
        {
            if (currentInput == null) return;
            AnnotateSegments("Emotion", emotion);
            StartCoroutine(textAnnotator.DeferredDrawUnderlines(currentInput));
        };


        string firstActor = modelSelectorHandler.GetNonDefaultOptions().FirstOrDefault();
        ModuleType firstModuleType = PackManifestHandler.Instance.GetAllModuleTypesForActor(firstActor).FirstOrDefault();
        string text = textAnnotator.GetPureText();
        List<ModuleLanguage> supportedLanguages = PackManifestHandler.Instance.GetAllSupportedLanguages(firstActor, firstModuleType);
        if (supportedLanguages.Count == 0)
        {
            throw new FileNotFoundException($"No supported languages found for actor '{firstActor}' with module type '{firstModuleType}'. Please import a language pack for this actor.");
        }
        ModuleLanguage firstlang = supportedLanguages[0];
        LingotionLogger.Debug($"Start First actor: {firstActor}, ModuleType: {firstModuleType}, Language: {firstlang.Iso639_2}, {firstlang.Iso3166_1}");
        currentInput = new ThespeonInput(new List<ThespeonInputSegment> { new(text) }, firstActor, moduleType: firstModuleType, defaultLanguage: firstlang.Iso639_2, defaultDialect: firstlang.Iso3166_1, defaultEmotion: Emotion.Interest);
        LingotionLogger.Debug("Input created: " + currentInput.ToJson());
        visualizerManager.UpdateVisualizer(currentInput.ToJson());
        StartCoroutine(textAnnotator.DeferredDrawUnderlines(currentInput));
        StartCoroutine(CheckForImportChanges());



        engine = npc.GetComponent<ThespeonEngine>();
        UnityEngine.Profiling.Profiler.BeginSample("Thespeon Preloading Actors");
        foreach (string actor in modelSelectorHandler.GetNonDefaultOptions())
        {
            List<ModuleType> moduleTypes = PackManifestHandler.Instance.GetAllModuleTypesForActor(actor);
            foreach (ModuleType moduleType in moduleTypes)
            {
                if (moduleType == ModuleType.None) continue;
                engine.TryPreloadActor(actor, moduleType, new() { PreferredBackendType = currentBackend });
            }
        }
        UnityEngine.Profiling.Profiler.EndSample();
    }
    private IEnumerator CheckForImportChanges()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            List<string> actorOptions = PackManifestHandler.Instance.GetAllActors();
            if (!actorOptions.SequenceEqual(modelSelectorHandler.GetNonDefaultOptions()))
            {
                modelSelectorHandler.SetOptions(actorOptions);
            }
        }
    }

    private void OnDestroy()
    {
        loadPredefinedInputHandler.DropdownValueChanged -= OnLoadInputSelected;
        BackendSelectorHandler.DropdownValueChanged -= OnBackendSelected;
    }
    private ThespeonInput OnTextChangedAndCleaned(string visualText)
    {
        List<ThespeonInputSegment> updatedSegments = new();
        if (string.IsNullOrEmpty(visualText))
        {

            updatedSegments.Add(new ThespeonInputSegment("\u00D8"));
        }
        else
        {
            List<string> newTextSegments = visualText.Split('|').ToList();

            int pipeDelta = newTextSegments.Count - currentInput.Segments.Count;

            if (pipeDelta == 0)
            {
                for (int i = 0; i < newTextSegments.Count; i++)
                {
                    string newSegmentText = newTextSegments[i];
                    if (string.IsNullOrWhiteSpace(newSegmentText)) continue;
                    ThespeonInputSegment newSegment = new(currentInput.Segments[i]);
                    newSegment.Text = newSegmentText;
                    RemoveDefaultKeys(ref newSegment);
                    updatedSegments.Add(newSegment);
                }
            }
            else
            {
                int j = 0;
                bool pipeDiffFound = false;
                if (pipeDelta > 0)
                {
                    for (int i = 0; i < newTextSegments.Count; i++)
                    {
                        string currentOldSegmentText = currentInput.Segments[j].Text;
                        string newText = newTextSegments[i];
                        if (pipeDiffFound || currentOldSegmentText != newText)
                        {
                            ThespeonInputSegment newSegment = new(currentInput.Segments[j])
                            {
                                Text = newText
                            };
                            RemoveDefaultKeys(ref newSegment);
                            updatedSegments.Add(newSegment);
                            if (pipeDiffFound)
                            {
                                j++;
                                pipeDiffFound = false;
                            }
                            else
                            {
                                pipeDiffFound = pipeDelta == 0;
                                pipeDelta--;
                            }
                        }
                        else
                        {
                            updatedSegments.Add(new(currentInput.Segments[j]));
                            j++;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < currentInput.Segments.Count; i++)
                    {
                        string currentOldSegmentText = currentInput.Segments[i].Text;
                        if (j >= newTextSegments.Count) break;
                        string newText = newTextSegments[j];
                        if (currentOldSegmentText != newText)
                        {
                            ThespeonInputSegment newSegment = new(currentInput.Segments[i])
                            {
                                Text = newText
                            };
                            RemoveDefaultKeys(ref newSegment);
                            updatedSegments.Add(newSegment);
                            i += Math.Abs(pipeDelta);
                            j++;
                        }
                        else
                        {
                            updatedSegments.Add(currentInput.Segments[i]);
                            j++;
                        }
                    }
                }
            }
        }

        MergeEqualSegments(ref updatedSegments);

        textAnnotator.SetTextContent(string.Join("|", updatedSegments.Select(seg => seg.Text)));
        currentInput.Segments = updatedSegments;
        visualizerManager.UpdateVisualizer(currentInput.ToJson());
        return currentInput;
    }

    private void AnnotateSegments(string key, object value, (int, int) manualSegmentRange = default)
    {
        if (currentInput == null) return;
        (int startSegment, int endSegment) = manualSegmentRange == default ? textAnnotator.GetSelectedSegments() : manualSegmentRange;
        List<ModuleLanguage> candidateLanguages = PackManifestHandler.Instance.GetAllSupportedLanguages(currentInput.ActorName, currentInput.ModuleType);
        if (startSegment < 0 || endSegment >= currentInput.Segments.Count || startSegment > endSegment)
        {
            if (currentInput.Segments.Count == 1 && currentInput.Segments[0].Text == "\u00D8")
            {
                if (key == "Emotion")
                {
                    currentInput.DefaultEmotion = (Emotion)value;
                    currentInput.Segments[0].Emotion = Emotion.None;
                }
                else if (key == "Language")
                {
                    string[] langDialect = (value as string).Split('|');
                    string language = langDialect[0];
                    string dialect = langDialect.Length > 1 ? langDialect[1] : null;
                    currentInput.DefaultLanguage = ModuleLanguage.BestMatch(candidateLanguages, language, dialect);
                    currentInput.Segments[0].Language = null;
                }
                else if (key == "IsCustomPronounced")
                {
                    currentInput.Segments[0].IsCustomPronounced = !currentInput.Segments[0].IsCustomPronounced;
                }
                visualizerManager.UpdateVisualizer(currentInput.ToJson());
                StartCoroutine(textAnnotator.DeferredDrawUnderlines(currentInput));
                return;
            }
            else
            {
                LingotionLogger.Debug("Invalid segment range for annotation: " + startSegment + " - " + endSegment + " with unknown reason\n " +
                    "Current segments count: " + currentInput.Segments.Count + "\n" +
                    "Current segments: " + string.Join("|", currentInput.Segments.Select(seg => seg.Text)));
            }
            return;
        }
        (int startSelection, int endSelection) = textAnnotator.GetSelectionExpandedToWords();
        int currentRunLength = currentInput.Segments.Take(startSegment).Sum(seg => seg.Text.Length);
        if (currentRunLength > startSelection) LingotionLogger.Debug($"Redundancy check: Visual run length {currentRunLength} exceeds start selection {startSelection}.");
        bool isSingleSegment = startSegment == endSegment;
        bool segmentSplittingDone = false;
        byte firstSplitAdjustment = 0;
        for (int i = startSegment; i <= endSegment; i++)
        {
            ThespeonInputSegment segment = currentInput.Segments[i];
            if (i == startSegment)
            {
                string preceedingSegmentText = segment.Text[..(startSelection - currentRunLength)];
                int newSegmentEnd = isSingleSegment ? endSelection - currentRunLength : segment.Text.Length;
                LingotionLogger.Debug($"Splitting segment {i} at {startSelection - currentRunLength} to {newSegmentEnd} ({isSingleSegment}). segment text: '{segment.Text}' lengt: {segment.Text.Length}");
                string newSegmentText = segment.Text[(startSelection - currentRunLength)..newSegmentEnd];
                if (!string.IsNullOrEmpty(preceedingSegmentText) && !string.IsNullOrEmpty(newSegmentText))
                {

                    ThespeonInputSegment newSegment = new(segment);
                    newSegment.Text = newSegmentText;
                    endSegment++;

                    currentInput.Segments.Insert(i + 1, newSegment);
                    i++;
                    firstSplitAdjustment = 1;
                    if (isSingleSegment && !string.IsNullOrEmpty(segment.Text[newSegmentEnd..]))
                    {
                        ThespeonInputSegment endSegmentPart = new(segment);
                        endSegmentPart.Text = segment.Text[newSegmentEnd..];
                        currentInput.Segments.Insert(i + 1, endSegmentPart);
                        segmentSplittingDone = true;
                    }
                    else
                    {
                    }
                    segment.Text = preceedingSegmentText;
                    currentRunLength += segment.Text.Length;
                    segment = currentInput.Segments[i];
                }
            }
            else if (i == endSegment && !segmentSplittingDone && string.IsNullOrEmpty(segment.Text[(endSelection - currentRunLength)..]) && string.IsNullOrEmpty(segment.Text[..(endSelection - currentRunLength)]))
            {
                ThespeonInputSegment endSegmentPart = new(segment)
                {
                    Text = segment.Text[(endSelection - currentRunLength)..]
                };
                segment.Text = segment.Text[..(endSelection - currentRunLength)];
                currentInput.Segments.Insert(i + 1, endSegmentPart);
                segmentSplittingDone = true;
            }
            if (key == "Emotion")
            {
                Emotion emotion = (Emotion)value;
                if (i == 0)
                {
                    for (int k = 0; k < currentInput.Segments.Count; k++)
                    {
                        if (k < startSegment + firstSplitAdjustment || k > endSegment)
                        {
                            currentInput.Segments[k].Emotion = currentInput.Segments[k].Emotion == Emotion.None ? currentInput.DefaultEmotion : currentInput.Segments[k].Emotion;
                        }
                        else
                        {
                            currentInput.Segments[k].Emotion = Emotion.None;
                        }
                    }
                    currentInput.DefaultEmotion = emotion;
                }
                else
                {
                    segment.Emotion = emotion != currentInput.DefaultEmotion ? emotion : Emotion.None;
                }
            }
            else if (key == "Language")
            {
                string[] langDialect = (value as string).Split('|');
                string language = langDialect[0];
                string dialect = langDialect.Length > 1 ? langDialect[1] : null;
                if (string.IsNullOrEmpty(language))
                {
                    LingotionLogger.Error("Language value cannot be null or empty");
                    return;
                }
                ModuleLanguage currentLanguage = ModuleLanguage.BestMatch(candidateLanguages, language, dialect);
                if (i == 0)
                {
                    for (int k = 0; k < currentInput.Segments.Count; k++)
                    {
                        if (k < startSegment + firstSplitAdjustment || k > endSegment)
                        {
                            currentInput.Segments[k].Language = currentInput.Segments[k].Language ?? currentInput.DefaultLanguage;
                        }
                        else
                        {
                            currentInput.Segments[k].Language = null;
                        }
                    }
                    currentInput.DefaultLanguage = currentLanguage;
                }
                else
                {
                    segment.Language = !currentLanguage.Equals(currentInput.DefaultLanguage) ? currentLanguage : null;
                }
            }
            else if (key == "IsCustomPronounced")
            {
                segment.IsCustomPronounced = !segment.IsCustomPronounced;
            }
            else
            {
                LingotionLogger.Error($"Unknown key for annotation: {key}");
                return;
            }
            RemoveDefaultKeys(ref segment);

            currentRunLength += segment.Text.Length;
        }
        MergeEqualSegments(ref currentInput.Segments);
        visualizerManager.UpdateVisualizer(currentInput.ToJson());
        textAnnotator.SetTextContent(string.Join("|", currentInput.Segments.Select(seg => seg.Text)), firstSplitAdjustment);
        StartCoroutine(textAnnotator.DeferredDrawUnderlines(currentInput));

    }
    private void RemoveDefaultKeys(ref ThespeonInputSegment segment)
    {
        Emotion defaultEmotion = currentInput.DefaultEmotion;
        ModuleLanguage defaultLanguage = currentInput.DefaultLanguage;
        if (segment.Emotion != Emotion.None && segment.Emotion == defaultEmotion)
        {
            segment.Emotion = Emotion.None;
        }
        if (segment.Language != null && segment.Language.Equals(defaultLanguage))
        {
            segment.Language = null;
        }
    }

    /// <summary>
    /// Merges segments in place that are equal (not considering text differances) or contain only word delimiters.
    /// Used to clean up segments after text changes or annotations.
    /// </summary>
    /// <param name="segments">The list of segments to merge.</param>
    private void MergeEqualSegments(ref List<ThespeonInputSegment> segments)
    {
        for (int i = segments.Count - 1; i > 0; i--)
        {
            string currentText = segments[i].Text;
            string prevText = segments[i - 1].Text;
            bool areEqual = segments[i].EqualsIgnoringText(segments[i - 1]);

            if (areEqual || currentText.All(c => textAnnotator.IsWordDelimiter(c)))
            {
                segments[i - 1].Text += currentText;
                segments.RemoveAt(i);
            }
        }
    }

    public void Synthesize()
    {
        InferenceConfigOverride config = new() { TargetBudgetTime = targetFrameTimeMs, PreferredBackendType = currentBackend };
        engine.Synthesize(currentInput, "MySession", config);
    }

    public void OnInsertIPAButton()
    {
        if (currentInput == null || currentInput.Segments.Count == 0)
        {
            LingotionLogger.Error("No input segments available to insert IPA.");
            return;
        }

        AnnotateSegments("IsCustomPronounced", null);
    }

    #region Dropdown Listeners
    private void OnModelSelected(string selectedModel)
    {
        currentInput.ActorName = selectedModel;

        List<ModuleType> moduleTypes = PackManifestHandler.Instance.GetAllModuleTypesForActor(selectedModel);
        qualitySelectorHandler.SetOptions(moduleTypes.Select(m => m.ToString()).ToList());
        if (!moduleTypes.Contains(currentInput.ModuleType))
        {
            currentInput.ModuleType = moduleTypes.FirstOrDefault();
        }
        List<ModuleLanguage> supportedLangauges = PackManifestHandler.Instance.GetAllSupportedLanguages(currentInput.ActorName, currentInput.ModuleType);
        languageSelectorHandler.SetOptions(MakeLanguageOptions(supportedLangauges));
        if (!supportedLangauges.Any(lang => lang.Equals(currentInput.DefaultLanguage)))
        {
            List<ModuleLanguage> firstCandidates = supportedLangauges.Where(l => l.Iso639_2.Equals("eng", StringComparison.OrdinalIgnoreCase)).ToList() ?? new() { supportedLangauges[0] };
            if (firstCandidates != null && firstCandidates.Count > 0)
            {
                OnLanguageSelected($"{firstCandidates[0].Iso639_2} ({firstCandidates[0].Iso3166_1})");
            }
        }
        visualizerManager.UpdateVisualizer(currentInput.ToJson());
    }

    private void OnQualitySelected(string selectedQuality)
    {
        currentInput.ModuleType = Enum.Parse<ModuleType>(selectedQuality);
        List<ModuleLanguage> supportedLangauges = PackManifestHandler.Instance.GetAllSupportedLanguages(currentInput.ActorName, currentInput.ModuleType);

        if (!supportedLangauges.Any(lang => lang.Equals(currentInput.DefaultLanguage)))
        {
            List<ModuleLanguage> firstCandidates = supportedLangauges.Where(l => l.Iso639_2.Equals("eng", StringComparison.OrdinalIgnoreCase)).ToList() ?? new() { supportedLangauges[0] };
            if (firstCandidates != null && firstCandidates.Count > 0)
            {
                OnLanguageSelected($"{firstCandidates[0].Iso639_2} ({firstCandidates[0].Iso3166_1})");
            }
        }

        visualizerManager.UpdateVisualizer(currentInput.ToJson());
    }

    private List<string> MakeLanguageOptions(List<ModuleLanguage> languagesForActor)
    {
        return languagesForActor.Select(lang => $"{lang.Iso639_2} ({lang.Iso3166_1})").ToList();
    }

    private void OnLanguageSelected(string selectedLanguage)
    {
        string iso639_2 = selectedLanguage.Split(' ')[0];
        string iso3166_1 = selectedLanguage.Split(' ')[1].Trim('(', ')');
        AnnotateSegments("Language", $"{iso639_2}|{iso3166_1}");
    }

    private void OnLoadInputSelected(string filename)
    {
        filename = Path.Combine(RuntimeFileLoader.RuntimeFiles, "ModelInputSamples", filename);
        currentInput = ThespeonInput.ParseFromJson(filename);
        try
        {
            if (currentInput.Speed != null) curvesToUI.SetAnimationCurve(currentInput.Speed, "speed");
            else
            {
                curvesToUI.SetAnimationCurve(new AnimationCurve(new Keyframe(0, 1)), "speed");
            }
            if (currentInput.Loudness != null) curvesToUI.SetAnimationCurve(currentInput.Loudness, "loudness");
            else
            {
                curvesToUI.SetAnimationCurve(new AnimationCurve(new Keyframe(0, 1)), "loudness");
            }

            string actorName = modelSelectorHandler.GetSelectedOption();
            if (string.IsNullOrEmpty(actorName))
            {
                actorName = modelSelectorHandler.GetNonDefaultOptions().FirstOrDefault();

            }
            currentInput.ActorName = actorName;


            string typeTag = qualitySelectorHandler.GetSelectedOption();

            if (string.IsNullOrEmpty(typeTag))
            {
                typeTag = PackManifestHandler.Instance.GetAllModuleTypesForActor(actorName).FirstOrDefault().ToString();
            }
            currentInput.ModuleType = Enum.Parse<ModuleType>(typeTag);

            string selectedLang = languageSelectorHandler.GetSelectedOption();
            List<string> languageOptions = MakeLanguageOptions(
                PackManifestHandler.Instance.GetAllSupportedLanguages(actorName, currentInput.ModuleType));
            if (!languageOptions.Contains(selectedLang)) selectedLang = languageOptions.FirstOrDefault();
            string iso639_2 = selectedLang.Split(' ')[0];
            string iso3166_1 = selectedLang.Split(' ')[1].Trim('(', ')');
            if (currentInput.DefaultLanguage == null || !languageOptions.Select(l => l.Split(' ')[0]).Contains(currentInput.DefaultLanguage.Iso639_2))
            {
                AnnotateSegments("Language", $"{iso639_2}|{iso3166_1}", (0, 0));
            }
            foreach (ThespeonInputSegment segment in currentInput.Segments)
            {
                if (segment.Language == null) continue;
                if (!languageOptions.Select(l => l.Split(' ')[0]).Contains(segment.Language.Iso639_2))
                {
                    segment.Language = null;
                }
            }
            visualizerManager.UpdateVisualizer(currentInput.ToJson());
            textAnnotator.SetTextContent(string.Join("|", currentInput.Segments.Select(seg => seg.Text)));
            StartCoroutine(textAnnotator.DeferredDrawUnderlines(currentInput));
        }
        catch (Exception ex)
        {
            LingotionLogger.Error("Error reading file: " + filename + "Error: " + ex.Message);
        }
    }
    private void OnBackendSelected(string selectedBackend)
    {
        if (!Enum.TryParse(selectedBackend, out BackendType backend))
        {
            LingotionLogger.Error($"Failed to parse backend type: {selectedBackend}");
            return;
        }
        if (currentBackend == backend)
        {
            LingotionLogger.Debug($"Backend {backend} is already selected. No changes made.");
            return;
        }
        currentBackend = backend;
        engine.TryUnloadAll();
        LingotionLogger.Debug($"Backend changed to {currentBackend}. Preloading actors for the new backend.");
        UnityEngine.Profiling.Profiler.BeginSample("Thespeon Reloading Actors");
        foreach (string actor in modelSelectorHandler.GetNonDefaultOptions())
        {
            List<ModuleType> moduleTypes = PackManifestHandler.Instance.GetAllModuleTypesForActor(actor);
            foreach (ModuleType moduleType in moduleTypes)
            {
                if (moduleType == ModuleType.None) continue;
                engine.TryPreloadActor(actor, moduleType, new() { PreferredBackendType = currentBackend });
            }
        }
        UnityEngine.Profiling.Profiler.EndSample();
    }

    #endregion

    private static List<string> GetJsonFileList()
    {
        string manifestPath = Path.Combine(RuntimeFileLoader.RuntimeFiles, "ModelInputSamples", "lingotion_model_input.manifest");
        if (string.IsNullOrEmpty(manifestPath))
        {
            LingotionLogger.Error($"Manifest file not found at: {manifestPath}");
            return new List<string>();
        }

        try
        {
            string manifestContent = RuntimeFileLoader.LoadFileAsString(manifestPath);
            List<string> result = new List<string>();
            string[] lines = manifestContent.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                result.Add(line.Trim());
            }
            return result;
        }
        catch (Exception ex)
        {
            LingotionLogger.Error($"Failed to read manifest file: {ex.Message}");
            return new List<string>();
        }
    }
}
