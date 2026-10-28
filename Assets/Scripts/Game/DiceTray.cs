using System;
using System.Collections.Generic;
using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// Runtime side of the DiceTray prefab. The prefab holds the floor, rails and invisible walls
    /// (edit them in Unity); this throws the planned dice and waits until everything lies still (or the settle time runs out,
    /// or the player taps the tray). What the dice show is the result: <see cref="ReadDice"/> hands it to the session.
    /// Dice that come with a decided value (physics switched off) are just placed showing it.
    /// </summary>
    public sealed class DiceTray : MonoBehaviour
    {
        [SerializeField] private TrayConfig config;
        [SerializeField] private DieBody diePrefab;
        [SerializeField, Tooltip("URP Lit material that the numbered dice are cloned from.")] private Material dieMaterial;
        [SerializeField, Tooltip("The visible floor slab. Its scale is the tray's outer size, which the camera fits to the screen.")] private Transform floor;
        [SerializeField, Tooltip("Parent of the spawned dice.")] private Transform diceRoot;

        private readonly List<DieBody> _dice = new List<DieBody>();
        private bool _rolling;
        private float _rollTime;
        private bool _instantSettled; // dice were placed by hand; report it from the next Update

        /// <summary>Seconds after a throw before dice that still move are set where they are.</summary>
        public float SettleSeconds = 5f;

        /// <summary>Dice are still moving and the result is not known yet.</summary>
        public bool IsRolling { get { return _rolling; } }

        /// <summary>A die hit something; the argument is the impact speed.</summary>
        public event Action<float> DieHit;

        /// <summary>Every die lies still (or was set by hand) and shows its number.</summary>
        public event Action Settled;

        /// <summary>Outer width (x) and depth (y) of the tray, rails included.</summary>
        public Vector2 Extent
        {
            get { Vector3 s = floor.lossyScale; return new Vector2(s.x, s.z); }
        }

        private void Awake()
        {
            Physics.gravity = new Vector3(0f, -config.gravity, 0f);
            DiceVisuals.Template = dieMaterial;
        }

        // ------------------------------------------------------------------ session changes

        /// <summary>Applies a session change: clear / remove / throw additional dice.</summary>
        public void Apply(TrayChange change, IList<DiceSkin> skinBySet)
        {
            if (change.ClearAll) ClearDice();
            for (int i = 0; i < change.ClearSets.Count; i++)
            {
                int set = change.ClearSets[i];
                RemoveDice(d => d.SetIndex == set);
            }
            for (int i = 0; i < change.RemoveTypes.Count; i++)
            {
                int set = change.RemoveTypes[i].Key;
                DieType type = change.RemoveTypes[i].Value;
                RemoveDice(d => d.SetIndex == set && DicePlanner.KindBelongsTo(d.Kind, type));
            }
            if (change.Add.Count > 0) AddDice(change.Add, skinBySet);
        }

        private void ClearDice()
        {
            RemoveDice(d => true);
            _rolling = false;
        }

        private void RemoveDice(Predicate<DieBody> match)
        {
            for (int i = _dice.Count - 1; i >= 0; i--)
            {
                if (_dice[i] != null && !match(_dice[i])) continue;
                if (_dice[i] != null) Destroy(_dice[i].gameObject);
                _dice.RemoveAt(i);
            }
            if (_dice.Count == 0) _rolling = false;
        }

        /// <summary>The losing copy of an advantage / disadvantage throw leaves the tray.</summary>
        public void DiscardPass(int set, int pass)
        {
            for (int i = _dice.Count - 1; i >= 0; i--)
            {
                DieBody d = _dice[i];
                if (d == null) { _dice.RemoveAt(i); continue; }
                if (d.SetIndex != set || d.Pass != pass) continue;
                _dice.RemoveAt(i);
                d.Dismiss(config.dismissSeconds);
            }
        }

        /// <summary>What every die in the tray shows right now.</summary>
        public void ReadDice(List<DieReading> into)
        {
            into.Clear();
            for (int i = 0; i < _dice.Count; i++)
            {
                DieBody d = _dice[i];
                into.Add(new DieReading(d.SetIndex, d.Pass, d.Kind, d.ReadTopValue()));
            }
        }

        /// <summary>
        /// Adds dice (existing dice stay). Dice without a decided value are thrown and start the settle timer;
        /// dice with one are placed at free spots showing it.
        /// </summary>
        private void AddDice(IList<PhysicalDie> plan, IList<DiceSkin> skinBySet)
        {
            bool others = _dice.Count > 0;
            var thrown = new List<PhysicalDie>();
            var placed = new List<PhysicalDie>();
            for (int i = 0; i < plan.Count; i++) (plan[i].HasTarget ? placed : thrown).Add(plan[i]);

            var fresh = new List<DieBody>(); // spawned, not placed yet: they do not count as obstacles
            for (int i = 0; i < placed.Count; i++) fresh.Add(SpawnDie(placed[i], skinBySet));
            for (int i = 0; i < placed.Count; i++)
            {
                DieBody die = fresh[i];
                Vector2 spot = FindFreeSpot(Footprint(die), fresh);
                die.PlaceShowing(transform.TransformPoint(new Vector3(spot.x, 0f, spot.y)), placed[i].TargetValue, UnityEngine.Random.Range(0f, 360f), transform.position.y);
                fresh[i] = null;
            }

            ThrowDice(thrown, skinBySet, others);

            if (thrown.Count > 0)
            {
                _rolling = true;
                _rollTime = 0f; // the new dice get the full time
            }
            else if (placed.Count > 0 && !_rolling)
            {
                _instantSettled = true;
            }
        }

        private DieBody SpawnDie(PhysicalDie pd, IList<DiceSkin> skinBySet)
        {
            DiceSkin skin = skinBySet[Mathf.Clamp(pd.SetIndex, 0, skinBySet.Count - 1)];
            DieBody die = DieBody.Spawn(diePrefab, diceRoot, config, pd.Kind, skin, pd.TargetValue, pd.SetIndex, pd.Pass);
            die.Hit += OnDieHit;
            _dice.Add(die);
            return die;
        }

        private void ThrowDice(List<PhysicalDie> thrown, IList<DiceSkin> skinBySet, bool others)
        {
            int perLayer = config.spawnColumns * config.spawnRowsPerLayer;
            float nearEdgeZ = -Extent.y * 0.5f + config.spawnEdgeInset;

            // shuffled slots, so which die lands where is random
            var order = new List<int>();
            for (int i = 0; i < thrown.Count; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }

            for (int n = 0; n < order.Count; n++)
            {
                DieBody die = SpawnDie(thrown[order[n]], skinBySet);

                Vector3 local, velocity;
                if (others) RandomEdgeThrow(out local, out velocity);
                else GridThrow(n, perLayer, nearEdgeZ, out local, out velocity);

                Vector3 spin = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(config.spinMin, config.spinMax);
                die.Launch(transform.TransformPoint(local), UnityEngine.Random.rotation, transform.TransformDirection(velocity), spin);
            }
        }

        private void GridThrow(int n, int perLayer, float nearEdgeZ, out Vector3 local, out Vector3 velocity)
        {
            int layer = n / perLayer;
            int slot = n % perLayer;
            int col = slot % config.spawnColumns, row = slot / config.spawnColumns;
            float jitter = config.spawnJitter;
            local = new Vector3(
                (col - (config.spawnColumns - 1) * 0.5f) * config.spawnSpacing + UnityEngine.Random.Range(-jitter, jitter),
                config.spawnHeight + layer * config.spawnLayerHeight,
                nearEdgeZ + row * config.spawnSpacing + UnityEngine.Random.Range(-jitter, jitter));

            Vector3 vMin = config.launchVelocityMin, vMax = config.launchVelocityMax;
            velocity = new Vector3(
                UnityEngine.Random.Range(vMin.x, vMax.x),
                UnityEngine.Random.Range(vMin.y, vMax.y),
                UnityEngine.Random.Range(vMin.z, vMax.z));
        }

        /// <summary>A random point on the tray's edge, thrown towards the middle.</summary>
        private void RandomEdgeThrow(out Vector3 local, out Vector3 velocity)
        {
            Vector2 half = Extent * 0.5f - Vector2.one * config.randomSpawnInset;
            Vector2 p;
            switch (UnityEngine.Random.Range(0, 4))
            {
                case 0: p = new Vector2(UnityEngine.Random.Range(-half.x, half.x), -half.y); break;
                case 1: p = new Vector2(UnityEngine.Random.Range(-half.x, half.x), half.y); break;
                case 2: p = new Vector2(-half.x, UnityEngine.Random.Range(-half.y, half.y)); break;
                default: p = new Vector2(half.x, UnityEngine.Random.Range(-half.y, half.y)); break;
            }
            local = new Vector3(p.x, config.spawnHeight, p.y);

            Vector2 toCentre = (-p).normalized;
            float speed = UnityEngine.Random.Range(config.launchVelocityMin.z, config.launchVelocityMax.z);
            velocity = new Vector3(toCentre.x * speed, UnityEngine.Random.Range(config.launchVelocityMin.y, config.launchVelocityMax.y), toCentre.y * speed);
        }

        private void OnDieHit(float speed)
        {
            if (DieHit != null) DieHit(speed);
        }

        // ------------------------------------------------------------------ settling

        private void Update()
        {
            if (_instantSettled)
            {
                _instantSettled = false;
                if (Settled != null) Settled();
                return;
            }
            if (!_rolling) return;

            float dt = Time.deltaTime;
            _rollTime += dt;
            float floorY = transform.position.y;

            bool allStill = true;
            for (int i = 0; i < _dice.Count; i++)
            {
                if (!_dice[i].Tick(dt, floorY)) allStill = false;
            }

            if (_rollTime >= SettleSeconds)
            {
                SetByHand(false);
                return;
            }
            if (!allStill) return;

            bool nudged = false;
            for (int i = 0; i < _dice.Count; i++)
            {
                if (_dice[i].NeedsNudge) { _dice[i].Nudge(); nudged = true; }
            }
            if (nudged) return; // wait for the hopped dice to settle again

            Finish();
        }

        /// <summary>The player tapped while dice roll: dice that still move go to free spots with a random face.</summary>
        public void ForceSettle()
        {
            if (_rolling) SetByHand(true);
        }

        private void Finish()
        {
            _rolling = false;
            if (Settled != null) Settled();
        }

        // ------------------------------------------------------------------ setting dice by hand

        /// <summary>
        /// Ends the roll. Dice that still move are either frozen where they are, tilted flat and nudged out of overlaps (timeout),
        /// or put at free spots showing a random face (<paramref name="relocateMoving"/>, the player's tap).
        /// Dice that already lie still stay, unless they are cocked on something, then they are tilted flat.
        /// </summary>
        private void SetByHand(bool relocateMoving)
        {
            float floorY = transform.position.y;
            var snapped = new List<DieBody>();
            var relocated = new List<DieBody>();
            for (int i = 0; i < _dice.Count; i++)
            {
                DieBody d = _dice[i];
                if (!d.Resting && relocateMoving) relocated.Add(d);
                else if (!d.Resting || !d.IsFlat()) { d.SnapFlat(floorY); snapped.Add(d); }
            }

            Separate(snapped);

            var waiting = new List<DieBody>(relocated); // still moving: not obstacles until placed
            for (int i = 0; i < relocated.Count; i++)
            {
                DieBody d = relocated[i];
                Vector2 spot = FindFreeSpot(Footprint(d), waiting);
                d.PlaceShowing(transform.TransformPoint(new Vector3(spot.x, 0f, spot.y)), d.RandomValue(), UnityEngine.Random.Range(0f, 360f), floorY);
                waiting[i] = null;
            }
            Finish();
        }

        private float Footprint(DieBody d)
        {
            return d.Radius * config.footprintScale;
        }

        private Vector2 Local2(DieBody d)
        {
            Vector3 p = transform.InverseTransformPoint(d.transform.position);
            return new Vector2(p.x, p.z);
        }

        private Vector2 Clamp(Vector2 p, float r)
        {
            Vector2 half = Extent * 0.5f - Vector2.one * (config.placeEdgeInset + r);
            return new Vector2(Mathf.Clamp(p.x, -half.x, half.x), Mathf.Clamp(p.y, -half.y, half.y));
        }

        /// <summary>A spot (tray-local x, z) where a die of footprint radius <paramref name="r"/> touches no other die (dice in <paramref name="ignore"/> do not count); the roomiest spot found when none is free.</summary>
        private Vector2 FindFreeSpot(float r, List<DieBody> ignore)
        {
            Vector2 half = Extent * 0.5f - Vector2.one * (config.placeEdgeInset + r);
            Vector2 best = Vector2.zero;
            float bestRoom = float.MinValue;
            for (int attempt = 0; attempt < Mathf.Max(1, config.placeAttempts); attempt++)
            {
                var p = new Vector2(UnityEngine.Random.Range(-half.x, half.x), UnityEngine.Random.Range(-half.y, half.y));
                float room = float.MaxValue; // smallest clearance to any other die (negative = overlap)
                for (int i = 0; i < _dice.Count; i++)
                {
                    DieBody o = _dice[i];
                    if (o == null || ignore.Contains(o)) continue;
                    float clearance = (Local2(o) - p).magnitude - (r + Footprint(o) + config.placeGap);
                    room = Mathf.Min(room, clearance);
                }
                if (room >= 0f) return p;
                if (room > bestRoom) { bestRoom = room; best = p; }
            }
            return best;
        }

        /// <summary>Pushes the given dice (and only them) off every die they overlap, keeping them inside the tray.</summary>
        private void Separate(List<DieBody> movable)
        {
            if (movable.Count == 0) return;
            var pos = new Dictionary<DieBody, Vector2>();
            for (int i = 0; i < _dice.Count; i++) pos[_dice[i]] = Local2(_dice[i]);

            for (int pass = 0; pass < config.separatePasses; pass++)
            {
                bool moved = false;
                for (int m = 0; m < movable.Count; m++)
                {
                    DieBody a = movable[m];
                    float ra = Footprint(a);
                    for (int i = 0; i < _dice.Count; i++)
                    {
                        DieBody b = _dice[i];
                        if (b == a) continue;
                        Vector2 delta = pos[a] - pos[b];
                        float need = ra + Footprint(b) + config.placeGap;
                        float dist = delta.magnitude;
                        if (dist >= need) continue;
                        Vector2 dir = dist > 1e-3f ? delta / dist : UnityEngine.Random.insideUnitCircle.normalized;
                        bool bMovable = movable.Contains(b);
                        float push = need - dist;
                        pos[a] = Clamp(pos[a] + dir * (bMovable ? push * 0.5f : push), ra);
                        if (bMovable) pos[b] = Clamp(pos[b] - dir * push * 0.5f, Footprint(b));
                        moved = true;
                    }
                }
                if (!moved) break;
            }

            for (int m = 0; m < movable.Count; m++)
                movable[m].ShiftTo(transform.TransformPoint(new Vector3(pos[movable[m]].x, 0f, pos[movable[m]].y)));
        }
    }
}
