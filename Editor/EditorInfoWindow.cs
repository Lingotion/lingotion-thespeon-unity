// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

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
    /// Allows user to import, delete, and see an overview of imported packs.
    /// </summary>
    /// 
    [Serializable]
    public class EditorInfoWindow : EditorWindow
    {
        private Dictionary<string, EditorInputContainer> _editorInputs = new();
        private List<float> _audioData = new();
        private bool _isSynthesizing = false;

        private ListView _actorPackListView;
        private ListView _languagePackListView; 
        private ListView _actorListView;
        private TextField _licenseField;
        private VisualElement _functionalRoot; // everything except the license field
        private VisualElement _licenseRoot;


        private HelpBox _missingPackHelpBox;
        private HelpBox _downloadGuideHelpBox;

        private Dictionary<string, ModuleLanguage> languageMappings = new();

        private void OnEnable()
        {
            PackManifestHandler.OnDataChanged -= UpdateDynamicData;
            PackManifestHandler.OnDataChanged += UpdateDynamicData;
            EditorLicenseKeyValidator.OnValidationComplete -= GateValidationResult;
            EditorLicenseKeyValidator.OnValidationComplete += GateValidationResult;

        }

        private void OnDisable()
        {
            PackManifestHandler.OnDataChanged -= UpdateDynamicData;
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
            _actorPackListView = new();
            _languagePackListView = new();
            _actorListView = new();
            _missingPackHelpBox = new();
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
            var togglePackOverviewTab = new ToolbarToggle { text = "Installed Packs" };
            var toggleActorsTab = new ToolbarToggle { text = "Actors" };


            var thespeonWindowContent = new VisualElement();
            thespeonWindowContent.style.flexGrow = 1;
            thespeonWindowContent.name = "Page container";
            thespeonWindowContent.style.flexDirection = FlexDirection.Column;

            VisualElement packOverviewTabContent = CreatePackOverviewTab();
            VisualElement actorsTabContent = CreateActorsTab();

            togglePackOverviewTab.value = true;
            packOverviewTabContent.style.display = DisplayStyle.Flex;
            actorsTabContent.style.display = DisplayStyle.None;

            togglePackOverviewTab.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    toggleActorsTab.SetValueWithoutNotify(false);
                    packOverviewTabContent.style.display = DisplayStyle.Flex;
                    actorsTabContent.style.display = DisplayStyle.None;
                }
                else
                {
                    togglePackOverviewTab.value = true;
                }
            });

            toggleActorsTab.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    togglePackOverviewTab.SetValueWithoutNotify(false);
                    packOverviewTabContent.style.display = DisplayStyle.None;
                    actorsTabContent.style.display = DisplayStyle.Flex;
                }
                else
                {
                    toggleActorsTab.value = true;
                }
            });

            thespeonWindowContent.Add(packOverviewTabContent);
            thespeonWindowContent.Add(actorsTabContent);

            thespeonWindowToolbar.Add(togglePackOverviewTab);
            thespeonWindowToolbar.Add(toggleActorsTab);

            _functionalRoot.Add(thespeonWindowToolbar);
            _functionalRoot.Add(thespeonWindowContent);

            rootVisualElement.Add(_functionalRoot);

        }
        
        /// <summary>
        /// Updates the UI elements that are dependent on mutable external data
        /// </summary>
        private void UpdateDynamicData()
        {
            var allActors = PackManifestHandler.Instance.GetAllActors();
            _actorListView.itemsSource = allActors;
            if (allActors.Count > 0 && _actorListView.selectedIndex < 0)
            {
                _actorListView.selectedIndex = 0;
            }
            else if (allActors.Count == 0)
            {
                _actorListView.ClearSelection();
            }

            _actorPackListView.itemsSource = PackManifestHandler.Instance.GetAllActorPackNames();
            _languagePackListView.itemsSource = PackManifestHandler.Instance.GetAllLanguagePackNames();
            var missing = PackManifestHandler.Instance.GetMissingLanguagePacks();
            if (missing.Count > 0)
            {
                _missingPackHelpBox.text = "You need to import the following language packs before you continue: \n " + string.Join(", ", missing);
                _missingPackHelpBox.style.display = DisplayStyle.Flex;
            }
            else
            {
                _missingPackHelpBox.style.display = DisplayStyle.None;
            }
        }

        private VisualElement CreatePackOverviewTab()
        {
            VisualElement result = new();
            result.name = "Pack Overview Tab";
            result.style.flexGrow = 1;

            var infoContainer = new VisualElement();
            infoContainer.style.flexDirection = FlexDirection.Column;
            infoContainer.style.justifyContent = Justify.SpaceBetween;
            infoContainer.style.minHeight = 106;

            _downloadGuideHelpBox.text = "To download Actor Packs and Language Packs, please click here or go to: https://portal.lingotion.com/";
            _downloadGuideHelpBox.messageType = HelpBoxMessageType.Info;


            var helpBoxInternalLabel = _downloadGuideHelpBox.Query<Label>().Class(HelpBox.labelUssClassName).First();
            _downloadGuideHelpBox.pickingMode = PickingMode.Position;
            _downloadGuideHelpBox.RegisterCallback<MouseEnterEvent>(_ => helpBoxInternalLabel.style.color = new Color(0.4f, 0.7f, 1f, 1f));
            _downloadGuideHelpBox.RegisterCallback<MouseLeaveEvent>(_ => helpBoxInternalLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 1f));
            _downloadGuideHelpBox.RegisterCallback<MouseUpEvent>(_ => Application.OpenURL("https://portal.lingotion.com/"));

            _missingPackHelpBox.messageType = HelpBoxMessageType.Error;

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;

            var importPackButton = new Button(() =>
            {
                EditorPackImporter.ImportThespeonPack();
            })
            { text = "Import Pack" };

            var deletePackButton = new Button(() =>
            {
                int selectedLanguageIndex = _languagePackListView.selectedIndex;
                int selectedActorIndex = _actorPackListView.selectedIndex;
                VisualElement rootElement;
                string selectedPackName;
                if (selectedLanguageIndex >= 0 && selectedLanguageIndex <= _languagePackListView.itemsSource.Count)
                {
                    selectedPackName = _languagePackListView.selectedItem as string;


                    rootElement = _languagePackListView.GetRootElementForIndex(selectedLanguageIndex);
                }
                else if (selectedActorIndex >= 0 && selectedActorIndex <= _actorPackListView.itemsSource.Count)
                {
                    selectedPackName = _actorPackListView.selectedItem as string;

                    rootElement = _actorPackListView.GetRootElementForIndex(selectedActorIndex);

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
                    $"Are you sure you want to delete the pack:\n\n\"{selectedPackName}\"\n\nfrom disk?\n\nThis will delete the following:\n{labelSummary}",
                    "Delete",
                    "Cancel"
                );

                if (confirm)
                {
                    EditorPackImporter.DeletePack(selectedPackName);
                }

                Repaint();
            })
            { text = "Delete Pack" };
            deletePackButton.SetEnabled(false);

            var regenerateInputsButton = new Button(() =>
            {
                PackManifestHandler.Instance.UpdateMappings();
            })
            { text = "Regenerate Input Assets" };

            var actorPackHeaderBar = new Toolbar();
            var actorPackHeaderLabel = new Label("Imported Actor Packs")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    alignSelf = Align.Center,
                    marginLeft = 5,
                }
            };
            actorPackHeaderBar.style.marginTop = 10;
            actorPackHeaderBar.style.height = 21;
            var actorPackScrollView = new ScrollView();
            actorPackScrollView.style.minHeight = 83;
            actorPackScrollView.style.maxHeight = 3*83;
            actorPackScrollView.style.marginLeft = 5;

            _actorPackListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            _actorPackListView.makeItem = () =>
            {
                var itemContainer = new Box();
                itemContainer.style.marginBottom = 4;
                itemContainer.focusable = false;
                itemContainer.style.flexGrow = 1;
                return itemContainer;
            };

            _actorPackListView.bindItem = (element, index) =>
            {
                string actorPackName = _actorPackListView.itemsSource[index] as string;

                var listElement = (Box)element;

                listElement.Clear();
                var packNameLabel = new Label("• "+actorPackName);
                packNameLabel.style.fontSize = 14;
                packNameLabel.style.marginTop = 3;
                packNameLabel.style.marginBottom = 4;
                packNameLabel.style.marginLeft = 3;
                listElement.Add(packNameLabel);
                foreach (var item in PackManifestHandler.Instance.GetAllModuleInfoInActorPack(actorPackName))
                {
                    var sublabel = new Label($"- {item}");
                    sublabel.style.marginLeft = 10;
                    listElement.Add(sublabel);
                }
            };

            _actorPackListView.unbindItem = (element, index) =>
            {
                var container = (Box)element;
                container.Clear();
            };

            _actorPackListView.selectedIndicesChanged += (selectedItem) =>
            {
                if (_actorPackListView.selectedIndex >= 0)
                {
                    _languagePackListView.ClearSelection();
                    deletePackButton.SetEnabled(true);
                }
            };

            var languagePackHeaderBar = new Toolbar();
            var languagePackHeaderLabel = new Label("Imported Language Packs")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    alignSelf = Align.Center,
                    marginLeft = 5,
                }
            };
            languagePackHeaderBar.style.marginTop = 10;

            languagePackHeaderBar.style.maxHeight = 21;
            var languagePackScrollView = new ScrollView();
            languagePackScrollView.style.minHeight = 83;
            languagePackScrollView.style.maxHeight = 100;
            languagePackScrollView.style.flexGrow = 1;
            languagePackScrollView.style.marginLeft = 5;

            _languagePackListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            _languagePackListView.makeItem = () =>
            {
                var itemContainer = new Box();
                itemContainer.style.marginBottom = 4;
                itemContainer.focusable = false;
                itemContainer.style.flexGrow = 1;
                return itemContainer;
            };

            _languagePackListView.bindItem = (element, index) =>
            {
                string languagePackName = _languagePackListView.itemsSource[index] as string;

                var listElement = (Box)element;

                listElement.Clear();
                var packNameLabel = new Label("• "+languagePackName);
                packNameLabel.style.fontSize = 14;
                packNameLabel.style.marginTop = 3;
                packNameLabel.style.marginBottom = 4;
                packNameLabel.style.marginLeft = 3;
                listElement.Add(packNameLabel);
                foreach (var item in PackManifestHandler.Instance.GetAllModuleInfoInLanguagePack(languagePackName))
                {
                    var sublabel = new Label($"- {item}");
                    sublabel.style.marginLeft = 10;
                    listElement.Add(sublabel);
                }
            };

            _languagePackListView.unbindItem = (element, index) =>
            {
                var container = (Box)element;
                container.Clear();
            };

            _languagePackListView.selectedIndicesChanged += (selectedItem) =>
            {
                if (_languagePackListView.selectedIndex >= 0)
                {
                    _actorPackListView.ClearSelection();
                    deletePackButton.SetEnabled(true);
                }
            };
            infoContainer.Add(_downloadGuideHelpBox);
            infoContainer.Add(_missingPackHelpBox);
            infoContainer.Add(buttonContainer);

            buttonContainer.Add(importPackButton);
            buttonContainer.Add(deletePackButton);
            buttonContainer.Add(regenerateInputsButton);

            actorPackHeaderBar.Add(actorPackHeaderLabel);
            actorPackScrollView.Add(_actorPackListView);

            languagePackHeaderBar.Add(languagePackHeaderLabel);
            languagePackScrollView.Add(_languagePackListView);

            result.Add(infoContainer);
            result.Add(actorPackHeaderBar);
            result.Add(actorPackScrollView);
            result.Add(languagePackHeaderBar);
            result.Add(languagePackScrollView);
            return result;
        }

        private VisualElement CreateActorsTab()
        {
            VisualElement result = new()
            {
                name = "Actor Overview tab",
                style =
                {
                    flexGrow = 1,
                }
            };


            TwoPaneSplitView actorListEditorSplit = new()
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
            TwoPaneSplitView actorInfoPane = CreateActorInfoPane(inferenceEditorPane);

            actorInfoPane.style.minHeight = 120;

            segmentEditor.Add(inferenceEditorPane);

            actorListEditorSplit.Add(actorInfoPane);
            actorListEditorSplit.Add(segmentEditor);
            actorListEditorSplit.fixedPaneInitialDimension = 200;

            result.Add(actorListEditorSplit);
            return result;
        }

        private TwoPaneSplitView CreateActorInfoPane(VisualElement editorPane)
        {
            TwoPaneSplitView result = new()
            {
                fixedPaneInitialDimension = 210
            };

            VisualElement actorListPane = new()
            {
                style =
                {
                    minWidth = 210
                }
            };

            var actorListToolbar = new Toolbar();
            MaskField actorMaskField = new("Module type filter")
            {
                focusable = false,
            };
            var layersEnumChoices = new List<string>(Enum.GetNames(typeof(ModuleType)));
            layersEnumChoices.RemoveAt(0);
            actorMaskField.choices = layersEnumChoices;
            int allMask = (1 << Enum.GetValues(typeof(ModuleType)).Length) - 1;
            actorMaskField.value = allMask;
            
            actorMaskField.RegisterValueChangedCallback(evt =>
            {

                var selectedTypes = Enum.GetValues(typeof(ModuleType))
                .Cast<ModuleType>()
                .Skip(1)
                .Where(t => (evt.newValue & (1 << (int)t)) != 0)
                .ToList();

                var filtered = PackManifestHandler.Instance.GetAllActors()
                .Where(actor =>
                {
                    var actorTypes = PackManifestHandler.Instance.GetAllModuleTypesForActor(actor);
                    return actorTypes.Any(actorType => (evt.newValue & (1 << (int)actorType - 1)) != 0);
                })
                .ToList();

                _actorListView.itemsSource = filtered;
                _actorListView.Rebuild();
            });

            var actorInfoPane = new VisualElement();
            var actorInfoToolbar = new Toolbar();
            var actorInfoHeader = new Label("Actor Information")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    alignSelf = Align.Center,
                    marginLeft = 5,
                }
            };

            var actorInfoSection = new VisualElement();

            _actorListView.selectedIndicesChanged += (selectedItem) =>
            {
                actorInfoSection.Clear();
                if(_actorListView.selectedItem == null)
                {
                    return;
                }
                var selectedActorName = _actorListView.selectedItem.ToString();

                var actorNameLabel = CreateSelectableLabel(selectedActorName);
                actorNameLabel.style.fontSize = 14;
                actorNameLabel.style.alignSelf = Align.Center;
                actorNameLabel.style.marginTop = 5;

                var actorTitleSeparator = new VisualElement();
                actorTitleSeparator.style.height = 1;
                actorTitleSeparator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
                actorTitleSeparator.style.marginTop = 4;
                actorTitleSeparator.style.marginBottom = 4;
                actorTitleSeparator.style.flexGrow = 1;

                var actorSpecificInfoContainer = new ScrollView()
                {
                    style =
                    {
                        marginLeft = 5,
                        marginRight = 5,
                    }};

                var currentModuleSizes = PackManifestHandler.Instance.GetAllModuleTypesForActor(selectedActorName);
                var actorModuleSizeList = new VisualElement();

                actorModuleSizeList.Add(new Label("Imported module sizes:"));
                actorModuleSizeList.style.whiteSpace = WhiteSpace.Normal;
                actorModuleSizeList.style.unityFontStyleAndWeight = FontStyle.Normal;
                actorModuleSizeList.style.marginTop = 5;

                foreach (var moduleType in currentModuleSizes)
                    actorModuleSizeList.Add(new Label($"{moduleType}")
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
                

                    var languagesForModule = PackManifestHandler.Instance.GetAllSupportedLanguageCodes(selectedActorName, moduleType);
                    if (languagesForModule.Count > 0)
                    {
                        var languagesLabel = new Label("Supported Languages:") { style = { unityFontStyleAndWeight = FontStyle.Italic } };
                        languagesLabel.style.marginTop = 4;
                        languagesLabel.style.marginLeft = 5;
                        moduleFoldout.Add(languagesLabel);

                        foreach (var langNameCodePair in languagesForModule)
                        {
                            var dialects = PackManifestHandler.Instance.GetAllDialectsInModuleLanguage(selectedActorName, moduleType, langNameCodePair.Value);
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

                actorSpecificInfoContainer.Add(actorModuleSizeList);
                actorSpecificInfoContainer.Add(modulesSection);

                actorInfoSection.Add(actorNameLabel);
                actorInfoSection.Add(actorTitleSeparator);
                actorInfoSection.Add(actorSpecificInfoContainer);
            };

            actorListToolbar.Add(actorMaskField);
            actorListPane.Add(actorListToolbar);
            actorListPane.Add(_actorListView);

            actorInfoToolbar.Add(actorInfoHeader);
            actorInfoPane.Add(actorInfoToolbar);
            actorInfoPane.Add(actorInfoSection);

            result.Add(actorListPane);
            result.Add(actorInfoPane);
            return result;
        }

        private void UpdateInferenceTestingWindow(VisualElement editorPane, List<ModuleType> currentModuleSizes)
        {
            editorPane.Clear();
            Toolbar actorToolbar = new();
            string actorName = _actorListView.selectedItem.ToString();
            ToolbarMenu moduleSizeSelectMenu = new() { text = $"Select model size for {actorName}..." };
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

                    currentEditingStatus.text = "Editing input for " + actorName + " " + moduleTypeName + ":";
                    UpdateEditingPane(inputEditingPane, actorName, moduleTypeName);
                });
            }
            
            if (currentModuleSizes.Count > 0)
            {
                string firstModule = currentModuleSizes[0].ToString();
                moduleSizeSelectMenu.text = firstModule;
                currentEditingStatus.text = "Editing input for " + actorName + " " + firstModule + ":";
                UpdateEditingPane(inputEditingPane, actorName, firstModule);
            }

            actorToolbar.Add(moduleSizeSelectMenu);
            actorToolbar.Add(currentEditingStatus);

            editorPane.Add(actorToolbar);
            editorPane.Add(inputEditingPane);
        }

        private void UpdateEditingPane(ScrollView editingPane, string actorName, string moduleType)
        {
            editingPane.Clear();

            string inputIndexer = actorName + moduleType;
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
                    ThespeonInput input = new(currentInputContainer.segments, actorName, Enum.Parse<ModuleType>(moduleType))
                    {
                        Speed = currentInputContainer.speed,
                        Loudness = currentInputContainer.loudness
                    };
                    ThespeonInference inferenceSession = new();
                    InferenceConfig config = new()
                    {
                        TargetBudgetTime = 0.01f,
                        TargetFrameTime = 0.1f
                    };

                    this.StartCoroutine(inferenceSession.Infer<float>(input, config, HandleAudioOutput, "", false));
                }


            })
            {
                text = "Generate audio"
            };

            var segmentEditor = new VisualElement();

            UpdateSegmentEditorWindow(segmentEditor, currentInputContainer.segments, actorName, moduleType);

            editingPane.Add(runInferenceButton);
            editingPane.Add(speedPropField);
            editingPane.Add(loudnessPropField);
            editingPane.Add(segmentEditor);
        }
        private void UpdateSegmentEditorWindow(VisualElement segmentEditorWindow, List<ThespeonInputSegment> currentInputSegments, string actorName, string moduleTypeString)
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
            Dictionary<string, ModuleLanguage> languageChoices = PackManifestHandler.Instance.GetAllLanguagesForActorAndModuleType(actorName, moduleType);
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
                Dictionary<string, ModuleLanguage> languageChoices = PackManifestHandler.Instance.GetAllLanguagesForActorAndModuleType(actorName, moduleType);
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

        private void HandleAudioOutput(ThespeonDataPacket<float> data)
        {
            if (data.metadata.status == DataPacketStatus.FAILED)
            {
                HandleSynthFailed();
                return;
            }
            _audioData.AddRange(data.data);
            if (data.isFinalPacket)
            {
                CreateAndSelectWav(_audioData.ToArray());
                _audioData.Clear();


                InferenceResourceCleanup.CleanupResources();
                LingotionLogger.Debug("final audio data packet received, audio synthesis complete.");
                _isSynthesizing = false;
            }
        }

        private void HandleSynthFailed()
        {
                _audioData.Clear();
                InferenceResourceCleanup.CleanupResources();
                _isSynthesizing = false;
        }

        private static void CreateAndSelectWav(float[] data)
        {
            string path = "Assets/Audio Test Lab Output.wav";
            WavExporter.SaveWav(path, data);

            if (!File.Exists(path))
            {
                LingotionLogger.Error("WAV file not found at: " + path);
                return;
            }

            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                LingotionLogger.Error("Failed to load AudioClip at path: " + path);
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
            string licenseKey = _licenseField.value;
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
