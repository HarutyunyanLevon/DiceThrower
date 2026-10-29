using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// The leather tray's body: the generated mesh is also the (static) collider, so dice meet the real wall, no invisible boxes.
    /// When a die hits the wall the leather flexes: vertices near the hit move outwards and spring back. That is visual only;
    /// the collider does not move (re-cooking a collider on every hit would cost far more than it is worth).
    /// Sits on the same GameObject as the MeshFilter, MeshRenderer and MeshCollider, because Unity sends collision events there.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class LeatherTray : MonoBehaviour
    {
        [SerializeField, Tooltip("Flex settings and the like come from here.")] private LeatherTrayStyle style;

        private struct Hit
        {
            public bool Active;
            public float Age, Amplitude;
            public int Count;
            public int[] Index;
            public float[] Weight;
        }

        private MeshFilter _filter;
        private Mesh _mesh;
        private Vector3[] _base, _work;
        private Vector2[] _dir;
        private float[] _disp;
        private int[] _flexVerts;
        private Hit[] _hits;
        private int _nextHit;
        private int _active;

        private void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            if (style == null || _filter.sharedMesh == null) { enabled = false; return; }

            // flex a private copy so the asset itself is never touched
            _mesh = Instantiate(_filter.sharedMesh);
            _mesh.name = _filter.sharedMesh.name + " (flex)";
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;

            _base = _mesh.vertices;
            _work = (Vector3[])_base.Clone();
            _dir = _mesh.uv2;
            _disp = new float[_base.Length];

            int count = 0;
            for (int i = 0; i < _dir.Length; i++) if (_dir[i].sqrMagnitude > 1e-6f) count++;
            _flexVerts = new int[count];
            count = 0;
            for (int i = 0; i < _dir.Length; i++) if (_dir[i].sqrMagnitude > 1e-6f) _flexVerts[count++] = i;

            _hits = new Hit[Mathf.Max(1, style.flexMaxHits)];
            for (int h = 0; h < _hits.Length; h++)
            {
                _hits[h].Index = new int[_flexVerts.Length];
                _hits[h].Weight = new float[_flexVerts.Length];
            }

            Bounds b = _mesh.bounds;
            b.Expand(style.flexMaxAmplitude * 2f); // the bounds are not recalculated while the leather moves
            _mesh.bounds = b;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!enabled || collision.contactCount == 0) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < style.flexMinSpeed) return;

            Vector3 local = transform.InverseTransformPoint(collision.GetContact(0).point);
            if (local.y < style.flexMinHitHeight) return; // the floor

            AddHit(local, Mathf.Min(style.flexMaxAmplitude, speed * style.flexAmplitudePerSpeed));
        }

        /// <summary>Starts a wobble at a point on the wall (tray-local), pushing it outwards by up to <paramref name="amplitude"/>.</summary>
        public void AddHit(Vector3 local, float amplitude)
        {
            if (_hits == null) return;
            int slot = _nextHit;
            _nextHit = (_nextHit + 1) % _hits.Length;
            if (!_hits[slot].Active) _active++;

            // which vertices move, and how much of the hit each gets: found once here, not every frame
            float radius = Mathf.Max(0.01f, style.flexRadius);
            float radiusSqr = radius * radius;
            int n = 0;
            for (int k = 0; k < _flexVerts.Length; k++)
            {
                int v = _flexVerts[k];
                float d2 = (_base[v] - local).sqrMagnitude;
                if (d2 >= radiusSqr) continue;
                float t = 1f - d2 / radiusSqr;
                _hits[slot].Index[n] = v;
                _hits[slot].Weight[n] = t * t;
                n++;
            }
            _hits[slot].Count = n;
            _hits[slot].Age = 0f;
            _hits[slot].Amplitude = amplitude;
            _hits[slot].Active = true;
        }

        private void LateUpdate()
        {
            if (_active == 0) return;

            float dt = Time.deltaTime;
            float duration = style.flexDecaySeconds * 5f;
            for (int h = 0; h < _hits.Length; h++)
            {
                if (!_hits[h].Active) continue;
                _hits[h].Age += dt;
                if (_hits[h].Age >= duration) { _hits[h].Active = false; _active--; continue; }

                float t = _hits[h].Age;
                float e = Mathf.Sin(2f * Mathf.PI * style.flexFrequency * t) * Mathf.Exp(-t / style.flexDecaySeconds);
                if (e < 0f) e *= 0.35f; // the rebound towards the inside is much smaller than the push out
                float amp = _hits[h].Amplitude * e;
                for (int k = 0; k < _hits[h].Count; k++) _disp[_hits[h].Index[k]] += amp * _hits[h].Weight[k];
            }

            for (int k = 0; k < _flexVerts.Length; k++)
            {
                int v = _flexVerts[k];
                Vector2 d = _dir[v] * _disp[v];
                _work[v] = new Vector3(_base[v].x + d.x, _base[v].y, _base[v].z + d.y);
                _disp[v] = 0f;
            }
            _mesh.vertices = _work;
        }
    }
}
