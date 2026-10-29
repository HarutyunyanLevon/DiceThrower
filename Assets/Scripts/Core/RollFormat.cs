using System.Collections.Generic;
using System.Text;

namespace DiceThrower.Core
{
    /// <summary>Text for rolls, shared by the Roll, Presets and History tabs and the pinned chips.</summary>
    public static class RollFormat
    {
        /// <summary>Between the formulas of the two sets on a pinned chip.</summary>
        public const string SetSeparator = " | ";

        private const string TermSeparator = " + ";

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

        // ------------------------------------------------------------------ formula

        /// <summary>
        /// The dice of a set as a formula, e.g. "d8 + 2d6 + 3": dice grouped by type, biggest die first, a single die without
        /// its count, the modifier left out when it is 0. Empty for an empty pool. This is the one formula text of the app.
        /// </summary>
        public static string Formula(RollSetConfig c)
        {
            var sb = new StringBuilder();
            for (int i = DieInfo.TypeCount - 1; i >= 0; i--) // AllTypes runs smallest to biggest
            {
                DieType t = DieInfo.AllTypes[i];
                int n = c.Pool.Get(t);
                if (n == 0) continue;
                if (sb.Length > 0) sb.Append(TermSeparator);
                if (n > 1) sb.Append(n);
                sb.Append(DieInfo.Label(t));
            }
            if (sb.Length == 0) return "";
            AppendModifier(sb, c.Modifier);
            return sb.ToString();
        }

        /// <summary>The formula followed by " ADV" / " DIS" (lists, where there is no advantage button). Empty for an empty pool.</summary>
        public static string FormulaTagged(RollSetConfig c)
        {
            string formula = Formula(c);
            string tag = AdvantageTag(c.Advantage);
            return formula.Length > 0 && tag.Length > 0 ? formula + " " + tag : formula;
        }

        /// <summary>The tagged formulas of the sets that would be thrown (enabled, with dice), joined by <see cref="SetSeparator"/>. Empty when there are none.</summary>
        public static string Summary(IList<RollSetConfig> sets)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < sets.Count; i++)
            {
                if (!sets[i].Enabled) continue;
                string formula = FormulaTagged(sets[i]);
                if (formula.Length == 0) continue;
                if (sb.Length > 0) sb.Append(SetSeparator);
                sb.Append(formula);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ breakdown

        /// <summary>
        /// What the kept dice rolled, term by term in the order of <see cref="Formula"/>, e.g. "5 + (3 + 5) + 3" for d8 + 2d6 + 3.
        /// Brackets mark a group of several dice, and are left out when the set has only one die type (8d6 reads "3 + 5 + ...").
        /// When that does not fit in <paramref name="maxChars"/> characters every group shows just its sum: "5 + 8 + 3".
        /// Advantage / disadvantage adds the dropped total, e.g. "17 + 2   (4 dropped)".
        /// </summary>
        public static string Breakdown(SetResult r, int maxChars = int.MaxValue)
        {
            if (r == null || r.Chosen.Dice.Count == 0) return "";

            List<List<int>> groups = GroupByType(r.Chosen.Dice);
            string text = Detailed(groups, r.Modifier);
            if (text.Length > maxChars) text = Sums(groups, r.Modifier);
            if (r.Passes.Length == 2) text += "   (" + r.Passes[1 - r.ChosenPass].Subtotal + " dropped)";
            return text;
        }

        /// <summary>The rolled values per die type, biggest die first; a value keeps its place inside its group.</summary>
        private static List<List<int>> GroupByType(List<DieRoll> dice)
        {
            var groups = new List<List<int>>();
            for (int i = DieInfo.TypeCount - 1; i >= 0; i--)
            {
                DieType t = DieInfo.AllTypes[i];
                List<int> values = null;
                for (int d = 0; d < dice.Count; d++)
                {
                    if (dice[d].Type != t) continue;
                    if (values == null) values = new List<int>();
                    values.Add(dice[d].Value);
                }
                if (values != null) groups.Add(values);
            }
            return groups;
        }

        private static string Detailed(List<List<int>> groups, int modifier)
        {
            var sb = new StringBuilder();
            bool brackets = groups.Count > 1;
            for (int g = 0; g < groups.Count; g++)
            {
                if (g > 0) sb.Append(TermSeparator);
                List<int> values = groups[g];
                bool wrap = brackets && values.Count > 1;
                if (wrap) sb.Append('(');
                for (int v = 0; v < values.Count; v++)
                {
                    if (v > 0) sb.Append(TermSeparator);
                    sb.Append(values[v]);
                }
                if (wrap) sb.Append(')');
            }
            AppendModifier(sb, modifier);
            return sb.ToString();
        }

        private static string Sums(List<List<int>> groups, int modifier)
        {
            var sb = new StringBuilder();
            for (int g = 0; g < groups.Count; g++)
            {
                if (g > 0) sb.Append(TermSeparator);
                int sum = 0;
                for (int v = 0; v < groups[g].Count; v++) sum += groups[g][v];
                sb.Append(sum);
            }
            AppendModifier(sb, modifier);
            return sb.ToString();
        }

        private static void AppendModifier(StringBuilder sb, int modifier)
        {
            if (modifier > 0) sb.Append(TermSeparator).Append(modifier);
            else if (modifier < 0) sb.Append(" - ").Append(-modifier);
        }
    }
}
