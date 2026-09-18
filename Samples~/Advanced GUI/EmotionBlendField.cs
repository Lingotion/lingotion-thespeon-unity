using System;
using System.Collections.Generic;
using System.Linq;
using Lingotion.Thespeon.Core;
using UnityEngine.UIElements;

namespace Lingotion.Thespeon.Samples.AdvancedGUI
{
    /// <summary>
    /// Displays one emotion blend as a row of chips and lets the user edit it in a modal.
    /// A blend is a set of emotions with weights that sum to 1; an empty blend means "no opinion here", which
    /// Thespeon treats as contributing no keypoint to the emotion curve.
    /// </summary>
    public class EmotionBlendField : VisualElement
    {
        private readonly string title;
        private readonly Dictionary<Emotion, float> blend = new();
        private bool isEditorOpen;

        /// <summary>Raised after the user applies a change in the editor modal.</summary>
        public event Action Changed;

        /// <summary>
        /// Every emotion except the None sentinel, which exists only to mean "unset" and is stripped by
        /// Thespeon during sanitization.
        /// </summary>
        private static readonly List<Emotion> SelectableEmotions =
            Enum.GetValues(typeof(Emotion))
                .Cast<Emotion>()
                .Where(emotion => emotion != Emotion.None)
                .OrderBy(emotion => emotion.ToString())
                .ToList();

        public EmotionBlendField(string title)
        {
            this.title = title;

            AddToClassList("lt-emotion-box");
            RegisterCallback<PointerDownEvent>(evt =>
            {
                OpenEditor();
                evt.StopPropagation();
            });

            RefreshChips();
        }

        /// <summary>
        /// A copy of the blend, ready to hand to a ThespeonInputSegment. Empty when no emotion is set.
        /// </summary>
        public Dictionary<Emotion, float> GetBlend() => new(blend);

        /// <summary>Replaces the blend, normalizing it first. Does not raise Changed.</summary>
        public void SetBlend(IEnumerable<KeyValuePair<Emotion, float>> weights)
        {
            blend.Clear();
            foreach (var entry in Normalize(weights)) blend[entry.Key] = entry.Value;
            RefreshChips();
        }

        /// <summary>
        /// Drops None and non-positive weights, then scales what remains so the weights sum to 1. An input
        /// with nothing positive in it normalizes to an empty blend.
        /// </summary>
        public static Dictionary<Emotion, float> Normalize(IEnumerable<KeyValuePair<Emotion, float>> weights)
        {
            Dictionary<Emotion, float> accumulated = new();
            if (weights != null)
            {
                foreach (var entry in weights)
                {
                    if (entry.Key == Emotion.None || entry.Value <= 0f || float.IsNaN(entry.Value)) continue;
                    accumulated.TryGetValue(entry.Key, out float existing);
                    accumulated[entry.Key] = existing + entry.Value;
                }
            }

            float sum = accumulated.Values.Sum();
            if (accumulated.Count == 0 || sum <= 0f) return new Dictionary<Emotion, float>();

            foreach (Emotion emotion in accumulated.Keys.ToList()) accumulated[emotion] /= sum;
            return accumulated;
        }

        private void RefreshChips()
        {
            Clear();

            if (blend.Count == 0)
            {
                Add(BuildChip("NONE", null));
                return;
            }

            foreach (var entry in blend.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key.ToString()))
            {
                Add(BuildChip(entry.Key.ToString().ToUpperInvariant(), entry.Value));
            }
        }

