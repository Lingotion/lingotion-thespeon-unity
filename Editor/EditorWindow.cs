// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service found at www.lingotion.com.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Lingotion.Thespeon
{
    public class ExampleWindow : EditorWindow
    {
        // Local caches from PackFoldersWatcher
        private Dictionary<string, ActorData> actorCache 
            = new Dictionary<string, ActorData>();
        private List<LanguageData> languageCache 
            = new List<LanguageData>();

        // Foldouts
        private Dictionary<string, bool> actorFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> modulesFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> languageFoldouts = new Dictionary<string, bool>();

        // Grouped by nameinenglish for the Language Overview
        private Dictionary<string, List<LanguageData>> groupedLanguages 
            = new Dictionary<string, List<LanguageData>>();

        // We'll store the result of our "requirement" check here
        private string requirementStatusMessage = "";
        // If missing languages exist, we display a warning; otherwise show success or hide

        [MenuItem("Window/Lingotion/Lingotion Thespeon Info")]
        public static void ShowWindow()
        {
            var window = GetWindow<ExampleWindow>("Lingotion Thespeon");
            window.Show();
        }

        private void OnEnable()
        {
            // Subscribe for updates
            PackFoldersWatcher.OnActorDataUpdated += OnActorDataUpdated;
            // Force an initial load
            PackFoldersWatcher.UpdateActorDetailedInfo();
        }

        private void OnDisable()
        {
            PackFoldersWatcher.OnActorDataUpdated -= OnActorDataUpdated;
        }

        private void OnActorDataUpdated()
        {
            // Copy from the Watcher
            actorCache = new Dictionary<string, ActorData>(PackFoldersWatcher.ActorDataCache);
            languageCache = new List<LanguageData>(PackFoldersWatcher.LanguageDataCache);

            // Build grouping for language overview
            BuildGroupedLanguages();

            // *** This is the crucial step ***
            // Check if actors' needed languages are satisfied by the language packs
            CheckRequirementStatus();

            // Refresh the window
            Repaint();
        }

        /// <summary>
        /// Builds a dictionary keyed by nameinenglish (or "Unknown"), 
        /// with a list of LanguageData. We will later display only iso639_2 codes.
        /// </summary>
        private void BuildGroupedLanguages()
        {
            groupedLanguages.Clear();

            foreach (var lang in languageCache)
            {
                // If an Actor gave us nameinenglish, great; otherwise "Unknown"
                string displayName = lang.languagecode?.nameinenglish;
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = "Unknown";
                }

                if (!groupedLanguages.ContainsKey(displayName))
                {
                    groupedLanguages[displayName] = new List<LanguageData>();
                }

                groupedLanguages[displayName].Add(lang);
            }
        }

        /// <summary>
        /// Gathers all iso639_2 codes needed by actors, compares to what we have in language packs.
        /// If any are missing, we store a warning message in 'requirementStatusMessage'.
        /// Otherwise we say everything is good.
        /// </summary>
        private void CheckRequirementStatus()
        {
            // Collect all iso639_2 from actors
            HashSet<string> actorLangs = new HashSet<string>();
            foreach (var actor in actorCache.Values)
            {
                foreach (var lang in actor.aggregatedLanguages)
                {
                    if (!string.IsNullOrEmpty(lang.iso639_2))
                    {
                        actorLangs.Add(lang.iso639_2);
                    }
                }
            }

            // Collect all iso639_2 from language packs
            HashSet<string> packLangs = new HashSet<string>();
            foreach (var lang in languageCache)
            {
                if (!string.IsNullOrEmpty(lang.iso639_2))
                {
                    packLangs.Add(lang.iso639_2);
                }
            }

            // Which needed iso639_2 are missing from the language packs?
            var missing = actorLangs.Where(code => !packLangs.Contains(code)).ToList();
            if (missing.Count > 0)
            {
                requirementStatusMessage = "Missing language packs for: " 
                                        + string.Join(", ", missing);
            }
            else
            {
                requirementStatusMessage = "All required language packs are found!";
            }
        }

        private void OnGUI()
        {
            // Info about where to download packs
            EditorGUILayout.HelpBox(
                "To download Actor Packs and Language Packs please register as a Developer at the link below:",
                MessageType.Info
            );
            // Selectable link
            DrawSelectableLabel("https://portal.lingotion.com");

            EditorGUILayout.Space();
            DrawImportButtons();

            EditorGUILayout.LabelField("Refresh Data", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh Actor Data", GUILayout.Width(200)))
            {
                PackFoldersWatcher.UpdateActorDetailedInfo();
            }

            DrawRequirementStatus();

            EditorGUILayout.Space();
            DrawActorOverview();

            EditorGUILayout.Space();
            DrawLanguageOverview();
        }

        /// <summary>
        /// Displays the requirement status if there's a problem,
        /// or success if everything is good. Also provides a
        /// selectable label so the user can copy the full message.
        /// </summary>
        private void DrawRequirementStatus()
        {
            // If we have never set requirementStatusMessage yet, do nothing
            if (string.IsNullOrEmpty(requirementStatusMessage))
                return;

            if (requirementStatusMessage.StartsWith("Missing language packs"))
            {
                // Show a warning
                EditorGUILayout.HelpBox(requirementStatusMessage, MessageType.Warning);

            }
            else
            {
                // Show a success message
                EditorGUILayout.HelpBox(requirementStatusMessage, MessageType.Info);

            }
        }

        // -------------------------------------------
        // ACTOR OVERVIEW
        // -------------------------------------------
        private void DrawActorOverview()
        {
            EditorGUILayout.LabelField("Imported Actors Overview", EditorStyles.boldLabel);

            if (actorCache.Count == 0)
            {
                EditorGUILayout.LabelField("No Actors found.");
                return;
            }

            foreach (var kvp in actorCache)
            {
                string username = kvp.Key;
                ActorData actorData = kvp.Value;

                if (!actorFoldouts.ContainsKey(username))
                    actorFoldouts[username] = false;

                // Foldout for this actor
                actorFoldouts[username] = EditorGUILayout.Foldout(
                    actorFoldouts[username],
                    $"{username}",
                    true
                );

                if (actorFoldouts[username])
                {
                    EditorGUI.indentLevel++;

                    // 1) Aggregated Languages
                    EditorGUILayout.LabelField("Languages:");
                    EditorGUI.indentLevel++;
                    var distinctIso2 = actorData.aggregatedLanguages
                        .Select(l => l.iso639_2 ?? "unknown")
                        .Distinct()
                        .ToList();
                    foreach (var iso2 in distinctIso2)
                    {
                        DrawSelectableLabel($"- iso639_2 : {iso2}");
                    }
                    EditorGUI.indentLevel--;

                    // 2) Aggregated Tags
                    EditorGUILayout.LabelField("Tags:");
                    EditorGUI.indentLevel++;
                    foreach (var tagKvp in actorData.aggregatedTags)
                    {
                        string tagKey = tagKvp.Key;
                        // combine all possible values for that tagKey
                        string combinedValues = string.Join(", ", tagKvp.Value);

                        DrawSelectableLabel($"- {tagKey}: {combinedValues}");
                    }
                    EditorGUI.indentLevel--;

                    // 3) Modules foldout
                    string modsKey = username + "_modules";
                    if (!modulesFoldouts.ContainsKey(modsKey))
                        modulesFoldouts[modsKey] = false;

                    modulesFoldouts[modsKey] = EditorGUILayout.Foldout(
                        modulesFoldouts[modsKey],
                        "Modules",
                        true
                    );

                    if (modulesFoldouts[modsKey])
                    {
                        EditorGUI.indentLevel++;
                        foreach (var mod in actorData.modules)
                        {
                            DrawSelectableLabel($"Module: {mod.moduleName}");
                            EditorGUI.indentLevel++;

                            // Display module-level tags
                            EditorGUILayout.LabelField("Tags:");
                            EditorGUI.indentLevel++;
                            foreach (var kvp2 in mod.tags)
                            {
                                DrawSelectableLabel($"- {kvp2.Key}: {kvp2.Value}");
                            }
                            EditorGUI.indentLevel--;

                            // Display module-level languages (distinct iso639_2)
                            EditorGUILayout.LabelField("Languages:");
                            EditorGUI.indentLevel++;
                            var distinctIso2Module = mod.languages
                                .Select(l => l.iso639_2 ?? "unknown")
                                .Distinct()
                                .ToList();
                            foreach (var iso2 in distinctIso2Module)
                            {
                                DrawSelectableLabel($"- iso639_2 : {iso2}");
                            }
                            EditorGUI.indentLevel -= 2;

                            EditorGUILayout.Space();
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space();
            }
        }

        // --------------------------------------
        // LANGUAGE OVERVIEW
        // --------------------------------------
        private void DrawLanguageOverview()
        {
            EditorGUILayout.LabelField("Imported Languages Overview", EditorStyles.boldLabel);

            if (groupedLanguages.Count == 0)
            {
                EditorGUILayout.LabelField("No languages found.");
                return;
            }

            // Each group is "nameinenglish" or "Unknown"
            foreach (var kvp in groupedLanguages)
            {
                string langName = kvp.Key; 
                List<LanguageData> variants = kvp.Value;

                if (!languageFoldouts.ContainsKey(langName))
                    languageFoldouts[langName] = false;

                languageFoldouts[langName] = EditorGUILayout.Foldout(
                    languageFoldouts[langName],
                    $"Language: {langName}", true);

                if (languageFoldouts[langName])
                {
                    EditorGUI.indentLevel++;

                    // We only want to display distinct iso639_2 across all variants
                    var distinctIso2 = variants
                        .Select(l => l.iso639_2 ?? "unknown")
                        .Distinct()
                        .ToList();

                    foreach (var iso2 in distinctIso2)
                    {
                        DrawSelectableLabel($"- iso639_2 : {iso2}");
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }

        // --------------------------------------
        // IMPORT BUTTONS
        // --------------------------------------
        private void DrawImportButtons()
        {
            EditorGUILayout.LabelField("Import Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Import Actor Pack", GUILayout.Width(200)))
            {
                PackImporterEditor.ImportActorPack();
                PackFoldersWatcher.UpdatePackMappings();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Import Language Pack", GUILayout.Width(200)))
            {
                PackImporterEditor.ImportLanguagePack();
                PackFoldersWatcher.UpdatePackMappings();
            }
        }

        /// <summary>
        /// Show just the minimal fields you want:
        /// iso639_2, iso3166_1, customdialect, plus nameinenglish 
        /// (if present).
        /// </summary>
        private string FormatLanguage(LanguageData lang)
        {
            string nameInEnglish = lang.languagecode?.nameinenglish ?? "Unknown";
            return $"{{ \"iso639_2\": \"{lang.iso639_2 ?? "null"}\", " +
                $"\"iso3166_1\": \"{lang.iso3166_1 ?? "null"}\", " +
                $"\"customdialect\": \"{lang.customdialect ?? "null"}\", " +
                $"\"nameinenglish\": \"{nameInEnglish}\" }}";
        }

        /// <summary>
        /// Renders a single-line SelectableLabel so the user can copy the text.
        /// </summary>
        private void DrawSelectableLabel(string text)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.SelectableLabel(rect, text);
        }
    }
}