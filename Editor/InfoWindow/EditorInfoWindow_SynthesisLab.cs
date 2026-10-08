// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Lingotion.Thespeon.Core;
using Lingotion.Thespeon.Core.IO;
using Lingotion.Thespeon.Inputs;
using Lingotion.Thespeon.Inference;
using Lingotion.Thespeon.Utils;
using Unity.EditorCoroutines.Editor;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Lingotion.Thespeon.Editor
{
    public partial class EditorInfoWindow
    {
        private EditorInputContainer _editorInput;
        private readonly List<float> _audioData = new();
        private bool _isSynthesizing = false;
        private const string DefaultWavOutputPath = "Assets/Audio Test Lab Output.wav";
        private string _outputWavPath = DefaultWavOutputPath;
        private ListView _characterListView;
        private readonly Dictionary<string, ModuleLanguage> languageMappings = new();

        private const string SynthLabStateFolder = "UserSettings";
        private const string SynthLabStateFileName = "LingotionThespeonSynthLab.json";
        private static string SynthLabStatePath => Path.Combine(SynthLabStateFolder, SynthLabStateFileName);

        [Serializable]
        private struct SerializedSegment
        {
            public string text;
            public string emotion;
            public bool isCustomPronounced;
            public string languageJson;
        }

        [Serializable]
        private struct SerializedContainer
        {
            public string key;
            public List<SerializedSegment> segments;
        }

        [Serializable]
        private struct SerializedSynthLabState
        {
            public bool hasInput;
            public List<SerializedSegment> segments;
            public List<SerializedContainer> containers;
            public string outputWavPath;
        }

        /// <summary>
        /// Persists the Synthesis Lab input state to a per-project file so it survives
        /// domain reloads (entering Play Mode), data changes, and window close/reopen.
        /// </summary>
        private void SaveSynthesisLabState()
        {
            var state = new SerializedSynthLabState { outputWavPath = _outputWavPath };
            if (_editorInput != null)
            {
                state.hasInput = true;
                state.segments = SerializeSegments(_editorInput.segments);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SynthLabStatePath));
                File.WriteAllText(SynthLabStatePath, JsonUtility.ToJson(state));
            }
            catch (Exception e)
            {
                LingotionLogger.Warning($"Failed to save Synthesis Lab input state: {e.Message}");
            }
        }

        /// <summary>
        /// Restores the Synthesis Lab input state previously saved by <see cref="SaveSynthesisLabState"/>.
        /// </summary>
        private void LoadSynthesisLabState()
        {
            if (!File.Exists(SynthLabStatePath))
            {
                return;
            }

            try
            {
                var state = JsonUtility.FromJson<SerializedSynthLabState>(File.ReadAllText(SynthLabStatePath));

                if (!string.IsNullOrWhiteSpace(state.outputWavPath))
                {
                    _outputWavPath = state.outputWavPath;
                }

                List<SerializedSegment> segments;
                if (state.hasInput)
                {
                    segments = state.segments;
                }
                else if (state.containers != null && state.containers.Count > 0)
                {
                    segments = state.containers[0].segments;
                }
                else
                {
                    return;
                }

                // Loading replaces in-memory state; destroy the existing container first so the
                // HideAndDontSave instance it holds is not leaked.
                DestroyEditorInput();
                _editorInput = CreateEditorInput();
                _editorInput.segments = DeserializeSegments(segments);
            }
            catch (Exception e)
            {
                LingotionLogger.Warning($"Failed to load Synthesis Lab input state: {e.Message}");
            }
        }

        private static List<SerializedSegment> SerializeSegments(List<ThespeonInputSegment> segments)
        {
            var serialized = new List<SerializedSegment>();
            if (segments == null)
            {
                return serialized;
            }

            foreach (var segment in segments)
            {
                if (segment == null)
                {
                    continue;
                }

                serialized.Add(new SerializedSegment
                {
                    text = segment.Text,
                    emotion = segment.Emotion.ToString(),
                    isCustomPronounced = segment.IsCustomPronounced,
                    languageJson = segment.Language?.ToJson()
                });
            }
            return serialized;
        }

        private static List<ThespeonInputSegment> DeserializeSegments(List<SerializedSegment> serialized)
        {
            var segments = new List<ThespeonInputSegment>();
            if (serialized == null)
            {
                return segments;
            }

            foreach (var seg in serialized)
            {
                Enum.TryParse(seg.emotion, out Emotion emotion);
                ModuleLanguage language = ParseModuleLanguage(seg.languageJson);
                var restored = new ThespeonInputSegment(" ", language, emotion, seg.isCustomPronounced)
                {
                    Text = seg.text ?? string.Empty
                };
                segments.Add(restored);
            }
            return segments;
        }

        private static EditorInputContainer CreateEditorInput()
        {
            var container = CreateInstance<EditorInputContainer>();
            container.hideFlags = HideFlags.HideAndDontSave;
            return container;
        }

        /// <summary>
        /// Destroys the in-memory input container. Required because it is a HideAndDontSave
        /// instance that is not reclaimed by the GC.
        /// </summary>
        private void DestroyEditorInput()
        {
            if (_editorInput != null)
            {
                DestroyImmediate(_editorInput);
            }
            _editorInput = null;
        }

        /// <summary>
        /// Returns the name of the language a segment is shown and generated in for the current selection:
        /// the segment's own language when the selection supports it, otherwise the closest one it does.
        /// </summary>
        /// <param name="language">The language stored on the segment.</param>
        /// <returns>A key of <see cref="languageMappings"/>, or null if the selection has no languages.</returns>
        private string ResolveLanguageKey(ModuleLanguage language)
        {
            if (languageMappings.Count == 0)
            {
                return null;
            }

            string exactKey = languageMappings.FirstOrDefault(x => x.Value.Equals(language)).Key;
            if (exactKey != null)
            {
                return exactKey;
            }

            if (language == null)
            {
                return languageMappings.Keys.First();
            }

            ModuleLanguage best = ModuleLanguage.BestMatch(languageMappings.Values.ToList(), language.Iso639_2, language.Iso3166_1);
            return languageMappings.First(x => x.Value == best).Key;
        }

        private static ModuleLanguage ParseModuleLanguage(string languageJson)
        {
            if (string.IsNullOrEmpty(languageJson)) return null;
            try
            {
                JObject o = JObject.Parse(languageJson);
                string iso639_2 = (string)o["iso639_2"];
                if (string.IsNullOrEmpty(iso639_2)) return null;
                return new ModuleLanguage(
                    iso639_2,
                    (string)o["iso639_3"],
                    (string)o["glottocode"],
                    (string)o["customdialect"],
                    (string)o["iso3166_1"],
                    (string)o["iso3166_2"]);
            }
            catch
            {
                return null;
            }
        }

        private void RefreshSynthesisLab()
        {
            if (_characterListView == null) return;

            var allCharacters = ManifestHandler.Instance.GetAllCharacters();
            _characterListView.itemsSource = allCharacters;
            if (allCharacters.Count > 0 && _characterListView.selectedIndex < 0)
                _characterListView.selectedIndex = 0;
            else if (allCharacters.Count == 0)
                _characterListView.ClearSelection();
        }

        private VisualElement CreateSynthesisLabTab()
        {
            var result = new VisualElement { style = { flexGrow = 1 } };
            var uxml = LoadUXML("SynthesisLabTab.uxml");
            uxml.CloneTree(result);

            _characterListView = result.Q<ListView>("CharacterListView");
            var inferenceEditorPane = result.Q<VisualElement>("InferenceEditorPane");
            var characterInfoSection = result.Q<VisualElement>("CharacterInfoSection");

            var characterMaskField = result.Q<MaskField>("CharacterMaskField");
            var layersEnumChoices = new List<string>(Enum.GetNames(typeof(ModuleType)));
            layersEnumChoices.RemoveAt(0);
            characterMaskField.choices = layersEnumChoices;
            int allMask = (1 << Enum.GetValues(typeof(ModuleType)).Length) - 1;
            characterMaskField.value = allMask;

            characterMaskField.RegisterValueChangedCallback(evt =>
            {
                var filtered = ManifestHandler.Instance.GetAllCharacters()
                    .Where(character =>
                    {
                        var characterTypes = ManifestHandler.Instance.GetAllModuleTypesForCharacter(character);
                        return characterTypes.Any(characterType => (evt.newValue & (1 << (int)characterType - 1)) != 0);
                    })
                    .ToList();
                _characterListView.itemsSource = filtered;
                _characterListView.Rebuild();
            });

            _characterListView.selectedIndicesChanged += _ =>
            {
                characterInfoSection.Clear();
                if (_characterListView.selectedItem == null)
                    return;

                var selectedCharacterName = _characterListView.selectedItem.ToString();
                var currentModuleSizes = ManifestHandler.Instance.GetAllModuleTypesForCharacter(selectedCharacterName);

                var characterNameLabel = CreateSelectableLabel(selectedCharacterName);
                characterNameLabel.style.fontSize = 14;
                characterNameLabel.style.alignSelf = Align.Center;
                characterNameLabel.style.marginTop = 5;

                var characterTitleSeparator = new VisualElement();
                characterTitleSeparator.AddToClassList("section-separator");

                var characterSpecificInfoContainer = new ScrollView()
                {
                    style =
                    {
                        marginLeft = 5,
                        marginRight = 5,
                    }
                };

                var characterModuleSizeList = new VisualElement();
                characterModuleSizeList.Add(new Label("Imported module sizes:"));
                characterModuleSizeList.style.whiteSpace = WhiteSpace.Normal;
                characterModuleSizeList.style.unityFontStyleAndWeight = FontStyle.Normal;
                characterModuleSizeList.style.marginTop = 5;

                foreach (var moduleType in currentModuleSizes)
                {
                    string moduleVersion = ManifestHandler.Instance.GetCharacterModuleVersion(selectedCharacterName, moduleType);
                    characterModuleSizeList.Add(new Label($"{moduleType} (v{moduleVersion})") { style = { whiteSpace = WhiteSpace.Normal } });
                }

                var modulesSection = new VisualElement();
                modulesSection.style.marginTop = 10;
                var modulesHeader = new Label("Modules")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 14,
                        marginBottom = 5
                    }
                };
                modulesSection.Add(modulesHeader);

                foreach (var moduleType in currentModuleSizes)
                {
                    string moduleVersion = ManifestHandler.Instance.GetCharacterModuleVersion(selectedCharacterName, moduleType);
                    var moduleFoldout = new Foldout { text = $"Module: {moduleType} (v{moduleVersion})", value = false };
                    moduleFoldout.style.marginLeft = 5;

                    var versionContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = 5, marginTop = 4 } };
                    versionContainer.Add(new Label("Version:") { style = { unityFontStyleAndWeight = FontStyle.Italic, marginTop = 2 } });
                    versionContainer.Add(CreateSelectableLabel(moduleVersion));
                    moduleFoldout.Add(versionContainer);

                    var languagesForModule = ManifestHandler.Instance.GetAllSupportedLanguageCodes(selectedCharacterName, moduleType);
                    if (languagesForModule.Count > 0)
                    {
                        var languagesLabel = new Label("Supported Languages:") { style = { unityFontStyleAndWeight = FontStyle.Italic } };
                        languagesLabel.style.marginTop = 4;
                        languagesLabel.style.marginLeft = 5;
                        moduleFoldout.Add(languagesLabel);

                        foreach (var langNameCodePair in languagesForModule)
                        {
                            var dialects = ManifestHandler.Instance.GetAllDialectsInModuleLanguage(selectedCharacterName, moduleType, langNameCodePair.Value);
                            var langContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = 15 } };
                            langContainer.Add(new Label("• " + langNameCodePair.Key + ":") { style = { unityFontStyleAndWeight = FontStyle.Italic, marginTop = 2 } });
                            langContainer.Add(CreateSelectableLabel(langNameCodePair.Value));
                            moduleFoldout.Add(langContainer);

                            foreach (var langModule in dialects)
                            {
                                var accentContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = 25 } };
                                var dialectsLabel = new Label("  Dialect: ") { style = { unityFontStyleAndWeight = FontStyle.Italic } };
                                dialectsLabel.style.marginTop = 2;
                                dialectsLabel.style.marginLeft = 5;
                                accentContainer.Add(dialectsLabel);
                                accentContainer.Add(CreateSelectableLabel(langModule.Value.Iso3166_1));
                                moduleFoldout.Add(accentContainer);
                            }
                        }
                    }
                    modulesSection.Add(moduleFoldout);
                }

                UpdateInferenceTestingWindow(inferenceEditorPane, currentModuleSizes);

                characterSpecificInfoContainer.Add(characterModuleSizeList);
                characterSpecificInfoContainer.Add(modulesSection);

                characterInfoSection.Add(characterNameLabel);
                characterInfoSection.Add(characterTitleSeparator);
                characterInfoSection.Add(characterSpecificInfoContainer);
            };

            return result;
        }

        private void UpdateInferenceTestingWindow(VisualElement editorPane, List<ModuleType> currentModuleSizes)
        {
            editorPane.Clear();
            Toolbar characterToolbar = new();
            string characterName = _characterListView.selectedItem.ToString();
            ToolbarMenu moduleSizeSelectMenu = new() { text = $"Select model size for {characterName}..." };
            var currentEditingStatus = new Label()
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    alignSelf = Align.Center,
                    marginLeft = 5,
                }
            };

            var inputEditingPane = new ScrollView();

            foreach (ModuleType type in currentModuleSizes)
            {
                string moduleTypeName = type.ToString();
                moduleSizeSelectMenu.menu.AppendAction(moduleTypeName, (a) =>
                {
                    moduleSizeSelectMenu.text = moduleTypeName;
                    currentEditingStatus.text = "Editing input for " + characterName + " " + moduleTypeName + ":";
                    UpdateEditingPane(inputEditingPane, characterName, moduleTypeName);
                });
            }

            if (currentModuleSizes.Count > 0)
            {
                string firstModule = currentModuleSizes[0].ToString();
                moduleSizeSelectMenu.text = firstModule;
                currentEditingStatus.text = "Editing input for " + characterName + " " + firstModule + ":";
                UpdateEditingPane(inputEditingPane, characterName, firstModule);
            }

            characterToolbar.Add(moduleSizeSelectMenu);
            characterToolbar.Add(currentEditingStatus);

            editorPane.Add(characterToolbar);
            editorPane.Add(inputEditingPane);
        }

        private void UpdateEditingPane(ScrollView editingPane, string characterName, string moduleType)
        {
            editingPane.Clear();

            if (_editorInput == null)
            {
                _editorInput = CreateEditorInput();
            }
            var currentInputContainer = _editorInput;

            var outputPathRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginLeft = 5, marginTop = 5, marginRight = 5 }
            };
            var outputPathField = new TextField("Output file")
            {
                value = _outputWavPath,
                tooltip = "Path the generated WAV is written to. Type a path or use Browse…",
                style = { flexGrow = 1 }
            };
            outputPathField.RegisterValueChangedCallback(evt => _outputWavPath = evt.newValue);
            var browseButton = new Button(() =>
            {
                string current = outputPathField.value;
                string startDir = string.IsNullOrWhiteSpace(current) ? "Assets" : (Path.GetDirectoryName(current) ?? "Assets");
                string defaultName = string.IsNullOrWhiteSpace(current) ? "Audio Test Lab Output" : Path.GetFileNameWithoutExtension(current);
                string chosen = EditorUtility.SaveFilePanel("Save generated audio", startDir, defaultName, "wav");
                if (!string.IsNullOrEmpty(chosen))
                    outputPathField.value = chosen;
            })
            { text = "Browse…" };
            outputPathRow.Add(outputPathField);
            outputPathRow.Add(browseButton);

            var runInferenceButton = new Button(() =>
            {
                if (_isSynthesizing)
                    return;

                if (currentInputContainer.segments == null ||
                    !currentInputContainer.segments.Any(segment => segment != null && !string.IsNullOrWhiteSpace(segment.Text)))
                {
                    LingotionLogger.Error("Cannot generate audio: please add at least one input segment with text before generating.");
                    return;
                }

                _outputWavPath = string.IsNullOrWhiteSpace(outputPathField.value) ? DefaultWavOutputPath : outputPathField.value;
                _isSynthesizing = true;
                try
                {
                    List<ThespeonInputSegment> segments = currentInputContainer.segments
                        .Where(segment => segment != null)
                        .Select(segment =>
                        {
                            var copy = new ThespeonInputSegment(segment);
                            string langKey = ResolveLanguageKey(segment.Language);
                            if (langKey != null)
                            {
                                copy.Language = languageMappings[langKey];
                            }
                            return copy;
                        })
                        .ToList();
                    ThespeonInput input = new(segments, characterName, Enum.Parse<ModuleType>(moduleType));
                    ThespeonInference inferenceSession = new("", OutputPacketHandler);
                    InferenceConfig config = new()
                    {
                        TargetBudgetTime = 0.01f,
                        TargetFrameTime = 0.1f
                    };

                    this.StartCoroutine(inferenceSession.Infer(input, config, false));
                }
                catch (Exception e)
                {
                    LingotionLogger.Error($"Failed to start audio synthesis: {e.Message}");
                    ResetSynthesisState();
                }
            })
            {
                text = "Generate audio"
            };

            void RefreshGenerateButtonState()
            {
                bool hasSegments = currentInputContainer.segments != null && currentInputContainer.segments.Count > 0;
                runInferenceButton.SetEnabled(hasSegments);
                runInferenceButton.tooltip = hasSegments ? null : "Add at least one input segment to generate audio.";
            }
            RefreshGenerateButtonState();

            var segmentEditor = new VisualElement();
            UpdateSegmentEditorWindow(segmentEditor, currentInputContainer.segments, characterName, moduleType, RefreshGenerateButtonState);

            editingPane.Add(outputPathRow);
            editingPane.Add(runInferenceButton);
            editingPane.Add(segmentEditor);
        }

        private void UpdateSegmentEditorWindow(VisualElement segmentEditorWindow, List<ThespeonInputSegment> currentInputSegments, string characterName, string moduleTypeString, Action onSegmentsChanged = null)
        {
            segmentEditorWindow.Clear();
            segmentEditorWindow.Add(new Label("Input segments:")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginLeft = 5,
                    marginTop = 5,
                    marginBottom = 5,
                }
            });

            ListView segmentView = new ListView
            {
                reorderable = true,
                showBorder = true,
                itemsSource = currentInputSegments,
                fixedItemHeight = 50,
            };

            var listToolbar = new Toolbar();
            listToolbar.style.flexDirection = FlexDirection.Row;
            listToolbar.style.minHeight = 23;

            var addButton = new Button(() =>
            {
                currentInputSegments.Add(new ThespeonInputSegment("New Segment", language: "eng", emotion: Emotion.Interest));
                segmentView.Rebuild();
                onSegmentsChanged?.Invoke();
            })
            {
                text = "+",
                style = { width = 20 }
            };

            var removeButton = new Button(() =>
            {
                if (segmentView.selectedIndex >= 0 && segmentView.selectedIndex < currentInputSegments.Count)
                {
                    currentInputSegments.RemoveAt(segmentView.selectedIndex);
                    segmentView.Rebuild();
                    onSegmentsChanged?.Invoke();
                }
            })
            {
                text = "-",
                style = { width = 20 }
            };

            var clearButton = new Button(() =>
            {
                currentInputSegments.Clear();
                segmentView.Rebuild();
                onSegmentsChanged?.Invoke();
            })
            { text = "Clear" };

            listToolbar.Add(addButton);
            listToolbar.Add(removeButton);
            listToolbar.Add(clearButton);

            languageMappings.Clear();
            if (!Enum.TryParse(moduleTypeString, out ModuleType moduleType))
                throw new ArgumentException("Invalid module type found.");

            Dictionary<string, ModuleLanguage> languageChoices = ManifestHandler.Instance.GetAllLanguagesForCharacterAndModuleType(characterName, moduleType);
            List<string> dropdownItems = new();
            foreach ((string name, ModuleLanguage lang) in languageChoices)
            {
                languageMappings[name] = lang;
                dropdownItems.Add(name);
            }
            dropdownItems.Sort();

            segmentView.makeItem = () =>
            {
                var segmentContainer = new VisualElement { style = { flexDirection = FlexDirection.Column } };
                var dropdownContainer = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var segmentTextFieldContainer = new VisualElement { style = { flexGrow = 1 } };

                var inputTextField = new TextField
                {
                    name = "inputTextField",
                    maxLength = 200,
                    style = { paddingTop = 4, paddingBottom = 4, height = 26 }
                };

                var textInput = inputTextField.Q("unity-text-input");
                textInput.style.whiteSpace = WhiteSpace.Normal;
                textInput.style.overflow = Overflow.Hidden;
                textInput.style.unityTextAlign = TextAnchor.UpperLeft;

                if (!Enum.TryParse(moduleTypeString, out ModuleType innerModuleType))
                    throw new ArgumentException("Invalid module type found.");

                Dictionary<string, ModuleLanguage> innerLanguageChoices = ManifestHandler.Instance.GetAllLanguagesForCharacterAndModuleType(characterName, innerModuleType);
                List<string> innerDropdownItems = new();
                foreach ((string name, ModuleLanguage item) in innerLanguageChoices)
                {
                    languageMappings[name] = item;
                    innerDropdownItems.Add(name);
                }
                innerDropdownItems.Sort();

                DropdownField languageDropdown = new DropdownField
                {
                    name = "languageDropdown",
                    choices = innerDropdownItems,
                    value = innerDropdownItems.Count > 0 ? innerDropdownItems[0] : null,
                };

                List<string> emotionChoices = Enum.GetNames(typeof(Emotion)).ToList();
                emotionChoices.RemoveAt(0);
                emotionChoices.Sort();

                DropdownField emotionsDropdown = new DropdownField
                {
                    name = "emotionsDropdown",
                    choices = emotionChoices,
                    value = emotionChoices[0],
                };

                Toggle IPAtoggle = new()
                {
                    name = "IPA toggle",
                    text = "IPA",
                    tooltip = "Mark this segment as IPA text.",
                    value = false,
                    style =
                    {
                        marginLeft = 5,
                        marginTop = 5,
                        marginBottom = 5,
                    }
                };

                segmentTextFieldContainer.Add(inputTextField);
                dropdownContainer.Add(languageDropdown);
                dropdownContainer.Add(emotionsDropdown);
                dropdownContainer.Add(IPAtoggle);
                segmentContainer.Add(dropdownContainer);
                segmentContainer.Add(segmentTextFieldContainer);
                return segmentContainer;
            };

            segmentView.bindItem = (item, index) =>
            {
                var segment = currentInputSegments[index];
                var textField = item.Q<TextField>("inputTextField");
                var emotionsDropdown = item.Q<DropdownField>("emotionsDropdown");
                var languageDropdown = item.Q<DropdownField>("languageDropdown");
                var IPAtoggle = item.Q<Toggle>("IPA toggle");

                textField.UnregisterValueChangedCallback(OnSegmentTextChange);
                emotionsDropdown.UnregisterValueChangedCallback(OnEmotionChange);
                languageDropdown.UnregisterValueChangedCallback(OnLanguageChange);
                IPAtoggle.UnregisterValueChangedCallback(OnIPAToggle);

                textField.SetValueWithoutNotify(segment.Text);
                emotionsDropdown.SetValueWithoutNotify(segment.Emotion.ToString());

                languageDropdown.SetValueWithoutNotify(ResolveLanguageKey(segment.Language));
                IPAtoggle.SetValueWithoutNotify(segment.IsCustomPronounced);

                item.userData = segment;

                textField.RegisterValueChangedCallback(OnSegmentTextChange);
                emotionsDropdown.RegisterValueChangedCallback(OnEmotionChange);
                languageDropdown.RegisterValueChangedCallback(OnLanguageChange);
                IPAtoggle.RegisterValueChangedCallback(OnIPAToggle);
            };

            segmentView.unbindItem = (item, index) =>
            {
                item.Q<TextField>("inputTextField").UnregisterValueChangedCallback(OnSegmentTextChange);
                item.Q<DropdownField>("emotionsDropdown").UnregisterValueChangedCallback(OnEmotionChange);
                item.Q<DropdownField>("languageDropdown").UnregisterValueChangedCallback(OnLanguageChange);
            };

            segmentEditorWindow.Add(listToolbar);
            segmentEditorWindow.Add(segmentView);
        }

        private static void OnSegmentTextChange(ChangeEvent<string> evt)
        {
            if (evt.target is TextField textField && textField.parent.parent.userData is ThespeonInputSegment segment)
                segment.Text = evt.newValue;
        }

        private static void OnEmotionChange(ChangeEvent<string> evt)
        {
            if (evt.target is DropdownField dropdown && dropdown.parent.parent.userData is ThespeonInputSegment segment
                && Enum.TryParse(evt.newValue, out Emotion parsed))
                segment.SetEmotion(parsed);
        }

        private void OnLanguageChange(ChangeEvent<string> evt)
        {
            if (evt.target is DropdownField dropdown && dropdown.parent.parent.userData is ThespeonInputSegment segment
                && languageMappings.TryGetValue(evt.newValue, out ModuleLanguage moduleLang))
                segment.Language = moduleLang;
        }

        private static void OnIPAToggle(ChangeEvent<bool> evt)
        {
            if (evt.target is Toggle toggle && toggle.parent.parent.userData is ThespeonInputSegment segment)
                segment.IsCustomPronounced = evt.newValue;
        }
                private void ResetSynthesisState()
        {
            _audioData.Clear();
            InferenceResourceCleanup.CleanupResources();
            _isSynthesizing = false;
        }

        private void OutputPacketHandler(ThespeonDataPacket packet)
        {
            switch (packet.CallbackType)
            {
                case SynthCallbackType.CB_AUDIO:
                    bool isFinalPacket = false;
                    // Check for "is_final" in metadata
                    if (packet.Metadata.TryGetValue("is_final", out PacketMetadataValue isFinalVal))
                        isFinalVal.TryGet(out isFinalPacket);

                    if (!packet.Payload.TryGet(out float[] audioSamples))
                    {
                        LingotionLogger.Error($"Editor audio synthesis failed! Faulty audio packet payload received, ignoring.");
                        return;
                    }
                    _audioData.AddRange(audioSamples);
                    if (isFinalPacket)
                    {
                        CreateAndSelectWav(_audioData.ToArray(), _outputWavPath);
                        LingotionLogger.Debug("final audio data packet received, audio synthesis complete.");
                        ResetSynthesisState();
                    }
                    break;

                case SynthCallbackType.CB_ERROR:
                    if (packet.Payload.TryGet(out string errorMsgValue))
                        LingotionLogger.Error($"Editor audio synthesis failed! Error packet received from session with message:\n {errorMsgValue}");
                    else
                        LingotionLogger.Error($"Editor audio synthesis failed! Empty error packet received from session.");
                    ResetSynthesisState();
                    break;

                default:
                    break;
            }
        }

        private static void CreateAndSelectWav(float[] data, string path = DefaultWavOutputPath)
        {
            if (string.IsNullOrWhiteSpace(path))
                path = DefaultWavOutputPath;

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Debug.Log($"Directory did not exist, creating directory: {directory}");
                    Directory.CreateDirectory(directory);
                }
                WavExporter.SaveWav(path, data);
            }
            catch (Exception e)
            {
                LingotionLogger.Error($"Editor audio synthesis failed! Could not save WAV file to path '{path}': {e.Message}");
                return;
            }

            if (!File.Exists(path))
            {
                LingotionLogger.Error("Editor audio synthesis failed! Could not save WAV file to path: " + path);
                return;
            }

            string assetPath = ToProjectRelativePath(path);
            if (assetPath == null)
            {
                Debug.Log($"Generated audio saved to: {Path.GetFullPath(path)}");
                return;
            }

            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
            {
                LingotionLogger.Error("Editor audio synthesis failed! Failed to load AudioClip at path: " + assetPath);
                return;
            }

            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }

        /// <summary>
        /// Returns the path expressed relative to the project root (e.g. "Assets/...") if it lies
        /// inside the project's Assets folder, otherwise null. Accepts both project-relative and
        /// absolute paths.
        /// </summary>
        private static string ToProjectRelativePath(string path)
        {
            // dataPath is "<project>/Assets"; the project root is its parent.
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');

            if (!fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                return null;

            string relative = fullPath.Substring(projectRoot.Length + 1);
            return relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || relative.Equals("Assets", StringComparison.OrdinalIgnoreCase)
                ? relative
                : null;
        }
    }
}
