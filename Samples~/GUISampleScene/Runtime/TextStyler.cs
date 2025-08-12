using TMPro;
using UnityEngine;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Collections;
using Lingotion.Thespeon.API;
using Lingotion.Thespeon.Utils;
using Newtonsoft.Json;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine.Profiling; 
using Lingotion.Thespeon.ModelInputFileLoader;
using Lingotion.Thespeon.FileLoader;
using System.IO;
using Lingotion.Thespeon.CurvesToUI;


public class TextStyler : MonoBehaviour
{
    //TMP_InputField is bugged in Unity where a replace edit (i.e.S selecting text and typing a letter instead of backspace between) will not update LineInfo.
    //The text will be 1 character and inputField.characterCount will correctly be 1 but LineInfo will contain info for the old text. Maybe the same goes for TextInfo, I haven't checked.
    public TMP_InputField inputField;               
    public RectTransform underlinePrefab;
    public TMP_Text jsonVisualizer;
    private List<UserSegment> segments;
    private UserModelInput modelInput;
    private List<RectTransform> activeUnderlines = new List<RectTransform>();
    private DropdownHandler modelSelectorHandler;
    private DropdownHandler qualitySelectorHandler;
    private DropdownHandler languageSelectorHandler;
    private DropdownHandler LoadPredefinedInputHandler;

    private Dictionary<string, Language> languageOptionValues;

    private CurvesToUI curvesToUI;
    private UnityEngine.Events.UnityAction<string> onTextChangedListener;





    public static TextStyler Instance { get; private set; }

