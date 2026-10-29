using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>What the 3D tray has to do to match a session change, applied in this order.</summary>
    public sealed class TrayChange
    {
        public bool ClearAll;
        public readonly List<int> ClearSets = new List<int>();
        public readonly List<KeyValuePair<int, DieType>> RemoveTypes = new List<KeyValuePair<int, DieType>>();
        public readonly List<PhysicalDie> Add = new List<PhysicalDie>();

        public bool IsEmpty
        {
            get { return !ClearAll && ClearSets.Count == 0 && RemoveTypes.Count == 0 && Add.Count == 0; }
        }
    }

    /// <summary>
    /// The live state of the Roll screen: both sets, their current rolled values, and the
    /// rules for what a tap / slide / toggle does. Pure logic; the view just applies TrayChange.
    /// </summary>
    public sealed class RollSession
    {
        public const int SetCount = 2;
        public const int AttackSet = 0, DamageSet = 1;

        public readonly RollSetConfig[] Sets = { new RollSetConfig(), new RollSetConfig() };
        public readonly DiceLimits Limits;

        private readonly IRng _rng;
        private readonly PoolRoll[][] _passes = new PoolRoll[SetCount][]; // null = no result for that set (nothing thrown, or still rolling)
        private readonly bool[] _thrown = new bool[SetCount];             // the set has dice in the tray
        private readonly int[] _discarded = new int[] { -1, -1 };          // advantage pass that lost (physics), or -1
        private readonly bool[] _discardPending = new bool[SetCount];     // the tray has not been told yet

        /// <summary>
        /// True: dice are thrown for real and the result is what they show (<see cref="Settle"/> hands the readings in).
        /// False: the result is decided here (RNG) and the tray only places dice showing it.
        /// </summary>
        public bool UsePhysics;

        public RollSession(IRng rng, DiceLimits limits)
        {
            _rng = rng;
            Limits = limits ?? new DiceLimits();
        }

        /// <summary>The set has a result (not while its dice are still rolling).</summary>
        public bool HasRoll(int set) { return _passes[set] != null; }

        /// <summary>Some set has dice in the tray that have not settled yet, so it has no result.</summary>
        public bool HasPendingResult
        {
            get
            {
                for (int s = 0; s < SetCount; s++) if (_thrown[s] && _passes[s] == null) return true;
                return false;
            }
        }

        /// <summary>The set has dice in the tray, settled or not.</summary>
        public bool HasThrown(int set) { return _thrown[set]; }

        /// <summary>
        /// Physics mode: every die in the tray lies still. Builds each thrown set's result from what the dice show.
        /// With advantage / disadvantage the set was thrown twice; the losing copy is reported by <see cref="TakeDiscard"/>
        /// and leaves the tray, and later calls keep that set's result as it is.
        /// </summary>
        public void Settle(IList<DieReading> readings)
        {
            for (int s = 0; s < SetCount; s++)
            {
                if (!_thrown[s]) { _passes[s] = null; continue; }
                if (_discarded[s] >= 0 && _passes[s] != null) continue; // already resolved, the losing dice are gone

                int passCount = Sets[s].Advantage == AdvantageMode.Normal ? 1 : 2;
                PoolRoll[] pools = DicePlanner.ToPools(readings, s, passCount);
                if (pools[0].Dice.Count == 0 && (passCount == 1 || pools[1].Dice.Count == 0))
                {
                    _passes[s] = null;
                    continue;
                }
                _passes[s] = pools;
                if (passCount == 2)
                {
                    _discarded[s] = 1 - GetResult(s).ChosenPass;
                    _discardPending[s] = true;
                }
            }
        }

        /// <summary>
        /// The advantage pass that lost and has to leave the tray, or -1. Reported once per throw.
        /// </summary>
        public int TakeDiscard(int set)
        {
            if (!_discardPending[set]) return -1;
            _discardPending[set] = false;
            return _discarded[set];
        }

        public SetResult GetResult(int set)
        {
            if (_passes[set] == null) return null;
            return RollEngine.Evaluate(Sets[set].Clone(), _passes[set]);
        }

        /// <summary>Lowest and highest possible total of a set (pool + modifier).</summary>
        public void GetRange(int set, out int min, out int max)
        {
            DicePool pool = Sets[set].Pool;
            min = Sets[set].Modifier;
            max = Sets[set].Modifier;
            for (int i = 0; i < DieInfo.TypeCount; i++)
            {
                DieType t = DieInfo.AllTypes[i];
                int n = pool.Get(t);
                min += n;
                max += n * DieInfo.Sides(t);
            }
        }

        /// <summary>True when at least one set holds a die.</summary>
        public bool HasDice
        {
            get
            {
                for (int s = 0; s < SetCount; s++)
                    if (!Sets[s].Pool.IsEmpty) return true;
                return false;
            }
        }

        /// <summary>Independent copies of both sets as they are now (what a preset stores).</summary>
        public RollSetConfig[] Snapshot()
        {
            var copy = new RollSetConfig[SetCount];
            for (int s = 0; s < SetCount; s++) copy[s] = Sets[s].Clone();
            return copy;
        }

        /// <summary>Everything currently rolled, as an independent copy (what the history stores). Null when nothing is rolled.</summary>
        public ThrowResult GetThrow()
        {
            var result = new ThrowResult { Sets = new SetResult[SetCount] };
            bool any = false;
            for (int s = 0; s < SetCount; s++)
            {
                if (_passes[s] == null) continue;
                var passes = new PoolRoll[_passes[s].Length];
                for (int p = 0; p < passes.Length; p++) passes[p] = _passes[s][p].Clone();
                result.Sets[s] = RollEngine.Evaluate(Sets[s].Clone(), passes);
                any = true;
            }
            return any ? result : null;
        }

        // ------------------------------------------------------------------ operations

        /// <summary>Replaces both sets with a saved setup (squeezed into the current limits) and throws it.</summary>
        public TrayChange Load(IList<RollSetConfig> sets)
        {
            Configure(sets);
            return ThrowAll();
        }

        /// <summary>Replaces both sets with a setup (squeezed into the current limits) without throwing. Nothing is rolled afterwards.</summary>
        public void Configure(IList<RollSetConfig> sets)
        {
            for (int s = 0; s < SetCount; s++)
            {
                ResetSet(s);
                RollSetConfig source = s < sets.Count ? sets[s] : new RollSetConfig();
                RollSetConfig target = Sets[s];
                target.Pool.Clear();
                for (int i = 0; i < DieInfo.TypeCount; i++)
                {
                    DieType t = DieInfo.AllTypes[i];
                    target.Pool.SetCount(t, source.Pool.Get(t), Limits);
                }
                target.Advantage = source.Advantage;
                target.Enabled = source.Enabled;
                target.Modifier = Limits.ClampModifier(source.Modifier);
            }
        }

        /// <summary>Throw button / tray tap: everything is rolled again.</summary>
        public TrayChange ThrowAll()
        {
            var c = new TrayChange { ClearAll = true };
            for (int s = 0; s < SetCount; s++)
            {
                ResetSet(s);
                if (Sets[s].Enabled && !Sets[s].Pool.IsEmpty) RollSetFresh(s, c, false);
            }
            return c;
        }

        /// <summary>Short press on a die button: adds one die and throws just that die.</summary>
        public TrayChange AddDie(int set, DieType type, out AddResult result)
        {
            var c = new TrayChange();
            result = Sets[set].Pool.TryAdd(type, Limits);
            if (result != AddResult.Added || !Sets[set].Enabled) return c;

            if (!_thrown[set] || Sets[set].Advantage != AdvantageMode.Normal)
            {
                RollSetFresh(set, c, true);
            }
            else
            {
                AddThrownDie(set, type, c);
            }
            return c;
        }

        /// <summary>
        /// Press-and-slide count: more = throw only the extras, fewer = clear that die type and
        /// throw the new amount.
        /// </summary>
        public TrayChange SetDieCount(int set, DieType type, int count, out AddResult result)
        {
            var c = new TrayChange();
            DicePool pool = Sets[set].Pool;
            int old = pool.Get(type);
            result = pool.SetCount(type, count, Limits);
            int now = pool.Get(type);
            if (!Sets[set].Enabled || now == old) return c;

            if (pool.IsEmpty)
            {
                ResetSet(set);
                c.ClearSets.Add(set);
                return c;
            }
            if (!_thrown[set] || Sets[set].Advantage != AdvantageMode.Normal)
            {
                RollSetFresh(set, c, true);
                return c;
            }

            if (now > old)
            {
                for (int i = 0; i < now - old; i++) AddThrownDie(set, type, c);
            }
            else
            {
                if (_passes[set] != null) _passes[set][0].Dice.RemoveAll(d => d.Type == type);
                c.RemoveTypes.Add(new KeyValuePair<int, DieType>(set, type));
                for (int i = 0; i < now; i++) AddThrownDie(set, type, c);
            }
            return c;
        }

        public void SetModifier(int set, int modifier)
        {
            Sets[set].Modifier = Limits.ClampModifier(modifier); // only the total changes, nothing is thrown
        }

        /// <summary>d20 header button: that set's whole pool is rolled again (twice for adv/dis).</summary>
        public TrayChange CycleAdvantage(int set)
        {
            var c = new TrayChange();
            Sets[set].CycleAdvantage();
            if (Sets[set].Enabled && !Sets[set].Pool.IsEmpty) RollSetFresh(set, c, true);
            return c;
        }

        /// <summary>Disabled = dice cleared and not thrown, but the pool is kept.</summary>
        public TrayChange SetEnabled(int set, bool enabled)
        {
            var c = new TrayChange();
            Sets[set].Enabled = enabled;
            if (!enabled)
            {
                ResetSet(set);
                c.ClearSets.Add(set);
            }
            else if (!Sets[set].Pool.IsEmpty)
            {
                RollSetFresh(set, c, true);
            }
            return c;
        }

        /// <summary>Clear button: empties both pools and the tray.</summary>
        public TrayChange ClearAll()
        {
            for (int s = 0; s < SetCount; s++)
            {
                Sets[s].Pool.Clear();
                ResetSet(s);
            }
            return new TrayChange { ClearAll = true };
        }

        // ------------------------------------------------------------------ helpers

        private void ResetSet(int set)
        {
            _passes[set] = null;
            _thrown[set] = false;
            _discarded[set] = -1;
            _discardPending[set] = false;
        }

        private void RollSetFresh(int set, TrayChange c, bool clearSetFirst)
        {
            if (clearSetFirst) c.ClearSets.Add(set);
            _discarded[set] = -1;
            _discardPending[set] = false;
            _thrown[set] = true;
            if (UsePhysics)
            {
                _passes[set] = null; // the result comes from the dice once they lie still
                int passCount = Sets[set].Advantage == AdvantageMode.Normal ? 1 : 2;
                for (int p = 0; p < passCount; p++) DicePlanner.PlanPool(Sets[set].Pool, set, p, c.Add);
                return;
            }
            SetResult r = RollEngine.Roll(Sets[set], _rng);
            _passes[set] = r.Passes;
            DicePlanner.Plan(r, set, c.Add);
        }

        /// <summary>One more die in a set that is already in the tray.</summary>
        private void AddThrownDie(int set, DieType type, TrayChange c)
        {
            if (UsePhysics)
            {
                _passes[set] = null;
                DicePlanner.PlanUnrolled(type, 1, set, 0, c.Add);
                return;
            }
            DieRoll d = RollEngine.RollDie(type, _rng);
            _passes[set][0].Dice.Add(d);
            DicePlanner.PlanDie(d, set, c.Add);
        }
    }
}
