using System.Collections.Generic;
using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// One imported die (a baked glTF mesh) ready for the tray: a DieShape for the top-face reading, a shared collision mesh,
    /// and the rotations that turn the die onto itself so a chosen number ends up on top.
    /// Corners/faces are the "items": faces for most dice, corners for the d4 (its number is read at the top corner).
    /// </summary>
    public sealed class ModelDie
    {
        private const float MatchTolerance = 2e-3f; // of the circumradius

        public readonly DieShape Shape;
        public readonly Mesh CollisionMesh;

        private readonly Vector3[] _pos;
        private readonly Vector3[] _normals;
        private readonly Vector2[] _uv;
        private readonly int[] _tris;
        private readonly Vector3[] _axes;   // direction of every item (face normal, or corner direction for the d4)
        private readonly bool _cornerMode;
        private readonly float _radius;
        private readonly Dictionary<int, Quaternion> _rotations = new Dictionary<int, Quaternion>();

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

            _radius = DieShape.RadiusOf(kind);
            float scale = _radius / maxLen;
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
                Radius = _radius,
                Verts = corners,
                FaceVerts = faceVerts,
                FaceNormals = faceNormals,
                FaceCenters = faceCenters,
                VertexLabeled = _cornerMode,
            };
            if (_cornerMode) Shape.InitialVertexLabels = (int[])json.values.Clone();
            else Shape.InitialFaceValue = FaceValues(kind, json.values);

            if (_cornerMode)
            {
                _axes = new Vector3[cornerCount];
                for (int i = 0; i < cornerCount; i++) _axes[i] = corners[i].normalized;
            }
            else
            {
                _axes = faceNormals;
            }

            CollisionMesh = NewMesh();
            CollisionMesh.name = "collision_" + kind;
        }

        /// <summary>The model's d10 shows 0..9. As a d10 the "0" face means 10; as the units die it stays 0.</summary>
        private static int[] FaceValues(DieKind kind, int[] baked)
        {
            var v = (int[])baked.Clone();
            if (kind == DieKind.D10)
                for (int i = 0; i < v.Length; i++)
                    if (v[i] == 0) v[i] = 10;
            return v;
        }

        // ------------------------------------------------------------------ mesh

        /// <summary>A fresh mesh of this die in its base orientation (every die gets its own, because it is turned after the roll).</summary>
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

        /// <summary>Shows the die turned by <paramref name="rotation"/> (relative to its base orientation).</summary>
        public void Apply(Mesh mesh, Quaternion rotation)
        {
            var verts = new Vector3[_pos.Length];
            var normals = new Vector3[_pos.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = rotation * _pos[i];
                normals[i] = rotation * _normals[i];
            }
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }

        // ------------------------------------------------------------------ symmetry

        /// <summary>The rotation of the die onto itself that takes item <paramref name="from"/> to the place of item <paramref name="to"/>.</summary>
        public Quaternion Rotation(int from, int to)
        {
            int key = from * 64 + to;
            Quaternion q;
            if (_rotations.TryGetValue(key, out q)) return q;
            q = Solve(from, to);
            _rotations[key] = q;
            return q;
        }

        /// <summary>Where the rotation sends every item: result[i] = the item that item i lands on.</summary>
        public int[] Permutation(Quaternion q)
        {
            var result = new int[_axes.Length];
            for (int i = 0; i < _axes.Length; i++)
            {
                Vector3 moved = q * _axes[i];
                float best = -2f;
                for (int j = 0; j < _axes.Length; j++)
                {
                    float d = Vector3.Dot(moved, _axes[j]);
                    if (d > best) { best = d; result[i] = j; }
                }
            }
            return result;
        }

        /// <summary>Aligns the two item directions, then tries every twist around the target that puts a corner on a corner and keeps the best fit.</summary>
        private Quaternion Solve(int from, int to)
        {
            Vector3 a = _axes[from], b = _axes[to];
            Quaternion align = Quaternion.FromToRotation(a, b);
            Vector3[] u = Shape.Verts;
            float tol = MatchTolerance * _radius;

            Vector3 reference = Vector3.zero;
            for (int i = 0; i < u.Length; i++)
            {
                if (Vector3.ProjectOnPlane(u[i], a).magnitude > 1e-3f * _radius) { reference = u[i]; break; }
            }
            Vector3 pr = Vector3.ProjectOnPlane(align * reference, b);

            Quaternion best = align;
            float bestError = Fit(align, u);
            for (int i = 0; i < u.Length; i++)
            {
                Vector3 pu = Vector3.ProjectOnPlane(u[i], b);
                if (Mathf.Abs(pu.magnitude - pr.magnitude) > tol || pr.magnitude < 1e-6f) continue;
                Quaternion candidate = Quaternion.AngleAxis(Vector3.SignedAngle(pr, pu, b), b) * align;
                float error = Fit(candidate, u);
                if (error < bestError) { bestError = error; best = candidate; }
            }
            return best;
        }

        /// <summary>Largest distance from a rotated corner to the nearest corner (0 for a perfect symmetry).</summary>
        private static float Fit(Quaternion q, Vector3[] u)
        {
            float worst = 0f;
            for (int i = 0; i < u.Length; i++)
            {
                Vector3 x = q * u[i];
                float nearest = float.MaxValue;
                for (int j = 0; j < u.Length; j++) nearest = Mathf.Min(nearest, (u[j] - x).sqrMagnitude);
                worst = Mathf.Max(worst, nearest);
            }
            return Mathf.Sqrt(worst);
        }
    }
}
