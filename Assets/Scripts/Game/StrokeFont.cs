using System.Collections.Generic;
using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// Tiny vector digit font (strokes in a 0.6 x 1.0 box, y up). Drawn with distance-to-segment
    /// anti-aliasing, so numbers stay crisp at any size/rotation without needing a font asset.
    /// </summary>
    public static class StrokeFont
    {
        public const float GlyphWidth = 0.6f;
        public const float Advance = 0.76f;     // distance between glyph origins
        public const float HalfStroke = 0.065f; // stroke half-width in glyph units

        private static readonly Dictionary<char, Vector2[][]> Glyphs = Build();

        private static Vector2[] Pts(params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return p;
        }

        private static Vector2[] Ellipse(float cx, float cy, float rx, float ry, int n)
        {
            var p = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI * 2f * i / n;
                p[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            }
            return p;
        }

        private static Vector2[] Rot180(Vector2[] src)
        {
            var p = new Vector2[src.Length];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(GlyphWidth - src[i].x, 1f - src[i].y);
            return p;
        }

        private static Dictionary<char, Vector2[][]> Build()
        {
            Vector2[] six = Pts(0.50f, 0.92f, 0.36f, 0.98f, 0.20f, 0.90f, 0.08f, 0.70f, 0.04f, 0.42f,
                                0.07f, 0.18f, 0.20f, 0.04f, 0.36f, 0.03f, 0.50f, 0.12f, 0.56f, 0.28f,
                                0.52f, 0.44f, 0.38f, 0.53f, 0.22f, 0.52f, 0.09f, 0.40f);
            var g = new Dictionary<char, Vector2[][]>();
            g['0'] = new[] { Ellipse(0.30f, 0.50f, 0.26f, 0.46f, 20) };
            g['1'] = new[] { Pts(0.12f, 0.78f, 0.32f, 0.97f, 0.32f, 0.03f) };
            g['2'] = new[] { Pts(0.04f, 0.74f, 0.12f, 0.90f, 0.30f, 0.97f, 0.48f, 0.90f, 0.56f, 0.74f,
                                 0.50f, 0.58f, 0.04f, 0.03f, 0.58f, 0.03f) };
            g['3'] = new[]
            {
                Pts(0.04f, 0.82f, 0.18f, 0.95f, 0.34f, 0.97f, 0.50f, 0.90f, 0.54f, 0.76f,
                    0.48f, 0.62f, 0.32f, 0.54f, 0.20f, 0.53f),
                Pts(0.32f, 0.54f, 0.50f, 0.46f, 0.57f, 0.30f, 0.52f, 0.12f, 0.36f, 0.03f,
                    0.18f, 0.04f, 0.04f, 0.16f),
            };
            g['4'] = new[] { Pts(0.44f, 0.03f, 0.44f, 0.97f, 0.02f, 0.30f, 0.58f, 0.30f) };
            g['5'] = new[] { Pts(0.54f, 0.97f, 0.10f, 0.97f, 0.07f, 0.56f, 0.24f, 0.62f, 0.42f, 0.60f,
                                 0.55f, 0.46f, 0.57f, 0.28f, 0.48f, 0.10f, 0.30f, 0.03f, 0.14f, 0.06f, 0.04f, 0.18f) };
            g['6'] = new[] { six };
            g['7'] = new[] { Pts(0.04f, 0.97f, 0.57f, 0.97f, 0.22f, 0.03f) };
            g['8'] = new[] { Ellipse(0.30f, 0.75f, 0.20f, 0.21f, 16), Ellipse(0.30f, 0.27f, 0.25f, 0.25f, 16) };
            g['9'] = new[] { Rot180(six) };
            return g;
        }

        /// <summary>One straight stroke segment in text space (digit offsets already applied).</summary>
        public struct Segment
        {
            public Vector2 A, B;
        }

        /// <summary>Total text width in glyph units.</summary>
        public static float TextWidth(string text)
        {
            if (text.Length == 0) return 0f;
            return (text.Length - 1) * Advance + GlyphWidth;
        }

        /// <summary>Flattens a string (plus optional underline) into segments in text space.</summary>
        public static List<Segment> Layout(string text, bool underline)
        {
            var list = new List<Segment>();
            for (int i = 0; i < text.Length; i++)
            {
                Vector2[][] strokes;
                if (!Glyphs.TryGetValue(text[i], out strokes)) continue;
                float ox = i * Advance;
                for (int s = 0; s < strokes.Length; s++)
                {
                    Vector2[] line = strokes[s];
                    for (int k = 0; k < line.Length - 1; k++)
                    {
                        list.Add(new Segment
                        {
                            A = new Vector2(line[k].x + ox, line[k].y),
                            B = new Vector2(line[k + 1].x + ox, line[k + 1].y),
                        });
                    }
                }
            }
            if (underline && text.Length > 0)
            {
                list.Add(new Segment { A = new Vector2(0f, -0.16f), B = new Vector2(TextWidth(text), -0.16f) });
            }
            return list;
        }

        public static float DistanceToSegment(float px, float py, Segment s)
        {
            float abx = s.B.x - s.A.x, aby = s.B.y - s.A.y;
            float apx = px - s.A.x, apy = py - s.A.y;
            float len2 = abx * abx + aby * aby;
            float t = len2 > 0f ? (apx * abx + apy * aby) / len2 : 0f;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float dx = apx - abx * t, dy = apy - aby * t;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
