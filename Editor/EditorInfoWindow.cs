// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Lingotion.Thespeon.Core;
using System.Collections.Generic;
using System;
using Lingotion.Thespeon.Inputs;
using Lingotion.Thespeon.Utils;
using Lingotion.Thespeon.Inference;
using Unity.EditorCoroutines.Editor;
using System.IO;
using System.Linq;
using Lingotion.Thespeon.Core.IO;

namespace Lingotion.Thespeon.Editor
{

    /// <summary>
    /// Allows user to import, delete, and see an overview of imported files.
    /// </summary>
    /// 
    [Serializable]
    public class EditorInfoWindow : EditorWindow
    {
        private Dictionary<string, EditorInputContainer> _editorInputs = new();
        private List<float> _audioData = new();
        private bool _isSynthesizing = false;

        private ListView _importedCharacterListView;
        private ListView _importedLanguageListView; 
        private ListView _characterListView;
        private TextField _licenseField;
        private VisualElement _functionalRoot; // everything except the license field
        private VisualElement _licenseRoot;


        private HelpBox _missingLanguageHelpBox;
        private HelpBox _downloadGuideHelpBox;

        private Dictionary<string, ModuleLanguage> languageMappings = new();

        private void OnEnable()
        {
            ManifestHandler.OnDataChanged -= UpdateDynamicData;
            ManifestHandler.OnDataChanged += UpdateDynamicData;
            EditorLicenseKeyValidator.OnValidationComplete -= GateValidationResult;
            EditorLicenseKeyValidator.OnValidationComplete += GateValidationResult;

        }

        private void OnDisable()
        {
            ManifestHandler.OnDataChanged -= UpdateDynamicData;
            EditorLicenseKeyValidator.OnValidationComplete -= GateValidationResult;
        }

        /// <summary>
        /// Reveals the Editor Info window.
        /// </summary>
        [MenuItem("Window/Lingotion/Thespeon Info")]
        public static void ShowWindow()
        {
            var window = GetWindow<EditorInfoWindow>("Lingotion Thespeon");
            window.titleContent = new GUIContent("Lingotion Thespeon");
            window.minSize = new Vector2(400, 400);
        }

        /// <summary>
        /// Creates the GUI skeleton.
        /// </summary>
        public void CreateGUI()
        {
            _importedCharacterListView = new();
            _importedLanguageListView = new();
            _characterListView = new();
            _missingLanguageHelpBox = new();
            _downloadGuideHelpBox = new();

            SetupGUI();
            UpdateDynamicData();
            EditorApplication.delayCall += ValidateAndGate;
        }

        /// <summary>
        /// Creates the GUI structure and binds dynamic data to it.
        /// </summary>
        private void SetupGUI()
        {
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            // --- License key root alternative to Thespeon Info Window functionality ---
            _licenseRoot = new VisualElement { style = { flexDirection = FlexDirection.Column } };

            _licenseField = new TextField("License Key")
            {
                isPasswordField = false, // set true if you want to hide characters
                tooltip = "Add your Lingotion Project's license key here. Your license key is required to use Lingotion Thespeon."
            };
            _licenseField.SetValueWithoutNotify(EditorLicenseKeyValidator.LoadLicenseFromFile());

            _licenseField.RegisterCallback<FocusOutEvent>(_ =>
            {
                ValidateAndGate();
            });

            var licenseKeyHelpBox = new HelpBox("Add your Lingotion Project's license key below. Your license key is required to use Lingotion Thespeon. \nTo get one please click here or go to https://portal.lingotion.com/", HelpBoxMessageType.Error);


            var licenseKeyHelpBoxInternalLabel = licenseKeyHelpBox.Query<Label>().Class(HelpBox.labelUssClassName).First();
            licenseKeyHelpBox.pickingMode = PickingMode.Position;
            licenseKeyHelpBox.RegisterCallback<MouseEnterEvent>(_ => licenseKeyHelpBoxInternalLabel.style.color = new Color(0.4f, 0.7f, 1f, 1f));
            licenseKeyHelpBox.RegisterCallback<MouseLeaveEvent>(_ => licenseKeyHelpBoxInternalLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 1f));
            licenseKeyHelpBox.RegisterCallback<MouseUpEvent>(_ => Application.OpenURL("https://portal.lingotion.com/"));

