using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Game;

namespace DiceThrower.UI
{
    /// <summary>
    /// Switches the theme. A theme is a style sheet that redefines the colour variables of Tavern.uss and may restyle frames and fonts;
    /// it is added last to the root, so its rules win. Only one theme sheet is on the root at a time.
    /// A theme can also carry a 3D scene (play surface, backdrop, lights), which the <see cref="ThemeSceneHost"/> swaps in.
    /// </summary>
    public sealed class ThemeApplier
    {
        private readonly VisualElement _root;
        private readonly ThemeSpec[] _themes;
        private readonly ThemeSceneHost _host;
        private StyleSheet _current;

        /// <param name="host">Swaps the 3D scene. Null: themes only change the style sheet.</param>
        public ThemeApplier(VisualElement root, ThemeSpec[] themes, ThemeSceneHost host = null)
        {
            _root = root;
            _themes = themes;
            _host = host;
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
            ThemeSpec theme = _themes[Clamp(index)];
            if (_host != null) _host.Show(theme.scene);
            if (theme.sheet == _current) return;
            if (_current != null) _root.styleSheets.Remove(_current);
            _root.styleSheets.Add(theme.sheet);
            _current = theme.sheet;
        }
    }
}
