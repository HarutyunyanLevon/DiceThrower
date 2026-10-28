using System;
using System.Collections.Generic;
using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// Runtime side of the DiceTray prefab. The prefab holds the floor, rails and invisible walls
    /// (edit them in Unity); this spawns the planned dice, waits until everything lies still, then
    /// remaps each die to the value the RollEngine already decided.
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

        /// <summary>A die hit something; the argument is the impact speed.</summary>
        public event Action<float> DieHit;

        /// <summary>Every die lies still and shows its decided number.</summary>
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
        }

        /// <summary>Spawns more dice above the tray (existing dice stay) and starts rolling.</summary>
        private void AddDice(IList<PhysicalDie> plan, IList<DiceSkin> skinBySet)
        {
            int perLayer = config.spawnColumns * config.spawnRowsPerLayer;
            float nearEdgeZ = -Extent.y * 0.5f + config.spawnEdgeInset;

            // shuffled slots, so which die lands where is random
            var order = new List<int>();
            for (int i = 0; i < plan.Count; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }

            for (int n = 0; n < order.Count; n++)
            {
                PhysicalDie pd = plan[order[n]];
                DiceSkin skin = skinBySet[Mathf.Clamp(pd.SetIndex, 0, skinBySet.Count - 1)];
                DieBody die = DieBody.Spawn(diePrefab, diceRoot, config, pd.Kind, skin, pd.TargetValue, pd.SetIndex);

                int layer = n / perLayer;
                int slot = n % perLayer;
                int col = slot % config.spawnColumns, row = slot / config.spawnColumns;
                float jitter = config.spawnJitter;
                var local = new Vector3(
                    (col - (config.spawnColumns - 1) * 0.5f) * config.spawnSpacing + UnityEngine.Random.Range(-jitter, jitter),
                    config.spawnHeight + layer * config.spawnLayerHeight,
                    nearEdgeZ + row * config.spawnSpacing + UnityEngine.Random.Range(-jitter, jitter));

                Vector3 vMin = config.launchVelocityMin, vMax = config.launchVelocityMax;
                var velocity = new Vector3(
                    UnityEngine.Random.Range(vMin.x, vMax.x),
                    UnityEngine.Random.Range(vMin.y, vMax.y),
                    UnityEngine.Random.Range(vMin.z, vMax.z));
                Vector3 spin = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(config.spinMin, config.spinMax);

                die.Hit += OnDieHit;
                die.Launch(transform.TransformPoint(local), UnityEngine.Random.rotation, transform.TransformDirection(velocity), spin);
                _dice.Add(die);
            }

            _rolling = true;
            _rollTime = 0f;
        }

        private void OnDieHit(float speed)
        {
            if (DieHit != null) DieHit(speed);
        }

        // ------------------------------------------------------------------ settling

        private void Update()
        {
            if (!_rolling) return;
            float dt = Time.deltaTime;
            _rollTime += dt;

            bool allStill = true;
            for (int i = 0; i < _dice.Count; i++)
            {
                if (!_dice[i].Tick(dt, transform.position.y)) allStill = false;
            }

            bool timedOut = _rollTime > config.maxRollSeconds;
            if (!allStill && !timedOut) return;

            if (!timedOut)
            {
                bool nudged = false;
                for (int i = 0; i < _dice.Count; i++)
                {
                    if (_dice[i].NeedsNudge) { _dice[i].Nudge(); nudged = true; }
                }
                if (nudged) return; // wait for the hopped dice to settle again
            }

            // everything is still: show the decided numbers
            for (int i = 0; i < _dice.Count; i++) _dice[i].RemapToTarget();
            _rolling = false;
            if (Settled != null) Settled();
        }
    }
}
