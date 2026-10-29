using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DiceThrower.Game;

namespace DiceThrower.Tests
{
    public class LeatherTrayGeometryTests
    {
        private const float SizeTolerance = 0.02f;
        private const int MaxCollisionTriangles = 600;
        private const float HighOnTheWall = 1f;

        private static LeatherTrayStyle MakeStyle(LeatherTrayPreset preset)
        {
            var s = ScriptableObject.CreateInstance<LeatherTrayStyle>();
            LeatherTrayPresets.Fill(s, preset);
            return s;
        }

        [Test]
        public void RenderMeshIsFiniteWithUnitNormalsAndMatchingArrays()
        {
            foreach (LeatherTrayPreset p in LeatherTrayPresets.All)
            {
                LeatherTrayStyle s = MakeStyle(p);
                LeatherTrayMeshData d = LeatherTrayGeometry.BuildRender(s);
                int n = d.Vertices.Length;
                Assert.AreEqual(n, d.Normals.Length, p.ToString());
                Assert.AreEqual(n, d.Uv.Length, p.ToString());
                Assert.AreEqual(n, d.FlexDir.Length, p.ToString());
                Assert.AreEqual(LeatherTrayMeshData.RenderSubmeshCount, d.Triangles.Length, p.ToString());
                for (int i = 0; i < n; i++)
                {
                    Assert.IsFalse(float.IsNaN(d.Vertices[i].x + d.Vertices[i].y + d.Vertices[i].z), p + " vertex " + i);
                    Assert.AreEqual(1f, d.Normals[i].magnitude, 0.01f, p + " normal " + i);
                }
                for (int sub = 0; sub < d.Triangles.Length; sub++)
                    foreach (int idx in d.Triangles[sub]) Assert.That(idx, Is.InRange(0, n - 1), p + " index");
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void OuterSizeMatchesTheStyle()
        {
            foreach (LeatherTrayPreset p in LeatherTrayPresets.All)
            {
                LeatherTrayStyle s = MakeStyle(p);
                LeatherTrayMeshData d = LeatherTrayGeometry.BuildRender(s);
                Assert.AreEqual(s.outerSize.x, d.OuterExtent.x, SizeTolerance, p + " width");
                Assert.AreEqual(s.outerSize.y, d.OuterExtent.y, SizeTolerance, p + " depth");
                Assert.Greater(d.FloorSize.x, 4f, p + " floor width");
                Assert.Greater(d.FloorSize.y, 7f, p + " floor depth");
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void WallsStandAboveTheFloor()
        {
            foreach (LeatherTrayPreset p in LeatherTrayPresets.All)
            {
                LeatherTrayStyle s = MakeStyle(p);
                LeatherTrayMeshData d = LeatherTrayGeometry.BuildRender(s);
                Assert.Greater(d.Height, s.wallLength * 0.6f, p.ToString());
                Assert.Less(d.Height, s.wallLength * (1f + s.cornerRise) * 1.2f + 0.5f, p.ToString());
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void CollisionMeshIsAClosedSolidAndSmall()
        {
            foreach (LeatherTrayPreset p in LeatherTrayPresets.All)
            {
                LeatherTrayStyle s = MakeStyle(p);
                LeatherTrayMeshData d = LeatherTrayGeometry.BuildCollision(s);
                Assert.AreEqual(1, d.Triangles.Length, p.ToString());
                Assert.LessOrEqual(d.TriangleCount, MaxCollisionTriangles, p + " collision triangles");

                // every edge belongs to exactly two triangles, once in each direction (closed and consistently wound)
                var directed = new Dictionary<long, int>();
                int[] t = d.Triangles[0];
                for (int i = 0; i < t.Length; i += 3)
                {
                    Edge(directed, t[i], t[i + 1]);
                    Edge(directed, t[i + 1], t[i + 2]);
                    Edge(directed, t[i + 2], t[i]);
                }
                foreach (KeyValuePair<long, int> kv in directed)
                {
                    Assert.AreEqual(1, kv.Value, p + " edge used twice in the same direction");
                    int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xFFFFFFFF);
                    Assert.IsTrue(directed.ContainsKey(Key(b, a)), p + " open edge " + a + "-" + b);
                }
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void CollisionSurfaceFollowsTheRenderedInnerSurface()
        {
            // at the floor's centre line the wall of both meshes starts at the same height and the rim is the same height (within the coarser sampling)
            foreach (LeatherTrayPreset p in LeatherTrayPresets.All)
            {
                LeatherTrayStyle s = MakeStyle(p);
                LeatherTrayMeshData r = LeatherTrayGeometry.BuildRender(s);
                LeatherTrayMeshData c = LeatherTrayGeometry.BuildCollision(s);
                Assert.AreEqual(r.FloorSize.x, c.FloorSize.x, 1e-3f, p.ToString());
                Assert.AreEqual(r.Height, c.Height, 0.35f, p + " rim height");
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void FloorVerticesDoNotFlexAndRimVerticesDo()
        {
            LeatherTrayStyle s = MakeStyle(LeatherTrayPreset.FoldedTan);
            LeatherTrayMeshData d = LeatherTrayGeometry.BuildRender(s);
            float lowest = float.MaxValue, highest = float.MinValue;
            int moving = 0;
            for (int i = 0; i < d.Vertices.Length; i++)
            {
                float y = d.Vertices[i].y;
                if (d.FlexDir[i].sqrMagnitude > 1e-6f) { moving++; lowest = Mathf.Min(lowest, y); highest = Mathf.Max(highest, y); }
                else Assert.Less(y, HighOnTheWall, "a vertex high on the wall must be able to flex (vertex " + i + ")");
            }
            Assert.Greater(moving, 100);
            Assert.Greater(lowest, -0.2f, "only the wall flexes, not the floor's underside");
            Assert.Greater(highest, s.wallLength * 0.8f);
            Object.DestroyImmediate(s);
        }

        [Test]
        public void StitchesAndRivetsAreBuiltOnlyWhenTheStyleAsksForThem()
        {
            LeatherTrayStyle folded = MakeStyle(LeatherTrayPreset.FoldedTan);
            LeatherTrayStyle wine = MakeStyle(LeatherTrayPreset.TooledWine);
            LeatherTrayMeshData a = LeatherTrayGeometry.BuildRender(folded);
            LeatherTrayMeshData b = LeatherTrayGeometry.BuildRender(wine);
            Assert.Greater(a.Triangles[LeatherTrayMeshData.StitchSubmesh].Length, 0);
            Assert.Greater(a.Triangles[LeatherTrayMeshData.HardwareSubmesh].Length, 0);
            Assert.AreEqual(0, b.Triangles[LeatherTrayMeshData.StitchSubmesh].Length);
            Assert.AreEqual(0, b.Triangles[LeatherTrayMeshData.HardwareSubmesh].Length);
            Object.DestroyImmediate(folded);
            Object.DestroyImmediate(wine);
        }

        [Test]
        public void TheSameStyleBuildsTheSameMesh()
        {
            LeatherTrayStyle s = MakeStyle(LeatherTrayPreset.FoldedTan);
            LeatherTrayMeshData a = LeatherTrayGeometry.BuildRender(s);
            LeatherTrayMeshData b = LeatherTrayGeometry.BuildRender(s);
            Assert.AreEqual(a.Vertices.Length, b.Vertices.Length);
            for (int i = 0; i < a.Vertices.Length; i += 37) Assert.AreEqual(a.Vertices[i], b.Vertices[i]);
            Object.DestroyImmediate(s);
        }

        private static long Key(int a, int b) { return ((long)a << 32) | (uint)b; }

        private static void Edge(Dictionary<long, int> into, int a, int b)
        {
            long k = Key(a, b);
            int c;
            into.TryGetValue(k, out c);
            into[k] = c + 1;
        }
    }
}
