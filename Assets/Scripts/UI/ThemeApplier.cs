using UnityEngine;
using UnityEngine.UIElements;

namespace DiceThrower.UI
{
    /// <summary>
    /// Switches the colour theme. A theme is a style sheet that redefines the colour variables of Tavern.uss;
    /// it is added last to the root, so its variables win. Only one theme sheet is on the root at a time.
    /// </summary>
    public sealed class ThemeApplier
    {
        private readonly VisualElement _root;
        private readonly ThemeSpec[] _themes;
        private StyleSheet _current;

        public ThemeApplier(VisualElement root, ThemeSpec[] themes)
        {
            _root = root;
            _themes = themes;
        }

        public int Count { get { return _themes.Length; } }

        public string NameOf(int index)
        {
            return _themes[Clamp(index)].name;
        }

        /// <summary>An index out of range (an old save, a removed theme) falls back to the nearest theme.</summary>
        public int Clamp(int index)
        {
            return Mathf.Clamp(index, 0, _themes.Length - 1);
        }

        public void Apply(int index)
        {
            StyleSheet sheet = _themes[Clamp(index)].sheet;
            if (sheet == _current) return;
            if (_current != null) _root.styleSheets.Remove(_current);
            _root.styleSheets.Add(sheet);
            _current = sheet;
        }
    }
}
