using System;
using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>RNG-first rolling. The physics tray later just displays what this decided.</summary>
    public static class RollEngine
    {
        public static SetResult Roll(RollSetConfig config, IRng rng)
        {
            RollSetConfig snapshot = config.Clone();
            int passCount = snapshot.Advantage == AdvantageMode.Normal ? 1 : 2;

            var passes = new PoolRoll[passCount];
            for (int p = 0; p < passCount; p++) passes[p] = RollPool(snapshot.Pool, rng);
            return Evaluate(snapshot, passes);
        }

        /// <summary>
        /// Works out which pass is kept, the total and the nat flags for already-rolled passes.
        /// Ties keep the first pass.
        /// </summary>
        public static SetResult Evaluate(RollSetConfig snapshot, PoolRoll[] passes)
        {
            int chosen = 0;
            if (passes.Length == 2)
            {
                int a = passes[0].Subtotal;
                int b = passes[1].Subtotal;
                if (snapshot.Advantage == AdvantageMode.Advantage) chosen = b > a ? 1 : 0;
                else if (snapshot.Advantage == AdvantageMode.Disadvantage) chosen = b < a ? 1 : 0;
            }

            var result = new SetResult
            {
                Config = snapshot,
                Passes = passes,
                ChosenPass = chosen,
                Modifier = snapshot.Modifier,
            };
            result.Total = result.Chosen.Subtotal + result.Modifier;

            List<DieRoll> kept = result.Chosen.Dice;
            for (int i = 0; i < kept.Count; i++)
            {
                if (kept[i].Type != DieType.D20) continue;
                if (kept[i].Value == 20) result.HasNat20 = true;
                if (kept[i].Value == 1) result.HasNat1 = true;
            }
            return result;
        }

        public static DieRoll RollDie(DieType type, IRng rng)
        {
            return new DieRoll(type, rng.Range(1, DieInfo.Sides(type)));
        }

        /// <summary>Rolls every enabled set; disabled sets come back as null.</summary>
        public static ThrowResult RollThrow(IList<RollSetConfig> sets, IRng rng)
        {
            var result = new ThrowResult { Sets = new SetResult[sets.Count] };
            for (int i = 0; i < sets.Count; i++)
            {
                if (sets[i].Enabled && !sets[i].Pool.IsEmpty) result.Sets[i] = Roll(sets[i], rng);
            }
            return result;
        }

        private static PoolRoll RollPool(DicePool pool, IRng rng)
        {
            var roll = new PoolRoll();
            for (int i = 0; i < DieInfo.TypeCount; i++)
            {
                DieType type = DieInfo.AllTypes[i];
                int count = pool.Get(type);
                int sides = DieInfo.Sides(type);
                for (int n = 0; n < count; n++) roll.Dice.Add(new DieRoll(type, rng.Range(1, sides)));
            }
            return roll;
        }
    }
}
