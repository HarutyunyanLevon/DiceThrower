using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Core;

namespace DiceThrower.UI
{
    /// <summary>
    /// A filled polygon (die silhouette, 48x48 design units) with a centred text label.
    /// Colours come from USS custom properties --die-fill, --die-edge, --die-ink, so themes
    /// can restyle every die from the stylesheet. With a sprite (the picture of a die from a dice set's atlas)
    /// the sprite is the background instead of the polygon and the label stays on top (class die-icon--sprite).
    /// </summary>
    public sealed class DieIcon : VisualElement
    {
        private static readonly CustomStyleProperty<Color> FillProp = new CustomStyleProperty<Color>("--die-fill");
        private static readonly CustomStyleProperty<Color> EdgeProp = new CustomStyleProperty<Color>("--die-edge");

        public static readonly Vector2[] Hexagon = P(24, 3, 42, 13, 42, 35, 24, 45, 6, 35, 6, 13);

        private readonly Vector2[] _pts;
        private readonly Label _label;
        private readonly float _textY;
        private Color _fill = new Color(0.5f, 0.5f, 0.5f);
        private Color _edge = Color.white;
        private Sprite _sprite;

        public DieIcon(Vector2[] pts, string text, float textY)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("die-icon");
            _pts = pts;
            _textY = textY;

            _label = new Label(text);
            _label.pickingMode = PickingMode.Ignore;
            _label.AddToClassList("lbl");
            _label.AddToClassList("die-icon__text");
            Add(_label);

            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
            RegisterCallback<GeometryChangedEvent>(OnGeometry);
        }

        /// <param name="sprite">The die's picture, or null for the plain polygon.</param>
        public static DieIcon ForType(DieType t, Sprite sprite = null)
        {
            var icon = new DieIcon(ShapeFor(t), DieInfo.Label(t), TextYFor(t));
            icon.SetSprite(sprite);
            return icon;
        }

        /// <summary>Shows the sprite instead of the polygon; null goes back to the polygon.</summary>
        public void SetSprite(Sprite sprite)
        {
            _sprite = sprite;
            EnableInClassList("die-icon--sprite", sprite != null);
            if (sprite != null) style.backgroundImage = new StyleBackground(sprite);
            else style.backgroundImage = StyleKeyword.Null;
            MarkDirtyRepaint();
        }

        public void SetText(string text) { _label.text = text; }

        private static Vector2[] P(params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return p;
        }

        public static Vector2[] ShapeFor(DieType t)
        {
            switch (t)
            {
                case DieType.D4: return P(24, 5, 44, 40, 4, 40);
                case DieType.D6: return P(9, 9, 39, 9, 39, 39, 9, 39);
                case DieType.D8: return P(24, 3, 43, 24, 24, 45, 5, 24);
                case DieType.D10: return P(24, 3, 42, 19, 24, 45, 6, 19);
                case DieType.D12: return P(24, 3, 43, 17, 36, 41, 12, 41, 5, 17);
                case DieType.D20: return Hexagon;
                default: return P(16, 4, 32, 4, 44, 16, 44, 32, 32, 44, 16, 44, 4, 32, 4, 16);
            }
        }

        public static float TextYFor(DieType t)
        {
            switch (t)
            {
                case DieType.D4: return 36f;
                case DieType.D12: return 29f;
                default: return 28f;
            }
        }

        private void OnStyle(CustomStyleResolvedEvent e)
        {
            Color c;
            if (e.customStyle.TryGetValue(FillProp, out c)) _fill = c;
            if (e.customStyle.TryGetValue(EdgeProp, out c)) _edge = c;
            MarkDirtyRepaint();
        }

        private void OnGeometry(GeometryChangedEvent e)
        {
            Rect r = contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (s <= 0f) return;
            _label.style.left = r.x + (r.width - s) * 0.5f;
            _label.style.width = s;
            _label.style.top = r.y + (r.height - s) * 0.5f + (_textY - 9f) / 48f * s;
            _label.style.height = 18f / 48f * s;
            _label.style.fontSize = s * 0.31f;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext mgc)
        {
            if (_sprite != null) return; // the sprite is the picture
            Rect r = contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (s <= 0f) return;
            float k = s / 48f;
            Vector2 o = new Vector2(r.x + (r.width - s) * 0.5f, r.y + (r.height - s) * 0.5f);

            Painter2D p = mgc.painter2D;
            p.fillColor = _fill;
            p.strokeColor = _edge;
            p.lineWidth = Mathf.Max(1f, k * 2f);
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(o + _pts[0] * k);
            for (int i = 1; i < _pts.Length; i++) p.LineTo(o + _pts[i] * k);
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }
    }

    /// <summary>Stroked line icon (24x24 design units) drawn in the element's USS 'color'.</summary>
    public sealed class LineIcon : VisualElement
    {
        private Vector2[][] _paths;

        public LineIcon(Vector2[][] paths)
        {
            pickingMode = PickingMode.Ignore;
            _paths = paths;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e => MarkDirtyRepaint());
        }

        public void SetPaths(Vector2[][] paths)
        {
            _paths = paths;
            MarkDirtyRepaint();
        }

        private static Vector2[] P(params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return p;
        }

        public static Vector2[][] ChevronDown { get { return new[] { P(6, 9, 12, 15, 18, 9) }; } }
        public static Vector2[][] ChevronUp { get { return new[] { P(6, 15, 12, 9, 18, 15) }; } }

        public static Vector2[][] Plus { get { return new[] { P(12, 5, 12, 19), P(5, 12, 19, 12) }; } }

        public static Vector2[][] Trash
        {
            get
            {
                return new[]
                {
                    P(4, 7, 20, 7), P(9, 7, 9, 4, 15, 4, 15, 7), P(6, 7, 7, 20, 17, 20, 18, 7),
                    P(10, 11, 10, 17), P(14, 11, 14, 17),
                };
            }
        }

        public static Vector2[][] Save
        {
            get
            {
                return new[]
                {
                    P(5, 4, 16, 4, 19, 7, 19, 20, 5, 20, 5, 4), P(8, 4, 8, 9, 15, 9, 15, 4), P(8, 20, 8, 14, 16, 14, 16, 20),
                };
            }
        }

        public static Vector2[][] Edit
        {
            get { return new[] { P(4, 20, 5, 15, 16, 4, 20, 8, 9, 19, 4, 20), P(13, 7, 17, 11) }; }
        }

        private void Draw(MeshGenerationContext mgc)
        {
            Rect r = contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (s <= 0f) return;
            float k = s / 24f;
            Vector2 o = new Vector2(r.x + (r.width - s) * 0.5f, r.y + (r.height - s) * 0.5f);

            Painter2D p = mgc.painter2D;
            p.strokeColor = resolvedStyle.color;
            p.lineWidth = Mathf.Max(1.5f, k * 2f);
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            for (int i = 0; i < _paths.Length; i++)
            {
                Vector2[] path = _paths[i];
                p.BeginPath();
                p.MoveTo(o + path[0] * k);
                for (int j = 1; j < path.Length; j++) p.LineTo(o + path[j] * k);
                p.Stroke();
            }
        }
    }
}
