using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DiceThrower.UI
{
    /// <summary>
    /// The theme picker of the Settings tab: a sheet over the bottom of the screen with one card per theme. It opens on the Roll tab, so a tap on a card
    /// shows that theme's skin and 3D scene at once (a live preview). Apply keeps the choice, Cancel brings the old theme back.
    /// </summary>
    public sealed class ThemePicker
    {
        private const string OpenClass = "theme-sheet--open";
        private const string CardClass = "theme-card";
        private const string SelectedClass = "theme-card--selected";

        private readonly VisualElement _sheet, _cards;
        private readonly ThemeApplier _theme;
        private readonly Action<int> _commit;
        private readonly Action _showRoll;
        private int _original, _selected;

        /// <param name="showRoll">Goes to the Roll tab, where the preview can be seen.</param>
        /// <param name="commit">Stores the chosen theme index.</param>
        public ThemePicker(VisualElement root, DataUiConfig cfg, ThemeApplier theme, Action showRoll, Action<int> commit)
        {
            _theme = theme;
            _showRoll = showRoll;
            _commit = commit;
            _sheet = root.Q<VisualElement>("theme-sheet");
            _cards = root.Q<VisualElement>("theme-cards");
            root.Q<Label>("theme-title").text = cfg.themePickerTitle;
            root.Q<Label>("theme-apply-text").text = cfg.themePickerApplyText;
            root.Q<Label>("theme-cancel-text").text = cfg.cancelText;

            for (int i = 0; i < theme.Count; i++)
            {
                int index = i;
                var card = new Label(theme.NameOf(i));
                card.AddToClassList("lbl");
                card.AddToClassList(CardClass);
                PressGesture.Tappable(card, () => Select(index));
                _cards.Add(card);
            }
            PressGesture.Tappable(root.Q<VisualElement>("theme-apply"), Apply);
            PressGesture.Tappable(root.Q<VisualElement>("theme-cancel"), Cancel);
        }

        /// <summary>Starts from the saved theme.</summary>
        public void Open(int current)
        {
            _original = _theme.Clamp(current);
            _showRoll();
            _sheet.AddToClassList(OpenClass);
            Select(_original);
        }

        private void Select(int index)
        {
            _selected = _theme.Clamp(index);
            _theme.Apply(_selected);
            for (int i = 0; i < _cards.childCount; i++) _cards[i].EnableInClassList(SelectedClass, i == _selected);
        }

        private void Apply()
        {
            _sheet.RemoveFromClassList(OpenClass);
            _commit(_selected);
        }

        private void Cancel()
        {
            _theme.Apply(_original);
            _sheet.RemoveFromClassList(OpenClass);
        }
    }
}
