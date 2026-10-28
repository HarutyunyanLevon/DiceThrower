using NUnit.Framework;
using UnityEngine;
using DiceThrower.Core;
using DiceThrower.Game;

namespace DiceThrower.Tests
{
    public class ShapeTests
    {
        [Test]
        public void EveryKindBuildsWithOutwardFacesAndUniqueValues()
        {
            foreach (DieKind k in System.Enum.GetValues(typeof(DieKind)))
            {
                DieShape s = DieShape.Get(k);
                Assert.AreEqual(DieInfo.FaceCount(k), s.FaceCount, k.ToString());
                for (int f = 0; f < s.FaceCount; f++)
                {
                    int[] fv = s.FaceVerts[f];
                    Vector3 a = s.Verts[fv[0]];
                    Vector3 cross = Vector3.Cross(s.Verts[fv[1]] - a, s.Verts[fv[2]] - a);
                    Assert.Greater(Vector3.Dot(cross, s.FaceNormals[f]), 0f, k + " winding");
                }
                if (!s.VertexLabeled)
                    Assert.AreEqual(s.FaceCount, new System.Collections.Generic.HashSet<int>(s.InitialFaceValue).Count, k.ToString());
            }
        }
    }
}
