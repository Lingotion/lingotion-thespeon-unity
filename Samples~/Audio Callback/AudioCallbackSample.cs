using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Lingotion.Thespeon.Inputs;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.Core;
using System;
using UnityEngine.InputSystem;
using System.Linq;
using System.Threading;

#if UNITY_EDITOR
using Unity.Burst;
#endif

/// <summary>
/// Highlights the words of a line in time with the audio. An audio sample request marker is inserted in front
/// of every word, Thespeon reports the sample index each marker ends up at, and the highlight advances as
/// playback passes them.
///
/// The text is shown with UI Toolkit, built in code, so the sample needs no fonts or UI assets of its own.
/// </summary>
[RequireComponent(typeof(ThespeonComponent))]
[RequireComponent(typeof(AudioSource))]
public class AudioCallbackSample : MonoBehaviour
{
    // The line to speak and highlight
    [TextArea(2, 5)]
    public string text = "This sample text will change colour as the actor says their line. Try it out!";
    public Color baseColor = Color.white;
    public Color pastColor = new Color(1f, .85f, .3f);
    public Color currentColor = new Color(1f, 1f, .1f);
    public int fontSize = 48;
    // How many samples to subtract to compensate for a audio stream delay
    public int compensation = 0;
    private ThespeonComponent _engine;
    private AudioSource _audioSource;
    private List<float> _audioData;
    private AudioClip _audioClip;
    private Queue<long> _markerIndices;
    private string[] _words;
    private int _currentWordIndex = -1;
    private int _currentSampleIndex = 0;
    private bool _isFirst = true;
    private UIDocument _document;
    private Label _label;

    void Awake()
    {
        // Build the panel settings before the UIDocument exists, so the document has everything it needs
        // the moment it is enabled.
        PanelSettings panelSettings = CreatePanelSettings();
        _document = GetComponent<UIDocument>();
        if (_document == null)
        {
            _document = gameObject.AddComponent<UIDocument>();
        }
        if (_document.panelSettings == null)
        {
            _document.panelSettings = panelSettings;
        }
    }

    void Start()
    {
#if UNITY_EDITOR
        if (BurstCompiler.Options.EnableBurstDebug)
        {
            Debug.LogWarning("[Warning] Burst Native Debug Mode Compilation is ON; performance will be slower in Editor when running Thespeon on CPU.");
        }
#endif
        _engine = GetComponent<ThespeonComponent>();
        // Connect callback when audio is received from Thespeon
        _engine.OnAudioReceived += OnAudioPacketReceive;
        _engine.OnAudioSampleRequestReceived += OnSampleRequestReceive;
        // Initialize audio data buffer
        _audioData = new();
        _markerIndices = new();
        // Create a streaming audio clip for playback
        _audioClip = AudioClip.Create("ThespeonClip", 1024, 1, 44100, true, OnAudioRead);
        // Start streaming audio from the clip
        _audioSource = GetComponent<AudioSource>();
        _audioSource.clip = _audioClip;
        _audioSource.loop = true;
        _audioSource.Play();

        BuildUI();
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
        {
            // reset state for new synthesis
            _isFirst = true;
            Interlocked.Exchange(ref _currentSampleIndex, 0);
            lock (_audioData)
            {
                _audioData.Clear();
            }
            _markerIndices.Clear();
            _currentWordIndex = -1;

            _words = text.Split(" ");
            ChangeTextColours();
            // Insert a sample request marker in front of each word, signaling that we want the audio sample index for that part of the text
            Func<string, string> addMarker = s => ControlCharacters.AudioSampleRequest + s;
            string markedInputString = string.Join(" ", _words.Select(addMarker));

            ThespeonInput input = new(new List<ThespeonInputSegment>() { new(markedInputString, emotion: Emotion.Interest) });
            _engine.Synthesize(input, sessionID: "AudioCallbackSample");
            return;
        }

        if (_audioSource == null || _audioSource.clip == null || _markerIndices == null || _markerIndices.Count == 0)
        {
            return;
        }
        long targetSample = _markerIndices.Peek();
        long currentAudibleSample = GetPlaybackSampleIndex();
        // Advance highlight if we've crossed into a new marker
        if (currentAudibleSample >= targetSample)
        {
            _markerIndices.Dequeue();
            _currentWordIndex++;
            ChangeTextColours();
        }
    }