            _licenseRoot.Add(licenseKeyHelpBox);
            _licenseRoot.Add(_licenseField);
            rootVisualElement.Add(_licenseRoot);


            // --- Thespeon Info Window Functionality appears only if License Key is Valid ---
            _functionalRoot = new VisualElement { name = "FunctionalRoot", style = { flexGrow = 1, flexDirection = FlexDirection.Column } };

            var thespeonWindowToolbar = new Toolbar();
            var toggleOverviewTab = new ToolbarToggle { text = "Overview" };
            var toggleSynthesisLabTab = new ToolbarToggle { text = "Synthesis Lab" };


            var thespeonWindowContent = new VisualElement();
            thespeonWindowContent.style.flexGrow = 1;
            thespeonWindowContent.name = "Page container";
            thespeonWindowContent.style.flexDirection = FlexDirection.Column;

            VisualElement importedTabContent = CreateOverviewTab();
            VisualElement synthesisLabTabContent = CreateSynthesisLabTab();

            toggleOverviewTab.value = true;
            importedTabContent.style.display = DisplayStyle.Flex;
            synthesisLabTabContent.style.display = DisplayStyle.None;

            toggleOverviewTab.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    toggleSynthesisLabTab.SetValueWithoutNotify(false);
                    importedTabContent.style.display = DisplayStyle.Flex;
                    synthesisLabTabContent.style.display = DisplayStyle.None;
                }
                else
                {
                    toggleOverviewTab.value = true;
                }
            });

            toggleSynthesisLabTab.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    toggleOverviewTab.SetValueWithoutNotify(false);
                    importedTabContent.style.display = DisplayStyle.None;
                    synthesisLabTabContent.style.display = DisplayStyle.Flex;
                }
                else
                {
                    toggleSynthesisLabTab.value = true;
                }
            });

            thespeonWindowContent.Add(importedTabContent);
            thespeonWindowContent.Add(synthesisLabTabContent);

            thespeonWindowToolbar.Add(toggleOverviewTab);
            thespeonWindowToolbar.Add(toggleSynthesisLabTab);

            _functionalRoot.Add(thespeonWindowToolbar);
            _functionalRoot.Add(thespeonWindowContent);

            rootVisualElement.Add(_functionalRoot);

        }
        
        /// <summary>
        /// Updates the UI elements that are dependent on mutable external data
        /// </summary>
        private void UpdateDynamicData()
        {
            var allCharacters = ManifestHandler.Instance.GetAllCharacters();
            _characterListView.itemsSource = allCharacters;
            if (allCharacters.Count > 0 && _characterListView.selectedIndex < 0)
            {
                _characterListView.selectedIndex = 0;
            }
            else if (allCharacters.Count == 0)
            {
                _characterListView.ClearSelection();
            }

            _importedCharacterListView.itemsSource = ManifestHandler.Instance.GetAllCharacterNames();
            _importedLanguageListView.itemsSource = ManifestHandler.Instance.GetAllLanguageNames();
            var missing = ManifestHandler.Instance.GetMissingLanguages();
            if (missing.Count > 0)
            {
                _missingLanguageHelpBox.text = "You need to import the following languages before you continue: \n " + string.Join(", ", missing);
                _missingLanguageHelpBox.style.display = DisplayStyle.Flex;
            }
            else
            {
                _missingLanguageHelpBox.style.display = DisplayStyle.None;
            }
        }

        private VisualElement CreateOverviewTab()
        {
            VisualElement result = new();
            result.name = "Overview Tab";
            result.style.flexGrow = 1;

            var infoContainer = new VisualElement();
            infoContainer.style.flexDirection = FlexDirection.Column;
            infoContainer.style.justifyContent = Justify.SpaceBetween;
            infoContainer.style.minHeight = 106;

            _downloadGuideHelpBox.text = "To download Lingotion files, please click here or go to: https://portal.lingotion.com/";
            _downloadGuideHelpBox.messageType = HelpBoxMessageType.Info;


            var helpBoxInternalLabel = _downloadGuideHelpBox.Query<Label>().Class(HelpBox.labelUssClassName).First();
            _downloadGuideHelpBox.pickingMode = PickingMode.Position;
            _downloadGuideHelpBox.RegisterCallback<MouseEnterEvent>(_ => helpBoxInternalLabel.style.color = new Color(0.4f, 0.7f, 1f, 1f));
            _downloadGuideHelpBox.RegisterCallback<MouseLeaveEvent>(_ => helpBoxInternalLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 1f));
            _downloadGuideHelpBox.RegisterCallback<MouseUpEvent>(_ => Application.OpenURL("https://portal.lingotion.com/"));

            _missingLanguageHelpBox.messageType = HelpBoxMessageType.Error;

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;

            var importButton = new Button(() =>
            {
                EditorImporter.ImportThespeon();
            })
            { text = "Import" };

            var deleteButton = new Button(() =>
            {
                int selectedLanguageIndex = _importedLanguageListView.selectedIndex;
                int selectedCharacterIndex = _importedCharacterListView.selectedIndex;
                VisualElement rootElement;
                string selectedName;
                if (selectedLanguageIndex >= 0 && selectedLanguageIndex < _importedLanguageListView.itemsSource.Count)
                {
                    selectedName = _importedLanguageListView.selectedItem as string;


                    rootElement = _importedLanguageListView.GetRootElementForIndex(selectedLanguageIndex);
                }
                else if (selectedCharacterIndex >= 0 && selectedCharacterIndex < _importedCharacterListView.itemsSource.Count)
                {
                    selectedName = _importedCharacterListView.selectedItem as string;

                    rootElement = _importedCharacterListView.GetRootElementForIndex(selectedCharacterIndex);

                }
                else
                {
                    return;
                }

                if (rootElement is not VisualElement container)
                {
                    return;
                }


                var labelTexts = container.Query<Label>().ToList()
                    .Skip(1)
                    .Select(label => label.text)
                    .Where(text => !string.IsNullOrWhiteSpace(text));

                string labelSummary = string.Join("\n", labelTexts);

                bool confirm = EditorUtility.DisplayDialog(
                    "Confirm Deletion",
                    $"Are you sure you want to delete:\n\n\"{selectedName}\"\n\nfrom disk?\n\nThis will delete the following:\n{labelSummary}",
                    "Delete",
                    "Cancel"
                );

                if (confirm)
                {
                    string configFilename = ManifestHandler.Instance.GetConfigFilename(selectedName);
                    if (!string.IsNullOrEmpty(configFilename))
                    {
                        EditorImporter.DeleteModule(configFilename);
                    }
                    else
                    {
                        LingotionLogger.Error($"Delete failed! Import manifest is out of sync. Try recompiling your project or contact support if the issue persists. Faulty entry: {selectedName}");
                    }
                }

                Repaint();
            })
            { text = "Delete" };
            deleteButton.SetEnabled(false);

            var regenerateInputsButton = new Button(() =>
            {
                ManifestHandler.Instance.UpdateMappings();
            })
            { text = "Regenerate Input Assets" };

            var characterHeaderBar = new Toolbar();
            var characterHeaderLabel = new Label("Imported Characters")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    alignSelf = Align.Center,
                    marginLeft = 5,
                }
            };
            characterHeaderBar.style.marginTop = 10;
            characterHeaderBar.style.height = 21;
            var characterScrollView = new ScrollView();
            characterScrollView.style.minHeight = 83;
            characterScrollView.style.maxHeight = 3*83;
            characterScrollView.style.marginLeft = 5;

            _importedCharacterListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            _importedCharacterListView.makeItem = () =>
            {
                var itemContainer = new Box();
                itemContainer.style.marginBottom = 4;
                itemContainer.focusable = false;
                itemContainer.style.flexGrow = 1;
                return itemContainer;
            };

            _importedCharacterListView.bindItem = (element, index) =>
            {
                string name = _importedCharacterListView.itemsSource[index] as string;

                var listElement = (Box)element;

                listElement.Clear();
                var nameLabel = new Label("• " + name);
                nameLabel.style.fontSize = 14;
                nameLabel.style.marginTop = 3;
                nameLabel.style.marginBottom = 4;
                nameLabel.style.marginLeft = 3;
                listElement.Add(nameLabel);
                foreach (var item in ManifestHandler.Instance.GetAllModuleInfoInCharacter(name))
                {
                    var sublabel = new Label($"- {item}");
                    sublabel.style.marginLeft = 10;
                    listElement.Add(sublabel);
                }
            };

            _importedCharacterListView.unbindItem = (element, index) =>
            {
                var container = (Box)element;
                container.Clear();
            };

            _importedCharacterListView.selectedIndicesChanged += (selectedItem) =>
            {
                if (_importedCharacterListView.selectedIndex >= 0)
                {
                    _importedLanguageListView.ClearSelection();
                    deleteButton.SetEnabled(true);
                }
            };

            var languageHeaderBar = new Toolbar();
            var languageHeaderLabel = new Label("Imported Languages")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    alignSelf = Align.Center,
                    marginLeft = 5,
                }
            };
            languageHeaderBar.style.marginTop = 10;

            languageHeaderBar.style.maxHeight = 21;
            var languageScrollView = new ScrollView();
            languageScrollView.style.minHeight = 83;
            languageScrollView.style.maxHeight = 100;
            languageScrollView.style.flexGrow = 1;
            languageScrollView.style.marginLeft = 5;

            _importedLanguageListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            _importedLanguageListView.makeItem = () =>
            {
                var itemContainer = new Box();
                itemContainer.style.marginBottom = 4;
                itemContainer.focusable = false;
                itemContainer.style.flexGrow = 1;
                return itemContainer;
            };

            _importedLanguageListView.bindItem = (element, index) =>
            {
                string languageName = _importedLanguageListView.itemsSource[index] as string;

                var listElement = (Box)element;

                listElement.Clear();
                var nameLabel = new Label("• "+languageName);
                nameLabel.style.fontSize = 14;
                nameLabel.style.marginTop = 3;
                nameLabel.style.marginBottom = 4;
                nameLabel.style.marginLeft = 3;
                listElement.Add(nameLabel);
                foreach (var item in ManifestHandler.Instance.GetAllModuleInfoInLanguage(languageName))
                {
                    var sublabel = new Label($"- {item}");
                    sublabel.style.marginLeft = 10;
                    listElement.Add(sublabel);
                }
            };

            _importedLanguageListView.unbindItem = (element, index) =>
            {
                var container = (Box)element;
                container.Clear();
            };

            _importedLanguageListView.selectedIndicesChanged += (selectedItem) =>
            {
                if (_importedLanguageListView.selectedIndex >= 0)
                {
                    _importedCharacterListView.ClearSelection();
                    deleteButton.SetEnabled(true);
                }
            };
            infoContainer.Add(_downloadGuideHelpBox);
            infoContainer.Add(_missingLanguageHelpBox);
            infoContainer.Add(buttonContainer);

            buttonContainer.Add(importButton);
            buttonContainer.Add(deleteButton);
            buttonContainer.Add(regenerateInputsButton);

            characterHeaderBar.Add(characterHeaderLabel);
            characterScrollView.Add(_importedCharacterListView);

            languageHeaderBar.Add(languageHeaderLabel);
            languageScrollView.Add(_importedLanguageListView);

            result.Add(infoContainer);
            result.Add(characterHeaderBar);
            result.Add(characterScrollView);
            result.Add(languageHeaderBar);
            result.Add(languageScrollView);
            return result;
        }

        private VisualElement CreateSynthesisLabTab()
        {
            VisualElement result = new()
            {
                name = "Synthesis Lab tab",
                style =
                {
                    flexGrow = 1,
                }
            };


            TwoPaneSplitView characterListEditorSplit = new()
            {
                orientation = TwoPaneSplitViewOrientation.Vertical
            };

            VisualElement segmentEditor = new()
            {
                style =
                {
                    minHeight = 120,
                }
            };
            Toolbar segmentEditorToolbar = new();
            segmentEditorToolbar.Add(new Label("Audio Test Lab")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    alignSelf = Align.Center,
                    marginLeft = 5,
                }
            });
            segmentEditor.Add(segmentEditorToolbar);


            VisualElement inferenceEditorPane = new();
            TwoPaneSplitView characterInfoPane = CreateCharacterInfoPane(inferenceEditorPane);

            characterInfoPane.style.minHeight = 120;

            segmentEditor.Add(inferenceEditorPane);

            characterListEditorSplit.Add(characterInfoPane);
            characterListEditorSplit.Add(segmentEditor);
            characterListEditorSplit.fixedPaneInitialDimension = 200;

            result.Add(characterListEditorSplit);
            return result;
        }

        private TwoPaneSplitView CreateCharacterInfoPane(VisualElement editorPane)
        {
            TwoPaneSplitView result = new()
            {
                fixedPaneInitialDimension = 210
            };

            VisualElement characterListPane = new()
            {
                style =
                {
                    minWidth = 210
                }
            };

            var characterListToolbar = new Toolbar();
            MaskField characterMaskField = new("Module type filter")
            {
                focusable = false,
            };
            var layersEnumChoices = new List<string>(Enum.GetNames(typeof(ModuleType)));
            layersEnumChoices.RemoveAt(0);
            characterMaskField.choices = layersEnumChoices;
            int allMask = (1 << Enum.GetValues(typeof(ModuleType)).Length) - 1;
            characterMaskField.value = allMask;
            
            characterMaskField.RegisterValueChangedCallback(evt =>
            {

                var selectedTypes = Enum.GetValues(typeof(ModuleType))
                .Cast<ModuleType>()
                .Skip(1)
                .Where(t => (evt.newValue & (1 << (int)t)) != 0)
                .ToList();

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

            var characterInfoPane = new VisualElement();
            var characterInfoToolbar = new Toolbar();
            var characterInfoHeader = new Label("Character Information")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    alignSelf = Align.Center,
                    marginLeft = 5,
                }
            };

            var characterInfoSection = new VisualElement();

            _characterListView.selectedIndicesChanged += (selectedItem) =>
            {
                characterInfoSection.Clear();
                if(_characterListView.selectedItem == null)
                {
                    return;
                }
                var selectedCharacterName = _characterListView.selectedItem.ToString();

                var characterNameLabel = CreateSelectableLabel(selectedCharacterName);
                characterNameLabel.style.fontSize = 14;
                characterNameLabel.style.alignSelf = Align.Center;
                characterNameLabel.style.marginTop = 5;

                var characterTitleSeparator = new VisualElement();
                characterTitleSeparator.style.height = 1;
                characterTitleSeparator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
                characterTitleSeparator.style.marginTop = 4;
                characterTitleSeparator.style.marginBottom = 4;
                characterTitleSeparator.style.flexGrow = 1;

                var characterSpecificInfoContainer = new ScrollView()
                {
                    style =
                    {
                        marginLeft = 5,
                        marginRight = 5,
                    }};

                var currentModuleSizes = ManifestHandler.Instance.GetAllModuleTypesForCharacter(selectedCharacterName);
                var characterModuleSizeList = new VisualElement();

                characterModuleSizeList.Add(new Label("Imported module sizes:"));
                characterModuleSizeList.style.whiteSpace = WhiteSpace.Normal;
                characterModuleSizeList.style.unityFontStyleAndWeight = FontStyle.Normal;
                characterModuleSizeList.style.marginTop = 5;

                foreach (var moduleType in currentModuleSizes)
                    characterModuleSizeList.Add(new Label($"{moduleType}")
                    {
                        style =
                        {
                            whiteSpace = WhiteSpace.Normal,
                        }
                    });  

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
                    var moduleFoldout = new Foldout() { text = $"Module: {moduleType}" };
                    moduleFoldout.value = false;
                    moduleFoldout.style.marginLeft = 5;
                

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
                            langContainer.Add(new Label("• "+ langNameCodePair.Key + ":"){ style = { unityFontStyleAndWeight = FontStyle.Italic, marginTop = 2 } });
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
  
                
                UpdateInferenceTestingWindow(editorPane, currentModuleSizes);

                characterSpecificInfoContainer.Add(characterModuleSizeList);
                characterSpecificInfoContainer.Add(modulesSection);

                characterInfoSection.Add(characterNameLabel);
                characterInfoSection.Add(characterTitleSeparator);
                characterInfoSection.Add(characterSpecificInfoContainer);
            };

            characterListToolbar.Add(characterMaskField);
            characterListPane.Add(characterListToolbar);
            characterListPane.Add(_characterListView);

            characterInfoToolbar.Add(characterInfoHeader);
            characterInfoPane.Add(characterInfoToolbar);
            characterInfoPane.Add(characterInfoSection);

            result.Add(characterListPane);
            result.Add(characterInfoPane);
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

            string inputIndexer = characterName + moduleType;
            if (!_editorInputs.TryGetValue(inputIndexer, out var currentInputContainer))
            {
                currentInputContainer = CreateInstance<EditorInputContainer>();
                _editorInputs[inputIndexer] = currentInputContainer;
            }

            SerializedObject currentSerializedContainer = new(currentInputContainer);

            PropertyField speedPropField = new(currentSerializedContainer.FindProperty("speed"))
            {
                label = "Speed"
            };
            PropertyField loudnessPropField = new(currentSerializedContainer.FindProperty("loudness"))
            {
                label = "Loudness"
            };

            speedPropField.Bind(currentSerializedContainer);
            loudnessPropField.Bind(currentSerializedContainer);

            var runInferenceButton = new Button(() =>
            {
                if (!_isSynthesizing)
                {
                    _isSynthesizing = true;
                    ThespeonInput input = new(currentInputContainer.segments, characterName, Enum.Parse<ModuleType>(moduleType))
                    {
                        Speed = currentInputContainer.speed,
                        Loudness = currentInputContainer.loudness
                    };
                    ThespeonInference inferenceSession = new("", OutputPacketHandler);
                    InferenceConfig config = new()
                    {
                        TargetBudgetTime = 0.01f,
                        TargetFrameTime = 0.1f
                    };

                    this.StartCoroutine(inferenceSession.Infer(input, config, false));
                }


            })
            {
                text = "Generate audio"
            };

            var segmentEditor = new VisualElement();

            UpdateSegmentEditorWindow(segmentEditor, currentInputContainer.segments, characterName, moduleType);

            editingPane.Add(runInferenceButton);
            editingPane.Add(speedPropField);
            editingPane.Add(loudnessPropField);
            editingPane.Add(segmentEditor);
        }
        private void UpdateSegmentEditorWindow(VisualElement segmentEditorWindow, List<ThespeonInputSegment> currentInputSegments, string characterName, string moduleTypeString)
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

            ListView segmentView = new ListView();
            segmentView.reorderable = true;
            segmentView.showBorder = true;
            segmentView.itemsSource = currentInputSegments;
            segmentView.fixedItemHeight = 50;
            
            var listToolbar = new Toolbar();
            listToolbar.style.flexDirection = FlexDirection.Row;
            listToolbar.style.minHeight = 23;

            var addButton = new Button(() =>
            {
                currentInputSegments.Add(new ThespeonInputSegment("New Segment", language: "eng", emotion: Emotion.Interest));  // or pass defaults
                segmentView.Rebuild();
            })
            {
                text = "+",
                style =
                {
                    width = 20
                }
             };

            var removeButton = new Button(() =>
            {
                if (segmentView.selectedIndex >= 0 && segmentView.selectedIndex < currentInputSegments.Count)
                {
                    currentInputSegments.RemoveAt(segmentView.selectedIndex);
                    segmentView.Rebuild();
                }
            })
            {
                text = "-",
                style =
                {
                    width = 20
                }
             };

            var clearButton = new Button(() =>
            {
                currentInputSegments.Clear();
                segmentView.Rebuild();
            })
            { text = "Clear" };

            listToolbar.Add(addButton);
            listToolbar.Add(removeButton);
            listToolbar.Add(clearButton);

            
            languageMappings.Clear();
            if (!Enum.TryParse(moduleTypeString, out ModuleType moduleType))
            {
                throw new ArgumentException("Invalid module type found.");
            }
            Dictionary<string, ModuleLanguage> languageChoices = ManifestHandler.Instance.GetAllLanguagesForCharacterAndModuleType(characterName, moduleType);
            List<string> dropdownItems = new();
            foreach ((string name, ModuleLanguage lang) in languageChoices)
            {
                string mappingString = name;
                languageMappings[mappingString] = lang;
                dropdownItems.Add(mappingString);
            }
            dropdownItems.Sort();

            segmentView.makeItem = () =>
            {
                var segmentContainer = new VisualElement { style = { flexDirection = FlexDirection.Column } };
                var dropdownContainer = new VisualElement{ style = { flexDirection = FlexDirection.Row } };
                var segmentTextFieldContainer = new VisualElement{ style = { flexGrow = 1 } };

                var inputTextField = new TextField
                {
                    name = "inputTextField",
                    maxLength = 200
                };
                inputTextField.style.paddingTop = 4;
                inputTextField.style.paddingBottom = 4;

                var textInput = inputTextField.Q("unity-text-input");
                textInput.style.whiteSpace = WhiteSpace.Normal;
                textInput.style.overflow = Overflow.Hidden;
                textInput.style.whiteSpace = WhiteSpace.Normal;
                textInput.style.overflow = Overflow.Hidden;
                textInput.style.unityTextAlign = TextAnchor.UpperLeft;
                inputTextField.style.height = 26;

                DropdownField languageDropdown = new DropdownField();
                languageDropdown.name = "languageDropdown";
                if (!Enum.TryParse(moduleTypeString, out ModuleType moduleType))
                {
                    throw new ArgumentException("Invalid module type found.");
                }
                Dictionary<string, ModuleLanguage> languageChoices = ManifestHandler.Instance.GetAllLanguagesForCharacterAndModuleType(characterName, moduleType);
                List<string> dropdownItems = new();
                foreach ((string name, ModuleLanguage item) in languageChoices)
                {
                    string mappingString = name;
                    languageMappings[mappingString] = item;
                    dropdownItems.Add(mappingString);
                }
                dropdownItems.Sort();
                languageDropdown.choices = dropdownItems;
                if (dropdownItems.Count > 0)
                {
                    languageDropdown.value = dropdownItems[0];
                }

                DropdownField emotionsDropdown = new DropdownField();
                emotionsDropdown.name = "emotionsDropdown";
                List<string> emotionChoices = Enum.GetNames(typeof(Emotion)).ToList();
                emotionChoices.RemoveAt(0);
                emotionChoices.Sort();
                emotionsDropdown.choices = emotionChoices;
                emotionsDropdown.value = emotionChoices[0];

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

                string langKey = languageMappings.FirstOrDefault(x => x.Value.Equals(segment.Language)).Key;
                if (string.IsNullOrEmpty(langKey))
                {
                    langKey = languageMappings.Keys.FirstOrDefault();
                    if (langKey != null) segment.Language = languageMappings[langKey];
                }
                languageDropdown.SetValueWithoutNotify(langKey);
                IPAtoggle.SetValueWithoutNotify(segment.IsCustomPronounced);


                item.userData = segment;

                textField.RegisterValueChangedCallback(OnSegmentTextChange);
                emotionsDropdown.RegisterValueChangedCallback(OnEmotionChange);
                languageDropdown.RegisterValueChangedCallback(OnLanguageChange);
                IPAtoggle.RegisterValueChangedCallback(OnIPAToggle);
            };

            segmentView.unbindItem = (item, index) =>
            {
                var textField = item.Q<TextField>("inputTextField");
                var emotionsDropdown = item.Q<DropdownField>("emotionsDropdown");
                var languageDropdown = item.Q<DropdownField>("languageDropdown");

                textField.UnregisterValueChangedCallback(OnSegmentTextChange);
                emotionsDropdown.UnregisterValueChangedCallback(OnEmotionChange);
                languageDropdown.UnregisterValueChangedCallback(OnLanguageChange);
            };

            segmentEditorWindow.Add(listToolbar);
            segmentEditorWindow.Add(segmentView);
        }
        private void OnSegmentTextChange(ChangeEvent<string> evt)
        {
            var textField = evt.target as TextField;
            var segment = textField?.parent.parent.userData as ThespeonInputSegment;
            if (segment != null)
            {
                segment.Text = evt.newValue;
            }
        }

        private void OnEmotionChange(ChangeEvent<string> evt)
        {
            var dropdown = evt.target as DropdownField;
            var segment = dropdown?.parent.parent.userData as ThespeonInputSegment;
            if (segment != null && Enum.TryParse(evt.newValue, out Emotion parsed))
            {
                segment.Emotion = parsed;
            }
        }

        private void OnLanguageChange(ChangeEvent<string> evt)
        {
            var dropdown = evt.target as DropdownField;
            var segment = dropdown?.parent.parent.userData as ThespeonInputSegment;
            if (segment != null && languageMappings.TryGetValue(evt.newValue, out ModuleLanguage moduleLang))
            {
                segment.Language = moduleLang;
            }
        }

        private void OnIPAToggle(ChangeEvent<bool> evt)
        {
            var toggle = evt.target as Toggle;
            if (toggle?.parent.parent.userData is ThespeonInputSegment segment)
            {
                segment.IsCustomPronounced = evt.newValue;
            }
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
                    if(packet.Metadata.TryGetValue("is_final", out PacketMetadataValue isFinalVal))
                    {
                        isFinalVal.TryGet(out isFinalPacket);
                    }
                    if(!packet.Payload.TryGet(out float[] audioSamples))
                    {
                        LingotionLogger.Error($"Editor audio synthesis failed! Faulty audio packet payload received, ignoring.");
                        return;
                    }
                    _audioData.AddRange(audioSamples);
                    if (isFinalPacket)
                    {
                        CreateAndSelectWav(_audioData.ToArray());
                        LingotionLogger.Debug("final audio data packet received, audio synthesis complete.");
                        ResetSynthesisState();
                    }
                    
                    break;
                case SynthCallbackType.CB_ERROR:
                    if(packet.Payload.TryGet(out string errorMsgValue))
                    {
                        LingotionLogger.Error($"Editor audio synthesis failed! Error packet received from session with message:\n {errorMsgValue}");
                    } else
                    {
                        LingotionLogger.Error($"Editor audio synthesis failed! Empty error packet received from session.");
                    }
                    ResetSynthesisState();
                    break;

                default:
                    break;
            }
        }

        private static void CreateAndSelectWav(float[] data)
        {
            string path = "Assets/Audio Test Lab Output.wav";
            WavExporter.SaveWav(path, data);

            if (!File.Exists(path))
            {
                LingotionLogger.Error("Editor audio synthesis failed! Could not save WAV file to path: " + path);
                return;
            }

            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                LingotionLogger.Error("Editor audio synthesis failed! Failed to load AudioClip at path: " + path);
                return;
            }

            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");

        }

        private static TextField CreateSelectableLabel(string labelText)
        {
            TextField result = new();
            result.isReadOnly = true;
            var textInput = result.Q("unity-text-input");
            textInput.style.unityFontStyleAndWeight = FontStyle.Normal;
            textInput.style.backgroundColor = new Color(0, 0, 0, 0);
            textInput.style.borderBottomWidth = 0;
            textInput.style.borderTopWidth = 0;
            textInput.style.borderLeftWidth = 0;
            textInput.style.borderRightWidth = 0;
            textInput.style.paddingLeft = 0;
            textInput.style.paddingRight = 0;
            textInput.pickingMode = PickingMode.Position;
            labelText += '\u200B'; 
            result.SetValueWithoutNotify(labelText);
            return result;
        }
        private void ToggleValid(bool enabled)
        {
            // _functionalRoot?.SetEnabled(enabled);
            _functionalRoot.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            _licenseRoot.style.display = enabled ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void GateValidationResult(EditorLicenseKeyValidator.ValidationResult result)
        {
            switch (result)
            {
                case EditorLicenseKeyValidator.ValidationResult.Valid:
                    ToggleValid(true);
                    break;

                case EditorLicenseKeyValidator.ValidationResult.Invalid:
                    ToggleValid(false);
                    break;

                case EditorLicenseKeyValidator.ValidationResult.Indeterminate:
                    // Do nothing — keep current state (e.g., connectivity/server issue)
                    break;
            }
        }
        private async void ValidateAndGate()
        {
            string licenseKey = _licenseField.value.Trim();
            EditorLicenseKeyValidator.SaveLicenseToFile(licenseKey);
            var result = await EditorLicenseKeyValidator.ValidateLicenseAsync(licenseKey);
            GateValidationResult(result);
        }

        private struct LanguageOption
        {
            public string languageKey;
            public ModuleLanguage languageObject;

            public LanguageOption(string key, ModuleLanguage variant)
            {
                languageKey = key;
                languageObject = variant;
            }

            public override string ToString()
            {
                return $"{languageKey} - {languageObject}";
            }
        }
        
    }
}
