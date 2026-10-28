using System.Collections.Generic;
using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// One texture per die kind + skin: a grid of cells, each holding one printed number.
    /// Every face is UV-mapped onto a cell with its own polygon shape, so changing which number
    /// a face shows (the remap) is just rewriting that face's UVs.
    /// The d4 is special: each cell holds three corner numbers (an ordered triple) and the
    /// number at the top corner is the result.
    /// </summary>
    public sealed class DiceAtlas
    {
        public const int CellPx = 128;

        public readonly DieShape Shape;
        public readonly int CellCount;
        public readonly int Cols;
        public readonly int Rows;
        public readonly int Width;
        public readonly int Height;
        public Texture2D Texture;

        private readonly float _kpx; // pixels per local face unit
        private readonly Dictionary<int, int> _cellOfValue = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _cellOfTriple = new Dictionary<int, int>();
        private readonly List<int[]> _triples = new List<int[]>();
        private readonly int[] _values;

        private DiceAtlas(DieShape shape)
        {
            Shape = shape;
            if (shape.VertexLabeled)
            {
                for (int a = 1; a <= 4; a++)
                for (int b = 1; b <= 4; b++)
                for (int c = 1; c <= 4; c++)
                {
                    if (a == b || b == c || a == c) continue;
                    _cellOfTriple[a * 100 + b * 10 + c] = _triples.Count;
                    _triples.Add(new[] { a, b, c });
                }
                CellCount = _triples.Count;
            }
            else
            {
                _values = DieInfo.FaceValues(shape.Kind);
                for (int i = 0; i < _values.Length; i++) _cellOfValue[_values[i]] = i;
                CellCount = _values.Length;
            }

            Cols = Mathf.CeilToInt(Mathf.Sqrt(CellCount));
            Rows = Mathf.CeilToInt(CellCount / (float)Cols);
            Width = Cols * CellPx;
            Height = Rows * CellPx;
            _kpx = CellPx * 0.5f * 0.94f / shape.FaceRMax;
        }

        /// <summary>Layout only (no pixels, no Texture2D). Handy for tests.</summary>
        public static DiceAtlas CreateLayout(DieShape shape)
        {
            return new DiceAtlas(shape);
        }

        public static DiceAtlas Create(DieShape shape, DiceSkin skin)
        {
            var atlas = new DiceAtlas(shape);
            Color32[] pixels = atlas.Render(skin);
            var tex = new Texture2D(atlas.Width, atlas.Height, TextureFormat.RGBA32, true, false);
            tex.name = shape.Kind + "_" + skin.name;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            atlas.Texture = tex;
            return atlas;
        }

        // ------------------------------------------------------------------ lookups

        public int CellForValue(int value)
        {
            return _cellOfValue[value];
        }

        public int CellForTriple(int a, int b, int c)
        {
            return _cellOfTriple[a * 100 + b * 10 + c];
        }

        public Vector2 UV(int cell, Vector2 local)
        {
            float ox = (cell % Cols) * CellPx + CellPx * 0.5f;
            float oy = (cell / Cols) * CellPx + CellPx * 0.5f;
            return new Vector2((ox + local.x * _kpx) / Width, (oy + local.y * _kpx) / Height);
        }

        // ------------------------------------------------------------------ pixels

        public Color32[] Render(DiceSkin skin)
        {
            var px = new Color32[Width * Height];
            for (int i = 0; i < px.Length; i++) px[i] = skin.Body;

            if (Shape.VertexLabeled) RenderD4(px, skin);
            else RenderValues(px, skin);
            return px;
        }

        private void RenderValues(Color32[] px, DiceSkin skin)
        {
            var labels = new List<string>();
            for (int i = 0; i < _values.Length; i++) labels.Add(DieInfo.FaceLabel(Shape.Kind, _values[i]));

            int corners = Shape.FaceVerts[0].Length;
            float scale = corners == 3 ? 1.1f : (corners == 4 && Shape.Kind != DieKind.D6 ? 1.0f : (corners == 5 ? 1.2f : 1.45f));
            float heightPx = Shape.FaceInRadius * _kpx * scale;

            for (int i = 0; i < _values.Length; i++)
            {
                string label = labels[i];
                bool underline = IsAmbiguousWhenFlipped(label, labels);
                float cx = (i % Cols) * CellPx + CellPx * 0.5f;
                float cy = (i / Cols) * CellPx + CellPx * 0.5f;
                DrawText(px, label, underline, cx, cy, heightPx, 0f, skin.Ink, i);
            }
        }

        private void RenderD4(Color32[] px, DiceSkin skin)
        {
            Vector2[] slots = Shape.FaceLocal[0]; // all d4 faces are congruent
            for (int i = 0; i < _triples.Count; i++)
            {
                float cx = (i % Cols) * CellPx + CellPx * 0.5f;
                float cy = (i / Cols) * CellPx + CellPx * 0.5f;
                for (int s = 0; s < 3; s++)
                {
                    Vector2 p = slots[s];
                    float r = p.magnitude;
                    float dx = p.x / r, dy = p.y / r;
                    float angle = Mathf.Atan2(-dx, dy); // glyph "up" points from the face centre to this corner
                    float px_ = cx + p.x * _kpx * 0.5f;
                    float py_ = cy + p.y * _kpx * 0.5f;
                    DrawText(px, _triples[i][s].ToString(), false, px_, py_, 0.42f * r * _kpx, angle, skin.Ink, i);
                }
            }
        }

        /// <summary>True when the label read upside-down is a different number on the same die (6/9, 16/91...).</summary>
        public static bool IsAmbiguousWhenFlipped(string label, List<string> all)
        {
            var chars = new char[label.Length];
            for (int i = 0; i < label.Length; i++)
            {
                char c = label[label.Length - 1 - i];
                switch (c)
                {
                    case '0': chars[i] = '0'; break;
                    case '1': chars[i] = '1'; break;
                    case '8': chars[i] = '8'; break;
                    case '6': chars[i] = '9'; break;
                    case '9': chars[i] = '6'; break;
                    default: return false;
                }
            }
            string flipped = new string(chars);
            return flipped != label && all.Contains(flipped);
        }

        private void DrawText(Color32[] px, string text, bool underline, float cx, float cy,
                              float heightPx, float angle, Color32 ink, int cell)
        {
            List<StrokeFont.Segment> segs = StrokeFont.Layout(text, underline);
            float totalW = StrokeFont.TextWidth(text);
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            float rx = cos, ry = sin;   // glyph right in pixel space
            float ux = -sin, uy = cos;  // glyph up in pixel space

            // pixel bounding box of the text rectangle, clipped to the cell
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            float[] us = { -0.25f, totalW + 0.25f };
            float[] vs = { -0.4f, 1.25f };
            for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++)
            {
                float dx = (us[a] - totalW * 0.5f) * heightPx;
                float dy = (vs[b] - 0.5f) * heightPx;
                float x = cx + rx * dx + ux * dy;
                float y = cy + ry * dx + uy * dy;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            int cellX0 = (cell % Cols) * CellPx, cellY0 = (cell / Cols) * CellPx;
            int x0 = Mathf.Max(cellX0, Mathf.FloorToInt(minX));
            int x1 = Mathf.Min(cellX0 + CellPx - 1, Mathf.CeilToInt(maxX));
            int y0 = Mathf.Max(cellY0, Mathf.FloorToInt(minY));
            int y1 = Mathf.Min(cellY0 + CellPx - 1, Mathf.CeilToInt(maxY));

            float aa = 1f / heightPx;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float relx = x + 0.5f - cx, rely = y + 0.5f - cy;
                float u = (relx * rx + rely * ry) / heightPx + totalW * 0.5f;
                float v = (relx * ux + rely * uy) / heightPx + 0.5f;

                float best = float.MaxValue;
                for (int i = 0; i < segs.Count; i++)
                {
                    float d = StrokeFont.DistanceToSegment(u, v, segs[i]);
                    if (d < best) best = d;
                }
                float cov = (StrokeFont.HalfStroke + aa * 0.5f - best) / aa;
                if (cov <= 0f) continue;
                if (cov > 1f) cov = 1f;

                int idx = y * Width + x;
                Color32 bg = px[idx];
                px[idx] = new Color32(
                    (byte)(bg.r + (ink.r - bg.r) * cov),
                    (byte)(bg.g + (ink.g - bg.g) * cov),
                    (byte)(bg.b + (ink.b - bg.b) * cov),
                    255);
            }
        }
    }
}
