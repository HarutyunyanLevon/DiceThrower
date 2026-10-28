using System;
using System.Collections.Generic;
using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// Pure geometry of one die kind, generated in code: vertices, faces (found with a tiny
    /// brute-force convex hull so no face tables have to be typed in), per-face 2D label
    /// coordinates, and which value sits on which face.
    /// Faces are wound clockwise as seen from outside, which is Unity's front-face order.
    /// </summary>
    public sealed class DieShape
    {
        public DieKind Kind;
        public float Radius;                 // circumradius in world units
        public Vector3[] Verts;
        public int[][] FaceVerts;            // vertex indices per face, clockwise from outside
        public Vector3[] FaceNormals;
        public Vector3[] FaceCenters;
        /// <summary>Per face, per vertex: (right, up) position as seen from outside, centred on the face centre.</summary>
        public Vector2[][] FaceLocal;
        public float FaceRMax;               // largest centre->vertex distance over all faces
        public float FaceInRadius;           // smallest centre->edge distance over all faces
        public int[] FaceMeshStart;          // first mesh vertex of each face (flat-shaded mesh layout)
        public int MeshVertexCount;
        /// <summary>d4 only: the number sits at the corners and the top vertex is read.</summary>
        public bool VertexLabeled;
        /// <summary>Value on each face at creation (null for the d4).</summary>
        public int[] InitialFaceValue;
        /// <summary>d4 only: number at each corner at creation. Null = 1..4 in corner order (the generated d4).</summary>
        public int[] InitialVertexLabels;

        public int FaceCount { get { return FaceVerts.Length; } }

        // ------------------------------------------------------------------ cache

        private static readonly Dictionary<DieKind, DieShape> Cache = new Dictionary<DieKind, DieShape>();

        public static DieShape Get(DieKind kind)
        {
            DieShape s;
            if (!Cache.TryGetValue(kind, out s))
            {
                s = Build(kind);
                Cache[kind] = s;
            }
            return s;
        }

        // ------------------------------------------------------------------ build

        /// <summary>Circumradius every die of this kind is scaled to (generated and imported models alike), so physics stays tuned.</summary>
        public static float RadiusOf(DieKind k)
        {
            switch (k)
            {
                case DieKind.D4: return 0.85f;
                case DieKind.D6: return 0.75f;
                case DieKind.D8: return 0.80f;
                case DieKind.D12: return 0.90f;
                case DieKind.D20: return 0.90f;
                default: return 0.85f; // all d10 variants
            }
        }

        private static bool IsD10(DieKind k)
        {
            return k == DieKind.D10 || k == DieKind.D10Tens || k == DieKind.D10Units;
        }

        private static Vector3[] BaseVertices(DieKind k)
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            float inv = 1f / phi;
            var v = new List<Vector3>();
            switch (k)
            {
                case DieKind.D4:
                    v.Add(new Vector3(1, 1, 1));
                    v.Add(new Vector3(1, -1, -1));
                    v.Add(new Vector3(-1, 1, -1));
                    v.Add(new Vector3(-1, -1, 1));
                    break;
                case DieKind.D6:
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                            for (int z = -1; z <= 1; z += 2)
                                v.Add(new Vector3(x, y, z));
                    break;
                case DieKind.D8:
                    v.Add(new Vector3(1, 0, 0)); v.Add(new Vector3(-1, 0, 0));
                    v.Add(new Vector3(0, 1, 0)); v.Add(new Vector3(0, -1, 0));
                    v.Add(new Vector3(0, 0, 1)); v.Add(new Vector3(0, 0, -1));
                    break;
                case DieKind.D12:
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                            for (int z = -1; z <= 1; z += 2)
                                v.Add(new Vector3(x, y, z));
                    for (int a = -1; a <= 1; a += 2)
                        for (int b = -1; b <= 1; b += 2)
                        {
                            v.Add(new Vector3(0, a * inv, b * phi));
                            v.Add(new Vector3(a * inv, b * phi, 0));
                            v.Add(new Vector3(a * phi, 0, b * inv));
                        }
                    break;
                case DieKind.D20:
                    for (int a = -1; a <= 1; a += 2)
                        for (int b = -1; b <= 1; b += 2)
                        {
                            v.Add(new Vector3(0, a, b * phi));
                            v.Add(new Vector3(a, b * phi, 0));
                            v.Add(new Vector3(a * phi, 0, b));
                        }
                    break;
                default:
                    AddTrapezohedron(v);
                    break;
            }
            return v.ToArray();
        }

        /// <summary>
        /// Pentagonal trapezohedron (the d10): two poles and a zig-zag equator of 10 vertices.
        /// The pole height is solved so every kite face is exactly planar.
        /// </summary>
        private static void AddTrapezohedron(List<Vector3> v)
        {
            const float a = 0.11f; // equator zig-zag half-height (ring radius is 1)
            Vector3 u0 = new Vector3(1f, a, 0f);
            Vector3 u1 = new Vector3(Mathf.Cos(72f * Mathf.Deg2Rad), a, Mathf.Sin(72f * Mathf.Deg2Rad));
            Vector3 l0 = new Vector3(Mathf.Cos(36f * Mathf.Deg2Rad), -a, Mathf.Sin(36f * Mathf.Deg2Rad));
            Vector3 n = Vector3.Cross(u1 - u0, l0 - u0);
            float h = a + n.x / n.y; // pole (0,h,0) lies in the plane through u0,u1,l0

            v.Add(new Vector3(0f, h, 0f));
            v.Add(new Vector3(0f, -h, 0f));
            for (int k = 0; k < 5; k++)
            {
                float up = 72f * k * Mathf.Deg2Rad;
                float lo = (72f * k + 36f) * Mathf.Deg2Rad;
                v.Add(new Vector3(Mathf.Cos(up), a, Mathf.Sin(up)));
                v.Add(new Vector3(Mathf.Cos(lo), -a, Mathf.Sin(lo)));
            }
        }

        private static DieShape Build(DieKind kind)
        {
            Vector3[] verts = BaseVertices(kind);

            // scale to the wanted circumradius
            float maxLen = 0f;
            for (int i = 0; i < verts.Length; i++) maxLen = Mathf.Max(maxLen, verts[i].magnitude);
            float radius = RadiusOf(kind);
            for (int i = 0; i < verts.Length; i++) verts[i] = verts[i] * (radius / maxLen);

            List<int[]> faces = HullFaces(verts, radius);
            int expected = DieInfo.FaceCount(kind);
            if (faces.Count != expected)
                throw new InvalidOperationException(kind + ": built " + faces.Count + " faces, expected " + expected);

            var s = new DieShape
            {
                Kind = kind,
                Radius = radius,
                Verts = verts,
                VertexLabeled = kind == DieKind.D4,
            };

            int fc = faces.Count;
            s.FaceVerts = new int[fc][];
            s.FaceNormals = new Vector3[fc];
            s.FaceCenters = new Vector3[fc];
            s.FaceLocal = new Vector2[fc][];
            s.FaceMeshStart = new int[fc];

            int meshVerts = 0;
            for (int f = 0; f < fc; f++)
            {
                OrientFace(s, f, faces[f], IsD10(kind));
                s.FaceMeshStart[f] = meshVerts;
                meshVerts += s.FaceVerts[f].Length;
            }
            s.MeshVertexCount = meshVerts;

            float rmax = 0f, inr = float.MaxValue;
            for (int f = 0; f < fc; f++)
            {
                Vector2[] loc = s.FaceLocal[f];
                for (int i = 0; i < loc.Length; i++)
                {
                    rmax = Mathf.Max(rmax, loc[i].magnitude);
                    Vector2 a = loc[i];
                    Vector2 b = loc[(i + 1) % loc.Length];
                    Vector2 e = b - a;
                    float dist = Mathf.Abs(a.x * e.y - a.y * e.x) / e.magnitude; // distance from origin to edge line
                    inr = Mathf.Min(inr, dist);
                }
            }
            s.FaceRMax = rmax;
            s.FaceInRadius = inr;

            if (!s.VertexLabeled) s.InitialFaceValue = AssignValues(s);
            return s;
        }

        /// <summary>Finds all faces of a convex point cloud centred on the origin (n^4, n is at most 20).</summary>
        private static List<int[]> HullFaces(Vector3[] v, float scale)
        {
            float eps = 1e-3f * scale;
            var result = new List<int[]>();
            var seen = new HashSet<string>();
            for (int i = 0; i < v.Length; i++)
            for (int j = i + 1; j < v.Length; j++)
            for (int k = j + 1; k < v.Length; k++)
            {
                Vector3 n = Vector3.Cross(v[j] - v[i], v[k] - v[i]);
                float len = n.magnitude;
                if (len < 1e-6f) continue;
                n = n / len;
                float d = Vector3.Dot(n, v[i]);

                bool pos = false, neg = false;
                for (int p = 0; p < v.Length; p++)
                {
                    float sdist = Vector3.Dot(n, v[p]) - d;
                    if (sdist > eps) pos = true;
                    else if (sdist < -eps) neg = true;
                }
                if (pos && neg) continue; // vertices on both sides: not a hull face
                if (pos) { n = -n; d = -d; } // make the normal point away from all vertices

                var idx = new List<int>();
                for (int p = 0; p < v.Length; p++)
                {
                    if (Mathf.Abs(Vector3.Dot(n, v[p]) - d) <= eps) idx.Add(p);
                }
                idx.Sort();
                string key = string.Join(",", idx.ConvertAll(x => x.ToString()).ToArray());
                if (seen.Add(key)) result.Add(idx.ToArray());
            }
            return result;
        }

        /// <summary>
        /// Computes the face normal and centre, sorts the vertices clockwise as seen from outside,
        /// and builds the (right, up) label coordinates with "up" toward the first vertex.
        /// </summary>
        private static void OrientFace(DieShape s, int f, int[] vertIdx, bool pickPoleFirst)
        {
            int n = vertIdx.Length;
            Vector3 c = Vector3.zero;
            for (int i = 0; i < n; i++) c += s.Verts[vertIdx[i]];
            c = c / n;

            Vector3 normal = Vector3.Cross(s.Verts[vertIdx[1]] - s.Verts[vertIdx[0]], s.Verts[vertIdx[2]] - s.Verts[vertIdx[0]]).normalized;
            if (Vector3.Dot(normal, c) < 0f) normal = -normal; // outward

            // temporary basis to sort by angle
            Vector3 up0 = s.Verts[vertIdx[0]] - c;
            up0 = (up0 - normal * Vector3.Dot(up0, normal)).normalized;
            Vector3 fwd = -normal;
            Vector3 right0 = Vector3.Cross(up0, fwd).normalized;

            var order = new List<int>(vertIdx);
            var angle = new Dictionary<int, float>();
            for (int i = 0; i < n; i++)
            {
                Vector3 d = s.Verts[vertIdx[i]] - c;
                angle[vertIdx[i]] = Mathf.Atan2(Vector3.Dot(d, up0), Vector3.Dot(d, right0));
            }
            order.Sort((a, b) => angle[b].CompareTo(angle[a])); // descending = clockwise seen from outside

            if (pickPoleFirst)
            {
                // d10 kites: put the pole vertex (on the vertical axis) in slot 0 so labels point their tip up
                int start = 0;
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = s.Verts[order[i]];
                    float off = p.x * p.x + p.z * p.z;
                    if (off < best) { best = off; start = i; }
                }
                var rotated = new List<int>();
                for (int i = 0; i < n; i++) rotated.Add(order[(start + i) % n]);
                order = rotated;
            }

            Vector3 up = s.Verts[order[0]] - c;
            up = (up - normal * Vector3.Dot(up, normal)).normalized;
            Vector3 right = Vector3.Cross(up, fwd).normalized;

            s.FaceVerts[f] = order.ToArray();
            s.FaceNormals[f] = normal;
            s.FaceCenters[f] = c;
            var local = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 d = s.Verts[order[i]] - c;
                local[i] = new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, up));
            }
            s.FaceLocal[f] = local;
        }

        /// <summary>Opposite faces get values that sum to (max+min), like a real die.</summary>
        private static int[] AssignValues(DieShape s)
        {
            int fc = s.FaceCount;
            int[] vals = DieInfo.FaceValues(s.Kind);
            var result = new int[fc];
            var done = new bool[fc];
            int pair = 0;
            for (int f = 0; f < fc; f++)
            {
                if (done[f]) continue;
                int opp = -1;
                float best = 2f;
                for (int g = 0; g < fc; g++)
                {
                    if (g == f) continue;
                    float dot = Vector3.Dot(s.FaceNormals[f], s.FaceNormals[g]);
                    if (dot < best) { best = dot; opp = g; }
                }
                result[f] = vals[pair];
                result[opp] = vals[fc - 1 - pair];
                done[f] = true;
                done[opp] = true;
                pair++;
            }
            return result;
        }
    }
}
