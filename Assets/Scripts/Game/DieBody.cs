using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// One physical die in the tray. Thrown for real, whatever it shows when it lies still is its value.
    /// A die with a target value (the RNG decided) is instead just placed showing that value.
    /// </summary>
    public sealed class DieBody : MonoBehaviour
    {
        public DieKind Kind { get; private set; }
        public int TargetValue { get; private set; }
        public int SetIndex { get; private set; }
        /// <summary>0, or 1 for the second copy thrown for advantage / disadvantage.</summary>
        public int Pass { get; private set; }
        public bool HasTarget { get { return TargetValue != PhysicalDie.NoTarget; } }
        /// <summary>The die has come to rest (or was set by hand) since it was last thrown.</summary>
        public bool Resting { get; private set; }
        /// <summary>Circumradius of the die.</summary>
        public float Radius { get { return _shape.Radius; } }

        /// <summary>Raised when the die hits something; the argument is the impact speed.</summary>
        public event System.Action<float> Hit;

        private DieVisual _visual;
        private DieShape _shape;
        private TrayConfig _cfg;
        private Mesh _mesh;
        private Vector2[] _uv;
        private ModelDie _model;       // imported model: its numbers are in its texture, no atlas needed
        private int[] _faceValue;   // non-d4: value printed on each face (what the die shows when that face is on top)
        private int[] _vertexLabel; // d4: number at each vertex
        private Rigidbody _rb;
        private DieFxDriver _fx;       // only dice with a DiceFX shader material

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
        public static DieBody Spawn(DieBody prefab, Transform parent, TrayConfig cfg, DieKind kind, DiceSkin skin, int targetValue, int setIndex, int pass)
        {
            DieVisual visual = DiceVisuals.Get(kind, skin);

            DieBody die = Instantiate(prefab, parent, false);
            die.name = "Die_" + kind + "_" + targetValue;
            die.Kind = kind;
            die.TargetValue = targetValue;
            die.SetIndex = setIndex;
            die.Pass = pass;
            die._cfg = cfg;
            die._visual = visual;
            die._shape = visual.Shape;
            die._rb = die.GetComponent<Rigidbody>();

            die._model = visual.Model;
            die._mesh = die._model != null ? die._model.NewMesh() : DiceVisuals.BuildMesh(visual.Shape, "die_" + kind);
            if (die._model == null) die._uv = new Vector2[visual.Shape.MeshVertexCount];
            die.GetComponent<MeshFilter>().sharedMesh = die._mesh;
            die.GetComponent<MeshRenderer>().sharedMaterial = visual.Material;
            die.GetComponent<MeshCollider>().sharedMesh = visual.CollisionMesh;
            die._fx = DieFxDriver.AttachIfFx(die, visual.Material, skin); // only dice whose material is a DiceFX shader get the driver

            die.InitLabels();
            return die;
        }

        // ------------------------------------------------------------------ labels

        private void InitLabels()
        {
            if (_shape.VertexLabeled)
            {
                _vertexLabel = _shape.InitialVertexLabels != null ? (int[])_shape.InitialVertexLabels.Clone() : new[] { 1, 2, 3, 4 }; // generated d4: label i+1 at vertex i
            }
            else
            {
                _faceValue = (int[])_shape.InitialFaceValue.Clone();
            }
            if (_model != null) return; // an imported model's numbers are in its texture
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

        // ------------------------------------------------------------------ throwing and settling

        public void Launch(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            transform.SetPositionAndRotation(position, rotation);
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = angularVelocity;
            _age = 0f;
            _stillTime = 0f;
            _nudges = 0;
            Resting = false;
            SetNatEffect(DieNatEffect.None); // a new throw ends the nat 20 / nat 1 effect
        }

        /// <summary>
        /// Shows the nat 20 / nat 1 effect of the die's dice set (nothing for a set without one). The tray turns it on for a d20 that lies
        /// showing 20 or 1, once every die is still; a new throw turns it off.
        /// </summary>
        public void SetNatEffect(DieNatEffect effect)
        {
            if (_fx != null) _fx.SetEffect(effect);
        }

        /// <summary>Call every frame while rolling. True once the die has come to rest.</summary>
        public bool Tick(float dt, float floorY)
        {
            Resting = TickRest(dt, floorY);
            return Resting;
        }

        private bool TickRest(float dt, float floorY)
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

        // ------------------------------------------------------------------ setting a die by hand

        /// <summary>Stops the die where it is.</summary>
        public void Freeze()
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.Sleep();
            Resting = true;
        }

        /// <summary>
        /// Stops the die and tilts it to lie flat on the face (the corner, for a d4) that is nearest to up, so what it shows stays the same.
        /// It ends on the floor at the same x/z.
        /// </summary>
        public void SnapFlat(float floorY)
        {
            Freeze();
            Quaternion q = transform.rotation;
            Vector3 current = q * (_shape.VertexLabeled ? _shape.Verts[TopVertex()].normalized : _shape.FaceNormals[TopFaceIndex()]);
            Quaternion flat = Quaternion.FromToRotation(current, Vector3.up) * q;
            Vector3 p = transform.position;
            SetPose(new Vector3(p.x, 0f, p.z), flat, floorY);
        }

        /// <summary>Puts the die on the floor at (x, z) showing <paramref name="value"/> on top, turned by <paramref name="yawDegrees"/>.</summary>
        public void PlaceShowing(Vector3 world, int value, float yawDegrees, float floorY)
        {
            Freeze();
            int item = System.Array.IndexOf(_shape.VertexLabeled ? _vertexLabel : _faceValue, value);
            if (item < 0) item = 0;
            Vector3 axis = _shape.VertexLabeled ? _shape.Verts[item].normalized : _shape.FaceNormals[item];
            Quaternion flat = Quaternion.AngleAxis(yawDegrees, Vector3.up) * Quaternion.FromToRotation(axis, Vector3.up);
            SetPose(new Vector3(world.x, 0f, world.z), flat, floorY);
        }

        /// <summary>A random number this die can show.</summary>
        public int RandomValue()
        {
            int[] labels = _shape.VertexLabeled ? _vertexLabel : _faceValue;
            return labels[Random.Range(0, labels.Length)];
        }

        /// <summary>Moves the die so its lowest corner touches the floor.</summary>
        private void SetPose(Vector3 xz, Quaternion rotation, float floorY)
        {
            float lowest = float.MaxValue;
            for (int i = 0; i < _shape.Verts.Length; i++) lowest = Mathf.Min(lowest, (rotation * _shape.Verts[i]).y);
            transform.SetPositionAndRotation(new Vector3(xz.x, floorY - lowest + 0.002f, xz.z), rotation);
            Resting = true;
        }

        /// <summary>The die shrinks away and is destroyed (it no longer counts for anything).</summary>
        public void Dismiss(float seconds)
        {
            _rb.isKinematic = true;
            GetComponent<Collider>().enabled = false;
            StartCoroutine(ShrinkAway(Mathf.Max(0.01f, seconds)));
        }

        private System.Collections.IEnumerator ShrinkAway(float seconds)
        {
            Vector3 from = transform.localScale;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                transform.localScale = from * (1f - t / seconds);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>Moves a resting die sideways (it stays on the floor).</summary>
        public void ShiftTo(Vector3 world)
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(world.x, p.y, world.z);
        }

        private int TopFaceIndex()
        {
            float dot;
            return TopFace(out dot);
        }

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
