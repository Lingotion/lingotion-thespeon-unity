// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Lingotion.Thespeon.Core;
using System.Linq;

namespace Lingotion.Thespeon.Editor
{
    public partial class EditorInfoWindow
    {
        private ListView _importedCharacterListView;
        private ListView _importedLanguageListView;
        private HelpBox _missingLanguageHelpBox;

        private void RefreshOverview()
        {
            if (_importedCharacterListView == null) return;

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
            var result = new VisualElement { style = { flexGrow = 1 } };
            var uxml = LoadUXML("OverviewTab.uxml");
            uxml.CloneTree(result);

            _missingLanguageHelpBox = result.Q<HelpBox>("MissingLanguageHelpBox");
            _importedCharacterListView = result.Q<ListView>("ImportedCharacterListView");
            _importedLanguageListView = result.Q<ListView>("ImportedLanguageListView");

            result.Q<Button>("DownloadGuideHelpBoxButton").clicked += () => Application.OpenURL(EditorLingotionUrls.PortalHome);

            var deleteButton = result.Q<Button>("DeleteButton");
            result.Q<Button>("ImportButton").clicked += () => EditorImporter.ImportThespeon();
            result.Q<Button>("RegenerateInputsButton").clicked += () => ManifestHandler.Instance.UpdateMappings();
            deleteButton.clicked += HandleDelete;

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
                nameLabel.AddToClassList("list-item-name");
                listElement.Add(nameLabel);
                foreach (var (info, version) in ManifestHandler.Instance.GetAllModuleInfoInCharacter(name))
                {
                    var sublabel = new Label($"- {info}");
                    sublabel.AddToClassList("list-item-sub");
                    listElement.Add(sublabel);

                    var versionLabel = new Label($"Version: {version}");
                    versionLabel.AddToClassList("list-item-version");
                    listElement.Add(versionLabel);
                }
            };

            _importedCharacterListView.unbindItem = (element, index) =>
            {
                ((Box)element).Clear();
            };

            _importedCharacterListView.selectedIndicesChanged += _ =>
            {
                if (_importedCharacterListView.selectedIndex >= 0)
                {
                    _importedLanguageListView.ClearSelection();
                    deleteButton.SetEnabled(true);
                }
            };

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
                var nameLabel = new Label("• " + languageName);
                nameLabel.AddToClassList("list-item-name");
                listElement.Add(nameLabel);
                foreach (var (info, version) in ManifestHandler.Instance.GetAllModuleInfoInLanguage(languageName))
                {
                    var sublabel = new Label($"- {info}");
                    sublabel.AddToClassList("list-item-sub");
                    listElement.Add(sublabel);

                    var versionLabel = new Label($"Version: {version}");
                    versionLabel.AddToClassList("list-item-version");
                    listElement.Add(versionLabel);
                }
            };

            _importedLanguageListView.unbindItem = (element, index) =>
            {
                ((Box)element).Clear();
            };

            _importedLanguageListView.selectedIndicesChanged += _ =>
            {
                if (_importedLanguageListView.selectedIndex >= 0)
                {
                    _importedCharacterListView.ClearSelection();
                    deleteButton.SetEnabled(true);
                }
            };

            return result;
        }

        private void HandleDelete()
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
                return;


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
                    EditorImporter.DeleteModule(configFilename);
                else
                    LingotionLogger.Error($"Delete failed! Import manifest is out of sync. Try recompiling your project or contact support if the issue persists. Faulty entry: {selectedName}");
            }

            Repaint();
        }
    }
}
