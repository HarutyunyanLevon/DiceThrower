using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>One die the tray has to put on the table.</summary>
    public struct PhysicalDie
    {
        /// <summary>No decided value: the die is thrown for real and whatever it shows counts.</summary>
        public const int NoTarget = -1;

        public DieKind Kind;
        /// <summary>The value the top face must show (RNG mode, the die is just placed), or <see cref="NoTarget"/>.</summary>
        public int TargetValue;
        /// <summary>Index of the roll set this die belongs to (for colouring / grouping).</summary>
        public int SetIndex;
        /// <summary>0, or 1 for the second copy of the pool thrown for advantage / disadvantage.</summary>
        public int Pass;

        public bool HasTarget { get { return TargetValue != NoTarget; } }

        public PhysicalDie(DieKind kind, int targetValue, int setIndex, int pass = 0)
        {
            Kind = kind;
            TargetValue = targetValue;
            SetIndex = setIndex;
            Pass = pass;
        }
    }

    /// <summary>The number a die in the tray shows, read after it came to rest.</summary>
    public struct DieReading
    {
        public int Set;
        public int Pass;
        public DieKind Kind;
        public int Value;

        public DieReading(int set, int pass, DieKind kind, int value)
        {
            Set = set;
            Pass = pass;
            Kind = kind;
            Value = value;
        }
    }

    /// <summary>Turns decided results into the list of physical dice to throw.</summary>
    public static class DicePlanner
    {
        /// <summary>
        /// Splits a d% result (1..100) into the tens die and the units die.
        /// 100 is "00" + "0"; 40 is "40" + "0"; 7 is "00" + "7".
        /// </summary>
        public static void SplitPercent(int value, out int tens, out int units)
        {
            if (value == 100)
            {
                tens = 0;
                units = 0;
                return;
            }
            tens = (value / 10) * 10;
            units = value % 10;
        }

        /// <summary>The physical dice a logical die turns into (d% = tens + units).</summary>
        public static void PlanDie(DieRoll d, int setIndex, List<PhysicalDie> into, int pass = 0)
        {
            switch (d.Type)
            {
                case DieType.D4: into.Add(new PhysicalDie(DieKind.D4, d.Value, setIndex, pass)); break;
                case DieType.D6: into.Add(new PhysicalDie(DieKind.D6, d.Value, setIndex, pass)); break;
                case DieType.D8: into.Add(new PhysicalDie(DieKind.D8, d.Value, setIndex, pass)); break;
                case DieType.D10: into.Add(new PhysicalDie(DieKind.D10, d.Value, setIndex, pass)); break;
                case DieType.D12: into.Add(new PhysicalDie(DieKind.D12, d.Value, setIndex, pass)); break;
                case DieType.D20: into.Add(new PhysicalDie(DieKind.D20, d.Value, setIndex, pass)); break;
                case DieType.D100:
                    int tens, units;
                    SplitPercent(d.Value, out tens, out units);
                    into.Add(new PhysicalDie(DieKind.D10Tens, tens, setIndex, pass));
                    into.Add(new PhysicalDie(DieKind.D10Units, units, setIndex, pass));
                    break;
            }
        }

        /// <summary>Dice to throw for real: <paramref name="count"/> dice of a type, no decided values (d% = a tens and a units die each).</summary>
        public static void PlanUnrolled(DieType type, int count, int setIndex, int pass, List<PhysicalDie> into)
        {
            for (int n = 0; n < count; n++)
            {
                if (type == DieType.D100)
                {
                    into.Add(new PhysicalDie(DieKind.D10Tens, PhysicalDie.NoTarget, setIndex, pass));
                    into.Add(new PhysicalDie(DieKind.D10Units, PhysicalDie.NoTarget, setIndex, pass));
                }
                else
                {
                    into.Add(new PhysicalDie(KindOf(type), PhysicalDie.NoTarget, setIndex, pass));
                }
            }
        }

        /// <summary>A whole pool to throw for real (one pass).</summary>
        public static void PlanPool(DicePool pool, int setIndex, int pass, List<PhysicalDie> into)
        {
            for (int i = 0; i < DieInfo.TypeCount; i++)
            {
                DieType t = DieInfo.AllTypes[i];
                PlanUnrolled(t, pool.Get(t), setIndex, pass, into);
            }
        }

        /// <summary>The physical kind of a logical die (d% has two kinds: use <see cref="PlanUnrolled"/>).</summary>
        public static DieKind KindOf(DieType type)
        {
            switch (type)
            {
                case DieType.D4: return DieKind.D4;
                case DieType.D6: return DieKind.D6;
                case DieType.D8: return DieKind.D8;
                case DieType.D10: return DieKind.D10;
                case DieType.D12: return DieKind.D12;
                default: return DieKind.D20;
            }
        }

        /// <summary>
        /// Turns what the tray read into one rolled pool per pass. A d% is the tens die plus the units die (two zeros = 100);
        /// tens and units dice are paired in the order they were read.
        /// </summary>
        public static PoolRoll[] ToPools(IList<DieReading> readings, int setIndex, int passCount)
        {
            var pools = new PoolRoll[passCount];
            for (int p = 0; p < passCount; p++)
            {
                pools[p] = new PoolRoll();
                var tens = new List<int>();
                var units = new List<int>();
                for (int i = 0; i < readings.Count; i++)
                {
                    DieReading r = readings[i];
                    if (r.Set != setIndex || r.Pass != p) continue;
                    if (r.Kind == DieKind.D10Tens) tens.Add(r.Value);
                    else if (r.Kind == DieKind.D10Units) units.Add(r.Value);
                    else pools[p].Dice.Add(new DieRoll(TypeOf(r.Kind), r.Value));
                }
                int pairs = System.Math.Min(tens.Count, units.Count);
                for (int i = 0; i < pairs; i++)
                {
                    int value = tens[i] + units[i];
                    pools[p].Dice.Add(new DieRoll(DieType.D100, value == 0 ? 100 : value));
                }
            }
            return pools;
        }

        private static DieType TypeOf(DieKind kind)
        {
            switch (kind)
            {
                case DieKind.D4: return DieType.D4;
                case DieKind.D6: return DieType.D6;
                case DieKind.D8: return DieType.D8;
                case DieKind.D10: return DieType.D10;
                case DieKind.D12: return DieType.D12;
                default: return DieType.D20;
            }
        }

        /// <summary>True when a physical die of this kind belongs to the logical die type.</summary>
        public static bool KindBelongsTo(DieKind kind, DieType type)
        {
            switch (type)
            {
                case DieType.D4: return kind == DieKind.D4;
                case DieType.D6: return kind == DieKind.D6;
                case DieType.D8: return kind == DieKind.D8;
                case DieType.D10: return kind == DieKind.D10;
                case DieType.D12: return kind == DieKind.D12;
                case DieType.D20: return kind == DieKind.D20;
                default: return kind == DieKind.D10Tens || kind == DieKind.D10Units;
            }
        }

        /// <summary>Only the kept pass is thrown; the discarded advantage pass is report-only.</summary>
        public static void Plan(SetResult result, int setIndex, List<PhysicalDie> into)
        {
            List<DieRoll> dice = result.Chosen.Dice;
            for (int i = 0; i < dice.Count; i++) PlanDie(dice[i], setIndex, into);
        }

        public static List<PhysicalDie> Plan(ThrowResult throwResult)
        {
            var list = new List<PhysicalDie>();
            for (int s = 0; s < throwResult.Sets.Length; s++)
            {
                if (throwResult.Sets[s] != null) Plan(throwResult.Sets[s], s, list);
            }
            return list;
        }
    }
}
