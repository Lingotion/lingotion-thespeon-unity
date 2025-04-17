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
using Lingotion.Thespeon.ThespeonRunscripts;
using Unity.Sentis;
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
    public TMP_Text jsonVizualizer;
    // private List<Dictionary<string, object>> segments;
    private List<UserSegment> segments;
    // private Dictionary<string, object> modelInputJson;
    private UserModelInput modelInput;
    private List<RectTransform> activeUnderlines = new List<RectTransform>();
    private DropdownHandler modelSelectorHandler;
    private DropdownHandler qualitySelectorHandler;
    private DropdownHandler languageSelectorHandler;
    private DropdownHandler LoadPredefinedInputHandler;

    private Dictionary<string, Language> languageOptionValues;
    // private Dictionary<string, string> qualityOptionValues; //Not used yet. Can be used if we want to display something other than the module name.
    // private List<int> emotionAnnotation;
    // private List<int> loudnessAnnotation;

    private CurvesToUI curvesToUI;
    




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
        curvesToUI = GetComponent<CurvesToUI>();
        if (curvesToUI == null)
        {
            Debug.LogError("CurvesToUI component not found on the same GameObject as TextStyler.");
        }
        curvesToUI.OnCurvesChanged += () => UpdateJsonVisualizer();

        

        InitializeJsonStructure();
        
        
        // emotionAnnotation = new List<int>(Enumerable.Repeat((int)Emotions.Emotionless, inputField.text.Length));
        // loudnessAnnotation = new List<int>(Enumerable.Repeat(1, inputField.text.Length));
        inputField.onValueChanged.AddListener(OnTextChanged);
        inputField.Select();  
        inputField.selectionStringAnchorPosition = 0; 
        inputField.selectionStringFocusPosition = inputField.text.Length;
        StartCoroutine(DeferredDrawUnderlines());
        List<(string, ActorTags)> availableActors=ThespeonAPI.GetActorsAvailabeOnDisk();       //Switch to returning List<Actor>?

        // get the GameComponent dropdown "Select Actor" and set the options to the registered actors.
        modelSelectorHandler = GameObject.Find("Model Selector").GetComponent<DropdownHandler>();

        qualitySelectorHandler = GameObject.Find("Quality Selector").GetComponent<DropdownHandler>();

        languageSelectorHandler = GameObject.Find("Lanugage Selector").GetComponent<DropdownHandler>();

        LoadPredefinedInputHandler = GameObject.Find("Input Selector").GetComponent<DropdownHandler>();

        if(availableActors.Count!=0)
        {
            string optionText = availableActors[0].Item1;
            // Debug.Log("Changing actorUsername to " + optionText);
            modelInput.actorUsername = optionText;
        
            Dictionary<string, List<ActorPack>> registeredActorPacks = ThespeonAPI.GetRegisteredActorPacks();
            ActorPackModule actorPackModule = registeredActorPacks
                .SelectMany(pack => pack.Value) // Access the Value (List<ActorPack>) of the KeyValuePair
                .SelectMany(actorPack => actorPack.modules) // Access the modules of each ActorPack
                .FirstOrDefault(module => module.actor_options.actors
                    .Any(actor => actor.username == optionText));

            string moduleName = actorPackModule.name;
            modelInput.moduleName = moduleName;
            // Debug.Log(actorPackModule.GetActorLanguages(actorPackModule.GetActor(optionText))[0].ToString());
            Language initLang = actorPackModule.GetActorLanguages(actorPackModule.GetActor(optionText))[0];
            if(initLang.languageKey != null) initLang.languageKey=null;
            // modelInputJson["defaultLanguage"] = initLang; HERE
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
                // Debug.Log("Changes made to registration: " + string.Join(",", registeredUsernames));
                modelSelectorHandler.SetOptions(registeredUsernames);
            }
        }
    }
    private void InitializeJsonStructure()
    {
        // Debug.Log("Initializing JSON structure");
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
        // AnnotateSegments(new Dictionary<string, object> {});

    }
    #endregion
    #region JSON and Segment Updating
    private void UpdateJsonVisualizer()
    {
        SampleCurves();
        jsonVizualizer.text = FormatJson(modelInput); //HERE
    }
    private void UpdateSegmentsFromText(){
        // Debug.Log("Start of UpdateSegmentsFromText");
        inputField.textComponent.ForceMeshUpdate();
        string visualText = inputField.text;
        List<string> newTextSegments = visualText.Split('|').ToList();
        List<UserSegment> updatedSegments = new List<UserSegment>();
        // string defaultEmotion = modelInputJson["defaultEmotion"].ToString();   HERE
        string defaultEmotion = modelInput.defaultEmotion;
        // string defaultLanguage = ((Language) modelInputJson["defaultLanguage"]).iso639_2; HERE
        string defaultLanguage = modelInput.defaultLanguage.iso639_2;
        //loop through segments and insert each one into updatedSegments only changing their text to the corresponding text in newTextSegments
        if(segments.Count == 0){        //initialization. Check what happens if text is empty.
            if(newTextSegments.Count != 1) Debug.LogError("Assertion failed in Initialization Update");
            // updatedSegments.Add(new Dictionary<string, object> { { "text", newTextSegments[0] } });
            updatedSegments.Add(new UserSegment(newTextSegments[0]));
        }
        else
        {
            int pipeDelta = newTextSegments.Count - segments.Count;
            // Pipe manually inserted or removed by user 
            if(pipeDelta != 0) 
            {
                // if (Math.Abs(newTextSegments.Count-segments.Count) != 1) Debug.LogError("Assertion failed in UpdateSegmentsFromText. Not only 1 pipe inserted.");
                //go through segments and see its text is the same as the segmentText at that index. If not, split that segment in two. (should be a concat of the current and next newTextSegments)
                int j=0;
                bool pipeDiffFound = false;
                if(pipeDelta > 0)   //segments added
                {
                    // Debug.Log("Pasted some shite: " +pipeDelta + "\nStrings: " + string.Join("|", newTextSegments) + "\n" + string.Join('|', segments.Select(seg => seg.text)));
                    for(int i = 0; i < newTextSegments.Count; i++)
                    {
                        string currentOldSegmentText = segments[j].text;

                        // if (string.IsNullOrWhiteSpace(currentOldSegmentText)) continue;
                        if(pipeDiffFound)   //mark as ready to move to next segment. Means we are at the leftover of the old segment
                        {
                            // Debug.Log("INSERTED SEGMENTS: Ready to move to next segment. Adding new segment with text: " + newTextSegments[i] + "old segment: " + currentOldSegmentText);
                            string newText = newTextSegments[i];
                            UserSegment newSegment = new UserSegment(segments[j]);
                            newSegment.text = newText;
                            RemoveDefaultKeys(ref newSegment);
                            updatedSegments.Add(newSegment);
                            j++;
                            pipeDiffFound=false;
                        }
                        else if(currentOldSegmentText != newTextSegments[i])    //Found segment where something was inserted.
                        {
                            // Debug.Log("INSERTED SEGMENTS: Where? " + currentOldSegmentText + "   inserted: " + newTextSegments[j]);
                            string newText = newTextSegments[i];
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
                        if(currentOldSegmentText != newTextSegments[j])     //found problem
                        {
                            // Debug.Log("REMOVED SEGMENTS: Found where removal happened:" + newTextSegments[j]);
                            // if (string.IsNullOrWhiteSpace(segmentText)) continue;
                            UserSegment newSegment = new UserSegment(segments[i]);
                            newSegment.text = newTextSegments[j];
                            RemoveDefaultKeys(ref newSegment);
                            updatedSegments.Add(newSegment);
                            i+=Math.Abs(pipeDelta);
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
                    UserSegment newSegment = new UserSegment(segments[i]);
                    newSegment.text = segmentText;
                    //if segment keys emotion or language are equal to any of the default values, remove them.
                    RemoveDefaultKeys(ref newSegment);
                    updatedSegments.Add(newSegment);
                }

            }
        }
        for (int i = updatedSegments.Count - 1; i > 0; i--)
        {
            string currentText = updatedSegments[i].text;
            string prevText = updatedSegments[i - 1].text.ToString();

            // bool areEqual = updatedSegments[i].Where(kv => kv.Key != "text").OrderBy(kv => kv.Key)   HERE
                    // .SequenceEqual(updatedSegments[i - 1].Where(kv => kv.Key != "text").OrderBy(kv => kv.Key));
            bool areEqual = updatedSegments[i].EqualsIgnoringText(updatedSegments[i - 1]);

            if (string.IsNullOrWhiteSpace(currentText) || areEqual || currentText.All(c => IsWordDelimiter(c)))
            {
                updatedSegments[i - 1].text += currentText;
                updatedSegments.RemoveAt(i);
            }
        }
        
        //turn of OnTextChanged Listener, set text and reenable it.
        inputField.onValueChanged.RemoveListener(OnTextChanged);
        inputField.text = string.Join("|", updatedSegments.Select(seg => seg.text));
        inputField.onValueChanged.AddListener(OnTextChanged);
        segments = updatedSegments;
        modelInput.segments = segments; // Update JSON structure
        UpdateJsonVisualizer(); // Ensure JSON updates dynamically

        // Debug.Log("End of UpdateSegmentsFromText");

    }
    
    //Check if it works as it should with PIPEs and also this no longer checks for if defaults are inserted, make sure to either filter for that after or before.
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
            //set all segments without an emotion key to the old default then set default to the new one.
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
        // Insert segment breaks at expanded start and end
        bool startIsBreak = text[visualInsertStart]=='|' || expandedStart == 0;
        bool endIsBreak = expandedEnd == pureText.Length || text[visualInsertEnd]=='|';
        text = text.Insert(visualInsertStart, "|");
        text = text.Insert(visualInsertEnd + 1, "|");
        // Regenerate segment break indices
        List<int> segmentBreaks = new List<int>() {0};
        for (int i = 0; i < text.Length; i++)
            if (text[i] == '|') segmentBreaks.Add(i);
        segmentBreaks.Add(text.Length);
        // Find the break indices for the inserted segment boundaries
        int insertStartBreakIndex = segmentBreaks.IndexOf(visualInsertStart) + (startIsBreak ? 1 : 0);
        int insertEndBreakIndex = segmentBreaks.IndexOf(visualInsertEnd+1);
        // if(segmentBreaks[insertEndBreakIndex] != segmentBreaks.Count-1)
        // {
        //     if(segmentBreaks[insertEndBreakIndex+1] == segmentBreaks[insertEndBreakIndex]+1)
        //     {
        //         insertEndBreakIndex++;
        //     }
        // }

        if(insertStartBreakIndex == -1 + (startIsBreak ? 1 : 0) || insertEndBreakIndex == -1) Debug.LogError("Failed to find segment break indices.");
        
        List<UserSegment> newSegments = new List<UserSegment>();

        List<string> newTextSegments = text.Split('|').ToList();

        if(newTextSegments.Count != segmentBreaks.Count-1) Debug.LogError("Texts and segmentBreaks are out of sync.");
        // Debug.Log("segmentBreaks: " + string.Join(",", segmentBreaks) + " Insert breaks: " + insertStartBreakIndex + " " + insertEndBreakIndex);

        //loop through newTextSegments and enumerating them to get the index
        bool inSelection=false;
        int j=0;
        for(int i = 0; i < newTextSegments.Count; i++)
        {
            string segmentText = newTextSegments[i];
            int segmentStart = segmentBreaks[i];
            int segmentEnd = segmentBreaks[i + 1];
            // if (string.IsNullOrWhiteSpace(segmentText)) continue;

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
                //add to newSegments the old segments[j] but update or add all the keys in newKeys and change the text to segmentText
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
                    // Debug.Log("Added key: " + key.Key + " with value: " + key.Value + " to segment: " + newSegment.text + "so its new value is: " + newSegment[key.Key]);
                }
                newSegment.text = segmentText;
                RemoveDefaultKeys(ref newSegment);
                newSegments.Add(newSegment);
            } else
            {
                //add the keys from segment segments[j] to newSegments but change the text to segmentText
                if(j == segments.Count) Debug.LogError($"j: {j} segments.Count: {segments.Count}, segment text: {segmentText}");
                UserSegment newSegment = new UserSegment (segments[j]);
                newSegment.text = segmentText;
                RemoveDefaultKeys(ref newSegment);
                newSegments.Add(newSegment);
            }
            j++;
        }


        //DO TWO LOOPS, once for whitespaces and another time for equal keys.
        // Merge segments where necessary
        for (int i = newSegments.Count - 1; i > 0; i--)
        {
            //Debug print all the key value pairs of prev and current
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
        for (int i = newSegments.Count - 1; i > 0; i--)
        {
            // Debug.Log(newSegments[i].text + " " + newSegments[i-1].text);
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
        // modelInputJson["segments"] = segments;
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
            float t = i / (float)(charCount - 1); // Normalized time (0 to 1)
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
        // emotionAnnotation = new List<int>(Enumerable.Repeat((int)Emotions.Emotionless, inputField.text.Length));
        // loudnessAnnotation = new List<int>(Enumerable.Repeat(1, inputField.text.Length));

        // Now call your underline drawing logic
        DrawUnderlines();
    }
    private void CleanText()
    {
        // Get the current text from the input field
        string originalText = inputField.text;
        int originalCaretPosition = inputField.caretPosition;

        // Remove double delimiters (e.g., "..", ",,", "!!")
        string cleanedText = System.Text.RegularExpressions.Regex.Replace(originalText, @"([.,!?])\1+", "$1");

        // Replace multiple spaces or tabs with a single space
        int lengthBefore = cleanedText.Length;
        cleanedText = System.Text.RegularExpressions.Regex.Replace(cleanedText, @"[ \t]+", " ");
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
        // Debug.Log("Drawing underlines");
        ClearUnderlines();
        TMP_Text textComponent = inputField.textComponent;
        TMP_TextInfo textInfo = textComponent.textInfo;
        
        if (inputField.text.Length == 0)
        {
            return;
        }

        // int defaultEmotionKey = (int)Enum.Parse(typeof(Emotions), modelInputJson["defaultEmotion"].ToString());
        int defaultEmotionKey = (int)Enum.Parse(typeof(Emotions), modelInput.defaultEmotion);

        int pureTextIndex = 0;
        foreach (var segment in segments)
        {
            string segmentText = segment.text;
            int segmentLength = segmentText.Length;
            int emotionKey = segment.emotion != null 
                ? (int)Enum.Parse(typeof(Emotions), segment.emotion) 
                : defaultEmotionKey;
            // Debug.Log($"Drawing underline for segment: {segmentText} with emotion name {Enum.GetName(typeof(Emotions), emotionKey)}");
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
                    // Debug.Log("New line: " + charInfo.lineNumber);
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
        float wierdOffset = 25f;
        float textWidth = Mathf.Abs(end.x - start.x);
        float anchorX = (start.x + end.x) / 2;
        float height = 1f;

        RectTransform underline1 = Instantiate(underlinePrefab, inputField.transform);
        underline1.anchoredPosition = new Vector2(anchorX, lineInfo.descender+wierdOffset);
        underline1.sizeDelta = new Vector2(textWidth, 1f);
        RectTransform underline2 = Instantiate(underlinePrefab, inputField.transform);
        underline2.anchoredPosition = new Vector2(anchorX, lineInfo.descender+wierdOffset-height);
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
        if(segment.languageObj!=null) //Debug.Log($"Language key: {segment.languageObj.ToDisplay()}, default language: {defaultLanguage}");
        if(segment.emotion!=null && segment.emotion == defaultEmotion)
        {
            segment.emotion=null;
        }
        if(segment.languageObj!=null && segment.languageObj.Equals(defaultLanguage))
        {
            segment.languageObj=null;
        }
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
        // Debug.Log("Synth pressed " + Time.realtimeSinceStartup);
        Profiler.BeginSample("Synth");
        GameObject.Find("NPC Object").GetComponent<ThespeonEngine>().Synthesize(modelInput);
        Profiler.EndSample();
    }
    public UserModelInput GetModelInput()
    {
        // string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(modelInputJson);
        // Debug.Log(jsonString);
        // UserModelInput modelInput = Newtonsoft.Json.JsonConvert.DeserializeObject<UserModelInput>(jsonString);
        //Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(modelInput)); 
        return JsonConvert.DeserializeObject<UserModelInput>(JsonConvert.SerializeObject(modelInput));      //Ugly hack for deep copy. See TUNI-29
    }
    public (string, List<int>) getTextAndEmotion()
    {
        string text = string.Join("", segments.Select(seg => seg.text));
        if(GetPureText() != text) Debug.LogError("Assertion failed in getTextAndEmotion");
        List<int> emotionValues = new List<int>();
        // int defaultEmotionKey = (int)Enum.Parse(typeof(Emotions), modelInputJson["defaultEmotion"].ToString());
        int defaultEmotionKey = (int)Enum.Parse(typeof(Emotions), modelInput.defaultEmotion);

        foreach (var segment in segments)
        {
            string segmentText = segment.text;
            int segmentLength = segmentText.Length;

            // Fetch the emotion key from the segment (default to Emotionless if not found)
            int emotionKey = segment.emotion != null
                ? (int)Enum.Parse(typeof(Emotions), segment.emotion) 
                : defaultEmotionKey;

            // Expand the emotion value for each character in the segment
            emotionValues.AddRange(Enumerable.Repeat(emotionKey, segmentLength));
        }
        if(text.Length != emotionValues.Count) Debug.LogError("Assertion failed in getTextAndEmotion");
        // Debug.Log("Text: " + text);
        // Debug.Log("Emotion: " + string.Join(",", emotionValues));

        return (text, emotionValues);
    }
    //Used as a listener on the inputField component. 
    public void OnTextChanged(string newText)
    {
        //Force all text to the set font size set in the inspector
        inputField.textComponent.fontSize = 8;
        CleanText();
        UpdateSegmentsFromText();
        StartCoroutine(DeferredDrawUnderlines());
        UpdateJsonVisualizer();


    }
    //Emotion klicked in EmotionWheel.
    public void buttonClicked(string emotionName)
    {
        if (Enum.TryParse<Emotions>(emotionName, out Emotions emotion))
        {
            // UpdateSegmentsFromText();
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
            // Debug.Log("Model IDs: " + string.Join(", ", modelIDs));

            if(modelInput.moduleName != null && !modelIDs.Contains(modelInput.moduleName))
            {
                modelInput.moduleName = modelIDs[0];
            }
            qualitySelectorHandler.SetOptions(nameAndTags.Where(pair => pair.Item1 == optionText).Select(x => x.Item2.quality.ToString()).ToList());

            //OBS assumes same language accross modules.
            List<Language> actorLanguages = actorPackModules[0].GetActorLanguages(actorPackModules[0].GetActor(optionText));
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

                // bool areEqual = segments[i].Where(kv => kv.Key != "text").OrderBy(kv => kv.Key)
                //         .SequenceEqual(segments[i - 1].Where(kv => kv.Key != "text").OrderBy(kv => kv.Key));
                bool areEqual = segments[i].EqualsIgnoringText(segments[i - 1]);

                if (areEqual || currentText.All(c => IsWordDelimiter(c)))
                {
                    segments[i - 1].text += currentText;
                    segments.RemoveAt(i);
                }
            }
            //if is one of the removed, change it to the first new available language
            // if(removedLanguages.Contains((Language) modelInputJson["defaultLanguage"]))
            // {
            //     modelInputJson["defaultLanguage"] = actorLanguages[0];
            // } 
            if(removedLanguages.Contains((Language) modelInput.defaultLanguage))
            {
                modelInput.defaultLanguage = actorLanguages[0];
            }

            // Apply changes
            // modelInputJson["segments"] = segments;
            modelInput.segments = segments;
            inputField.text = string.Join("|", segments.Select(seg => seg.text));

            languageOptionValues = actorLanguages.ToDictionary(language => language.ToDisplay());
            languageSelectorHandler.SetOptions(languageOptionValues.Keys.ToList());
                   
            UpdateSegmentsFromText();       //bit of a hack to update after this change. Should be done in a better way.
            UpdateJsonVisualizer();
        } else if(dropdown.name=="Quality Selector"){
            // Debug.Log("Selected model id: " + optionText);
            modelInput.moduleName = ThespeonAPI.GetActorPackModuleName(modelInput.actorUsername, new ActorTags(optionText));
            UpdateSegmentsFromText();       //bit of a hack to update after this change. Should be done in a better way.
            UpdateJsonVisualizer();
        }else if(dropdown.name=="Lanugage Selector"){
            Language selectedLanguage = languageOptionValues[optionText];
            // string defaultLanguage = ((Language) modelInputJson["defaultLanguage"]).iso639_2;
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
                // Debug.Log("Changing default language");
                // If all text is selected, update default language

                // modelInputJson["defaultLanguage"] = selectedLanguage;
                modelInput.defaultLanguage = selectedLanguage;
                UpdateSegmentsFromText();
                inputField.text = string.Join("|", segments.Select(seg => seg.text));
                UpdateJsonVisualizer();
            }
            else
            {
                // Debug.Log("Annotating segments with language");
                // If part of the text is selected, annotate that part with the new language
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
            // Debug.Log("Re-preloaded " + string.Join(", ", loadedModules));
        }
        // dropdown.value = 0;              This or no? If not and one wants to mark several disjopint segments with the same language one has to deselect and reselect the language again for onDropdownValueChanged to fire. But if yes then after selection it automatically goes back to the default option, not letting the user see what they just selected unless looking at the json.
    }


    //Callback method for GUI InsertIPA button to insert IPA symbols as a segment at caret position with annotations of the segment it is located in.
    public void OnInsertIPAButton()
    {
        int caretPosition = inputField.caretPosition;
        //find in which segment the caret is located
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
        // if the key "IsCustomPhonemized" is present, remove it, otherwise add it.
        if (segments[segmentIndex].isCustomPhonemized != null)           //Vill gärna själv ha "ipa" här.
        {
            segments[segmentIndex].isCustomPhonemized = null;
        }
        else
        {
            segments[segmentIndex].isCustomPhonemized = true;
        }
        // modelInputJson["segments"] = segments;
        modelInput.segments = segments;
        UpdateJsonVisualizer();
        // segments.Insert(segmentIndex, new Dictionary<string, object> { { "text", ipa } });
    }

    public void OnInsertPauseButton()
    {
        int caretPosition = inputField.caretPosition;
        if (caretPosition >= 0 && caretPosition <= inputField.text.Length)
        {
            inputField.text = inputField.text.Insert(caretPosition, "⏸");
        }
        // UpdateSegmentsFromText();
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
            
            // segments = (List<Dictionary<string, object>>)modelInputJson["segments"];
            string actorName = modelSelectorHandler.GetSelectedOption();
            string qualityTag = qualitySelectorHandler.GetSelectedOption();            
            string moduleID = ThespeonAPI.GetActorPackModuleName(actorName, new ActorTags(qualityTag));
            Language lang = modelInput.defaultLanguage;

            Dictionary<string, List<ActorPack>> registeredActorPacks = ThespeonAPI.GetRegisteredActorPacks();
            List<ActorPackModule> actorPackModules = registeredActorPacks
            .SelectMany(pack => pack.Value) // Access the List<ActorPack> from the KeyValuePair
            .SelectMany(actorPack => actorPack.modules) // Access the modules of each ActorPack
            .ToHashSet().ToList();
            
            ActorPackModule module=null;

            if(actorName==null)
            {
                moduleID = modelInput.moduleName;
                if(!actorPackModules.Select(m => m.name).Contains(moduleID)){       
                    module = actorPackModules[0];
                    Debug.LogWarning($"Module ID {moduleID} not found in registered actor packs. Using first available module and actor.");
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

                if(moduleID==null)
                {
                    if(!actorPackModules.Select(m => m.name).Contains(moduleID)){       
                        module = actorPackModules[0];
                        Debug.LogWarning($"Module ID {moduleID} not found in registered actor packs. Using first available module and actor.");
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
            if(modelInput.defaultEmotion != null && !Enum.IsDefined(typeof(Emotions), modelInput.defaultEmotion)) modelInput.defaultEmotion = "Emotionless";
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
    

    
    #endregion
}
