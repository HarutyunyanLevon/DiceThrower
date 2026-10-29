using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DiceThrower.Game
{
    /// <summary>
    /// The arrays of one generated tray mesh. Render meshes have four submeshes (top leather, rim + outside, stitching, hardware);
    /// the collision mesh has one and is a closed solid.
    /// Each vertex's <see cref="FlexDir"/> is the outward direction (x, z) the leather moves in, scaled by how free that spot is to move.
    /// </summary>
    public sealed class LeatherTrayMeshData
    {
        public const int TopSubmesh = 0, LiningSubmesh = 1, StitchSubmesh = 2, HardwareSubmesh = 3, RenderSubmeshCount = 4;

        public Vector3[] Vertices;
        public Vector3[] Normals;   // null for the collision mesh
        public Vector2[] Uv;        // null for the collision mesh
        public Vector2[] FlexDir;   // null for the collision mesh
        public int[][] Triangles;
        /// <summary>Playable floor size (inside the walls).</summary>
        public Vector2 FloorSize;
        /// <summary>Width (x) and depth (z) at the rim.</summary>
        public Vector2 OuterExtent;
        /// <summary>Height of the rim above the floor.</summary>
        public float Height;
        /// <summary>How many times the grain texture repeats once around the tray (a whole number, so the texture closes without a seam). 0 for the collision mesh.</summary>
        public int GrainTiles;

        public int TriangleCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Triangles.Length; i++) n += Triangles[i].Length / 3;
                return n;
            }
        }

        /// <summary>Fills <paramref name="into"/> (or a new mesh) with this data.</summary>
        public Mesh ToMesh(Mesh into)
        {
            Mesh m = into != null ? into : new Mesh();
            m.Clear();
            m.indexFormat = Vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.vertices = Vertices;
            if (Normals != null) m.normals = Normals;
            if (Uv != null) m.uv = Uv;
            if (FlexDir != null) m.uv2 = FlexDir;
            m.subMeshCount = Triangles.Length;
            for (int s = 0; s < Triangles.Length; s++) m.SetTriangles(Triangles[s], s, false);
            m.RecalculateBounds();
            if (Normals != null && Uv != null) m.RecalculateTangents(); // the leather's normal map needs them
            return m;
        }
    }

    /// <summary>
    /// Builds a leather tray from a <see cref="LeatherTrayStyle"/>: a rounded-rectangle floor with a wall swept around it.
    /// The wall's cross-section (floor, bend, flared wall, grooves, rolled rim, outer face) is computed per position along the
    /// edge, so the wall can sag between the corners and splay at them like folded leather. Pure maths, no scene needed.
    /// </summary>
    public static class LeatherTrayGeometry
    {
        // ------------------------------------------------------------------ constants of the construction

        private const float MinFillet = 0.05f;
        private const float MinWallScale = 0.3f;            // hand-made wobble never makes a wall shorter than this share of its length
        private const float MaxLeanDegrees = 70f;
        private const float IrregularAngleDegrees = 40f;   // lean wobble in degrees per 1.0 of Irregularity
        private const float StitchEmbed = 0.012f;           // stitches sink slightly into the leather so they never float
        private const float StitchTopTaper = 0.55f;         // stitch top width as a share of its base width
        private const float RivetDomeHeight = 0.8f;         // dome height as a share of its radius
        private const int RivetRings = 4, RivetSides = 10;
        private const float OuterSolveTolerance = 0.002f;
        private const int OuterSolvePasses = 6;

        private static readonly int[] NoiseHarmonics = { 3, 5, 8, 13 };
        private static readonly float[] NoiseWeights = { 1f, 0.7f, 0.5f, 0.3f };

        // ------------------------------------------------------------------ public entry points

        /// <summary>The render mesh: leather, rim, stitching and hardware.</summary>
        public static LeatherTrayMeshData BuildRender(LeatherTrayStyle style)
        {
            Vector2 floor = SolveFloor(style);
            Shell shell = BuildShell(style, RenderDetail(style), floor, true);
            return EmitRender(style, shell, floor);
        }

        /// <summary>
        /// The collision mesh: the same inner surface at lower resolution (no grooves, no stitches), closed into a solid so a fast die
        /// cannot get behind it. A few hundred triangles; static, so the physics cost is that of a handful of convex-vs-triangle tests per die.
        /// </summary>
        public static LeatherTrayMeshData BuildCollision(LeatherTrayStyle style)
        {
            Vector2 floor = SolveFloor(style);
            Shell shell = BuildShell(style, CollisionDetail(style), floor, false);
            return EmitCollision(shell, floor);
        }

        // ------------------------------------------------------------------ detail levels

        private struct Detail
        {
            public int Side, Corner, Fillet, Wall, Rim;
            public bool Grooves, Bead;
            public float Thick;
        }

        private static Detail RenderDetail(LeatherTrayStyle st)
        {
            return new Detail
            {
                Side = Mathf.Max(1, st.sideSegments),
                Corner = Mathf.Max(2, st.cornerSegments),
                Fillet = Mathf.Max(1, st.filletSegments),
                Wall = Mathf.Max(1, st.wallSegments),
                Rim = Mathf.Max(2, st.rimSegments),
                Grooves = true,
                Bead = true,
                Thick = Mathf.Max(0.01f, st.thickness),
            };
        }

        /// <summary>Half the render resolution (never below 2), a straight wall, no extras.</summary>
        private static Detail CollisionDetail(LeatherTrayStyle st)
        {
            Detail r = RenderDetail(st);
            return new Detail
            {
                Side = Mathf.Max(2, r.Side / 2),
                Corner = Mathf.Max(2, r.Corner / 2),
                Fillet = Mathf.Max(2, r.Fillet / 2),
                Wall = 1,
                Rim = 2,
                Grooves = false,
                Bead = false,
                Thick = Mathf.Max(0.05f, st.collisionThickness),
            };
        }

        // ------------------------------------------------------------------ the floor size follows from the outer size

        /// <summary>The walls take room, so the floor is found by building the tray and growing/shrinking it until its rim measures the wanted outer size.</summary>
        private static Vector2 SolveFloor(LeatherTrayStyle st)
        {
            Vector2 target = st.outerSize;
            Vector2 floor = target - Vector2.one * (st.wallLength * 0.4f);
            Detail d = RenderDetail(st);
            for (int pass = 0; pass < OuterSolvePasses; pass++)
            {
                floor = new Vector2(Mathf.Max(floor.x, 1f), Mathf.Max(floor.y, 1f));
                Shell s = BuildShell(st, d, floor, false);
                Vector2 got = new Vector2(2f * s.MaxX, 2f * s.MaxZ);
                Vector2 err = target - got;
                if (Mathf.Abs(err.x) < OuterSolveTolerance && Mathf.Abs(err.y) < OuterSolveTolerance) break;
                floor += err;
            }
            return new Vector2(Mathf.Max(floor.x, 1f), Mathf.Max(floor.y, 1f));
        }

        // ------------------------------------------------------------------ the swept shell

        private struct Sample
        {
            public Vector2 P;      // point on the floor's edge (x, z)
            public Vector2 N;      // outward direction (x, z)
            public float Corner;   // 0 in the middle of a side, 1 in a corner
            public bool ArcMid;    // middle of a corner arc
        }

        private sealed class Shell
        {
            public int M, K, Ki;
            public Vector3[] Pos;          // M * K, index i * K + j
            public Vector2[] Plan;         // outward direction per sample
            public float[] RowU;           // per row: how far up the wall it sits (0..1), drives how free it is to flex
            public float[] RowV;           // M * K: distance along the cross-section from the floor edge
            public float MaxX, MaxZ, TopY;
            public Vector3[][] StitchPos;  // per stitch row, per sample
            public Vector3[][] StitchNrm;
            public float[] StitchFlex;     // per stitch row: how free it is to flex
            public readonly List<Vector3> RivetPos = new List<Vector3>();
            public readonly List<Vector3> RivetNrm = new List<Vector3>();
            public readonly List<Vector2> RivetPlan = new List<Vector2>();
            public float RivetFlex;
        }

        private static Shell BuildShell(LeatherTrayStyle st, Detail q, Vector2 floorSize, bool extras)
        {
            List<Sample> samples = PlanSamples(floorSize * 0.5f, st.cornerRadius, q.Side, q.Corner, st.cornerSharpness);
            int M = samples.Count;
            float[] cum = new float[M + 1];
            for (int i = 0; i < M; i++) cum[i + 1] = cum[i] + (samples[(i + 1) % M].P - samples[i].P).magnitude;
            float perimeter = Mathf.Max(cum[M], 1e-3f);

            float[] fr, dep;
            WallFractions(st, q, out fr, out dep);
            int nFillet = q.Fillet + 1;
            int Ki = nFillet + (fr.Length - 1);
            int Kc = Mathf.Max(0, q.Rim - 1);
            int K = Ki + Kc + Ki;

            var sh = new Shell
            {
                M = M, K = K, Ki = Ki,
                Pos = new Vector3[M * K],
                Plan = new Vector2[M],
                RowU = new float[K],
                RowV = new float[M * K],
            };

            // how far up the wall each row sits
            float[] innerU = new float[Ki];
            for (int j = 0; j < nFillet; j++) innerU[j] = 0f;
            for (int m = 1; m < fr.Length; m++) innerU[nFillet + m - 1] = fr[m];
            for (int j = 0; j < Ki; j++) sh.RowU[j] = innerU[j];
            for (int j = 0; j < Kc; j++) sh.RowU[Ki + j] = 1f;
            for (int j = 0; j < Ki; j++) sh.RowU[K - 1 - j] = innerU[j];

            // seeded, periodic hand-made wobble
            var rng = new System.Random(st.seed);
            float[] phaseHeight = new float[NoiseHarmonics.Length], phaseLean = new float[NoiseHarmonics.Length];
            for (int h = 0; h < NoiseHarmonics.Length; h++)
            {
                phaseHeight[h] = (float)(rng.NextDouble() * 2.0 * System.Math.PI);
                phaseLean[h] = (float)(rng.NextDouble() * 2.0 * System.Math.PI);
            }

            int stitchRows = extras && st.stitchRowsFromRim != null ? st.stitchRowsFromRim.Length : 0;
            sh.StitchPos = new Vector3[stitchRows][];
            sh.StitchNrm = new Vector3[stitchRows][];
            sh.StitchFlex = new float[stitchRows];
            for (int r = 0; r < stitchRows; r++)
            {
                sh.StitchPos[r] = new Vector3[M];
                sh.StitchNrm[r] = new Vector3[M];
                sh.StitchFlex[r] = FlexWeight(1f - st.stitchRowsFromRim[r]);
            }
            sh.RivetFlex = FlexWeight(1f - st.rivetFromRim);

            var inner = new Vector2[Ki];
            var outer = new Vector2[Ki];
            var normal = new Vector2[Ki];
            var row = new Vector2[K];
            sh.MaxX = sh.MaxZ = 0f;
            sh.TopY = float.MinValue;

            for (int i = 0; i < M; i++)
            {
                Sample s = samples[i];
                sh.Plan[i] = s.N;

                float theta = 2f * Mathf.PI * cum[i] / perimeter;
                float noiseHeight = Noise(theta, phaseHeight), noiseLean = Noise(theta, phaseLean);
                float scale = Mathf.Lerp(1f - st.droop, 1f + st.cornerRise, s.Corner) * (1f + st.irregularity * noiseHeight);
                scale = Mathf.Max(MinWallScale, scale);
                float leanDeg = Mathf.Clamp(st.flareDegrees + st.cornerFlareDegrees * s.Corner + st.irregularity * IrregularAngleDegrees * noiseLean, 0f, MaxLeanDegrees);

                float a = leanDeg * Mathf.Deg2Rad;
                Vector2 w = new Vector2(Mathf.Sin(a), Mathf.Cos(a));        // up the wall
                Vector2 nIn = new Vector2(-Mathf.Cos(a), Mathf.Sin(a));    // faces the inside of the tray
                float length = st.wallLength * scale;

                // floor edge -> bend -> wall (grooves are notches pressed into the leather)
                float R = Mathf.Max(MinFillet, st.floorFillet);
                float turn = Mathf.PI * 0.5f - a;
                for (int k = 0; k <= q.Fillet; k++)
                {
                    float phi = turn * k / q.Fillet;
                    inner[k] = new Vector2(R * Mathf.Sin(phi), R - R * Mathf.Cos(phi));
                }
                Vector2 end = inner[q.Fillet];
                for (int m = 1; m < fr.Length; m++)
                    inner[nFillet + m - 1] = end + w * (fr[m] * length) - nIn * dep[m];

                // smooth cross-section directions, outer face = inner pushed out by the thickness
                for (int j = 0; j < Ki; j++)
                {
                    Vector2 dPrev = j > 0 ? (inner[j] - inner[j - 1]).normalized : Vector2.zero;
                    Vector2 dNext = j < Ki - 1 ? (inner[j + 1] - inner[j]).normalized : Vector2.zero;
                    Vector2 d = dPrev + dNext;
                    d = d.sqrMagnitude > 1e-8f ? d.normalized : w;
                    normal[j] = new Vector2(-d.y, d.x);
                    float bead = q.Bead ? st.rimBead * Smooth(st.beadStart, 1f, innerU[j]) : 0f;
                    outer[j] = inner[j] - normal[j] * (q.Thick + bead);
                }

                // rolled rim: a half circle from the top of the inner face over to the top of the outer face
                Vector2 top = inner[Ki - 1], topOuter = outer[Ki - 1];
                Vector2 centre = (top + topOuter) * 0.5f;
                Vector2 toTop = top - centre;
                float radius = toTop.magnitude;

                for (int j = 0; j < Ki; j++) row[j] = inner[j];
                for (int c = 1; c <= Kc; c++)
                {
                    float psi = Mathf.PI * c / q.Rim;
                    row[Ki + c - 1] = centre + toTop * Mathf.Cos(psi) + w * (radius * Mathf.Sin(psi));
                }
                for (int j = 0; j < Ki; j++) row[K - 1 - j] = outer[j];

                float along = 0f;
                for (int j = 0; j < K; j++)
                {
                    if (j > 0) along += (row[j] - row[j - 1]).magnitude;
                    sh.RowV[i * K + j] = along;
                    Vector3 p = new Vector3(s.P.x + s.N.x * row[j].x, row[j].y, s.P.y + s.N.y * row[j].x);
                    sh.Pos[i * K + j] = p;
                    sh.MaxX = Mathf.Max(sh.MaxX, Mathf.Abs(p.x));
                    sh.MaxZ = Mathf.Max(sh.MaxZ, Mathf.Abs(p.z));
                    sh.TopY = Mathf.Max(sh.TopY, p.y);
                }

                if (!extras) continue;

                Vector3 n3 = new Vector3(s.N.x * nIn.x, nIn.y, s.N.y * nIn.x);
                for (int r = 0; r < stitchRows; r++)
                {
                    Vector2 onWall = end + w * ((1f - st.stitchRowsFromRim[r]) * length);
                    sh.StitchPos[r][i] = new Vector3(s.P.x + s.N.x * onWall.x, onWall.y, s.P.y + s.N.y * onWall.x);
                    sh.StitchNrm[r][i] = n3;
                }
                if (st.rivets && s.ArcMid)
                {
                    Vector2 onWall = end + w * ((1f - st.rivetFromRim) * length);
                    sh.RivetPos.Add(new Vector3(s.P.x + s.N.x * onWall.x, onWall.y, s.P.y + s.N.y * onWall.x));
                    sh.RivetNrm.Add(n3);
                    sh.RivetPlan.Add(s.N);
                }
            }
            return sh;
        }

        /// <summary>How free a spot at this height up the wall (0 = floor, 1 = rim) is to flex.</summary>
        private static float FlexWeight(float u)
        {
            return Smooth(0f, 1f, u);
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(1e-5f, b - a));
            return t * t * (3f - 2f * t);
        }

        private static float Noise(float theta, float[] phase)
        {
            float sum = 0f, weight = 0f;
            for (int h = 0; h < NoiseHarmonics.Length; h++)
            {
                sum += NoiseWeights[h] * Mathf.Sin(NoiseHarmonics[h] * theta + phase[h]);
                weight += NoiseWeights[h];
            }
            return sum / weight;
        }

        /// <summary>Where up the wall the cross-section has points (as shares of the wall length) and how deep each is pressed in (grooves).</summary>
        private static void WallFractions(LeatherTrayStyle st, Detail q, out float[] fractions, out float[] depths)
        {
            int segs = Mathf.Max(1, q.Wall);
            float half = q.Grooves && st.wallLength > 1e-4f ? st.grooveWidth * 0.5f / st.wallLength : 0f;
            var centres = new List<float>();
            if (q.Grooves && st.grooveFromRim != null)
            {
                for (int g = 0; g < st.grooveFromRim.Length; g++)
                {
                    float c = 1f - st.grooveFromRim[g];
                    if (c - half > 0.05f && c + half < 0.98f) centres.Add(c);
                }
            }

            var list = new List<KeyValuePair<float, float>>();
            for (int k = 0; k <= segs; k++)
            {
                float u = k / (float)segs;
                bool covered = false;
                if (k != 0 && k != segs)
                    for (int c = 0; c < centres.Count; c++)
                        if (Mathf.Abs(u - centres[c]) < half * 1.05f + 1e-4f) covered = true;
                if (!covered) list.Add(new KeyValuePair<float, float>(u, 0f));
            }
            for (int c = 0; c < centres.Count; c++)
            {
                list.Add(new KeyValuePair<float, float>(centres[c] - half, 0f));
                list.Add(new KeyValuePair<float, float>(centres[c], st.grooveDepth));
                list.Add(new KeyValuePair<float, float>(centres[c] + half, 0f));
            }
            list.Sort((x, y) => x.Key.CompareTo(y.Key));

            var f = new List<float>();
            var d = new List<float>();
            for (int i = 0; i < list.Count; i++)
            {
                if (f.Count > 0 && list[i].Key - f[f.Count - 1] < 1e-4f) continue;
                f.Add(list[i].Key);
                d.Add(list[i].Value);
            }
            fractions = f.ToArray();
            depths = d.ToArray();
        }

        // ------------------------------------------------------------------ the floor's outline

        private static List<Sample> PlanSamples(Vector2 half, float cornerRadius, int sideSegs, int cornerSegs, float sharpness)
        {
            float rc = Mathf.Clamp(cornerRadius, 0.05f, Mathf.Min(half.x, half.y) - 0.05f);
            float ax = half.x - rc, az = half.y - rc;
            var list = new List<Sample>();
            AddSide(list, new Vector2(half.x, -az), new Vector2(half.x, az), new Vector2(1f, 0f), sideSegs, sharpness);
            AddArc(list, new Vector2(ax, az), 0f, rc, cornerSegs);
            AddSide(list, new Vector2(ax, half.y), new Vector2(-ax, half.y), new Vector2(0f, 1f), sideSegs, sharpness);
            AddArc(list, new Vector2(-ax, az), 90f, rc, cornerSegs);
            AddSide(list, new Vector2(-half.x, az), new Vector2(-half.x, -az), new Vector2(-1f, 0f), sideSegs, sharpness);
            AddArc(list, new Vector2(-ax, -az), 180f, rc, cornerSegs);
            AddSide(list, new Vector2(-ax, -half.y), new Vector2(ax, -half.y), new Vector2(0f, -1f), sideSegs, sharpness);
            AddArc(list, new Vector2(ax, -az), 270f, rc, cornerSegs);
            return list;
        }

        private static void AddSide(List<Sample> list, Vector2 from, Vector2 to, Vector2 n, int segs, float sharpness)
        {
            if ((to - from).magnitude < 1e-3f) return;
            for (int k = 0; k < segs; k++)
            {
                float u = k / (float)segs;
                float corner = Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.PI * u)), sharpness);
                list.Add(new Sample { P = Vector2.Lerp(from, to, u), N = n, Corner = corner });
            }
        }

        private static void AddArc(List<Sample> list, Vector2 centre, float startDegrees, float radius, int segs)
        {
            for (int k = 0; k < segs; k++)
            {
                float ang = (startDegrees + 90f * k / segs) * Mathf.Deg2Rad;
                Vector2 n = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                list.Add(new Sample { P = centre + n * radius, N = n, Corner = 1f, ArcMid = k == segs / 2 });
            }
        }

        // ------------------------------------------------------------------ render mesh

        private static LeatherTrayMeshData EmitRender(LeatherTrayStyle st, Shell sh, Vector2 floorSize)
        {
            int M = sh.M, K = sh.K, Ki = sh.Ki;
            float tile = st.GrainTileSize;

            var V = new List<Vector3>();
            var N = new List<Vector3>();
            var UV = new List<Vector2>();
            var FD = new List<Vector2>();
            var top = new List<int>();
            var lining = new List<int>();
            var stitch = new List<int>();
            var hardware = new List<int>();

            // ---- the wall: a grid, M + 1 columns (the last repeats the first so the texture can close its loop) x K rows
            float[] rowLength = new float[K];
            for (int j = 0; j < K; j++)
            {
                float len = 0f;
                for (int i = 0; i < M; i++) len += (sh.Pos[((i + 1) % M) * K + j] - sh.Pos[i * K + j]).magnitude;
                rowLength[j] = Mathf.Max(len, 1e-3f);
            }
            int tiles = Mathf.Max(1, Mathf.RoundToInt(rowLength[0] / tile));
            float[] run = new float[K];

            for (int i = 0; i <= M; i++)
            {
                int ic = i % M, ip = (ic + 1) % M, im = (ic - 1 + M) % M;
                for (int j = 0; j < K; j++)
                {
                    V.Add(sh.Pos[ic * K + j]);

                    Vector3 di = sh.Pos[ip * K + j] - sh.Pos[im * K + j];
                    Vector3 dj = sh.Pos[ic * K + Mathf.Min(j + 1, K - 1)] - sh.Pos[ic * K + Mathf.Max(j - 1, 0)];
                    Vector3 n = Vector3.Cross(di, dj);
                    N.Add(n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up);

                    UV.Add(new Vector2(tiles * run[j] / rowLength[j], sh.RowV[ic * K + j] / tile));
                    FD.Add(sh.Plan[ic] * FlexWeight(sh.RowU[j]));
                }
                for (int j = 0; j < K; j++) run[j] += (sh.Pos[ip * K + j] - sh.Pos[ic * K + j]).magnitude;
            }

            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < K - 1; j++)
                {
                    List<int> target = j < Ki - 1 ? top : lining;
                    int a = i * K + j, b = (i + 1) * K + j, c = i * K + j + 1, d = (i + 1) * K + j + 1;
                    target.Add(a); target.Add(b); target.Add(c);
                    target.Add(b); target.Add(d); target.Add(c);
                }
            }

            // ---- the floor: a fan from the middle to the floor's edge
            int centre = V.Count;
            V.Add(Vector3.zero); N.Add(Vector3.up); UV.Add(Vector2.zero); FD.Add(Vector2.zero);
            int ringStart = V.Count;
            for (int i = 0; i < M; i++)
            {
                Vector3 p = sh.Pos[i * K];
                V.Add(p); N.Add(Vector3.up); UV.Add(new Vector2(p.x / tile, p.z / tile)); FD.Add(Vector2.zero);
            }
            for (int i = 0; i < M; i++)
            {
                top.Add(centre); top.Add(ringStart + (i + 1) % M); top.Add(ringStart + i);
            }

            // ---- stitching
            for (int r = 0; r < sh.StitchPos.Length; r++)
                EmitStitchRow(st, sh, r, V, N, UV, FD, stitch);

            // ---- rivets
            for (int r = 0; r < sh.RivetPos.Count; r++)
                EmitRivet(st, sh.RivetPos[r], sh.RivetNrm[r], sh.RivetPlan[r] * sh.RivetFlex, V, N, UV, FD, hardware);

            return new LeatherTrayMeshData
            {
                Vertices = V.ToArray(),
                Normals = N.ToArray(),
                Uv = UV.ToArray(),
                FlexDir = FD.ToArray(),
                Triangles = new[] { top.ToArray(), lining.ToArray(), stitch.ToArray(), hardware.ToArray() },
                FloorSize = floorSize,
                OuterExtent = new Vector2(2f * sh.MaxX, 2f * sh.MaxZ),
                Height = sh.TopY,
                GrainTiles = tiles,
            };
        }

        /// <summary>Evenly spaced stitches along the closed curve of one stitch row.</summary>
        private static void EmitStitchRow(LeatherTrayStyle st, Shell sh, int row, List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<Vector2> FD, List<int> tris)
        {
            int M = sh.M;
            Vector3[] pos = sh.StitchPos[row], nrm = sh.StitchNrm[row];
            float[] cum = new float[M + 1];
            for (int i = 0; i < M; i++) cum[i + 1] = cum[i] + (pos[(i + 1) % M] - pos[i]).magnitude;
            float total = cum[M];
            int count = Mathf.Max(1, Mathf.FloorToInt(total / Mathf.Max(0.05f, st.stitchPitch)));
            float pitch = total / count;

            int seg = 0;
            for (int s = 0; s < count; s++)
            {
                float d = (s + 0.5f) * pitch;
                while (seg < M - 1 && cum[seg + 1] < d) seg++;
                int next = (seg + 1) % M;
                float span = Mathf.Max(1e-5f, cum[seg + 1] - cum[seg]);
                float t = Mathf.Clamp01((d - cum[seg]) / span);
                Vector3 centre = Vector3.Lerp(pos[seg], pos[next], t);
                Vector3 normal = Vector3.Slerp(nrm[seg], nrm[next], t).normalized;
                Vector3 tangent = (pos[next] - pos[seg]).normalized;
                tangent = (tangent - normal * Vector3.Dot(tangent, normal)).normalized;
                Vector3 side = Vector3.Cross(normal, tangent);
                Vector2 flex = Vector2.Lerp(sh.Plan[seg], sh.Plan[next], t) * sh.StitchFlex[row];

                float hl = st.stitchLength * 0.5f, hw = st.stitchWidth * 0.5f, h = st.stitchHeight;
                Vector2[] section =
                {
                    new Vector2(-hw, -StitchEmbed),
                    new Vector2(-hw * StitchTopTaper, h),
                    new Vector2(hw * StitchTopTaper, h),
                    new Vector2(hw, -StitchEmbed),
                };
                for (int f = 0; f < 3; f++)
                {
                    Vector3 a0 = centre - tangent * hl + side * section[f].x + normal * section[f].y;
                    Vector3 a1 = centre - tangent * hl + side * section[f + 1].x + normal * section[f + 1].y;
                    Vector3 b0 = centre + tangent * hl + side * section[f].x + normal * section[f].y;
                    Vector3 b1 = centre + tangent * hl + side * section[f + 1].x + normal * section[f + 1].y;
                    Vector3 outward = ((a0 + a1 + b0 + b1) * 0.25f) - (centre + normal * (-StitchEmbed * 2f));
                    AddFlatQuad(a0, b0, b1, a1, outward + normal * 0.0001f, flex, V, N, UV, FD, tris);
                }
            }
        }

        /// <summary>A flat-shaded quad whose winding is chosen so that it faces <paramref name="outward"/>.</summary>
        private static void AddFlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Vector2 flex,
            List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<Vector2> FD, List<int> tris)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            bool flip = Vector3.Dot(n, outward) < 0f;
            n = flip ? -n : n;
            n = n.sqrMagnitude > 1e-14f ? n.normalized : outward.normalized;
            int start = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            for (int k = 0; k < 4; k++) { N.Add(n); UV.Add(Vector2.zero); FD.Add(flex); }
            if (!flip) { tris.Add(start); tris.Add(start + 1); tris.Add(start + 2); tris.Add(start); tris.Add(start + 2); tris.Add(start + 3); }
            else { tris.Add(start); tris.Add(start + 2); tris.Add(start + 1); tris.Add(start); tris.Add(start + 3); tris.Add(start + 2); }
        }

        /// <summary>A smooth brass dome standing on the leather.</summary>
        private static void EmitRivet(LeatherTrayStyle st, Vector3 centre, Vector3 normal, Vector2 flex,
            List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<Vector2> FD, List<int> tris)
        {
            Vector3 e1 = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 e2 = Vector3.Cross(normal, e1);
            float r = st.rivetRadius;
            int first = V.Count;
            for (int ring = 0; ring <= RivetRings; ring++)
            {
                float phi = Mathf.PI * 0.5f * ring / RivetRings;   // 0 = top of the dome, 90 degrees = where it meets the leather
                float rr = r * Mathf.Sin(phi), hh = r * RivetDomeHeight * Mathf.Cos(phi);
                for (int s = 0; s < RivetSides; s++)
                {
                    float th = 2f * Mathf.PI * s / RivetSides;
                    Vector3 radial = e1 * Mathf.Cos(th) + e2 * Mathf.Sin(th);
                    V.Add(centre + normal * hh + radial * rr);
                    N.Add((normal * Mathf.Cos(phi) + radial * Mathf.Sin(phi)).normalized);
                    UV.Add(Vector2.zero);
                    FD.Add(flex);
                }
            }
            for (int ring = 0; ring < RivetRings; ring++)
            {
                for (int s = 0; s < RivetSides; s++)
                {
                    int s1 = (s + 1) % RivetSides;
                    int a = first + ring * RivetSides + s, b = first + ring * RivetSides + s1;
                    int c = first + (ring + 1) * RivetSides + s, d = first + (ring + 1) * RivetSides + s1;
                    // wind so the front faces away from the leather; the second triangle is never degenerate (the top ring is a single point)
                    Vector3 faceN = Vector3.Cross(V[d] - V[b], V[c] - V[b]);
                    Vector3 outward = (V[a] + V[b] + V[c] + V[d]) * 0.25f - centre;
                    bool flip = Vector3.Dot(faceN, outward) < 0f;
                    if (ring > 0)
                    {
                        if (!flip) { tris.Add(a); tris.Add(b); tris.Add(c); }
                        else { tris.Add(a); tris.Add(c); tris.Add(b); }
                    }
                    if (!flip) { tris.Add(b); tris.Add(d); tris.Add(c); }
                    else { tris.Add(b); tris.Add(c); tris.Add(d); }
                }
            }
        }

        // ------------------------------------------------------------------ collision mesh

        private static LeatherTrayMeshData EmitCollision(Shell sh, Vector2 floorSize)
        {
            int M = sh.M, K = sh.K;
            var V = new List<Vector3>(sh.Pos);
            var tris = new List<int>();

            for (int i = 0; i < M; i++)
            {
                int i1 = (i + 1) % M;
                for (int j = 0; j < K - 1; j++)
                {
                    int a = i * K + j, b = i1 * K + j, c = i * K + j + 1, d = i1 * K + j + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }

            int centreTop = V.Count;
            V.Add(Vector3.zero);
            int centreBottom = V.Count;
            V.Add(new Vector3(0f, sh.Pos[K - 1].y, 0f));
            for (int i = 0; i < M; i++)
            {
                int i1 = (i + 1) % M;
                tris.Add(centreTop); tris.Add(i1 * K); tris.Add(i * K);                              // floor, facing up
                tris.Add(centreBottom); tris.Add(i * K + K - 1); tris.Add(i1 * K + K - 1);           // underside, facing down
            }

            return new LeatherTrayMeshData
            {
                Vertices = V.ToArray(),
                Triangles = new[] { tris.ToArray() },
                FloorSize = floorSize,
                OuterExtent = new Vector2(2f * sh.MaxX, 2f * sh.MaxZ),
                Height = sh.TopY,
            };
        }
    }
}