        private static VisualElement BuildChip(string name, float? weight)
        {
            VisualElement chip = new() { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("lt-chip");
            if (weight == null) chip.AddToClassList("lt-chip--empty");

            Label nameLabel = new(name);
            nameLabel.AddToClassList("lt-chip__name");
            chip.Add(nameLabel);

            if (weight != null)
            {
                Label weightLabel = new($"{weight.Value * 100f:0}%");
                weightLabel.AddToClassList("lt-chip__weight");
                chip.Add(weightLabel);
            }

            return chip;
        }

        // ------------------------------------------------------------------ editor modal

        private void OpenEditor()
        {
            // Parented inside the styled subtree, not to panel.visualTree - see OverlayHost.
            VisualElement root = OverlayHost.Resolve(this);
            if (root == null || isEditorOpen) return;

            isEditorOpen = true;
            List<(Emotion emotion, FillSlider slider)> rows = new();

            VisualElement scrim = new();
            scrim.AddToClassList("lt-modal-scrim");

            void CloseEditor()
            {
                scrim.RemoveFromHierarchy();
                isEditorOpen = false;
            }

            scrim.RegisterCallback<PointerDownEvent>(evt =>
            {
                CloseEditor();
                evt.StopPropagation();
            });

            VisualElement modal = new();
            modal.AddToClassList("lt-modal");
            modal.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            Label titleLabel = new(title);
            titleLabel.AddToClassList("lt-modal__title");
            modal.Add(titleLabel);

            Label hint = new("Weights are normalized to sum to 100%. Remove every emotion to leave this "
                             + "boundary unset, which contributes no keypoint to the emotion curve.");
            hint.AddToClassList("lt-modal__hint");
            modal.Add(hint);

            ScrollView rowContainer = new();
            rowContainer.AddToClassList("lt-modal__rows");
            modal.Add(rowContainer);

            Label emptyNotice = new("No emotions - this boundary is unset.");
            emptyNotice.AddToClassList("lt-modal__empty");
            rowContainer.Add(emptyNotice);

            PillDropdown addDropdown = new("+  Add emotion...");
            addDropdown.AddToClassList("lt-add-emotion");

            void RefreshAddChoices()
            {
                IEnumerable<string> available = SelectableEmotions
                    .Where(emotion => rows.All(row => row.emotion != emotion))
                    .Select(emotion => emotion.ToString());

                addDropdown.SetValue(null, notify: false);
                addDropdown.SetChoices(available, autoSelect: false);
                emptyNotice.style.display = rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void AddRow(Emotion emotion, float weight)
            {
                VisualElement row = new();
                row.AddToClassList("lt-emotion-row");

                Label name = new(emotion.ToString().ToUpperInvariant());
                name.AddToClassList("lt-emotion-row__name");
                row.Add(name);

                // Weights are edited as whole percentages; they are renormalized on apply anyway.
                FillSlider slider = new(0f, 100f, weight * 100f, "0'%'");
                slider.AddToClassList("lt-emotion-row__slider");
                row.Add(slider);

                Button remove = new() { text = "×" };
                remove.AddToClassList("lt-emotion-row__remove");
                remove.clicked += () =>
                {
                    rows.RemoveAll(candidate => candidate.emotion == emotion);
                    row.RemoveFromHierarchy();
                    RefreshAddChoices();
                };
                row.Add(remove);

                rows.Add((emotion, slider));
                rowContainer.Add(row);
            }

            addDropdown.ValueChanged += choice =>
            {
                if (choice == null || !Enum.TryParse(choice, out Emotion emotion)) return;
                AddRow(emotion, 1f);
                RefreshAddChoices();
            };

            foreach (var entry in blend.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key.ToString()))
            {
                AddRow(entry.Key, entry.Value);
            }
            RefreshAddChoices();

            modal.Add(addDropdown);

            VisualElement footer = new();
            footer.AddToClassList("lt-modal__footer");

            Button cancel = new(CloseEditor) { text = "Cancel" };
            cancel.AddToClassList("lt-button");
            footer.Add(cancel);

            Button apply = new(() =>
            {
                SetBlend(rows.Select(row => new KeyValuePair<Emotion, float>(row.emotion, row.slider.Value / 100f)));
                CloseEditor();
                Changed?.Invoke();
            }) { text = "Apply" };
            apply.AddToClassList("lt-button");
            apply.AddToClassList("lt-button--primary");
            footer.Add(apply);

            modal.Add(footer);
            scrim.Add(modal);
            root.Add(scrim);
        }
    }
}
