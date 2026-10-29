using NUnit.Framework;
using UnityEngine;
using DiceThrower.Core;
using DiceThrower.Game;

namespace DiceThrower.Tests
{
    public class DiePoseTests
    {
        private static readonly Vector3 Toward = Vector3.back; // the picture camera sits at -z

        [Test]
        public void EveryKindLooksAtTheCameraWithItsUpAxisUpright()
        {
            foreach (DieKind kind in System.Enum.GetValues(typeof(DieKind)))
            {
                DieShape shape = DieShape.Get(kind);
                Vector3 view, up;
                DiePose.Axes(shape, out view, out up);
                Quaternion pose = DiePose.Still(shape, Vector2.zero);

                Assert.Greater(Vector3.Dot(pose * view.normalized, Toward), 0.999f, kind + " view");
                Vector3 upright = pose * up;
                Assert.Less(Mathf.Abs(upright.x), 1e-3f, kind + " roll");
                Assert.Greater(upright.y, 0f, kind + " up");
            }
        }

        [Test]
        public void TheTiltIsAppliedAfterTheTurn()
        {
            DieShape shape = DieShape.Get(DieKind.D20);
            Quaternion flat = DiePose.Still(shape, Vector2.zero);
            Quaternion tilted = DiePose.Still(shape, new Vector2(-20f, 0f));
            Assert.AreEqual(20f, Quaternion.Angle(flat, tilted), 0.01f);
        }

        [Test]
        public void TheD10StandsOnItsPoleAxis()
        {
            DieShape shape = DieShape.Get(DieKind.D10);
            Vector3 view, up;
            DiePose.Axes(shape, out view, out up);

            Assert.AreEqual(0f, Vector3.Dot(view.normalized, up.normalized), 1e-3f, "seen from the side");
            int faces = 0;
            for (int f = 0; f < shape.FaceCount; f++)
            {
                foreach (int corner in shape.FaceVerts[f])
                {
                    if ((shape.Verts[corner].normalized - up.normalized).sqrMagnitude < 1e-6f) { faces++; break; }
                }
            }
            Assert.AreEqual(5, faces, "the up axis ends in a corner that five faces share");
        }

        [Test]
        public void TheD4ShowsAFaceThatHoldsItsHighestCorner()
        {
            DieShape shape = DieShape.Get(DieKind.D4);
            Vector3 view, up;
            DiePose.Axes(shape, out view, out up);

            Assert.Less(Mathf.Abs(Vector3.Dot(view.normalized, up.normalized)), 0.99f, "up is not along the view");
            int best = 0;
            int[] labels = shape.InitialVertexLabels;
            for (int i = 1; i < shape.Verts.Length; i++)
            {
                int value = labels != null ? labels[i] : i + 1;
                int bestValue = labels != null ? labels[best] : best + 1;
                if (value > bestValue) best = i;
            }
            Assert.Greater(Vector3.Dot(view.normalized, shape.Verts[best].normalized), 0.2f, "the highest corner is on the visible face");
        }
    }
}
