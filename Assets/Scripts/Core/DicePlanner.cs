using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>One die that will physically be thrown, and the face it must end up showing.</summary>
    public struct PhysicalDie
    {
        public DieKind Kind;
        /// <summary>The value the top face must show once the die settles.</summary>
        public int TargetValue;
        /// <summary>Index of the roll set this die belongs to (for colouring / grouping).</summary>
        public int SetIndex;

        public PhysicalDie(DieKind kind, int targetValue, int setIndex)
        {
            Kind = kind;
            TargetValue = targetValue;
            SetIndex = setIndex;
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
        public static void PlanDie(DieRoll d, int setIndex, List<PhysicalDie> into)
        {
            switch (d.Type)
            {
                case DieType.D4: into.Add(new PhysicalDie(DieKind.D4, d.Value, setIndex)); break;
                case DieType.D6: into.Add(new PhysicalDie(DieKind.D6, d.Value, setIndex)); break;
                case DieType.D8: into.Add(new PhysicalDie(DieKind.D8, d.Value, setIndex)); break;
                case DieType.D10: into.Add(new PhysicalDie(DieKind.D10, d.Value, setIndex)); break;
                case DieType.D12: into.Add(new PhysicalDie(DieKind.D12, d.Value, setIndex)); break;
                case DieType.D20: into.Add(new PhysicalDie(DieKind.D20, d.Value, setIndex)); break;
                case DieType.D100:
                    int tens, units;
                    SplitPercent(d.Value, out tens, out units);
                    into.Add(new PhysicalDie(DieKind.D10Tens, tens, setIndex));
                    into.Add(new PhysicalDie(DieKind.D10Units, units, setIndex));
                    break;
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
