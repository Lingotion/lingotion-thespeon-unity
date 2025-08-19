using System;
using System.Collections;
using System.Collections.Generic;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class RLETextAnnotator : MonoBehaviour
{
    public TMP_InputField inputField;
    public RectTransform underlinePrefab;
    private List<RectTransform> activeUnderlines = new List<RectTransform>();
    private UnityAction<string> onTextChangedListener;
    public event Func<string, ThespeonInput> OnTextChangedAndCleaned;
    public event UnityAction<Emotion> OnChangeEmotion;
    private readonly List<(string, string)> colors = new()
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

    public static RLETextAnnotator Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    void Start()
    {
        onTextChangedListener = (text) => { StartCoroutine(OnTextChanged(text)); };
        inputField.onValueChanged.AddListener(onTextChangedListener);
    }

    void Update()
    {

    }

    private IEnumerator OnTextChanged(string newText)
    {
        yield return null;
        inputField.onValueChanged.RemoveListener(onTextChangedListener);
        inputField.textComponent.fontSize = 8;
        CleanText();
        ThespeonInput currentInput = OnTextChangedAndCleaned?.Invoke(inputField.text);
        inputField.onValueChanged.AddListener(onTextChangedListener);
        yield return DeferredDrawUnderlines(currentInput);
    }
    public void SetTextContent(string text, int deltaPipesBeforeSelection = 0)
    {
        if(text.Equals("\u00D8")) text = "";
        inputField.onValueChanged.RemoveListener(onTextChangedListener);
        if (deltaPipesBeforeSelection != 0) AdjustSelectionAndCaretPositions(deltaPipesBeforeSelection);
        inputField.text = text;
        inputField.onValueChanged.AddListener(onTextChangedListener);
    }

    /// <summary>
    /// When setting new text content, if the only differance is a number of inserted | characters adjust the inputField.caretPosition, inputField.selectionAnchorPosition and inputField.selectionFocusPosition accordingly 
    /// </summary>
    /// <param name="newText">the text to set.</param>
    private void AdjustSelectionAndCaretPositions(int adjustment)
    {
        int currentCaretPosition = inputField.caretPosition;
        int currentAnchorPosition = inputField.selectionAnchorPosition;
        int currentFocusPosition = inputField.selectionFocusPosition;
        inputField.caretPosition = currentCaretPosition + adjustment;
        inputField.selectionAnchorPosition = currentAnchorPosition + adjustment;
        inputField.selectionFocusPosition = currentFocusPosition + adjustment;
    }
    public IEnumerator DeferredDrawUnderlines(ThespeonInput currentInput)
    {
        yield return null;
        inputField.textComponent.ForceMeshUpdate();
        DrawUnderlines(currentInput);
    }
    private void DrawUnderlines(ThespeonInput currentInput)
    {
        ClearUnderlines();
        TMP_Text textComponent = inputField.textComponent;
        TMP_TextInfo textInfo = textComponent.textInfo;

        if (inputField.text.Length == 0)
        {
            return;
        }

        int defaultEmotionKey = (int)currentInput.DefaultEmotion;

        int pureTextIndex = 0;
        foreach (ThespeonInputSegment segment in currentInput.Segments)
        {
            string segmentText = segment.Text;
            int segmentLength = segmentText.Length;
            int emotionKey = segment.Emotion != Emotion.None
                ? (int)segment.Emotion
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
                    CreateUnderline(start, end, emotionKey, textInfo.lineInfo[currentLine]);

                    currentLine = charInfo.lineNumber;
                    start = charInfo.bottomLeft;
                }
                end = charInfo.bottomRight;
            }

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
        underline1.anchoredPosition = new Vector2(anchorX, lineInfo.descender + constOffset);
        underline1.sizeDelta = new Vector2(textWidth, 1f);
        RectTransform underline2 = Instantiate(underlinePrefab, inputField.transform);
        underline2.anchoredPosition = new Vector2(anchorX, lineInfo.descender + constOffset - height);
        underline2.sizeDelta = new Vector2(textWidth, 1f);

        var (c1, c2) = colors[emotionKey];
        if (ColorUtility.TryParseHtmlString(c1, out Color underlineColor1) && ColorUtility.TryParseHtmlString(c2, out Color underlineColor2))
        {
            underline1.GetComponent<Image>().color = underlineColor1;
            underline2.GetComponent<Image>().color = underlineColor2;
            activeUnderlines.Add(underline1);
            activeUnderlines.Add(underline2);
        }
        else
        {
            throw new ArgumentException($"Invalid color code. {c1}, {c2}. Make sure your Emotionkey satisfies 1 <= Emotionkey <= 33. Now you have {emotionKey}");
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

    private void CleanText()
    {
        string originalText = inputField.text;
        int originalCaretPosition = inputField.caretPosition;
        string cleanedText = System.Text.RegularExpressions.Regex.Replace(originalText, @"([.,!?])\1+", "$1");

        int lengthBefore = cleanedText.Length;
        cleanedText = System.Text.RegularExpressions.Regex.Replace(cleanedText, @"[\s\u2028\u2029]+", " ");
        int spaceDiff = lengthBefore - cleanedText.Length;
        cleanedText = cleanedText.Trim('|');

        int charactersRemoved = originalText.Length - cleanedText.Length;
        int adjustedCaretPosition = originalCaretPosition - charactersRemoved;
        if (adjustedCaretPosition < cleanedText.Length)
        {
            if (spaceDiff != 0 && cleanedText[adjustedCaretPosition] == ' ')
            {
                adjustedCaretPosition++;
            }
        }
        adjustedCaretPosition = Mathf.Clamp(adjustedCaretPosition, 0, cleanedText.Length);

        if (originalText != cleanedText)
        {
            inputField.text = cleanedText;

            inputField.caretPosition = adjustedCaretPosition;
            inputField.textComponent.ForceMeshUpdate();
        }
    }

    public string GetPureText()
    {
        return inputField.text.Replace("|", "");
    }

    
    public void EmotionClicked(string emotionName)
    {
        if (Enum.TryParse(emotionName, out Emotion emotion))
        {
            if (emotion == Emotion.None)
            {
                LingotionLogger.Warning("No emotion selected.");
                return;
            }
            OnChangeEmotion?.Invoke(emotion);
        }
        else
        {
            throw new ArgumentException($"Invalid emotion name: /{emotionName}/");
        }
    }

    public (int, int) GetSelectionExpandedToWords()
    {
        if (inputField == null || string.IsNullOrEmpty(inputField.text)) return (-1, -1);

        string pureText = GetPureText();

        int selectionStart = inputField.selectionAnchorPosition;
        int selectionEnd = inputField.selectionFocusPosition;

        if (selectionStart > selectionEnd)
            (selectionStart, selectionEnd) = (selectionEnd, selectionStart);

        int pureSelectionStart = ConvertVisualToPureIndex(selectionStart);
        int pureSelectionEnd = ConvertVisualToPureIndex(selectionEnd);

        return ExpandToWordBoundaries(pureText, pureSelectionStart, pureSelectionEnd);      
    }

    public (int, int) GetSelectedSegments()
    {
        if (inputField == null || string.IsNullOrEmpty(inputField.text)) return (-1, -1);

        string text = inputField.text;

        int selectionStart = inputField.selectionAnchorPosition;
        int selectionEnd = inputField.selectionFocusPosition;
        if (selectionStart > selectionEnd)
        {
            (selectionStart, selectionEnd) = (selectionEnd, selectionStart);
        }

        List<int> selectedIndices = new List<int>();

        string[] segments = text.Split('|');
        int currentPos = 0;

        for (int i = 0; i < segments.Length; i++)
        {
            int segStart = currentPos;
            int segEnd = currentPos + segments[i].Length;

            if (selectionEnd >= segStart && selectionStart <= segEnd)
            {
                selectedIndices.Add(i);
            }

            currentPos = segEnd + 1;
        }
        if (selectedIndices.Count == 0)
        {
            LingotionLogger.Warning("No segments selected. " + selectionStart + ", " + selectionEnd);
            return (-1, -1);
        }
        return (selectedIndices[0], selectedIndices[selectedIndices.Count - 1]);
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


    /// <summary>
    /// Expands the given start and end indices to the nearest word boundaries in the text. Any whitespaces, punctuation, or the pause character '⏸' 
    /// </summary>
    /// <param name="text"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
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

    public bool IsWordDelimiter(char c)
    {
        return char.IsWhiteSpace(c) || char.IsPunctuation(c) || c == '⏸';
    }

    public void OnInsertPauseButton()
    {
        int caretPosition = inputField.caretPosition;
        if (caretPosition >= 0 && caretPosition <= inputField.text.Length)
        {
            inputField.text = inputField.text.Insert(caretPosition, "⏸");
        }
    }
}
