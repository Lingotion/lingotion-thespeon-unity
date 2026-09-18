using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Engine;
using Lingotion.Thespeon.Inputs;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lingotion.Thespeon.Samples.AdvancedGUI
{
    /// <summary>
    /// A runtime control panel for Thespeon's delivery controls: pick a character, module size, compute backend
    /// and language, set an emotion blend plus speed and loudness at the start and the end of the line, and
    /// synthesize. The start/end values are keypoints on a curve that spans the line, so the delivery moves
    /// smoothly from one state into the other rather than switching abruptly.
    ///
    /// Ctrl+D reveals developer mode, where the line can be split into several segments, each with its own text
    /// and its own boundary values, to build a curve with more than two keypoints.
    ///
    /// While a line plays, a karaoke overlay across the bottom of the screen shows the speaker's portrait and the
    /// line being revealed in time with the audio, as in the Unreal demo. The timing comes from audio sample
    /// request markers inserted in front of every word - see the Audio Callback sample for that feature on its
    /// own.
    ///
    /// The whole UI is built in code with UI Toolkit and styled from Resources/AdvancedThespeonGUI.uss, so
    /// dropping this component on a GameObject is all that is needed - there is no prefab or panel asset to
    /// wire up.
    /// </summary>
    [RequireComponent(typeof(ThespeonComponent))]
    [RequireComponent(typeof(AudioSource))]
    public class AdvancedThespeonGUI : MonoBehaviour
    {
        /// <summary>Optional per-character portrait. Characters without one get a placeholder.</summary>
        [Serializable]
        public class PortraitOverride
        {
            public string characterName;
            public Texture2D portrait;
        }

        [Tooltip("The line offered when the panel opens.")]
        [TextArea(2, 5)]
        public string defaultText = "I found something in the old mine, and I really don't think we should have opened it!";

        [Tooltip("Portraits for specific characters. Anything not listed here is looked up in "
                 + "Resources/Portraits/<character-name>.png, and falls back to a placeholder.")]
        public List<PortraitOverride> portraitOverrides = new();

        [Tooltip("Session ID passed to Thespeon. Reusing one ID means a new synthesis replaces the previous one.")]
        public string sessionID = "AdvancedGUISession";

        [Tooltip("Show the portrait-and-subtitle overlay that reveals the line in time with the audio.")]
        public bool showKaraokeOverlay = true;

        [Tooltip("Extra sample frames to subtract from the tracked playback position, on top of the DSP buffer "
                 + "latency Unity reports. Raise it if the overlay runs ahead of what you hear.")]
        public int karaokeLatencyCompensationSamples = 0;

        [Tooltip("How long the overlay stays on screen after the line has finished playing, in seconds.")]
        public float karaokeLingerSeconds = 1.5f;

        private const int OutputSampleRate = 44100;

        // The "." ".." "..." cycle shown in the karaoke bubble while the first audio is on its way.
        private const int WaitingDotCount = 3;
        private const float WaitingDotsPerSecond = 3f;

        // Speed and loudness are multipliers around 1. The range here is what the panel exposes, not a hard
        // limit in the engine.
        private const float ControlMinimum = 0.5f;
        private const float ControlMaximum = 2.0f;

        /// <summary>
        /// The editable state of one segment. Kept separate from ThespeonInputSegment because that type wants
        /// non-empty text at construction, while a segment being edited is allowed to be empty.
        /// </summary>
        private class SegmentState
        {
            public string text = string.Empty;
            public string languageLabel;
            public Dictionary<Emotion, float> startEmotion = new();
            public Dictionary<Emotion, float> endEmotion = new();
            public float startSpeed = 1f;
            public float endSpeed = 1f;
            public float startLoudness = 1f;
            public float endLoudness = 1f;
        }

        private ThespeonComponent engine;
        private AudioSource audioSource;
        private UIDocument document;

        private readonly List<float> audioData = new();
        private AudioClip audioClip;
        private int receivedSamples;
        private bool isSynthesizing;

        // Karaoke. The words of the line currently being spoken, and one marker sample index per word, in the
        // same order. Markers arrive from Thespeon before the first audio packet of the session.
        private readonly List<string> karaokeWords = new();
        private readonly List<long> karaokeMarkers = new();
        private int karaokeCursor;
        private int karaokeWordIndex = -1;
        // Sample frames of *generated* audio handed to the output so far, written on the audio thread and read on
        // the main one. Silence played while waiting for the next packet is deliberately not counted, the same way
        // Unreal's AudioStreamComponent only advances SamplesConsumed over samples it actually had: if generation
        // stalls mid-line, the reveal has to stall with it rather than run on ahead of the voice.
        private int consumedSamples;
        // Set from Synthesize and cleared by the first audio packet, so the counter starts at the line's first
        // audible sample rather than wherever the always-running streaming clip happened to be.
        private bool isAwaitingFirstPacket;
        // Time.unscaledTime at which the overlay should come down, or 0 while a line is still playing.
        private float karaokeHideTime;

        // Segment model. There is always at least one segment; the delivery controls edit the selected one.
        private readonly List<SegmentState> segments = new();
        private int selectedSegment;
        private bool isDeveloperMode;
        // Set while pushing a segment into the widgets, so the widgets' change callbacks don't write it back.
        private bool isRefreshingControls;

        // UI
        private PillDropdown characterDropdown;
        private PillDropdown moduleDropdown;
        private PillDropdown backendDropdown;
        private PillDropdown languageDropdown;
        private VisualElement portraitContainer;
        private TextField textField;
        private EmotionBlendField startEmotions;
        private EmotionBlendField endEmotions;
        private FillSlider startLoudness;
        private FillSlider endLoudness;
        private FillSlider startSpeed;
        private FillSlider endSpeed;
        private Button synthesizeButton;
        private Label statusLabel;
        private VisualElement segmentBar;
        private Label segmentCounter;
        private Button previousSegmentButton;
        private Button nextSegmentButton;
        private Button deleteSegmentButton;
        private KaraokeOverlay karaokeOverlay;

        // The languages currently offered, keyed by the text shown in the dropdown.
        private readonly Dictionary<string, ModuleLanguage> languagesByLabel = new();

        private static readonly Dictionary<string, SupportedBackendType?> BackendChoices = new()
        {
            // "Default" means: send no override at all and let the Thespeon default settings decide.
            { "Default", null },
            { "CPU", SupportedBackendType.CPU },
            { "GPU Compute", SupportedBackendType.GPUCompute }
        };

        private void Awake()
        {
            engine = GetComponent<ThespeonComponent>();
            audioSource = GetComponent<AudioSource>();

            // Build the panel settings before the UIDocument exists, so the document has everything it needs
            // the moment it is enabled.
            PanelSettings panelSettings = CreatePanelSettings();

            document = GetComponent<UIDocument>();
            if (document == null) document = gameObject.AddComponent<UIDocument>();
            if (document.panelSettings == null) document.panelSettings = panelSettings;
        }

        private void Start()
        {
            engine.OnAudioReceived += OnAudioPacketReceived;
            engine.OnAudioSampleRequestReceived += OnAudioSampleRequestReceived;
            engine.OnSynthesisComplete += OnSynthesisComplete;
            engine.OnSynthesisFailed += OnSynthesisFailed;

            // A looping streaming clip that we fill as packets arrive, so playback can start before synthesis
            // has finished.
            audioClip = AudioClip.Create("ThespeonClip", 1024, 1, OutputSampleRate, true, OnAudioRead);
            audioSource.clip = audioClip;
            audioSource.loop = true;
            audioSource.Play();

            BuildUI();
        }

        private void OnDestroy()
        {
            if (engine != null)
            {
                engine.OnAudioReceived -= OnAudioPacketReceived;
                engine.OnAudioSampleRequestReceived -= OnAudioSampleRequestReceived;
                engine.OnSynthesisComplete -= OnSynthesisComplete;
                engine.OnSynthesisFailed -= OnSynthesisFailed;
            }

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }
        }

        // ------------------------------------------------------------------ panel setup

        /// <summary>
        /// Builds a PanelSettings at runtime so the sample needs no authored assets. The theme comes from the
        /// .tss shipped alongside this script, which imports Unity's default runtime theme.
        /// </summary>
        private static PanelSettings CreatePanelSettings()
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "Advanced GUI Panel Settings";
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;

            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("AdvancedThespeonGUITheme");
            if (theme != null) settings.themeStyleSheet = theme;
            else Debug.LogWarning("Advanced GUI: could not load AdvancedThespeonGUITheme from Resources. "
                                  + "Built-in controls such as the text field may render unstyled.");

            return settings;
        }

        // ------------------------------------------------------------------ UI construction

        private void BuildUI()
        {
            VisualElement root = document.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("Advanced GUI: the UIDocument has no root element, so the panel cannot be built. "
                               + "Check that its Panel Settings are assigned.");
                return;
            }
            root.Clear();

            StyleSheet styleSheet = Resources.Load<StyleSheet>("AdvancedThespeonGUI");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            else Debug.LogError("Advanced GUI: could not load AdvancedThespeonGUI.uss from Resources. "
                                + "The panel will render without styling.");

            // The brand palette is declared as variables on this class. It has to sit on the root rather than
            // on the window, because floating menus and modals are parented here and need to inherit it.
            root.AddToClassList("lt-theme");

            VisualElement container = new();
            container.AddToClassList("lt-root");
            root.Add(container);

            VisualElement window = new();
            window.AddToClassList("lt-window");
            container.Add(window);

            window.Add(BuildRail());
            window.Add(BuildMain());

            // Sits on the container rather than inside the window, so it spans the screen and draws over the
            // panel the way the Unreal demo's subtitle overlay does.
            karaokeOverlay = new KaraokeOverlay();
            container.Add(karaokeOverlay);

            // Trickle down so the shortcut is caught before the text field, which otherwise has keyboard focus
            // and would swallow it. Mirrors the Unreal demo's preview key handling.
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.Focus();

            segments.Clear();
            segments.Add(new SegmentState
            {
                text = defaultText,
                startEmotion = new Dictionary<Emotion, float> { { Emotion.Joy, 1f } },
                endEmotion = new Dictionary<Emotion, float> { { Emotion.Fear, 1f } }
            });
            selectedSegment = 0;

            PopulateCharacters();
            RefreshControls();
            RefreshSegmentBar();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Command on macOS, where Ctrl+D is not the usual modifier.
            if (evt.keyCode != KeyCode.D || !(evt.ctrlKey || evt.commandKey)) return;

            ToggleDeveloperMode();
            evt.StopPropagation();
        }

        private VisualElement BuildRail()
        {
            VisualElement rail = new();
            rail.AddToClassList("lt-rail");

            rail.Add(Caption("CHARACTER"));
            characterDropdown = new PillDropdown("No characters imported");
            characterDropdown.ValueChanged += _ => OnCharacterChanged();
            rail.Add(characterDropdown);

            portraitContainer = new VisualElement();
            portraitContainer.AddToClassList("lt-portrait");
            rail.Add(portraitContainer);

            VisualElement spacer = new();
            spacer.AddToClassList("lt-rail__spacer");
            rail.Add(spacer);

            rail.Add(Caption("MODULE SIZE"));
            moduleDropdown = new PillDropdown("—");
            moduleDropdown.ValueChanged += _ => PopulateLanguages();
            rail.Add(moduleDropdown);

            rail.Add(Caption("COMPUTE BACKEND", spaced: true));
            backendDropdown = new PillDropdown("Default");
            backendDropdown.SetChoices(BackendChoices.Keys, preferred: "Default");
            rail.Add(backendDropdown);

            synthesizeButton = new Button(Synthesize) { text = "Synthesize" };
            synthesizeButton.AddToClassList("lt-synthesize");
            rail.Add(synthesizeButton);

            statusLabel = new Label(string.Empty);
            statusLabel.AddToClassList("lt-status");
            rail.Add(statusLabel);

            return rail;
        }

        private VisualElement BuildMain()
        {
            VisualElement main = new();
            main.AddToClassList("lt-main");

            main.Add(BuildSegmentBar());

            textField = new TextField { multiline = true, value = defaultText };
            textField.AddToClassList("lt-text");
            textField.RegisterValueChangedCallback(evt =>
            {
                if (isRefreshingControls) return;
                Selected.text = evt.newValue;
            });
            main.Add(textField);

            VisualElement delivery = new();
            delivery.AddToClassList("lt-delivery");
            main.Add(delivery);

            Label languageCaption = Caption("LANGUAGE");
            languageCaption.style.unityTextAlign = TextAnchor.MiddleCenter;
            delivery.Add(languageCaption);

            languageDropdown = new PillDropdown("—", centered: true);
            languageDropdown.ValueChanged += label =>
            {
                if (isRefreshingControls) return;
                Selected.languageLabel = label;
            };
            delivery.Add(languageDropdown);

            Label deliveryTitle = new("DELIVERY");
            deliveryTitle.AddToClassList("lt-section-title");
            delivery.Add(deliveryTitle);

            // Emotion blends, side by side.
            startEmotions = new EmotionBlendField("START EMOTIONS");
            startEmotions.Changed += () => Selected.startEmotion = startEmotions.GetBlend();
            endEmotions = new EmotionBlendField("END EMOTIONS");
            endEmotions.Changed += () => Selected.endEmotion = endEmotions.GetBlend();

            VisualElement emotionRow = new();
            emotionRow.AddToClassList("lt-columns");
            emotionRow.Add(Column("START EMOTIONS", startEmotions));
            emotionRow.Add(Column("END EMOTIONS", endEmotions));
            delivery.Add(emotionRow);

            // Loudness and speed, side by side.
            startLoudness = NewControlSlider(value => Selected.startLoudness = value);
            startSpeed = NewControlSlider(value => Selected.startSpeed = value);
            endLoudness = NewControlSlider(value => Selected.endLoudness = value);
            endSpeed = NewControlSlider(value => Selected.endSpeed = value);

            VisualElement controlRow = new();
            controlRow.AddToClassList("lt-columns");
            controlRow.style.marginTop = 22;
            controlRow.Add(Column(null,
                SliderCaption("START LOUDNESS"), startLoudness,
                SliderCaption("START SPEED"), startSpeed));
            controlRow.Add(Column(null,
                SliderCaption("END LOUDNESS"), endLoudness,
                SliderCaption("END SPEED"), endSpeed));
            delivery.Add(controlRow);

            return main;
        }

        private FillSlider NewControlSlider(Action<float> apply)
        {
            FillSlider slider = new(ControlMinimum, ControlMaximum, 1f);
            slider.ValueChanged += value =>
            {
                if (!isRefreshingControls) apply(value);
            };
            return slider;
        }

        /// <summary>
        /// The segment navigation bar. Hidden until Ctrl+D turns on developer mode.
        /// </summary>
        private VisualElement BuildSegmentBar()
        {
            segmentBar = new VisualElement();
            segmentBar.AddToClassList("lt-segment-bar");
            segmentBar.style.display = DisplayStyle.None;

            previousSegmentButton = new Button(() => SelectSegment(selectedSegment - 1)) { text = "◀" };
            previousSegmentButton.AddToClassList("lt-segment-bar__arrow");
            segmentBar.Add(previousSegmentButton);

            segmentCounter = new Label();
            segmentCounter.AddToClassList("lt-segment-bar__counter");
            segmentBar.Add(segmentCounter);

            nextSegmentButton = new Button(() => SelectSegment(selectedSegment + 1)) { text = "▶" };
            nextSegmentButton.AddToClassList("lt-segment-bar__arrow");
            segmentBar.Add(nextSegmentButton);

            Button addButton = new(CreateSegment) { text = "+ Segment" };
            addButton.AddToClassList("lt-button");
            segmentBar.Add(addButton);

            deleteSegmentButton = new Button(DeleteSegment) { text = "Delete" };
            deleteSegmentButton.AddToClassList("lt-button");
            segmentBar.Add(deleteSegmentButton);

            return segmentBar;
        }

        private static Label Caption(string text, bool spaced = false)
        {
            Label label = new(text);
            label.AddToClassList("lt-caption");
            if (spaced) label.AddToClassList("lt-caption--spaced");
            return label;
        }

        private static Label SliderCaption(string text)
        {
            Label label = new(text);
            label.AddToClassList("lt-slider-caption");
            return label;
        }

        private static VisualElement Column(string caption, params VisualElement[] children)
        {
            VisualElement column = new();
            column.AddToClassList("lt-column");

            if (caption != null)
            {
                Label label = new(caption);
                label.AddToClassList("lt-column__caption");
                column.Add(label);
            }

            foreach (VisualElement child in children) column.Add(child);
            return column;
        }

        // ------------------------------------------------------------------ segments

        /// <summary>
        /// The segment being edited. Guarantees the list is non-empty and the index in range, so callers never
        /// have to check.
        /// </summary>
        private SegmentState Selected
        {
            get
            {
                if (segments.Count == 0) segments.Add(new SegmentState());
                selectedSegment = Mathf.Clamp(selectedSegment, 0, segments.Count - 1);
                return segments[selectedSegment];
            }
        }

        /// <summary>
        /// Ctrl+D. Turning developer mode off collapses every segment's text back into the first one and drops
        /// the rest, so the panel cannot be left in a multi-segment state with the controls hidden.
        /// </summary>
        private void ToggleDeveloperMode()
        {
            isDeveloperMode = !isDeveloperMode;

            if (!isDeveloperMode && segments.Count > 1)
            {
                segments[0].text = string.Concat(segments.Select(segment => segment.text));
                segments.RemoveRange(1, segments.Count - 1);
            }

            if (!isDeveloperMode)
            {
                selectedSegment = 0;
                RefreshControls();
            }

            RefreshSegmentBar();
            SetStatus(isDeveloperMode
                ? "Developer mode on: editing segment by segment. Ctrl+D to collapse."
                : string.Empty);
        }

        private void SelectSegment(int index)
        {
            if (index < 0 || index >= segments.Count) return;

            selectedSegment = index;
            RefreshControls();
            RefreshSegmentBar();
        }

        /// <summary>Inserts an empty segment after the selected one and moves to it.</summary>
        private void CreateSegment()
        {
            int insertAt = selectedSegment + 1;
            // No emotion blend and neutral speed/loudness: an unset boundary contributes no keypoint, so the
            // new segment inherits whatever the surrounding curve is doing.
            segments.Insert(insertAt, new SegmentState { languageLabel = Selected.languageLabel });
            selectedSegment = insertAt;

            RefreshControls();
            RefreshSegmentBar();
        }

        /// <summary>
        /// Removes the selected segment, folding its text into a neighbour so nothing typed is lost. The list
        /// never becomes empty.
        /// </summary>
        private void DeleteSegment()
        {
            if (segments.Count == 0) return;

            string removedText = segments[selectedSegment].text;
            bool hasPredecessor = selectedSegment > 0;
            bool hasSuccessor = selectedSegment < segments.Count - 1;

            if (hasPredecessor) segments[selectedSegment - 1].text += removedText;
            else if (hasSuccessor) segments[selectedSegment + 1].text = removedText + segments[selectedSegment + 1].text;

            segments.RemoveAt(selectedSegment);
            // Follow the merged text back to the predecessor.
            if (hasPredecessor) selectedSegment--;
            if (segments.Count == 0) segments.Add(new SegmentState());
            selectedSegment = Mathf.Clamp(selectedSegment, 0, segments.Count - 1);

            RefreshControls();
            RefreshSegmentBar();
        }

        /// <summary>Pushes the selected segment into the widgets without triggering their change callbacks.</summary>
        private void RefreshControls()
        {
            SegmentState segment = Selected;

            isRefreshingControls = true;
            try
            {
                textField.SetValueWithoutNotify(segment.text);
                startEmotions.SetBlend(segment.startEmotion);
                endEmotions.SetBlend(segment.endEmotion);
                startSpeed.SetValueWithoutNotify(segment.startSpeed);
                endSpeed.SetValueWithoutNotify(segment.endSpeed);
                startLoudness.SetValueWithoutNotify(segment.startLoudness);
                endLoudness.SetValueWithoutNotify(segment.endLoudness);

                if (segment.languageLabel != null) languageDropdown.SetValue(segment.languageLabel, notify: false);
                else segment.languageLabel = languageDropdown.Value;
            }
            finally
            {
                isRefreshingControls = false;
            }
        }

        private void RefreshSegmentBar()
        {
            segmentBar.style.display = isDeveloperMode ? DisplayStyle.Flex : DisplayStyle.None;
            segmentCounter.text = $"SEGMENT {selectedSegment + 1} / {segments.Count}";
            previousSegmentButton.SetEnabled(selectedSegment > 0);
            nextSegmentButton.SetEnabled(selectedSegment < segments.Count - 1);
            deleteSegmentButton.SetEnabled(segments.Count > 1);
        }

        // ------------------------------------------------------------------ manifest driven choices

        private void PopulateCharacters()
        {
            List<string> characters = ManifestHandler.Instance.GetAllCharacters();
            characterDropdown.SetChoices(characters);

            if (characters == null || characters.Count == 0)
            {
                SetStatus("No character modules imported. Import a character pack first.", isError: true);
                synthesizeButton.SetEnabled(false);
            }
        }

        private void OnCharacterChanged()
        {
            RefreshPortrait();

            string character = characterDropdown.Value;
            if (string.IsNullOrEmpty(character))
            {
                moduleDropdown.SetChoices(null);
                return;
            }

            List<ModuleType> moduleTypes = ManifestHandler.Instance.GetAllModuleTypesForCharacter(character);
            // Largest first - it is the best-sounding module the user has imported.
            IEnumerable<string> labels = moduleTypes
                .Where(moduleType => moduleType != ModuleType.None)
                .OrderByDescending(moduleType => (int)moduleType)
                .Select(moduleType => moduleType.ToString());

            moduleDropdown.SetChoices(labels);
            // SetChoices only notifies when the selection actually changed, so refresh languages regardless.
            PopulateLanguages();
        }

        private void PopulateLanguages()
        {
            languagesByLabel.Clear();

            string character = characterDropdown.Value;
            if (string.IsNullOrEmpty(character) || !TryGetSelectedModuleType(out ModuleType moduleType))
            {
                languageDropdown.SetChoices(null);
                synthesizeButton.SetEnabled(false);
                SetStatus($"No module size selected (character '{character ?? "none"}', "
                          + $"module '{moduleDropdown.Value ?? "none"}'), so there are no languages to offer.",
                    isError: true);
                return;
            }

            // Guarded because this reads the manifest: a malformed or unexpected entry would otherwise throw
            // straight through the dropdown's change callback and abandon the rest of the UI setup.
            List<ModuleLanguage> supported;
            try
            {
                supported = ManifestHandler.Instance.GetAllSupportedLanguages(character, moduleType)
                            ?? new List<ModuleLanguage>();
            }
            catch (Exception exception)
            {
                languageDropdown.SetChoices(null);
                synthesizeButton.SetEnabled(false);
                SetStatus($"Could not read the languages for {character} ({moduleType}): {exception.Message}",
                    isError: true);
                Debug.LogException(exception);
                return;
            }
            foreach (ModuleLanguage language in supported)
            {
                // Two entries can share a display label; the first one wins.
                languagesByLabel.TryAdd(LanguageLabel(language), language);
            }

            // Keep the selected segment's language if this character still offers it, so switching module size
            // does not silently move the line to a different language.
            languageDropdown.SetChoices(languagesByLabel.Keys.OrderBy(label => label), preferred: Selected.languageLabel);
            synthesizeButton.SetEnabled(languageDropdown.ChoiceCount > 0 && !isSynthesizing);

            // An empty dropdown has three different causes and they are not distinguishable on screen, so say
            // which one it is. A label can go missing even when the manifest returned entries: the dropdown
            // discards blank labels, and ModuleLanguage.ToString() is blank when it has no ISO 639-2 code.
            if (languageDropdown.ChoiceCount == 0)
            {
                SetStatus($"{character} ({moduleType}): manifest returned {supported.Count} language(s), "
                          + $"{languagesByLabel.Count} distinct label(s), 0 selectable. See the console.",
                    isError: true);

                Debug.LogWarning($"Advanced GUI: no selectable language for {character} ({moduleType}). "
                                 + $"GetAllSupportedLanguages returned {supported.Count} entries: "
                                 + string.Join(", ", supported.Select(DescribeLanguage)));
                return;
            }

            SyncSegmentLanguages();
            SetStatus(string.Empty);
        }

        /// <summary>
        /// The label a language is shown and stored under. ModuleLanguage.ToString() gives "eng (US)", but it
        /// returns an empty string when the entry has no ISO 639-2 code - and a blank label would be discarded,
        /// leaving an empty dropdown with nothing to select. So fall back to whatever else identifies it.
        /// </summary>
        private static string LanguageLabel(ModuleLanguage language)
        {
            if (language == null) return "unknown";

            string label = language.ToString();
            if (!string.IsNullOrWhiteSpace(label)) return label;

            foreach (string candidate in new[] { language.Iso639_3, language.Glottocode, language.CustomDialect })
            {
                if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
            }

            return "unknown";
        }

        /// <summary>Spells out a ModuleLanguage's fields, including what its display label came out as.</summary>
        private static string DescribeLanguage(ModuleLanguage language)
        {
            if (language == null) return "<null>";
            return $"[iso639_2='{language.Iso639_2}' iso639_3='{language.Iso639_3}' "
                   + $"iso3166_1='{language.Iso3166_1}' custom='{language.CustomDialect}' "
                   + $"label='{language}']";
        }

        /// <summary>
        /// Points every segment at a language this character actually supports. Segments remember their language
        /// as a display label, and the set of valid labels is rebuilt whenever the character or module size
        /// changes - so a label that no longer exists has to be replaced rather than left to fail at synthesis.
        /// </summary>
        private void SyncSegmentLanguages()
        {
            if (languagesByLabel.Count == 0) return;

            string fallback = languageDropdown.Value ?? languagesByLabel.Keys.First();
            foreach (SegmentState segment in segments)
            {
                if (segment.languageLabel == null || !languagesByLabel.ContainsKey(segment.languageLabel))
                {
                    segment.languageLabel = fallback;
                }
            }

            // The widgets may now be showing a stale label for the selected segment.
            if (textField != null) RefreshControls();
        }

        private bool TryGetSelectedModuleType(out ModuleType moduleType)
        {
            moduleType = ModuleType.None;
            return !string.IsNullOrEmpty(moduleDropdown.Value)
                   && Enum.TryParse(moduleDropdown.Value, out moduleType)
                   && moduleType != ModuleType.None;
        }

        // ------------------------------------------------------------------ portrait

        private void RefreshPortrait()
        {
            portraitContainer.Clear();
            string character = characterDropdown.Value;

            Texture2D portrait = LoadPortrait(character);
            if (portrait != null)
            {
                Image image = new() { image = portrait, scaleMode = ScaleMode.ScaleAndCrop };
                image.AddToClassList("lt-portrait__image");
                portraitContainer.Add(image);
                return;
            }

            // Placeholder: initials, plus a hint about where a real portrait would go.
            Label initials = new(Initials(character));
            initials.AddToClassList("lt-portrait__initials");
            portraitContainer.Add(initials);

            Label hint = new(string.IsNullOrEmpty(character) ? string.Empty : $"Portraits/{Slug(character)}.png");
            hint.AddToClassList("lt-portrait__hint");
            portraitContainer.Add(hint);
        }

        /// <summary>
        /// Finds a portrait for the given character name. An explicit override wins; otherwise the name is
        /// slugged and looked up under Resources/Portraits. Returns null when nothing matches, which is what
        /// puts the placeholder on screen.
        /// </summary>
        private Texture2D LoadPortrait(string character)
        {
            if (string.IsNullOrEmpty(character)) return null;

            PortraitOverride match = portraitOverrides?.FirstOrDefault(
                entry => entry != null && entry.portrait != null
                         && string.Equals(entry.characterName, character, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.portrait;

            string slug = Slug(character);
            string[] candidates =
            {
                slug,
                // The naming the portrait pack ships with.
                $"{slug}-portrait",
                // Names that run words together in the manifest but not in the file name, or the reverse:
                // both "MonLee" and "Mon Lee" end up looking for "monlee".
                slug.Replace("-", string.Empty)
            };

            foreach (string candidate in candidates)
            {
                Texture2D portrait = Resources.Load<Texture2D>($"Portraits/{candidate}");
                if (portrait != null) return portrait;
            }

            return null;
        }

        /// <summary>Turns "Aaron Archer" into "aaron-archer".</summary>
        private static string Slug(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            char[] characters = value.Trim().ToLowerInvariant()
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray();

            return string.Join("-", new string(characters).Split('-', StringSplitOptions.RemoveEmptyEntries));
        }

        private static string Initials(string character)
        {
            if (string.IsNullOrEmpty(character)) return "?";

            string[] words = character.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string initials = string.Concat(words.Take(2).Select(word => word[0]));
            return initials.ToUpperInvariant();
        }

        // ------------------------------------------------------------------ synthesis

        private void Synthesize()
        {
            if (string.IsNullOrEmpty(characterDropdown.Value) || !TryGetSelectedModuleType(out ModuleType moduleType))
            {
                SetStatus("Select a character and a module size first.", isError: true);
                return;
            }

            if (languagesByLabel.Count == 0)
            {
                SetStatus("No languages available for this character and module size.", isError: true);
                return;
            }

            // Names the offending segment and label rather than a generic "select a language", because with
            // several segments the one at fault is not necessarily the one on screen.
            int badLanguage = segments.FindIndex(
                segment => segment.languageLabel == null || !languagesByLabel.ContainsKey(segment.languageLabel));
            if (badLanguage >= 0)
            {
                string label = segments[badLanguage].languageLabel ?? "none";
                SetStatus($"Segment {badLanguage + 1} of {segments.Count} has language '{label}', which this "
                          + "character and module size do not offer. Pick one from the language dropdown.",
                    isError: true);
                return;
            }

            if (segments.All(segment => string.IsNullOrWhiteSpace(segment.text)))
            {
                SetStatus("Nothing to say - the text is empty.", isError: true);
                return;
            }

            // Thespeon rejects an empty segment, so an empty one in the middle of the list is a mistake worth
            // naming rather than silently dropping - its boundary values would go with it.
            int emptyIndex = segments.FindIndex(segment => string.IsNullOrWhiteSpace(segment.text));
            if (emptyIndex >= 0)
            {
                SetStatus($"Segment {emptyIndex + 1} of {segments.Count} has no text.", isError: true);
                return;
            }

            // Rebuilds the word list and, with the overlay on, the marked-up text the timing comes from. Done
            // before the input is built so the segments below can use the marked text.
            List<string> markedTexts = PrepareKaraoke();

            // Each segment's start/end pairs are keypoints on curves spanning the whole input, so the values
            // stay continuous across segment boundaries: one segment's end blend meets the next one's start.
            List<ThespeonInputSegment> inputSegments = segments
                .Select((segment, index) => new ThespeonInputSegment(
                    markedTexts[index],
                    startEmotion: segment.startEmotion,
                    endEmotion: segment.endEmotion,
                    language: ResolveLanguage(segment.languageLabel),
                    isCustomPronounced: false,
                    startSpeed: segment.startSpeed,
                    endSpeed: segment.endSpeed,
                    startLoudness: segment.startLoudness,
                    endLoudness: segment.endLoudness))
                .ToList();

            ThespeonInput input = new(
                inputSegments,
                characterName: characterDropdown.Value,
                moduleType: moduleType,
                // A fallback for the case where no segment pins an emotion at all, which would otherwise leave
                // the emotion curve with no keypoints. Taken from the first blend that has one, as in Unreal.
                defaultEmotion: DominantEmotion());

            InferenceConfigOverride configOverride = null;
            if (backendDropdown.Value != null
                && BackendChoices.TryGetValue(backendDropdown.Value, out SupportedBackendType? backend)
                && backend.HasValue)
            {
                configOverride = new InferenceConfigOverride { PreferredBackendType = backend.Value.ToBackendType() };
            }

            lock (audioData) audioData.Clear();
            // Marker indices are relative to the start of the line, so the clock has to be too. Reset after the
            // buffer is emptied: with nothing real left to consume, the count cannot move again until this line's
            // first sample goes out.
            Interlocked.Exchange(ref consumedSamples, 0);
            receivedSamples = 0;
            isAwaitingFirstPacket = true;
            isSynthesizing = true;
            synthesizeButton.SetEnabled(false);
            SetStatus("Synthesizing...");

            engine.Synthesize(input, sessionID: sessionID, configOverride: configOverride);
        }

        /// <summary>
        /// Turns a stored language label back into a ModuleLanguage. Falls back to whatever the dropdown shows,
        /// which matters when a segment kept a label from a character that no longer offers that language.
        /// </summary>
        private ModuleLanguage ResolveLanguage(string label)
        {
            if (label != null && languagesByLabel.TryGetValue(label, out ModuleLanguage language)) return language;
            return languagesByLabel.TryGetValue(languageDropdown.Value ?? string.Empty, out ModuleLanguage current)
                ? current
                : null;
        }

        /// <summary>
        /// The heaviest emotion in the first segment that has a blend at all, or None when nothing is set.
        /// </summary>
        private Emotion DominantEmotion()
        {
            foreach (SegmentState segment in segments)
            {
                Dictionary<Emotion, float> blend = segment.startEmotion.Count > 0 ? segment.startEmotion : segment.endEmotion;
                if (blend.Count == 0) continue;
                return blend.OrderByDescending(entry => entry.Value).First().Key;
            }

            return Emotion.None;
        }

        private void OnAudioPacketReceived(string session, float[] data)
        {
            receivedSamples += data.Length;
            lock (audioData) audioData.AddRange(data);

            // Only used to swap the waiting animation for the line. The sample counter is reset back in
            // Synthesize, not here: the audio thread may already have taken part of this packet by now, and
            // zeroing the counter after that would put the reveal permanently behind by however much it took.
            isAwaitingFirstPacket = false;
        }

        private void OnSynthesisComplete(string session)
        {
            isSynthesizing = false;
            synthesizeButton.SetEnabled(true);
            SetStatus($"{receivedSamples / (float)OutputSampleRate:0.00}s generated.");
        }

        private void OnSynthesisFailed(string session)
        {
            isSynthesizing = false;
            isAwaitingFirstPacket = false;
            synthesizeButton.SetEnabled(true);
            SetStatus("Synthesis failed - see the console for details.", isError: true);
            HideKaraoke();
        }

        // Called on the audio thread whenever Unity needs more samples. This is also the karaoke clock: what it
        // takes out of the buffer is what the output is about to play.
        private void OnAudioRead(float[] data)
        {
            int copyLength;
            lock (audioData)
            {
                copyLength = Mathf.Min(data.Length, audioData.Count);
                audioData.CopyTo(0, data, 0, copyLength);
                audioData.RemoveRange(0, copyLength);
                if (copyLength < data.Length) Array.Fill(data, 0f, copyLength, data.Length - copyLength);
            }

            // Only the real samples, never the zero-fill: see consumedSamples.
            if (copyLength > 0) Interlocked.Add(ref consumedSamples, copyLength);
        }

        // ------------------------------------------------------------------ karaoke overlay

        /// <summary>
        /// Resets the karaoke state for a new line, puts the speaker and the line on the overlay, and returns the
        /// text to send for each segment: the same text with an audio sample request marker in front of every
        /// word, which is what makes Thespeon report where each word begins in the audio.
        ///
        /// With the overlay off the segment texts are returned untouched, so nothing about the synthesis changes.
        /// </summary>
        private List<string> PrepareKaraoke()
        {
            karaokeWords.Clear();
            karaokeMarkers.Clear();
            karaokeCursor = 0;
            karaokeWordIndex = -1;
            karaokeHideTime = 0f;

            if (!showKaraokeOverlay)
            {
                karaokeOverlay?.Hide();
                return segments.Select(segment => segment.text).ToList();
            }

            List<string> markedTexts = new(segments.Count);
            foreach (SegmentState segment in segments)
            {
                string[] words = KaraokeOverlay.SplitWords(segment.text);
                karaokeWords.AddRange(words);
                markedTexts.Add(string.Join(" ", words.Select(word => ControlCharacters.AudioSampleRequest + word)));
            }

            if (karaokeOverlay != null)
            {
                karaokeOverlay.SetSpeaker(characterDropdown.Value, LoadPortrait(characterDropdown.Value));
                karaokeOverlay.SetLine(karaokeWords);
                karaokeOverlay.Show();
            }

            return markedTexts;
        }

        private void OnAudioSampleRequestReceived(string session, long[] sampleIndices)
        {
            if (sampleIndices != null) karaokeMarkers.AddRange(sampleIndices);
        }

        /// <summary>
        /// Advances the overlay to wherever playback has reached. Markers hold the sample index each word starts
        /// at, so the word being spoken is the last marker already passed, and how far into it we are comes from
        /// the distance to the next marker.
        /// </summary>
        private void Update()
        {
            if (karaokeOverlay == null || !showKaraokeOverlay) return;

            if (karaokeHideTime > 0f)
            {
                if (Time.unscaledTime >= karaokeHideTime) HideKaraoke();
                return;
            }

            if (karaokeWords.Count == 0) return;

            // Nothing has been heard yet, so there is no position to reveal to. The markers for the whole line
            // have usually already arrived by now - advancing on them here would reveal the line before a word
            // of it has been spoken - so the bubble shows a dotted animation until the first packet lands.
            if (isAwaitingFirstPacket)
            {
                int dots = 1 + Mathf.FloorToInt(Time.unscaledTime * WaitingDotsPerSecond) % WaitingDotCount;
                karaokeOverlay.SetWaiting(new string('.', dots));
                return;
            }

            long audible = GetPlaybackSampleIndex();
            while (karaokeCursor < karaokeMarkers.Count && audible >= karaokeMarkers[karaokeCursor])
            {
                karaokeWordIndex = karaokeCursor;
                karaokeCursor++;
            }

            karaokeOverlay.SetProgress(karaokeWordIndex, WordFraction(audible));

            // Everything generated has been played out, and nothing more is coming.
            bool isDrained;
            lock (audioData) isDrained = audioData.Count == 0;
            if (!isSynthesizing && !isAwaitingFirstPacket && isDrained && receivedSamples > 0)
            {
                karaokeOverlay.RevealAll();
                karaokeHideTime = Time.unscaledTime + Mathf.Max(0f, karaokeLingerSeconds);
            }
        }

        /// <summary>
        /// How far through the word being spoken playback has got, in 0-1, from its own marker to the next word's.
        /// The overlay uses it to bring the following word in gradually. The last word has no marker after it to
        /// measure against, so it runs to the end of the audio generated so far.
        /// </summary>
        private float WordFraction(long audible)
        {
            if (karaokeWordIndex < 0 || karaokeWordIndex >= karaokeMarkers.Count) return 0f;

            long start = karaokeMarkers[karaokeWordIndex];
            long end = karaokeWordIndex + 1 < karaokeMarkers.Count
                ? karaokeMarkers[karaokeWordIndex + 1]
                : receivedSamples;

            if (end <= start) return 1f;
            return Mathf.Clamp01((audible - start) / (float)(end - start));
        }

        /// <summary>
        /// The sample index that is audible right now: the generated samples taken out of the buffer, less the
        /// buffers between there and the speaker that have not been played yet. Same shape as Unreal's
        /// UAudioStreamComponent::GetPlaybackSampleIndex(CompensationSamples).
        /// </summary>
        private long GetPlaybackSampleIndex()
        {
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
            long audible = Volatile.Read(ref consumedSamples)
                           - (long)bufferLength * bufferCount
                           - karaokeLatencyCompensationSamples;
            return audible < 0 ? 0 : audible;
        }

        private void HideKaraoke()
        {
            karaokeHideTime = 0f;
            karaokeOverlay?.Hide();
        }

        // ------------------------------------------------------------------ status

        private void SetStatus(string message, bool isError = false)
        {
            if (statusLabel == null) return;
            statusLabel.text = message;
            statusLabel.EnableInClassList("lt-status--error", isError);
        }
    }
}
