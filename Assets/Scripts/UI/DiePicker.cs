using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Core;
using DiceThrower.Game;

namespace DiceThrower.UI
{
    /// <summary>
    /// The small popover behind a dice row's "+" button: the die types that are not on the row yet, as buttons.
    /// Tap one to pick it; tap anywhere else to close. It floats above the anchor, right-aligned with it, inside
    /// <c>parent</c> (the screen, or the preset editor so that it is drawn above that popup).
    /// </summary>
    public sealed class DiePicker
    {
        private readonly VisualElement _parent;
        private readonly VisualElement _scrim;   // catches the taps outside the popover
        private readonly VisualElement _popover;
        private string _rowClass;

        public DiePicker(VisualElement parent)
        {
            _parent = parent;

            _scrim = new VisualElement();
            _scrim.AddToClassList("picker-scrim");
            _scrim.style.display = DisplayStyle.None;
            PressGesture.Tappable(_scrim, Hide);

            _popover = new VisualElement();
            _popover.AddToClassList("picker");
            _scrim.Add(_popover);
            parent.Add(_scrim);
        }

        /// <param name="anchor">The "+" button; the popover sits above it.</param>
        /// <param name="types">The die types to offer. Nothing is shown when empty.</param>
        /// <param name="rowClass">The USS class of the dice row (dice-row--atk), so the dice get that row's colours.</param>
        /// <param name="skin">Unused: the buttons are flat silhouettes in the row's colour. Kept so callers need no change.</param>
        /// <param name="onPick">Called with the chosen type after the popover closed.</param>
        public void Show(VisualElement anchor, List<DieType> types, string rowClass, DiceSkin skin, Action<DieType> onPick)
        {
            if (types.Count == 0) return;

            if (!string.IsNullOrEmpty(_rowClass)) _popover.RemoveFromClassList(_rowClass);
            _rowClass = rowClass;
            if (!string.IsNullOrEmpty(_rowClass)) _popover.AddToClassList(_rowClass);

            _popover.Clear();
            for (int i = 0; i < types.Count; i++) _popover.Add(BuildButton(types[i], skin, onPick));

            Rect anchorBox = anchor.worldBound;
            Vector2 corner = _parent.WorldToLocal(new Vector2(anchorBox.xMax, anchorBox.yMin)); // top right of the anchor
            _popover.style.right = Mathf.Max(0f, _parent.layout.width - corner.x);
            _popover.style.bottom = Mathf.Max(0f, _parent.layout.height - corner.y);
            _scrim.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _scrim.style.display = DisplayStyle.None;
        }

        private VisualElement BuildButton(DieType type, DiceSkin skin, Action<DieType> onPick)
        {
            var button = new VisualElement();
            button.AddToClassList("die-btn");
            button.Add(DieIcon.ForType(type)); // flat silhouette, tinted by the row's colours
            PressGesture.Tappable(button, () =>
            {
                Hide();
                onPick(type);
            });
            return button;
        }
    }
}
