using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// Feeds a DiceFX shader die with what is going on: Energy (0..1, how fast the die moves; eases down when it stops), Flash (0..1, impacts),
    /// Nat 20 and Nat 1 (0..1, fade in and out, see <see cref="SetEffect"/>) and a random Seed per die. It also owns the optional objects of
    /// the die's <see cref="DiceFxEffects"/> (rays, rain cloud): they are made once, shown while their effect is on, and placed relative to the die.
    /// A tray die gets it from DieBody.Spawn, a Dice tab preview die from <see cref="DicePreviewRig"/>, only when the material has the DiceFX properties.
    /// Values go through a MaterialPropertyBlock and are pushed only when they change, so a resting die costs nothing.
    /// </summary>
    public sealed class DieFxDriver : MonoBehaviour
    {
        private static readonly int EnergyId = Shader.PropertyToID("_Energy");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int HighlightId = Shader.PropertyToID("_Highlight");
        private static readonly int GloomId = Shader.PropertyToID("_Gloom");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int EyeCenterId = Shader.PropertyToID("_EyeCenter");
        private static readonly int EyeDirId = Shader.PropertyToID("_EyeDir");
        private static readonly int EyeUpId = Shader.PropertyToID("_EyeUp");
        private static readonly int EyeRadiusId = Shader.PropertyToID("_EyeRadius");

        // liquid-core eye (Beholder): a spring-damped offset in the die's cavity, a pupil direction that lags the die's spin and turns up when it settles
        private const float EyeSpring = 38f;
        private const float EyeDamping = 4.5f;
        private const float EyeAccelGain = 0.012f;
        private const float EyeSpinDrag = 0.6f;       // how much of the die's turn the eye follows while it tumbles
        private const float EyeSettleRadiansPerSecond = 3.5f;      // the pupil is always turning up, also while the die tumbles
        private const float EyeFloat = 0.3f;                     // the eye floats up toward the top of the cavity (fraction of the free room)

        private Renderer _renderer;
        private Rigidbody _rb;
        private DieBody _hitSource;
        private float _radius;
        private DiceFxEffects _effects;
        private DiceFxConfig _cfg;
        private Transform _extraParent;
        private Camera _viewCamera;
        private bool _autoTick;
        private bool _extrasShown = true;
        private MaterialPropertyBlock _block;
        private GameObject _extra20, _extra1;

        private DieNatEffect _effect;
        private float _seed;
        private float _energy, _flash, _nat20, _nat1;
        private float _pushedEnergy = -1f, _pushedFlash = -1f, _pushedNat20 = -1f, _pushedNat1 = -1f;

        private bool _hasEye;
        private float _eyeRadius, _eyeMaxOffset;
        private Vector3 _eyeOffset, _eyeOffsetVel, _eyeDir, _eyeUp;
        private Vector3 _prevPos, _prevVel;
        private Quaternion _prevRot;
        private bool _eyeStarted;
        private Vector3 _pushedEyeCenter, _pushedEyeDir, _pushedEyeUp;

        public DieNatEffect Effect { get { return _effect; } }

        /// <summary>The effect is on, or still fading in or out.</summary>
        public bool Busy { get { return _effect != DieNatEffect.None || _nat20 > 0.001f || _nat1 > 0.001f; } }

        /// <summary>An extra object (rays, cloud) is on screen at the moment.</summary>
        public bool ExtrasVisible
        {
            get { return (_extra20 != null && _extra20.activeSelf) || (_extra1 != null && _extra1.activeSelf); }
        }

        // ------------------------------------------------------------------ attaching

        /// <summary>For a tray die: adds the driver when the material is a DiceFX material (has the _Energy property).</summary>
        public static DieFxDriver AttachIfFx(DieBody die, Material material, DiceSkin skin)
        {
            if (die == null || material == null || !material.HasProperty(EnergyId)) return null;
            DieFxDriver driver = die.GetComponent<DieFxDriver>();
            if (driver == null) driver = die.gameObject.AddComponent<DieFxDriver>();
            driver.Bind(die.GetComponent<Renderer>(), die.GetComponent<Rigidbody>(), die.Radius,
                        skin != null ? skin.Effects : null, die.transform.parent, null, true);
            driver._hitSource = die;       // after Bind: Bind unbinds first, which would drop the subscription
            die.Hit += driver.OnHit;
            return driver;
        }

        /// <summary>For a preview die in a <see cref="DicePreviewRig"/>: binds (or unbinds, for a plain material) and returns the driver, or null.</summary>
        public static DieFxDriver BindPreview(GameObject slot, Renderer renderer, Material material, float radius, DiceFxEffects effects, Transform extraParent, Camera camera)
        {
            DieFxDriver driver = slot.GetComponent<DieFxDriver>();
            if (material == null || !material.HasProperty(EnergyId))
            {
                if (driver != null) driver.Unbind();
                return null;
            }
            if (driver == null) driver = slot.AddComponent<DieFxDriver>();
            driver.Bind(renderer, null, radius, effects, extraParent, camera, false);
            return driver;
        }

        private void Bind(Renderer renderer, Rigidbody rb, float radius, DiceFxEffects effects, Transform extraParent, Camera camera, bool autoTick)
        {
            Unbind();
            _renderer = renderer;
            _rb = rb;
            _radius = radius;
            _effects = effects;
            _extraParent = extraParent;
            _viewCamera = camera;
            _autoTick = autoTick;
            _cfg = DiceFxConfig.Current;
            if (_block == null) _block = new MaterialPropertyBlock();
            _seed = Random.value * _cfg.seedRange;
            _energy = _flash = _nat20 = _nat1 = 0f;
            _effect = DieNatEffect.None;
            _pushedEnergy = _pushedFlash = _pushedNat20 = _pushedNat1 = -1f;
            _extrasShown = true;
            SetupEye();

            if (effects != null)
            {
                _extra20 = MakeExtra(effects.nat20Prefab, !autoTick);
                _extra1 = MakeExtra(effects.nat1Prefab, !autoTick);
            }
            Push();
        }

        /// <summary>Removes the objects made for the previous skin; the die goes back to a plain renderer.</summary>
        public void Unbind()
        {
            if (_hitSource != null) _hitSource.Hit -= OnHit;
            _hitSource = null;
            DestroyExtra(ref _extra20);
            DestroyExtra(ref _extra1);
            _hasEye = false;
            _effects = null;
            _effect = DieNatEffect.None;
        }

        private GameObject MakeExtra(GameObject prefab, bool hidden)
        {
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, _extraParent);
            if (hidden) go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            return go;
        }

        private static void DestroyExtra(ref GameObject go)
        {
            if (go != null) Destroy(go);
            go = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        // ------------------------------------------------------------------ control

        /// <summary>Turns a nat 20 / nat 1 effect on (it fades in) or off with None (it fades out).</summary>
        public void SetEffect(DieNatEffect effect)
        {
            _effect = effect;
        }

        /// <summary>The preview rig draws one slot at a time; the extras of the other slots are hidden meanwhile.</summary>
        public void ShowExtras(bool shown)
        {
            _extrasShown = shown;
            UpdateExtras();
        }

        private void OnHit(float impactSpeed)
        {
            _flash = Mathf.Max(_flash, Mathf.Clamp01(impactSpeed / Mathf.Max(_cfg.flashImpactSpeed, Mathf.Epsilon)));
        }

        private void Update()
        {
            if (_autoTick) Tick(Time.deltaTime);
        }

        /// <summary>Advances the fades and pushes the values. Called by Update for tray dice, by the preview rig for preview dice.</summary>
        public void Tick(float dt)
        {
            if (_renderer == null) return;

            float target = 0f;
            if (_rb != null && !_rb.IsSleeping())
            {
                float speed = _rb.linearVelocity.magnitude + _rb.angularVelocity.magnitude * _radius;
                target = Mathf.Clamp01(speed / Mathf.Max(_cfg.fullEnergySpeed, Mathf.Epsilon));
            }
            float rate = target > _energy ? _cfg.energyRisePerSecond : 1f / Mathf.Max(_cfg.energyCoolSeconds, Mathf.Epsilon);
            _energy = Mathf.MoveTowards(_energy, target, rate * dt);
            _flash = Mathf.MoveTowards(_flash, 0f, dt / Mathf.Max(_cfg.flashFadeSeconds, Mathf.Epsilon));

            float step = dt / Mathf.Max(_cfg.effectFadeSeconds, Mathf.Epsilon);
            _nat20 = Mathf.MoveTowards(_nat20, _effect == DieNatEffect.Nat20 ? 1f : 0f, step);
            _nat1 = Mathf.MoveTowards(_nat1, _effect == DieNatEffect.Nat1 ? 1f : 0f, step);

            bool eyeMoved = _hasEye && TickEye(dt);
            if (eyeMoved || _energy != _pushedEnergy || _flash != _pushedFlash || _nat20 != _pushedNat20 || _nat1 != _pushedNat1) Push();
            UpdateExtras();
        }

        // ------------------------------------------------------------------ liquid-core eye

        private void SetupEye()
        {
            _hasEye = false;
            _eyeStarted = false;
            if (_renderer == null || _renderer.sharedMaterial == null || !_renderer.sharedMaterial.HasProperty(EyeRadiusId)) return;
            var filter = _renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            Mesh mesh = filter.sharedMesh;
            float scale = _renderer.transform.lossyScale.x;
            // inner room = distance from the centre to the nearest face plane (the inscribed sphere), so the eye never pokes out of a d4 or a d20
            float room = Mathf.Infinity;
            if (mesh.isReadable)
            {
                Vector3[] verts = mesh.vertices;
                Vector3[] norms = mesh.normals;
                for (int i = 0; i < verts.Length && i < norms.Length; i++) room = Mathf.Min(room, Mathf.Abs(Vector3.Dot(verts[i], norms[i])));
            }
            if (float.IsInfinity(room) || room < 1e-4f)
            {
                Vector3 ext = mesh.bounds.extents;
                room = Mathf.Min(ext.x, Mathf.Min(ext.y, ext.z)) * 0.6f;
            }
            room *= scale;
            _eyeRadius = room * 0.65f;
            _eyeMaxOffset = Mathf.Max(0f, room * 0.9f - _eyeRadius);
            _eyeOffset = _eyeOffsetVel = Vector3.zero;
            _eyeDir = Vector3.up;
            _eyeUp = Vector3.forward;
            _hasEye = _eyeRadius > 1e-5f;
        }

        private Camera EyeCamera()
        {
            return _viewCamera != null ? _viewCamera : Camera.main;
        }

        /// <summary>Slosh the eye with the die's acceleration, let it lag the die's spin, and turn the pupil up toward the camera once the die has settled. True when anything changed.</summary>
        private bool TickEye(float dt)
        {
            Transform t = transform;
            if (!_eyeStarted)
            {
                _prevPos = t.position;
                _prevVel = Vector3.zero;
                _prevRot = t.rotation;
                _eyeStarted = true;
                _pushedEyeCenter = new Vector3(float.NaN, 0f, 0f);
            }
            dt = Mathf.Clamp(dt, 1e-4f, 0.05f);

            Vector3 vel = (t.position - _prevPos) / dt;
            Vector3 accel = (vel - _prevVel) / dt;
            _prevPos = t.position;
            _prevVel = vel;

            // spring-damped offset, driven opposite to the die's acceleration (the liquid stays behind)
            Vector3 restOffset = Vector3.up * (_eyeMaxOffset * EyeFloat);       // buoyant: the eye rests toward the top, never sinks to a side
            Vector3 force = -accel * EyeAccelGain - (_eyeOffset - restOffset) * EyeSpring - _eyeOffsetVel * EyeDamping;
            _eyeOffsetVel += force * dt;
            _eyeOffset += _eyeOffsetVel * dt;
            if (_eyeOffset.sqrMagnitude > _eyeMaxOffset * _eyeMaxOffset)
            {
                _eyeOffset = _eyeOffset.normalized * _eyeMaxOffset;
                _eyeOffsetVel *= 0.5f;
            }

            // the eye mostly stays put in the world while the die turns around it, with a little drag toward the die's own turn
            Quaternion turn = t.rotation * Quaternion.Inverse(_prevRot);
            _prevRot = t.rotation;
            Quaternion drag = Quaternion.Slerp(Quaternion.identity, turn, EyeSpinDrag * (1f - 0.5f * Mathf.Clamp01(_energy)));
            _eyeDir = drag * _eyeDir;
            _eyeUp = drag * _eyeUp;

            // settled: pupil up, toward the camera
            float settle = Mathf.Lerp(0.55f, 1f, 1f - Mathf.Clamp01(_energy));      // always: the eye comes up on top, the spin drag below only adds a little life
            Camera cam = EyeCamera();
            Vector3 toCam = cam != null ? (cam.transform.position - t.position).normalized : Vector3.up;
            Vector3 target = (Vector3.up * 0.75f + toCam * 0.25f).normalized;
            _eyeDir = Vector3.RotateTowards(_eyeDir, target, settle * settle * EyeSettleRadiansPerSecond * dt, 0f).normalized;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.forward;
            _eyeUp = Vector3.Lerp(_eyeUp, camUp, Mathf.Clamp01(settle * settle * 2f * dt));
            _eyeUp = Vector3.ProjectOnPlane(_eyeUp, _eyeDir);
            if (_eyeUp.sqrMagnitude < 1e-4f) _eyeUp = Vector3.ProjectOnPlane(Vector3.forward, _eyeDir);
            if (_eyeUp.sqrMagnitude < 1e-4f) _eyeUp = Vector3.ProjectOnPlane(Vector3.right, _eyeDir);
            _eyeUp.Normalize();

            Vector3 center = t.position + _eyeOffset;
            bool changed = (center - _pushedEyeCenter).sqrMagnitude > 1e-10f || (_eyeDir - _pushedEyeDir).sqrMagnitude > 1e-8f || (_eyeUp - _pushedEyeUp).sqrMagnitude > 1e-8f
                           || float.IsNaN(_pushedEyeCenter.x);
            return changed;
        }

        private void Push()
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(EnergyId, _energy);
            _block.SetFloat(FlashId, _flash);
            _block.SetFloat(HighlightId, _nat20);
            _block.SetFloat(GloomId, _nat1);
            _block.SetFloat(SeedId, _seed);
            if (_hasEye)
            {
                _pushedEyeCenter = transform.position + _eyeOffset;
                _pushedEyeDir = _eyeDir;
                _pushedEyeUp = _eyeUp;
                _block.SetVector(EyeCenterId, _pushedEyeCenter);
                _block.SetVector(EyeDirId, _eyeDir);
                _block.SetVector(EyeUpId, _eyeUp);
                _block.SetFloat(EyeRadiusId, _eyeRadius);
            }
            _renderer.SetPropertyBlock(_block);
            _pushedEnergy = _energy;
            _pushedFlash = _flash;
            _pushedNat20 = _nat20;
            _pushedNat1 = _nat1;
        }

        // ------------------------------------------------------------------ extra objects

        private void UpdateExtras()
        {
            if (_effects == null) return;
            PlaceExtra(_extra20, _nat20, _effects.nat20Size, _effects.nat20Height, _effects.nat20Lateral);
            PlaceExtra(_extra1, _nat1, _effects.nat1Size, _effects.nat1Height, _effects.nat1Lateral);
        }

        /// <summary>Shown while its amount is above zero; grows from nothing with the amount, so it also fades in and out.</summary>
        private void PlaceExtra(GameObject extra, float amount, float size, float height, float lateral)
        {
            if (extra == null) return;
            bool on = _extrasShown && amount > 0.001f;
            if (extra.activeSelf != on) extra.SetActive(on);
            if (!on) return;

            Transform t = extra.transform;
            t.position = transform.position + Vector3.up * (height * _radius) + LateralDirection() * (lateral * _radius);
            t.rotation = Quaternion.identity;
            float parentScale = _extraParent != null ? Mathf.Max(_extraParent.lossyScale.x, Mathf.Epsilon) : 1f;
            t.localScale = Vector3.one * (size * _radius * amount / parentScale);
        }

        /// <summary>Toward the top of the screen along the table (so an extra "above" the die reads as above on a tilted camera).</summary>
        private Vector3 LateralDirection()
        {
            Camera cam = _viewCamera != null ? _viewCamera : Camera.main;
            if (cam == null) return Vector3.zero;
            Vector3 flat = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
            return flat.sqrMagnitude < 1e-4f ? Vector3.zero : flat.normalized;
        }
    }
}
