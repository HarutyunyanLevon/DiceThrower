using System.Collections.Generic;
using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>Everything shareable between dice of the same kind and skin.</summary>
    public sealed class DieVisual
    {
        public DieShape Shape;
        public DiceAtlas Atlas;
        public Material Material;
        public Mesh CollisionMesh;
    }

    public static class DiceVisuals
    {
        /// <summary>Set by the DiceTray from its Die Material (a URP Lit asset, so the shader survives builds).</summary>
        public static Material Template;

        private static readonly Dictionary<string, DieVisual> Cache = new Dictionary<string, DieVisual>();

        public static DieVisual Get(DieKind kind, DiceSkin skin)
        {
            string key = kind + "|" + skin.Key;
            DieVisual v;
            if (Cache.TryGetValue(key, out v) && v.Atlas.Texture != null) return v;

            DieShape shape = DieShape.Get(kind);
            DiceAtlas atlas = DiceAtlas.Create(shape, skin);
            Material mat = NewMaterial();
            mat.mainTexture = atlas.Texture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas.Texture);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", skin.Smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", skin.Metallic);

            v = new DieVisual
            {
                Shape = shape,
                Atlas = atlas,
                Material = mat,
                CollisionMesh = BuildMesh(shape, "collision_" + kind),
            };
            Cache[key] = v;
            return v;
        }

        private static Material NewMaterial()
        {
            if (Template == null) Debug.LogError("DiceVisuals.Template is not set: assign the Die Material on the DiceTray.");
            return new Material(Template);
        }

        /// <summary>Flat-shaded mesh: every face has its own vertices (so UVs and normals are per face).</summary>
        public static Mesh BuildMesh(DieShape s, string name)
        {
            var verts = new Vector3[s.MeshVertexCount];
            var normals = new Vector3[s.MeshVertexCount];
            var uvs = new Vector2[s.MeshVertexCount];
            var tris = new List<int>();

            for (int f = 0; f < s.FaceCount; f++)
            {
                int[] fv = s.FaceVerts[f];
                int start = s.FaceMeshStart[f];
                for (int i = 0; i < fv.Length; i++)
                {
                    verts[start + i] = s.Verts[fv[i]];
                    normals[start + i] = s.FaceNormals[f];
                }
                for (int i = 1; i < fv.Length - 1; i++)
                {
                    tris.Add(start);
                    tris.Add(start + i);
                    tris.Add(start + i + 1);
                }
            }

            var mesh = new Mesh();
            mesh.name = name;
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
