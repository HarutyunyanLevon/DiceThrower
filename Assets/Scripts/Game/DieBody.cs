using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// One physical die in the tray. The result was already decided by the RollEngine; this die
    /// just tumbles for real, and once it has settled the number on its top face is remapped
    /// (by rewriting UVs) to the decided value.
    /// </summary>
    public sealed class DieBody : MonoBehaviour
    {
        public DieKind Kind { get; private set; }
        public int TargetValue { get; private set; }
        public int SetIndex { get; private set; }

        /// <summary>Raised when the die hits something; the argument is the impact speed.</summary>
        public event System.Action<float> Hit;

        private DieVisual _visual;
        private DieShape _shape;
        private TrayConfig _cfg;
        private Mesh _mesh;
        private Vector2[] _uv;
        private int[] _faceValue;   // non-d4: value printed on each face
        private int[] _vertexLabel; // d4: number at each vertex
        private Rigidbody _rb;

        private float _age;
        private float _stillTime;
        private int _nudges;

        private void OnCollisionEnter(Collision collision)
        {
            if (Hit != null) Hit(collision.relativeVelocity.magnitude);
        }

        // ------------------------------------------------------------------ creation

        /// <summary>
        /// Instantiates the Die prefab (Rigidbody, MeshCollider, MeshFilter, MeshRenderer and their
        /// physics settings live there) and gives it the generated mesh and numbered texture.
        /// </summary>
        public static DieBody Spawn(DieBody prefab, Transform parent, TrayConfig cfg, DieKind kind, DiceSkin skin, int targetValue, int setIndex)
        {
            DieVisual visual = DiceVisuals.Get(kind, skin);

            DieBody die = Instantiate(prefab, parent, false);
            die.name = "Die_" + kind + "_" + targetValue;
            die.Kind = kind;
            die.TargetValue = targetValue;
            die.SetIndex = setIndex;
            die._cfg = cfg;
            die._visual = visual;
            die._shape = visual.Shape;
            die._rb = die.GetComponent<Rigidbody>();

            die._mesh = DiceVisuals.BuildMesh(visual.Shape, "die_" + kind);
            die._uv = new Vector2[visual.Shape.MeshVertexCount];
            die.GetComponent<MeshFilter>().sharedMesh = die._mesh;
            die.GetComponent<MeshRenderer>().sharedMaterial = visual.Material;
            die.GetComponent<MeshCollider>().sharedMesh = visual.CollisionMesh;

            die.InitLabels();
            return die;
        }

        // ------------------------------------------------------------------ labels (the remap machinery)

        private void InitLabels()
        {
            if (_shape.VertexLabeled)
            {
                _vertexLabel = new[] { 1, 2, 3, 4 }; // d4: label i+1 at vertex i
            }
            else
            {
                _faceValue = (int[])_shape.InitialFaceValue.Clone();
            }
            for (int f = 0; f < _shape.FaceCount; f++) ApplyFaceUV(f);
            _mesh.uv = _uv;
        }

        private int CellOfFace(int f)
        {
            if (_shape.VertexLabeled)
            {
                int[] fv = _shape.FaceVerts[f];
                return _visual.Atlas.CellForTriple(_vertexLabel[fv[0]], _vertexLabel[fv[1]], _vertexLabel[fv[2]]);
            }
            return _visual.Atlas.CellForValue(_faceValue[f]);
        }

        private void ApplyFaceUV(int f)
        {
            int cell = CellOfFace(f);
            Vector2[] local = _shape.FaceLocal[f];
            int start = _shape.FaceMeshStart[f];
            for (int i = 0; i < local.Length; i++) _uv[start + i] = _visual.Atlas.UV(cell, local[i]);
        }

        // ------------------------------------------------------------------ reading the die

        private int TopFace(out float topDot)
        {
            Quaternion q = transform.rotation;
            int best = 0;
            topDot = -2f;
            for (int f = 0; f < _shape.FaceCount; f++)
            {
                float d = Vector3.Dot(q * _shape.FaceNormals[f], Vector3.up);
                if (d > topDot) { topDot = d; best = f; }
            }
            return best;
        }

        private int TopVertex()
        {
            Quaternion q = transform.rotation;
            int best = 0;
            float hi = float.MinValue;
            for (int i = 0; i < _shape.Verts.Length; i++)
            {
                float y = (q * _shape.Verts[i]).y;
                if (y > hi) { hi = y; best = i; }
            }
            return best;
        }

        /// <summary>The number currently showing on top.</summary>
        public int ReadTopValue()
        {
            if (_shape.VertexLabeled) return _vertexLabel[TopVertex()];
            float dot;
            return _faceValue[TopFace(out dot)];
        }

        /// <summary>False when the die is leaning on something instead of lying flat.</summary>
        public bool IsFlat()
        {
            if (_shape.VertexLabeled) return true; // a d4 always rests on a face; the top vertex is unambiguous
            float dot;
            TopFace(out dot);
            return dot > _cfg.flatDot;
        }

        /// <summary>Makes the top of the die show <see cref="TargetValue"/>.</summary>
        public void RemapToTarget()
        {
            if (_shape.VertexLabeled) RemapD4();
            else RemapFaces();
            _mesh.uv = _uv;
        }

        private void RemapFaces()
        {
            float dot;
            int top = TopFace(out dot);
            if (_faceValue[top] == TargetValue) return;

            int other = System.Array.IndexOf(_faceValue, TargetValue);
            if (other < 0) return;
            int tmp = _faceValue[top];
            _faceValue[top] = _faceValue[other];
            _faceValue[other] = tmp;
            ApplyFaceUV(top);
            ApplyFaceUV(other);
        }

        /// <summary>
        /// d4: relabel the corners with a 3-cycle (an even permutation), which keeps the numbers'
        /// handedness so no mirrored texture cells are needed, and puts the target on the top corner.
        /// </summary>
        private void RemapD4()
        {
            int top = TopVertex();
            if (_vertexLabel[top] == TargetValue) return;

            int w = System.Array.IndexOf(_vertexLabel, TargetValue);
            if (w < 0) return;
            int x = 0;
            while (x == top || x == w) x++;

            int labelTop = _vertexLabel[top];
            _vertexLabel[top] = _vertexLabel[w];
            _vertexLabel[w] = _vertexLabel[x];
            _vertexLabel[x] = labelTop;
            for (int f = 0; f < _shape.FaceCount; f++) ApplyFaceUV(f);
        }

        // ------------------------------------------------------------------ throwing and settling

        public void Launch(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            transform.SetPositionAndRotation(position, rotation);
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = angularVelocity;
            _age = 0f;
            _stillTime = 0f;
            _nudges = 0;
        }

        /// <summary>Call every frame while rolling. True once the die has come to rest.</summary>
        public bool Tick(float dt, float floorY)
        {
            _age += dt;
            if (transform.position.y < floorY - _cfg.fallMargin)
            {
                // fell out of the world: put it back above the tray
                transform.position = new Vector3(0f, floorY + _cfg.respawnHeight, 0f);
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _stillTime = 0f;
                return false;
            }
            if (_age < _cfg.minDieAgeSeconds) return false;

            bool still = _rb.linearVelocity.sqrMagnitude < _cfg.stillLinearSqr && _rb.angularVelocity.sqrMagnitude < _cfg.stillAngularSqr;
            _stillTime = still ? _stillTime + dt : 0f;
            return _stillTime >= _cfg.stillSeconds || _rb.IsSleeping();
        }

        public bool NeedsNudge { get { return !IsFlat() && _nudges < _cfg.maxNudges; } }

        /// <summary>A die cocked against another die or a wall gets a small hop to fall flat.</summary>
        public void Nudge()
        {
            _nudges++;
            _stillTime = 0f;
            _rb.WakeUp();
            var hop = new Vector3(Random.Range(-_cfg.nudgeSideSpeed, _cfg.nudgeSideSpeed), _cfg.nudgeUpSpeed, Random.Range(-_cfg.nudgeSideSpeed, _cfg.nudgeSideSpeed));
            _rb.AddForce(hop, ForceMode.VelocityChange);
            _rb.AddTorque(Random.onUnitSphere * _cfg.nudgeSpin, ForceMode.VelocityChange);
        }
    }
}
