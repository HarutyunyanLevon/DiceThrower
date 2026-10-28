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
        public const int SetCount = 2; // 0 = Attack, 1 = Damage

        public readonly RollSetConfig[] Sets = { new RollSetConfig(), new RollSetConfig() };
        public readonly DiceLimits Limits;

        private readonly IRng _rng;
        private readonly PoolRoll[][] _passes = new PoolRoll[SetCount][]; // null = nothing rolled for that set

        public RollSession(IRng rng, DiceLimits limits)
        {
            _rng = rng;
            Limits = limits ?? new DiceLimits();
        }

        public bool HasRoll(int set) { return _passes[set] != null; }

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
                _passes[s] = null;
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
                _passes[s] = null;
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

            if (_passes[set] == null || Sets[set].Advantage != AdvantageMode.Normal)
            {
                RollSetFresh(set, c, true);
            }
            else
            {
                DieRoll d = RollEngine.RollDie(type, _rng);
                _passes[set][0].Dice.Add(d);
                DicePlanner.PlanDie(d, set, c.Add);
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
                _passes[set] = null;
                c.ClearSets.Add(set);
                return c;
            }
            if (_passes[set] == null || Sets[set].Advantage != AdvantageMode.Normal)
            {
                RollSetFresh(set, c, true);
                return c;
            }

            if (now > old)
            {
                for (int i = 0; i < now - old; i++) AddRolledDie(set, type, c);
            }
            else
            {
                _passes[set][0].Dice.RemoveAll(d => d.Type == type);
                c.RemoveTypes.Add(new KeyValuePair<int, DieType>(set, type));
                for (int i = 0; i < now; i++) AddRolledDie(set, type, c);
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
                _passes[set] = null;
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
                _passes[s] = null;
            }
            return new TrayChange { ClearAll = true };
        }

        // ------------------------------------------------------------------ helpers

        private void RollSetFresh(int set, TrayChange c, bool clearSetFirst)
        {
            SetResult r = RollEngine.Roll(Sets[set], _rng);
            _passes[set] = r.Passes;
            if (clearSetFirst) c.ClearSets.Add(set);
            DicePlanner.Plan(r, set, c.Add);
        }

        private void AddRolledDie(int set, DieType type, TrayChange c)
        {
            DieRoll d = RollEngine.RollDie(type, _rng);
            _passes[set][0].Dice.Add(d);
            DicePlanner.PlanDie(d, set, c.Add);
        }
    }
}
