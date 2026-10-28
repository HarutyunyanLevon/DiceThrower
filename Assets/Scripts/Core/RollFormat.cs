using System.Collections.Generic;
using System.Text;

namespace DiceThrower.Core
{
    /// <summary>Text for rolls, shared by the Roll, Presets and History tabs.</summary>
    public static class RollFormat
    {
        public static string Modifier(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }

        /// <summary>"ADV" / "DIS" for the d20 header button; empty for a normal roll.</summary>
        public static string AdvantageTag(AdvantageMode mode)
        {
            switch (mode)
            {
                case AdvantageMode.Advantage: return "ADV";
                case AdvantageMode.Disadvantage: return "DIS";
                default: return "";
            }
        }

        /// <summary>The kept dice and the modifier, e.g. "4 + 17 + 3   (12 dropped)".</summary>
        public static string Breakdown(SetResult r)
        {
            if (r == null) return "";
            var sb = new StringBuilder();
            List<DieRoll> dice = r.Chosen.Dice;
            for (int i = 0; i < dice.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(dice[i].Value);
            }
            if (r.Modifier > 0) sb.Append(" + ").Append(r.Modifier);
            else if (r.Modifier < 0) sb.Append(" - ").Append(-r.Modifier);
            if (r.Passes.Length == 2) sb.Append("   (").Append(r.Passes[1 - r.ChosenPass].Subtotal).Append(" dropped)");
            return sb.ToString();
        }

        /// <summary>The setup as dice notation, e.g. "2d6 + 1d20 + 3 ADV". Empty for an empty pool.</summary>
        public static string Notation(RollSetConfig c)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < DieInfo.TypeCount; i++)
            {
                DieType t = DieInfo.AllTypes[i];
                int n = c.Pool.Get(t);
                if (n == 0) continue;
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append(n).Append(DieInfo.Label(t));
            }
            if (sb.Length == 0) return "";

            if (c.Modifier > 0) sb.Append(" + ").Append(c.Modifier);
            else if (c.Modifier < 0) sb.Append(" - ").Append(-c.Modifier);
            string tag = AdvantageTag(c.Advantage);
            if (tag.Length > 0) sb.Append(' ').Append(tag);
            return sb.ToString();
        }
    }
}
