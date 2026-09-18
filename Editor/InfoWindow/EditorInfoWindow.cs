// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Lingotion.Thespeon.Core;
using System.Collections.Generic;
using System;
using System.IO;
using Lingotion.Thespeon.Utils;

namespace Lingotion.Thespeon.Editor
{

    /// <summary>
    /// Allows user to import, delete, and see an overview of imported files.
    /// </summary>
    ///
    [Serializable]
    public partial class EditorInfoWindow : EditorWindow
    {

        private void OnEnable()
        {
            ManifestHandler.OnDataChanged -= UpdateDynamicData;
            ManifestHandler.OnDataChanged += UpdateDynamicData;
            EditorLicenseKeyValidator.OnValidationComplete -= GateValidationResult;
            EditorLicenseKeyValidator.OnValidationComplete += GateValidationResult;
            LoadSynthesisLabState();
        }

        private void OnDisable()
        {
            ManifestHandler.OnDataChanged -= UpdateDynamicData;
            EditorLicenseKeyValidator.OnValidationComplete -= GateValidationResult;
            SaveSynthesisLabState();
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

            var uss = LoadUSS("ThespeonEditor.uss");
            rootVisualElement.styleSheets.Add(uss);

            var uxml = LoadUXML("EditorInfoWindow.uxml");
            uxml.CloneTree(rootVisualElement);

            SetupLicenseGate();

            var toggleOverviewTab = rootVisualElement.Q<ToolbarToggle>("ToggleOverviewTab");
            var toggleSynthesisLabTab = rootVisualElement.Q<ToolbarToggle>("ToggleSynthesisLabTab");
            var pageContainer = rootVisualElement.Q<VisualElement>("PageContainer");

            try
            {
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

                pageContainer.Add(importedTabContent);
                pageContainer.Add(synthesisLabTabContent);
            }
            catch (InvalidOperationException e)
            {
                LingotionLogger.Error($"Failed to set up Thespeon Info Window tabs: {e.Message}");
            }
        }

        /// <summary>
        /// Updates the UI elements that are dependent on mutable external data
        /// </summary>
        private void UpdateDynamicData()
        {
            RefreshSynthesisLab();
            RefreshOverview();
        }

        private static TextField CreateSelectableLabel(string labelText)
        {
            TextField result = new() { isReadOnly = true };
            result.AddToClassList("selectable-label");
            var textInput = result.Q("unity-text-input");
            textInput.pickingMode = PickingMode.Position;
            labelText += '​';
            result.SetValueWithoutNotify(labelText);
            return result;
        }

        private static VisualTreeAsset LoadUXML(string fileName)
        {
            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"Packages/com.lingotion.thespeon/Editor/InfoWindow/{fileName}");
            if (uxml == null)
            {
                throw new FileNotFoundException($"Could not find UXML asset at path: Packages/com.lingotion.thespeon/Editor/InfoWindow/{fileName}");
            }
            return uxml;
        }

        private static StyleSheet LoadUSS(string fileName)
        {
            StyleSheet uss = AssetDatabase.LoadAssetAtPath<StyleSheet>($"Packages/com.lingotion.thespeon/Editor/InfoWindow/{fileName}");
            if (uss == null)
            {
                throw new FileNotFoundException($"Could not find USS asset at path: Packages/com.lingotion.thespeon/Editor/InfoWindow/{fileName}");
            }
            return uss;
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
