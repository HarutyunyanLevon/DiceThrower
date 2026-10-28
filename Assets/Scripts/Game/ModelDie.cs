using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// One imported die (a baked glTF mesh) ready for the tray: a DieShape for the top-face reading and a shared collision mesh.
    /// The numbers are in the texture; what a die shows is read from its top face (its top corner for the d4).
    /// </summary>
    public sealed class ModelDie
    {
        public readonly DieShape Shape;
        public readonly Mesh CollisionMesh;

        private readonly Vector3[] _pos;
        private readonly Vector3[] _normals;
        private readonly Vector2[] _uv;
        private readonly int[] _tris;
        private readonly bool _cornerMode;

        public ModelDie(DieKind kind, DieModelJson json)
        {
            int n = json.pos.Length / 3;
            int faceSize = json.faceSize;
            int faceCount = n / faceSize;

            int cornerCount = 0;
            for (int i = 0; i < n; i++) cornerCount = Mathf.Max(cornerCount, json.corner[i] + 1);

            var raw = new Vector3[n];
            var corners = new Vector3[cornerCount];
            float maxLen = 0f;
            for (int i = 0; i < n; i++)
            {
                raw[i] = new Vector3(json.pos[i * 3], json.pos[i * 3 + 1], json.pos[i * 3 + 2]);
                corners[json.corner[i]] = raw[i];
            }
            for (int i = 0; i < cornerCount; i++) maxLen = Mathf.Max(maxLen, corners[i].magnitude);

            float radius = DieShape.RadiusOf(kind);
            float scale = radius / maxLen;
            _pos = new Vector3[n];
            for (int i = 0; i < n; i++) _pos[i] = raw[i] * scale;
            for (int i = 0; i < cornerCount; i++) corners[i] *= scale;

            var faceVerts = new int[faceCount][];
            var faceNormals = new Vector3[faceCount];
            var faceCenters = new Vector3[faceCount];
            _normals = new Vector3[n];
            for (int f = 0; f < faceCount; f++)
            {
                faceVerts[f] = new int[faceSize];
                Vector3 c = Vector3.zero;
                for (int j = 0; j < faceSize; j++)
                {
                    faceVerts[f][j] = json.corner[f * faceSize + j];
                    c += _pos[f * faceSize + j];
                }
                c /= faceSize;
                Vector3 normal = Vector3.Cross(_pos[f * faceSize + 1] - _pos[f * faceSize], _pos[f * faceSize + 2] - _pos[f * faceSize]).normalized;
                if (Vector3.Dot(normal, c) < 0f) normal = -normal; // outward
                faceNormals[f] = normal;
                faceCenters[f] = c;
                for (int j = 0; j < faceSize; j++) _normals[f * faceSize + j] = normal;
            }

            _uv = new Vector2[n];
            for (int i = 0; i < n; i++) _uv[i] = new Vector2(json.uv[i * 2], json.uv[i * 2 + 1]);
            _tris = (int[])json.tris.Clone();

            _cornerMode = kind == DieKind.D4;
            Shape = new DieShape
            {
                Kind = kind,
                Radius = radius,
                Verts = corners,
                FaceVerts = faceVerts,
                FaceNormals = faceNormals,
                FaceCenters = faceCenters,
                VertexLabeled = _cornerMode,
            };
            if (_cornerMode) Shape.InitialVertexLabels = (int[])json.values.Clone();
            else Shape.InitialFaceValue = FaceValues(kind, json.values);

            CollisionMesh = NewMesh();
            CollisionMesh.name = "collision_" + kind;
        }

        /// <summary>
        /// The model's d10 shows 0..9. As a d10 the "0" face means 10; as the units die it stays 0;
        /// as the tens die the same faces mean 0, 10 .. 90 (the model has no 00..90 numbering of its own).
        /// </summary>
        private static int[] FaceValues(DieKind kind, int[] baked)
        {
            var v = (int[])baked.Clone();
            for (int i = 0; i < v.Length; i++)
            {
                if (kind == DieKind.D10 && v[i] == 0) v[i] = 10;
                else if (kind == DieKind.D10Tens) v[i] *= 10;
            }
            return v;
        }

        // ------------------------------------------------------------------ mesh

        /// <summary>A fresh mesh of this die in its base orientation (every die gets its own mesh instance).</summary>
        public Mesh NewMesh()
        {
            var mesh = new Mesh { name = "die_" + Shape.Kind };
            mesh.vertices = (Vector3[])_pos.Clone();
            mesh.normals = (Vector3[])_normals.Clone();
            mesh.uv = _uv;
            mesh.triangles = _tris;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