    // Simply add the received data to the audio buffer.
    void OnAudioPacketReceive(string sessionID, float[] data)
    {
        lock (_audioData)
        {
            _audioData.AddRange(data);
        }
        if (_isFirst)
        {
            Interlocked.Exchange(ref _currentSampleIndex, 0);
            _isFirst = false;
        }
    }

    void OnSampleRequestReceive(string sessionID, long[] sampleIndices)
    {
        for (int i = 0; i < sampleIndices.Length; i++)
        {
            _markerIndices.Enqueue(sampleIndices[i]);
        }
    }

    // Whenever the Unity audio thread needs data, it calls this function for us to fill the float[] data.
    void OnAudioRead(float[] data)
    {
        lock (_audioData)
        {
            int currentCopyLength = Mathf.Min(data.Length, _audioData.Count);
            // take slice of buffer
            _audioData.CopyTo(0, data, 0, currentCopyLength);
            _audioData.RemoveRange(0, currentCopyLength);
            if (currentCopyLength < data.Length)
            {
                Array.Fill(data, 0f, currentCopyLength, data.Length - currentCopyLength);
            }
        }
    }

    // Triggers before Unity sends audio to be consumed
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_isFirst)
        {
            Interlocked.Add(ref _currentSampleIndex, data.Length / channels);
        }
    }

    // Tries to compensate for latency due to DSPBuffersize
    private long GetPlaybackSampleIndex()
    {
        int dspBuf, numBuf;
        AudioSettings.GetDSPBufferSize(out dspBuf, out numBuf);
        int outputLatencySamples = dspBuf * numBuf;
        long audible = Volatile.Read(ref _currentSampleIndex) - outputLatencySamples - compensation;

        return audible < 0 ? 0 : audible;
    }

    void OnDestroy()
    {
        _engine.OnAudioReceived -= OnAudioPacketReceive;
        _engine.OnAudioSampleRequestReceived -= OnSampleRequestReceive;
        if (_audioSource != null)
        {
            _audioSource.Stop();
            _audioSource.clip = null; // Clear the clip to release resources
        }
    }

    /// <summary>
    /// Builds a PanelSettings at runtime so the sample needs no authored panel asset. The theme is the .tss
    /// in this sample's Resources folder, which imports Unity's default runtime theme - that is also where
    /// the label's font comes from.
    /// </summary>
    private static PanelSettings CreatePanelSettings()
    {
        PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.name = "Audio Callback Panel Settings";
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match = 0.5f;

        ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("AudioCallbackSampleTheme");
        if (theme != null)
        {
            settings.themeStyleSheet = theme;
        }
        else
        {
            Debug.LogWarning("Audio Callback: could not load AudioCallbackSampleTheme from Resources. The text may not render.");
        }
        return settings;
    }

    // A black backdrop filling the screen, with the line centred on it.
    private void BuildUI()
    {
        VisualElement root = _document.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("Audio Callback: the UIDocument has no root element, so the text cannot be shown. Check that its Panel Settings are assigned.");
            return;
        }
        root.Clear();

        VisualElement backdrop = new();
        backdrop.style.flexGrow = 1;
        backdrop.style.backgroundColor = Color.black;
        backdrop.style.justifyContent = Justify.Center;
        backdrop.style.alignItems = Align.Center;
        root.Add(backdrop);

        _label = new Label();
        _label.enableRichText = true;
        _label.style.fontSize = fontSize;
        _label.style.whiteSpace = WhiteSpace.Normal;
        _label.style.unityTextAlign = TextAnchor.MiddleCenter;
        _label.style.maxWidth = Length.Percent(80);
        backdrop.Add(_label);

        _words = text.Split(" ");
        ChangeTextColours();
    }

    void ChangeTextColours()
    {
        if (_label == null || _words == null || _words.Length == 0)
        {
            return;
        }

        var labelText = "";
        for (int i = 0; i < _words.Length; i++)
        {
            if (i > 0)
            {
                labelText += " ";
            }

            if (i < _currentWordIndex)
            {
                labelText += $"<color=#{ColorUtility.ToHtmlStringRGBA(pastColor)}>{_words[i]}</color>";
            }
            else if (i == _currentWordIndex)
            {
                labelText += $"<b><color=#{ColorUtility.ToHtmlStringRGBA(currentColor)}>{_words[i]}</color></b>";
            }
            else
            {
                labelText += $"<color=#{ColorUtility.ToHtmlStringRGBA(baseColor)}>{_words[i]}</color>";
            }
        }

        _label.text = labelText;
    }
}