    #region Inits
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    private void Start()
    {
        onTextChangedListener = (text) => { StartCoroutine(OnTextChanged(text)); };

        curvesToUI = GetComponent<CurvesToUI>();
        if (curvesToUI == null)
        {
            Debug.LogError("CurvesToUI component not found on the same GameObject as TextStyler.");
        }
        curvesToUI.OnCurvesChanged += () => UpdateJsonVisualizer();

        InitializeJsonStructure();

        inputField.onValueChanged.AddListener(onTextChangedListener);

        inputField.Select();  
        inputField.selectionStringAnchorPosition = 0; 
        inputField.selectionStringFocusPosition = inputField.text.Length;
        StartCoroutine(DeferredDrawUnderlines());
        List<(string, ActorTags)> availableActors=ThespeonAPI.GetActorsAvailabeOnDisk();

        modelSelectorHandler = GameObject.Find("Model Selector").GetComponent<DropdownHandler>();

        qualitySelectorHandler = GameObject.Find("Quality Selector").GetComponent<DropdownHandler>();

        languageSelectorHandler = GameObject.Find("Language Selector").GetComponent<DropdownHandler>();

        LoadPredefinedInputHandler = GameObject.Find("Input Selector").GetComponent<DropdownHandler>();

        if(availableActors.Count!=0)
        {
            string optionText = availableActors[0].Item1;
            modelInput.actorUsername = optionText;
        
            Dictionary<string, List<ActorPack>> registeredActorPacks = ThespeonAPI.GetRegisteredActorPacks();
            ActorPackModule actorPackModule = registeredActorPacks
                .SelectMany(pack => pack.Value)
                .SelectMany(actorPack => actorPack.modules)
                .FirstOrDefault(module => module.actor_options.actors
                    .Any(actor => actor.username == optionText));

            string moduleName = actorPackModule.name;
            modelInput.moduleName = moduleName;
            Language initLang = actorPackModule.GetActorLanguages(actorPackModule.GetActor(optionText))[0];
            if(initLang.languageKey != null) initLang.languageKey=null;
            modelInput.defaultLanguage = initLang;
            UpdateJsonVisualizer();
        } else{
            Debug.LogWarning("No actors have been registered.");
        }
        LoadPredefinedInputHandler.SetOptions(ModelInputFileLoader.GetJsonFileList().ToList());

        StartCoroutine(CheckForRegistrationChanges());

    }
    private IEnumerator CheckForRegistrationChanges()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f); // Check every second (adjust as needed)
            List<string> registeredUsernames = ThespeonAPI.GetRegisteredActorPacks().Select(item => item.Key).ToList();
            if (!registeredUsernames.SequenceEqual(modelSelectorHandler.GetNonDefaultOptions()))
            {
                modelSelectorHandler.SetOptions(registeredUsernames);
            }
        }
    }
    private void InitializeJsonStructure()
    {
        modelInput = new UserModelInput() {
                moduleName = "",
                actorUsername = "",
                defaultLanguage = new Language(),
                defaultEmotion = Emotions.Interest.ToString(),
                segments = new List<UserSegment>()
            };
        segments = modelInput.segments;
        UpdateSegmentsFromText();
        SampleCurves();

    }
    #endregion
    #region JSON and Segment Updating
    private void UpdateJsonVisualizer()
    {
        SampleCurves();
        jsonVisualizer.text = FormatJson(modelInput);
    }
    private void UpdateSegmentsFromText(){
        inputField.textComponent.ForceMeshUpdate();
        string visualText = inputField.text;
        List<string> newTextSegments = visualText.Split('|').ToList();
        List<UserSegment> updatedSegments = new List<UserSegment>();
        if(segments.Count == 0){
            if(newTextSegments.Count != 1) Debug.LogError("Assertion failed in Initialization Update");
            updatedSegments.Add(new UserSegment(newTextSegments[0]));
        }
        else
        {
            int pipeDelta = newTextSegments.Count - segments.Count;
            // Pipe manually inserted or removed by user 
            if(pipeDelta != 0) 
            {
                int j=0;
                bool pipeDiffFound = false;
                if(pipeDelta > 0)   //segments added
                {
                    for(int i = 0; i < newTextSegments.Count; i++)
                    {
                        string currentOldSegmentText = segments[j].text;

                        if(pipeDiffFound)   //mark as ready to move to next segment. Means we are at the leftover of the old segment
                        {
                            string newText = newTextSegments[i];
                            if (string.IsNullOrWhiteSpace(segments[j].text)) segments[j].text=" ";
                            UserSegment newSegment = new UserSegment(segments[j]);
                            newSegment.text = newText;
                            RemoveDefaultKeys(ref newSegment);
                            updatedSegments.Add(newSegment);
                            j++;
                            pipeDiffFound=false;
                        }
                        else if(currentOldSegmentText != newTextSegments[i])    //Found segment where something was inserted.
                        {
                            string newText = newTextSegments[i];
                            if (string.IsNullOrWhiteSpace(segments[j].text)) segments[j].text=" ";
                            UserSegment newSegment = new UserSegment(segments[j]);
                            newSegment.text = newText;
                            RemoveDefaultKeys(ref newSegment);
                            updatedSegments.Add(newSegment);
                            pipeDiffFound=pipeDelta == 0;
                            pipeDelta--;
                        }
                        else
                        {
                            updatedSegments.Add(segments[j]);
                            j++;
                        }
                    }
                } else {  //segments removed
                    for(int i = 0; i < segments.Count; i++)
                    {
                        string currentOldSegmentText = segments[i].text;
                        if(j >= newTextSegments.Count) break; // Last segment was removed.
                        if(currentOldSegmentText != newTextSegments[j])     //found segments where removal happened
                        {
                            if (string.IsNullOrWhiteSpace(segments[j].text)) segments[j].text=" ";
                            UserSegment newSegment = new UserSegment(segments[i]);
                            newSegment.text = newTextSegments[j];
                            RemoveDefaultKeys(ref newSegment);
                            updatedSegments.Add(newSegment);
                            i+=Math.Abs(pipeDelta);
                            j++;
                        }
                        else
                        {
                            updatedSegments.Add(segments[i]);
                            j++;
                        }
                        
                    }
                }
            }
            else{
                for(int i = 0; i < newTextSegments.Count; i++)
                {
                    string segmentText = newTextSegments[i];
                    if (string.IsNullOrWhiteSpace(segmentText)) continue;
                    if (string.IsNullOrWhiteSpace(segments[i].text)) segments[i].text=" ";
                    UserSegment newSegment = new UserSegment(segments[i]);
                    newSegment.text = segmentText;
                    RemoveDefaultKeys(ref newSegment);
                    updatedSegments.Add(newSegment);
                }

            }
        }
        for (int i = updatedSegments.Count - 1; i > 0; i--)
        {
            string currentText = updatedSegments[i].text;
            string prevText = updatedSegments[i - 1].text.ToString();

            bool areEqual = updatedSegments[i].EqualsIgnoringText(updatedSegments[i - 1]);

            if (string.IsNullOrWhiteSpace(currentText) || areEqual || currentText.All(c => IsWordDelimiter(c)))
            {
                updatedSegments[i - 1].text += currentText;
                updatedSegments.RemoveAt(i);
            }
        }        
        //turn of OnTextChanged Listener, set text and reenable it.
        inputField.onValueChanged.RemoveListener(onTextChangedListener);
        inputField.text = string.Join("|", updatedSegments.Select(seg => seg.text));
        inputField.onValueChanged.AddListener(onTextChangedListener);
        segments = updatedSegments;
        modelInput.segments = segments;
        UpdateJsonVisualizer();
    }
    
    private void AnnotateSegments(Dictionary<string, object> newKeys)
    {
        if (inputField == null || string.IsNullOrEmpty(inputField.text)) return;

        string text = inputField.text;
        string pureText = GetPureText();

        int selectionStart = inputField.selectionAnchorPosition;
        int selectionEnd = inputField.selectionFocusPosition;

        if (selectionStart == selectionEnd) return; // No selection

        if (selectionStart > selectionEnd)
            (selectionStart, selectionEnd) = (selectionEnd, selectionStart);

        int pureSelectionStart = ConvertVisualToPureIndex(selectionStart);
        int pureSelectionEnd = ConvertVisualToPureIndex(selectionEnd);

        (int expandedStart, int expandedEnd) = ExpandToWordBoundaries(pureText, pureSelectionStart, pureSelectionEnd);
  
        if(expandedStart == 0 && newKeys.ContainsKey("emotion")){       //Change default emotion if annotating the start of the text.
            for(int i = 0; i < segments.Count; i++)
            {
                if(segments[i].emotion==null)
                {
                    segments[i].emotion = modelInput.defaultEmotion;
                }
            }
            modelInput.defaultEmotion = newKeys["emotion"].ToString(); 
        } 
        
        int visualInsertStart = ConvertPureToVisualIndex(expandedStart);
        int visualInsertEnd = ConvertPureToVisualIndex(expandedEnd);
        bool startIsBreak = text[visualInsertStart]=='|' || expandedStart == 0;
        bool endIsBreak = expandedEnd == pureText.Length || text[visualInsertEnd]=='|';
        text = text.Insert(visualInsertStart, "|");
        text = text.Insert(visualInsertEnd + 1, "|");
        List<int> segmentBreaks = new List<int>() {0};
        for (int i = 0; i < text.Length; i++)
            if (text[i] == '|') segmentBreaks.Add(i);
        segmentBreaks.Add(text.Length);
        int insertStartBreakIndex = segmentBreaks.IndexOf(visualInsertStart) + (startIsBreak ? 1 : 0);
        int insertEndBreakIndex = segmentBreaks.IndexOf(visualInsertEnd+1);

        if(insertStartBreakIndex == -1 + (startIsBreak ? 1 : 0) || insertEndBreakIndex == -1) Debug.LogError("Failed to find segment break indices.");
        
        List<UserSegment> newSegments = new List<UserSegment>();

        List<string> newTextSegments = text.Split('|').ToList();

        if(newTextSegments.Count != segmentBreaks.Count-1) Debug.LogError("Texts and segmentBreaks are out of sync.");

        bool inSelection=false;
        int j=0;
        for(int i = 0; i < newTextSegments.Count; i++)
        {
            string segmentText = newTextSegments[i];
            int segmentStart = segmentBreaks[i];
            int segmentEnd = segmentBreaks[i + 1];

            if(i == insertStartBreakIndex)
            {
                inSelection=true;     
                j--;           
            }
            if(i == insertEndBreakIndex)
            {
                j--;
                inSelection=false;
            }
            if(inSelection)
            {
                UserSegment newSegment = new UserSegment(segments[j]);
                foreach (var key in newKeys)

                {
                    if(key.Key == "emotion")
                    {
                        newSegment.emotion = key.Value.ToString();
                    }
                    else if(key.Key == "language")
                    {
                        newSegment.languageObj = (Language)key.Value;
                    }
                    else
                    {
                        newSegment[key.Key] = key.Value;        //string indexing is ok. Only to be used here though.
                    }
                }
                newSegment.text = segmentText;
                RemoveDefaultKeys(ref newSegment);
                newSegments.Add(newSegment);
            } else
            {
                if(j == segments.Count) Debug.LogError($"j: {j} segments.Count: {segments.Count}, segment text: {segmentText}");
                UserSegment newSegment = new UserSegment (segments[j]);
                newSegment.text = segmentText;
                RemoveDefaultKeys(ref newSegment);
                newSegments.Add(newSegment);
            }
            j++;
        }


        for (int i = newSegments.Count - 1; i > 0; i--)//merge whitespace and empty segments
        {
            string currentText = newSegments[i].text;
            if (string.IsNullOrWhiteSpace(currentText))
            {
                newSegments[i - 1].text += currentText;
                newSegments.RemoveAt(i);
            }
        }
        if(string.IsNullOrWhiteSpace(newSegments[0].text))
        {
            newSegments.RemoveAt(0);
        }
        for (int i = newSegments.Count - 1; i > 0; i--)//merge segments with same keys
        {
            string currentText = newSegments[i].text;
            string prevText = newSegments[i - 1].text;

            bool areEqual = newSegments[i].EqualsIgnoringText(newSegments[i - 1]);
            if (areEqual || currentText.All(c => IsWordDelimiter(c)))
            {
                newSegments[i - 1].text += currentText;
                newSegments.RemoveAt(i);
            }
        }

        // Apply changes
        segments = newSegments;
        modelInput.segments=segments;
        inputField.text = string.Join("|", segments.Select(seg => seg.text));
        UpdateJsonVisualizer();
        StartCoroutine(DeferredDrawUnderlines());
    }

    private void SampleCurves(){
        if (curvesToUI == null || string.IsNullOrEmpty(inputField.text))
            return;

        int charCount = GetPureText().Length;
        List<double> speedSamples = new List<double>();
        List<double> loudnessSamples = new List<double>();

        // Sample the curves evenly based on the number of characters
        for (int i = 0; i < charCount; i++)
        {
            float t = charCount!=1 ? i / (float)(charCount - 1):0.5f; // Normalized time (0 to 1)
            double speedValue = curvesToUI.speedCurve.Evaluate(t); 
            if(0f <= speedValue && speedValue < 1f) 
            {
                speedValue=speedValue*0.5f+0.5f;
            }
            double loudnessValue = curvesToUI.loudnessCurve.Evaluate(t);

            if(0f <= loudnessValue && loudnessValue < 1f) 
            {
                loudnessValue=loudnessValue*0.5f+0.5f;
            }
            speedSamples.Add(Math.Round(Math.Clamp(speedValue, 0.1f, 2f) * 100d) / 100f);
            loudnessSamples.Add(Math.Round(Math.Clamp(loudnessValue, 0f, 2f)* 100d) / 100f);
        }
        modelInput.speed = speedSamples;
        modelInput.loudness = loudnessSamples;
    }
    #endregion
    #region Underline Drawing
    private IEnumerator DeferredDrawUnderlines()
    {
        yield return null; // Wait for the next frame to allow TMP to update textInfo
        inputField.textComponent.ForceMeshUpdate(); // Ensure the mesh is updated
        DrawUnderlines();
    }
    private void CleanText()
    {
        // Get the current text from the input field
        string originalText = inputField.text;
        int originalCaretPosition = inputField.caretPosition;
        // Remove double delimiters (e.g., "..", ",,", "!!")
        string cleanedText = System.Text.RegularExpressions.Regex.Replace(originalText, @"([.,!?])\1+", "$1");

        // Replace multiple spaces, tabs or newlines with a single space
        int lengthBefore = cleanedText.Length;
        cleanedText = System.Text.RegularExpressions.Regex.Replace(cleanedText, @"[\s\u2028\u2029]+", " ");
        int spaceDiff= lengthBefore - cleanedText.Length;
        /// Remove any leading or trailing | characters
        cleanedText = cleanedText.Trim('|');

        // Calculate how many characters were removed before the caret's position
        int charactersRemoved = originalText.Length - cleanedText.Length;
        int adjustedCaretPosition = originalCaretPosition - charactersRemoved;
        if (adjustedCaretPosition < cleanedText.Length)
        {
            if(spaceDiff!=0 && cleanedText[adjustedCaretPosition]==' ')
            {
                adjustedCaretPosition++;
            }
        }
        // Clamp caret position to ensure it's within valid bounds
        adjustedCaretPosition = Mathf.Clamp(adjustedCaretPosition, 0, cleanedText.Length);

        // Update the input field text only if it has changed
        if (originalText != cleanedText)
        {
            inputField.text = cleanedText;

            // Set the caret position after updating the text
            inputField.caretPosition = adjustedCaretPosition;
            inputField.textComponent.ForceMeshUpdate();
        }
    }
    private void ClearUnderlines()
    {
        foreach (var underline in activeUnderlines)
        {
            Destroy(underline.gameObject);
        }
        activeUnderlines.Clear();
    }
    private void DrawUnderlines()
    {
        ClearUnderlines();
        TMP_Text textComponent = inputField.textComponent;
        TMP_TextInfo textInfo = textComponent.textInfo;
        
        if (inputField.text.Length == 0)
        {
            return;
        }

        int defaultEmotionKey = (int)Enum.Parse(typeof(Emotions), modelInput.defaultEmotion);

        int pureTextIndex = 0;
        foreach (var segment in segments)
        {
            string segmentText = segment.text;
            int segmentLength = segmentText.Length;
            int emotionKey = segment.emotion != null 
                ? (int)Enum.Parse(typeof(Emotions), segment.emotion) 
                : defaultEmotionKey;
            int visualStartCharIndex = ConvertPureToVisualIndex(pureTextIndex);
            int visualEndCharIndex = ConvertPureToVisualIndex(pureTextIndex + segmentLength - 1);

            if (visualStartCharIndex >= textInfo.characterCount) break;

            int currentLine = textInfo.characterInfo[visualStartCharIndex].lineNumber;

            Vector3 start = textInfo.characterInfo[visualStartCharIndex].bottomLeft;
            Vector3 end = textInfo.characterInfo[visualEndCharIndex].bottomRight;
            for (int i = visualStartCharIndex; i <= visualEndCharIndex && i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;
                
                if (charInfo.lineNumber != currentLine)
                {
                    // Create underline for the previous line
                    CreateUnderline(start, end, emotionKey, textInfo.lineInfo[currentLine]);
                    
                    // Move to the new line
                    currentLine = charInfo.lineNumber;
                    start = charInfo.bottomLeft;
                }
                end = charInfo.bottomRight;
            }
            
            // Create underline for the last collected part of segment
            CreateUnderline(start, end, emotionKey, textInfo.lineInfo[currentLine]);

            pureTextIndex += segmentLength;
        }
    }
    private void CreateUnderline(Vector3 start, Vector3 end, int emotionKey, TMP_LineInfo lineInfo)
    {
        float constOffset = 30f;
        float textWidth = Mathf.Abs(end.x - start.x);
        float anchorX = (start.x + end.x) / 2;
        float height = 1f;

        RectTransform underline1 = Instantiate(underlinePrefab, inputField.transform);
        underline1.anchoredPosition = new Vector2(anchorX, lineInfo.descender+constOffset);
        underline1.sizeDelta = new Vector2(textWidth, 1f);
        RectTransform underline2 = Instantiate(underlinePrefab, inputField.transform);
        underline2.anchoredPosition = new Vector2(anchorX, lineInfo.descender+constOffset-height);
        underline2.sizeDelta = new Vector2(textWidth, 1f);

        var (c1,c2) = colors[emotionKey];
        if(ColorUtility.TryParseHtmlString(c1, out Color underlineColor1) && ColorUtility.TryParseHtmlString(c2, out Color underlineColor2))
        {
            underline1.GetComponent<Image>().color = underlineColor1;
            underline2.GetComponent<Image>().color = underlineColor2;
            activeUnderlines.Add(underline1);
            activeUnderlines.Add(underline2);
        }
        else
        {
            throw new Exception($"Invalid color code. {c1}, {c2}. Make sure your 1 <= Emotionkey <= 33. Now you have {emotionKey}");
        }
        
    }
    #endregion
    #region Helper Functions
    private string FormatJson(UserModelInput input)
    {
        return JsonConvert.SerializeObject(input, Formatting.Indented);
    }
    private (int, int) ExpandToWordBoundaries(string text, int start, int end)
    {
        while (start > 0 && !IsWordDelimiter(text[start - 1]))
        {
            start--;
        }
        if (end != 0 && !IsWordDelimiter(text[end - 1]))
        {
            while (end < text.Length && !IsWordDelimiter(text[end]))
            {
                end++;
            }
        }
        return (start, end);
    }
    private bool IsWordDelimiter(char c)
    {
        return char.IsWhiteSpace(c) || char.IsPunctuation(c) || c=='⏸';
    }
    private string GetPureText()
    {
        return inputField.text.Replace("|", "");
    }
    private int ConvertVisualToPureIndex(int visualIndex)
    {
        int pureIndex = 0;
        for (int i = 0; i < visualIndex; i++)
        {
            if (inputField.text[i] != '|')
            {
                pureIndex++;
            }
        }
        return pureIndex;
    }
    private int ConvertPureToVisualIndex(int pureIndex)
    {
        int visualIndex = 0;
        int count = 0;
        while (count < pureIndex && visualIndex < inputField.text.Length)
        {
            if (inputField.text[visualIndex] != '|')
            {
                count++;
            }
            visualIndex++;
        }
        return visualIndex;
    }
    
    private void RemoveDefaultKeys(ref UserSegment segment)
    {
        string defaultEmotion = modelInput.defaultEmotion;
        Language defaultLanguage = modelInput.defaultLanguage;
        if(segment.emotion!=null && segment.emotion == defaultEmotion)
        {
            segment.emotion=null;
        }
        if(segment.languageObj!=null && segment.languageObj.Equals(defaultLanguage))
        {
            segment.languageObj=null;
        }
    }

    private void UpdateLanguageSelector(List<ActorPackModule> actorPackModules, string actorName)
    {
        ActorPackModule actorPackModule = actorPackModules.Where(module => module.name == modelInput.moduleName).FirstOrDefault();
        if (actorPackModule == null)
        {
            Debug.LogError($"No matching ActorPackModule found for moduleName: {modelInput.moduleName}");
            return;
        }
        List<Language> actorLanguages = actorPackModule.GetActorLanguages(actorPackModule.GetActor(actorName));
        actorLanguages.ForEach(lang => lang.languageKey=null);
        List<Language> removedLanguages = new List<Language>();
        if(languageOptionValues != null)
        {
            languageSelectorHandler.SetToDefaultOption();
            removedLanguages = languageOptionValues.Values.Except(actorLanguages).ToList();
            languageOptionValues.Clear();
        }
        foreach (var lang in removedLanguages)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].languageObj!=null && segments[i].languageObj.Equals(lang))
                {
                    segments[i].languageObj = null;  //Or new Language()?
                }
            }
        }
        //Merge equal keys
        for (int i = segments.Count - 1; i > 0; i--)
        {
            string currentText = segments[i].text;
            string prevText = segments[i - 1].text;
            bool areEqual = segments[i].EqualsIgnoringText(segments[i - 1]);

            if (areEqual || currentText.All(c => IsWordDelimiter(c)))
            {
                segments[i - 1].text += currentText;
                segments.RemoveAt(i);
            }
        }

        if(removedLanguages.Contains(modelInput.defaultLanguage))
        {
            modelInput.defaultLanguage = actorLanguages[0];
        }

        modelInput.segments = segments;
        inputField.text = string.Join("|", segments.Select(seg => seg.text));

        languageOptionValues = actorLanguages.ToDictionary(language => language.ToDisplay());
        languageSelectorHandler.SetOptions(languageOptionValues.Keys.ToList());
    }
    

    #endregion
    #region Enums
    //enums for emotionkeys and their colors for underline
    List<(string,string)> colors = new List<(string,string)>
    {
        (null, null), // Placeholder for index 0 (empty emotion key)
        ("#ffe854", "#ffe854"), // Ecstasy
        ("#00b400", "#00b400"), // Admiration
        ("#008000", "#008000"), // Terror
        ("#0089e0", "#0089e0"), // Amazement
        ("#0000f0", "#0000f0"), // Grief
        ("#de00de", "#de00de"), // Loathing
        ("#d40000", "#d40000"), // Rage
        ("#ff7d00", "#ff7d00"), // Vigilance
        ("#ffff54", "#ffff54"), // Joy
        ("#54ff54", "#54ff54"), // Trust
        ("#009600", "#009600"), // Fear
        ("#59bdff", "#59bdff"), // Surprise
        ("#5151ff", "#5151ff"), // Sadness
        ("#ff54ff", "#ff54ff"), // Disgust
        ("#ff0000", "#ff0000"), // Anger
        ("#ffa854", "#ffa854"), // Anticipation
        ("#ffffb1", "#ffffb1"), // Serenity
        ("#8cff8c", "#8cff8c"), // Acceptance
        ("#8cc68c", "#8cc68c"), // Apprehension
        ("#a5dbff", "#a5dbff"), // Distraction
        ("#8c8cff", "#8c8cff"), // Pensiveness
        ("#ffc6ff", "#ffc6ff"), // Boredom
        ("#ff8c8c", "#ff8c8c"), // Annoyance
        ("#ffc48c", "#ffc48c"), // Interest
        ("#e8e8e8", "#e8e8e8"), // Emotionless
        ("#ff54ff", "#ff0000"), // Contempt 
        ("#5151ff", "#ff54ff"), // Remorse 
        ("#59bdff", "#5151ff"), // Disapproval 
        ("#009600", "#59bdff"), // Awe 
        ("#54ff54", "#009600"), // Submission 
        ("#ffff54", "#54ff54"), // Love 
        ("#ffa854", "#ffff54"), // Optimism 
        ("#ff0000", "#ffa854")  // Aggressiveness 
    };
    private enum Emotions
    {
        Ecstasy = 1,
        Admiration = 2,
        Terror = 3,
        Amazement = 4,
        Grief = 5,
        Loathing = 6,
        Rage = 7,
        Vigilance = 8,
        Joy = 9,
        Trust = 10,
        Fear = 11,
        Surprise = 12,
        Sadness = 13,
        Disgust = 14,
        Anger = 15,
        Anticipation = 16,
        Serenity = 17,
        Acceptance = 18,
        Apprehension = 19,
        Distraction = 20,
        Pensiveness = 21,
        Boredom = 22,
        Annoyance = 23,
        Interest = 24,
        Emotionless = 25,
        Contempt = 26,
        Remorse = 27,
        Disapproval = 28,
        Awe = 29,
        Submission = 30,
        Love = 31,
        Optimism = 32,
        Aggressiveness = 33
    }
    #endregion
    #region Public Methods
    public void Synthesize()   
    {
        Profiler.BeginSample("Synth");
        GameObject.Find("NPC Object").GetComponent<ThespeonEngine>().Synthesize(new UserModelInput(modelInput));
        Profiler.EndSample();
    }
    public UserModelInput GetModelInput()
    {
        return new UserModelInput(modelInput);
    }
    public (string, List<int>) getTextAndEmotion()
    {
        string text = string.Join("", segments.Select(seg => seg.text));
        if(GetPureText() != text) Debug.LogError("Assertion failed in getTextAndEmotion");
        List<int> emotionValues = new List<int>();
        int defaultEmotionKey = (int)Enum.Parse(typeof(Emotions), modelInput.defaultEmotion);

        foreach (var segment in segments)
        {
            string segmentText = segment.text;
            int segmentLength = segmentText.Length;

            int emotionKey = segment.emotion != null
                ? (int)Enum.Parse(typeof(Emotions), segment.emotion) 
                : defaultEmotionKey;

            // Expand the emotion value for each character in the segment
            emotionValues.AddRange(Enumerable.Repeat(emotionKey, segmentLength));
        }
        if(text.Length != emotionValues.Count) Debug.LogError("Assertion failed in getTextAndEmotion");

        return (text, emotionValues);
    }
    //Used as a listener on the inputField component. 

    public IEnumerator OnTextChanged(string newText)
    {
        yield return null;
        inputField.onValueChanged.RemoveListener(onTextChangedListener);
        inputField.textComponent.fontSize = 8;
        CleanText();
        UpdateSegmentsFromText();
        inputField.onValueChanged.AddListener(onTextChangedListener);
        yield return DeferredDrawUnderlines();
        UpdateJsonVisualizer();


    }
    //Emotion klicked in EmotionWheel.
    public void ButtonClicked(string emotionName)
    {
        if (Enum.TryParse<Emotions>(emotionName, out Emotions emotion))
        {
            AnnotateSegments(new Dictionary<string, object> { { "emotion", Enum.GetName(typeof(Emotions), emotion) } });
        }
        else
        {
            throw new Exception($"Invalid emotion name: /{emotionName}/");
        }
    }
    //Used for dropdowns to change the emotion and language
    public void DropdownValueChanged(TMP_Dropdown dropdown)
    {
        string optionText=dropdown.options[dropdown.value].text;
        if(dropdown.name=="Model Selector"){
            modelInput.actorUsername = optionText;



            Dictionary<string, List<ActorPack>> registeredActorPacks = ThespeonAPI.GetRegisteredActorPacks();
            List<ActorPackModule> actorPackModules = registeredActorPacks
                .SelectMany(pack => pack.Value) // Access the List<ActorPack> from the KeyValuePair
                .SelectMany(actorPack => actorPack.modules) // Access the modules of each ActorPack
                .Where(module => module.actor_options.actors
                    .Any(actor => actor.username == optionText)).ToHashSet().ToList();

            if(actorPackModules.Count == 0)
            {
                Debug.LogWarning($"No actors registered with name {optionText}.");
                return;
            }
            List<(string, ActorTags)> nameAndTags = ThespeonAPI.GetActorsAvailabeOnDisk();
            List<string> modelIDs = actorPackModules.Select(module => module.name).ToList();

            if(modelInput.moduleName != null && !modelIDs.Contains(modelInput.moduleName))
            {
                modelInput.moduleName = modelIDs[0];
            }
            qualitySelectorHandler.SetOptions(nameAndTags.Where(pair => pair.Item1 == optionText).Select(x => x.Item2.quality.ToString()).ToList());

            UpdateLanguageSelector(actorPackModules, optionText);
                   
            UpdateSegmentsFromText();       
            UpdateJsonVisualizer();
        } else if(dropdown.name=="Quality Selector"){
            modelInput.moduleName = ThespeonAPI.GetActorPackModuleName(modelInput.actorUsername, new ActorTags(optionText));
            List<ActorPackModule> actorPackModules = ThespeonAPI.GetRegisteredActorPacks()
                .SelectMany(pack => pack.Value) // Access the List<ActorPack> from the KeyValuePair
                .SelectMany(actorPack => actorPack.modules) // Access the modules of each ActorPack
                .Where(module => module.name == modelInput.moduleName).ToHashSet().ToList();
            UpdateLanguageSelector(actorPackModules, modelInput.actorUsername);
            UpdateSegmentsFromText();       
            UpdateJsonVisualizer();
        }else if(dropdown.name=="Language Selector"){
            Language selectedLanguage = languageOptionValues[optionText];
            string defaultLanguage = modelInput.defaultLanguage.iso639_2;
            int selectionStart = inputField.selectionAnchorPosition;
            int selectionEnd = inputField.selectionFocusPosition;

            if (selectionStart == selectionEnd) return;

            if (selectionStart > selectionEnd)
                (selectionStart, selectionEnd) = (selectionEnd, selectionStart);

            int pureSelectionStart = ConvertVisualToPureIndex(selectionStart);
            int pureSelectionEnd = ConvertVisualToPureIndex(selectionEnd);

            string text = GetPureText();

            // Check if entire text is selected
            bool entireTextSelected = pureSelectionStart == 0 && pureSelectionEnd == text.Length;

            if (entireTextSelected)
            {
                modelInput.defaultLanguage = selectedLanguage;
                UpdateSegmentsFromText();
                inputField.text = string.Join("|", segments.Select(seg => seg.text));
                UpdateJsonVisualizer();
            }
            else
            {
                AnnotateSegments(new Dictionary<string, object> { { "language", selectedLanguage } });

            }
        } else if(dropdown.name == "Backend Selector"){
            List<string> loadedModules = ThespeonAPI.GetLoadedActorPackModules();
            foreach(string ActorPackModuleName in loadedModules)
            {
                ThespeonAPI.UnloadActorPackModule(ActorPackModuleName);
            }
            ThespeonAPI.SetBackend(optionText == "GPU" ? BackendType.GPUCompute : BackendType.CPU);

            List<(string, ActorTags)> availableActors = ThespeonAPI.GetActorsAvailabeOnDisk();
            Dictionary<string, (string, ActorTags)> moduleToNameAndTags = availableActors.ToDictionary(x => ThespeonAPI.GetActorPackModuleName(x.Item1, x.Item2) , x => (x.Item1, x.Item2));

            foreach(string ActorPackModuleName in loadedModules)
            {
                
                ThespeonAPI.PreloadActorPackModule(moduleToNameAndTags[ActorPackModuleName].Item1, moduleToNameAndTags[ActorPackModuleName].Item2);
            }
        }
    }


    //Callback method for GUI InsertIPA button to insert IPA symbols as a segment at caret position with annotations of the segment it is located in.
    public void OnInsertIPAButton()
    {
        int caretPosition = inputField.caretPosition;
        int pureCaretPosition = ConvertVisualToPureIndex(caretPosition);
        int segmentStart = 0;
        int segmentEnd = 0;
        int segmentIndex = 0;
        foreach (var segment in segments)
        {
            string segmentText = segment.text;
            segmentEnd += segmentText.Length;
            if (pureCaretPosition >= segmentStart && pureCaretPosition <= segmentEnd)
            {
                break;
            }
            segmentStart = segmentEnd;
            segmentIndex++;
        }
        if (segments[segmentIndex].isCustomPhonemized != null)
        {
            segments[segmentIndex].isCustomPhonemized = null;
        }
        else
        {
            segments[segmentIndex].isCustomPhonemized = true;
        }
        modelInput.segments = segments;
        UpdateJsonVisualizer();
    }

    public void OnInsertPauseButton()
    {
        int caretPosition = inputField.caretPosition;
        if (caretPosition >= 0 && caretPosition <= inputField.text.Length)
        {
            inputField.text = inputField.text.Insert(caretPosition, "⏸");
        }
    }

    public void OnLoadInput(string filename){ 
        if(filename == "Load Predefined Input") return;
        filename=Path.Combine(ModelInputFileLoader.modelInputSamplesPath, filename);
        try 
        {
            string jsonString = RuntimeFileLoader.LoadFileAsString(filename);
            modelInput = JsonConvert.DeserializeObject<UserModelInput>(jsonString);
            if (modelInput.speed!=null) curvesToUI.SetAnimationCurve(modelInput.speed, "speed");
            else
            {
                curvesToUI.SetAnimationCurve(new List<double>(){1.0}, "speed");
            }
            if (modelInput.loudness!=null) curvesToUI.SetAnimationCurve(modelInput.loudness, "loudness");
            else
            {
                curvesToUI.SetAnimationCurve(new List<double>(){1.0}, "loudness");
            }
            
            string actorName = modelSelectorHandler.GetSelectedOption();
            string qualityTag = qualitySelectorHandler.GetSelectedOption();
            string moduleID="";
            try {
                moduleID = ThespeonAPI.GetActorPackModuleName(actorName, new ActorTags(qualityTag));
            }
            catch (ArgumentException)
            {
                actorName = actorName != null ? actorName:ThespeonAPI.GetRegisteredActorPacks().First().Key;
            }
            Language lang = modelInput.defaultLanguage;

            Dictionary<string, List<ActorPack>> registeredActorPacks = ThespeonAPI.GetRegisteredActorPacks();
            List<ActorPackModule> actorPackModules = registeredActorPacks
            .SelectMany(pack => pack.Value) 
            .SelectMany(actorPack => actorPack.modules) 
            .ToHashSet().ToList();
            
            ActorPackModule module=null;

            if(actorName==null)
            {
                moduleID = modelInput.moduleName;
                if(!actorPackModules.Select(m => m.name).Contains(moduleID)){       
                    module = actorPackModules[0];
                    modelInput.moduleName = module.name;
                    modelInput.actorUsername = module.actor_options.actors[0].username;
                }else
                {
                    module = actorPackModules.Where(m => m.name == moduleID).First();
                }
            }
            else
            {
                modelInput.actorUsername = actorName;

                if(moduleID=="")
                {
                    if(!actorPackModules.Select(m => m.name).Contains(moduleID)){       
                        module = actorPackModules.Where(m => m.actor_options.actors[0].username == actorName).First();
                        modelInput.moduleName = module.name;
                    }else
                    {
                        module = actorPackModules.Where(m => m.name == moduleID).First();
                    }
                } else 
                {
                    modelInput.moduleName = moduleID;
                    module = actorPackModules.Where(m => m.name == moduleID).First();
                }
            }
            if(lang != null)
            {
                if(modelInput.defaultLanguage != null && module.language_options.languages.FindIndex(language => language.Equals(modelInput.defaultLanguage)) == -1)
                {
                    modelInput.defaultLanguage = module.language_options.languages[0];
                } else 
                {
                    modelInput.defaultLanguage = modelInput.defaultLanguage;
                }
            } else if (languageSelectorHandler.GetSelectedOption() != null)
            {
                string langOption=languageSelectorHandler.GetSelectedOption();
                if(module.language_options.languages.FindIndex(language => language.Equals(languageOptionValues[langOption])) == -1)
                {
                    modelInput.defaultLanguage = module.language_options.languages[0];
                } else 
                {
                    modelInput.defaultLanguage=languageOptionValues[langOption];
                }
            } else {
                modelInput.defaultLanguage = module.language_options.languages[0];
            }
            modelInput.defaultLanguage.languageKey = null;
            if(modelInput.defaultEmotion != null && !Enum.IsDefined(typeof(Emotions), modelInput.defaultEmotion)) modelInput.defaultEmotion = Emotions.Interest.ToString();
            foreach (var segment in modelInput.segments)
            {
                if(segment.emotion != null && !Enum.IsDefined(typeof(Emotions), segment.emotion))
                {
                    segment.emotion = modelInput.defaultEmotion;
                }
                if(segment.languageObj != null && module.language_options.languages.FindIndex(language => language.Equals(segment.languageObj)) == -1)
                {
                    segment.languageObj = modelInput.defaultLanguage;
                }
            }
            

            var (errors, warnings) = modelInput.ValidateAndWarn();
            
            
            segments = modelInput.segments;

            inputField.text = string.Join("|", segments.Select(seg => seg.text));
            UpdateJsonVisualizer();
            StartCoroutine(DeferredDrawUnderlines());
        }
        catch (Exception ex)
        {
            Debug.LogError("Error reading file: " + filename + "Error: " + ex.Message);
        }
    
    }

    public void CopyTextToClipboard()
    {
        GUIUtility.systemCopyBuffer = jsonVisualizer.text;
        Debug.Log("Text copied to clipboard: \n" + jsonVisualizer.text);
    }
    

    
    #endregion
}
