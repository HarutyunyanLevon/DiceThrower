using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DiceThrower.UI
{
    /// <summary>Small element builders shared by the views, so every label and icon button is made the same way.</summary>
    public static class UiFactory
    {
        /// <summary>A label that lets pointer events through to the element underneath.</summary>
        public static Label Label(string text, params string[] classes)
        {
            var l = new Label(text);
            l.pickingMode = PickingMode.Ignore;
            l.AddToClassList("lbl");
            for (int i = 0; i < classes.Length; i++) l.AddToClassList(classes[i]);
            return l;
        }

        /// <summary>A round icon button for a list row. It does not pass the press on to the row.</summary>
        public static VisualElement IconButton(Vector2[][] icon, string extraClass, float tapSlopPx, Action onTap)
        {
            var button = new VisualElement();
            button.AddToClassList("icon-btn");
            if (!string.IsNullOrEmpty(extraClass)) button.AddToClassList(extraClass);
            var glyph = new LineIcon(icon);
            glyph.AddToClassList("icon-btn__icon");
            button.Add(glyph);
            ListTap.Attach(button, onTap, tapSlopPx, true);
            return button;
        }

        /// <summary>Seconds to the milliseconds the UI Toolkit scheduler wants.</summary>
        public static long Millis(float seconds) { return (long)(seconds * 1000f); }

        public static VisualElement Column(params string[] classes)
        {
            var v = new VisualElement();
            v.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < classes.Length; i++) v.AddToClassList(classes[i]);
            return v;
        }
    }
}
