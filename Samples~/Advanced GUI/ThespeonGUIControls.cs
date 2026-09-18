using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lingotion.Thespeon.Samples.AdvancedGUI
{
    /// <summary>
    /// Works out where floating UI - dropdown menus, modals - should be parented.
    /// </summary>
    internal static class OverlayHost
    {
        /// <summary>
        /// Returns the outermost ancestor that is still inside the UIDocument's root, which is where the
        /// sample's style sheet lives. Parenting a popup to panel.visualTree instead would put it outside that
        /// subtree, and it would render with no styling at all: no absolute positioning, no dismiss overlay.
        /// </summary>
        public static VisualElement Resolve(VisualElement element)
        {
            VisualElement panelRoot = element?.panel?.visualTree;
            if (element == null || panelRoot == null) return null;

            VisualElement candidate = element;
            while (candidate.parent != null && candidate.parent != panelRoot) candidate = candidate.parent;
            return candidate;
        }
    }

    /// <summary>
    /// A horizontal slider drawn as a filled bar with the current value printed inside it.
    /// UI Toolkit's built-in Slider draws a thin track and a separate handle, so this sample uses its own
    /// element to get the filled-pill look. Drag anywhere on the bar to set the value.
    /// </summary>
    public class FillSlider : VisualElement
    {
        private readonly VisualElement fill;
        private readonly Label valueLabel;
        private readonly float lowValue;
        private readonly float highValue;
        private readonly string format;
        private float currentValue;

        /// <summary>Raised whenever the user drags the slider. Not raised by SetValueWithoutNotify.</summary>
        public event Action<float> ValueChanged;

        /// <summary>The current value, always within [lowValue, highValue].</summary>
        public float Value => currentValue;

        public FillSlider(float lowValue, float highValue, float initialValue, string format = "0.0#")
        {
            this.lowValue = lowValue;
            this.highValue = highValue;
            this.format = format;

            AddToClassList("lt-slider");

            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("lt-slider__fill");
            Add(fill);

            valueLabel = new Label { pickingMode = PickingMode.Ignore };
            valueLabel.AddToClassList("lt-slider__value");
            Add(valueLabel);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);

            SetValueWithoutNotify(initialValue);
        }

        /// <summary>Sets the value and refreshes the bar without raising ValueChanged.</summary>
        public void SetValueWithoutNotify(float newValue)
        {
            currentValue = Mathf.Clamp(newValue, lowValue, highValue);
            float normalized = Mathf.Approximately(highValue, lowValue)
                ? 0f
                : (currentValue - lowValue) / (highValue - lowValue);

            fill.style.width = Length.Percent(normalized * 100f);
            valueLabel.text = currentValue.ToString(format);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            this.CapturePointer(evt.pointerId);
            SetValueFromPointer(evt.localPosition.x);
            // Keep the click from reaching enclosing elements - the emotion box, for instance, opens a
            // modal on click and the modal's own rows contain sliders.
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId)) return;
            SetValueFromPointer(evt.localPosition.x);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId)) return;
            this.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void SetValueFromPointer(float localX)
        {
            float width = contentRect.width;
            if (width <= 0f) return;

            float normalized = Mathf.Clamp01(localX / width);
            SetValueWithoutNotify(Mathf.Lerp(lowValue, highValue, normalized));
            ValueChanged?.Invoke(currentValue);
        }
    }

    /// <summary>
    /// A rounded dropdown that opens its choices in a floating list. Built from plain VisualElements so it
    /// looks the same regardless of which UI Toolkit theme the project uses.
    /// </summary>
    public class PillDropdown : VisualElement
    {
        private readonly Label label;
        private readonly string emptyText;
        private readonly List<string> choices = new();
        private string selected;
        private VisualElement openMenu;

        /// <summary>Raised when the selection changes, including when SetChoices picks a new fallback.</summary>
        public event Action<string> ValueChanged;

        /// <summary>The selected choice, or null when there is nothing to select.</summary>
        public string Value => selected;

        /// <summary>How many choices the dropdown actually offers, after empty labels are discarded.</summary>
        public int ChoiceCount => choices.Count;

        public PillDropdown(string emptyText = "—", bool centered = false)
        {
            this.emptyText = emptyText;

            AddToClassList("lt-pill");
            if (centered) AddToClassList("lt-pill--centered");

            label = new Label(emptyText) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("lt-pill__label");
            Add(label);

            Label chevron = new("▾") { pickingMode = PickingMode.Ignore };
            chevron.AddToClassList("lt-pill__chevron");
            Add(chevron);

            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (enabledSelf) OpenMenu();
                evt.StopPropagation();
            });

            SetEnabled(false);
        }

        /// <summary>
        /// Replaces the list of choices. Keeps the current selection if it still exists, otherwise falls back
        /// to <paramref name="preferred"/> and then to the first choice, raising ValueChanged if that changes
        /// the selection. This is what makes the character -> module size -> language chain cascade.
        /// </summary>
        /// <param name="autoSelect">
        /// When false the dropdown is left showing its placeholder instead of falling back to a choice. Used
        /// for action-style dropdowns such as "add an emotion", where a selection means "do this now".
        /// </param>
        public void SetChoices(IEnumerable<string> newChoices, string preferred = null, bool autoSelect = true)
        {
            choices.Clear();
            if (newChoices != null) choices.AddRange(newChoices.Where(choice => !string.IsNullOrEmpty(choice)));

            string next = null;
            if (selected != null && choices.Contains(selected)) next = selected;
            else if (!autoSelect) next = null;
            else if (preferred != null && choices.Contains(preferred)) next = preferred;
            else if (choices.Count > 0) next = choices[0];

            SetEnabled(choices.Count > 0);
            SetValue(next, notify: next != selected);
        }

        /// <summary>Selects a choice explicitly. Ignored if the choice is not in the list.</summary>
        public void SetValue(string newValue, bool notify = true)
        {
            if (newValue != null && !choices.Contains(newValue)) return;

            selected = newValue;
            label.text = selected ?? emptyText;
            if (notify) ValueChanged?.Invoke(selected);
        }

        private void OpenMenu()
        {
            // Clicking the pill while its menu is open closes it again.
            if (openMenu != null)
            {
                CloseMenu();
                return;
            }

            VisualElement root = OverlayHost.Resolve(this);
            if (root == null || choices.Count == 0) return;

            // A full-screen blocker catches the click that dismisses the menu.
            VisualElement blocker = new();
            blocker.AddToClassList("lt-blocker");
            blocker.RegisterCallback<PointerDownEvent>(evt =>
            {
                CloseMenu();
                evt.StopPropagation();
            });
            openMenu = blocker;

            VisualElement menu = new();
            menu.AddToClassList("lt-menu");
            menu.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            ScrollView scroll = new();
            foreach (string choice in choices)
            {
                string captured = choice;
                Button item = new(() =>
                {
                    CloseMenu();
                    SetValue(captured);
                }) { text = captured };
                item.AddToClassList("lt-menu__item");
                if (captured == selected) item.AddToClassList("lt-menu__item--selected");
                scroll.Add(item);
            }
            menu.Add(scroll);

            // The menu is positioned in the host's coordinate space, not the panel's.
            Rect anchor = root.WorldToLocal(worldBound);
            menu.style.left = anchor.xMin;
            menu.style.top = anchor.yMax + 4f;
            menu.style.minWidth = anchor.width;

            // Flip the menu above the pill if it would run off the bottom of the panel.
            menu.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float panelHeight = root.contentRect.height;
                if (anchor.yMax + 4f + evt.newRect.height > panelHeight)
                {
                    menu.style.top = Mathf.Max(0f, anchor.yMin - 4f - evt.newRect.height);
                }
            });

            blocker.Add(menu);
            root.Add(blocker);
        }

        private void CloseMenu()
        {
            openMenu?.RemoveFromHierarchy();
            openMenu = null;
        }
    }
}
