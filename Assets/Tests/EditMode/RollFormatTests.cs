using System.Collections.Generic;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class RollFormatTests
    {
        private static RollSetConfig Config(int modifier, params DieType[] dice)
        {
            var c = new RollSetConfig { Modifier = modifier };
            DiceLimits unlimited = DiceLimits.Unlimited;
            foreach (DieType t in dice) c.Pool.TryAdd(t, unlimited);
            return c;
        }

        /// <summary>A one-pass result made of exactly these dice (in this order).</summary>
        private static SetResult Result(int modifier, params DieRoll[] dice)
        {
            var pass = new PoolRoll();
            pass.Dice.AddRange(dice);
            return RollEngine.Evaluate(new RollSetConfig { Modifier = modifier }, new[] { pass });
        }

        private static DieRoll[] Dice(DieType type, params int[] values)
        {
            var dice = new DieRoll[values.Length];
            for (int i = 0; i < values.Length; i++) dice[i] = new DieRoll(type, values[i]);
            return dice;
        }

        // ------------------------------------------------------------------ formula

        [Test]
        public void FormulaGroupsDiceAndPutsTheBiggestDieFirst()
        {
            Assert.AreEqual("d8 + 2d6 + 3", RollFormat.Formula(Config(3, DieType.D6, DieType.D8, DieType.D6)));
            Assert.AreEqual("d% + d20 + 2d4", RollFormat.Formula(Config(0, DieType.D4, DieType.D100, DieType.D20, DieType.D4)));
        }

        [Test]
        public void FormulaHidesAZeroModifierAndShowsANegativeOne()
        {
            Assert.AreEqual("d20", RollFormat.Formula(Config(0, DieType.D20)));
            Assert.AreEqual("d20 + 5", RollFormat.Formula(Config(5, DieType.D20)));
            Assert.AreEqual("d20 - 2", RollFormat.Formula(Config(-2, DieType.D20)));
        }

        [Test]
        public void FormulaOfAnEmptyPoolIsEmptyEvenWithAModifier()
        {
            Assert.AreEqual("", RollFormat.Formula(Config(5)));
            Assert.AreEqual("", RollFormat.FormulaTagged(new RollSetConfig { Modifier = 5, Advantage = AdvantageMode.Advantage }));
        }

        [Test]
        public void TaggedFormulaAddsAdvantageOrDisadvantage()
        {
            RollSetConfig c = Config(5, DieType.D20);
            Assert.AreEqual("d20 + 5", RollFormat.FormulaTagged(c));
            c.Advantage = AdvantageMode.Advantage;
            Assert.AreEqual("d20 + 5 ADV", RollFormat.FormulaTagged(c));
            c.Advantage = AdvantageMode.Disadvantage;
            Assert.AreEqual("d20 + 5 DIS", RollFormat.FormulaTagged(c));
        }

        [Test]
        public void SummaryJoinsTheSetsThatWouldBeThrown()
        {
            var attack = Config(5, DieType.D20);
            var damage = Config(0, DieType.D6, DieType.D6, DieType.D6);
            Assert.AreEqual("d20 + 5 | 3d6", RollFormat.Summary(new[] { attack, damage }));

            damage.Enabled = false;
            Assert.AreEqual("d20 + 5", RollFormat.Summary(new[] { attack, damage }));

            attack.Pool.Clear();
            Assert.AreEqual("", RollFormat.Summary(new[] { attack, damage }));
        }

        [Test]
        public void ModifierAndAdvantageTags()
        {
            Assert.AreEqual("+4", RollFormat.Modifier(4));
            Assert.AreEqual("-4", RollFormat.Modifier(-4));
            Assert.AreEqual("0", RollFormat.Modifier(0));
            Assert.AreEqual("", RollFormat.AdvantageTag(AdvantageMode.Normal));
            Assert.AreEqual("DIS", RollFormat.AdvantageTag(AdvantageMode.Disadvantage));
        }

        // ------------------------------------------------------------------ breakdown

        [Test]
        public void BreakdownOfOneDieTypeListsEveryDieWithoutBrackets()
        {
            SetResult fireball = Result(0, Dice(DieType.D6, 3, 5, 1, 6, 2, 4, 4, 6));
            Assert.AreEqual("3 + 5 + 1 + 6 + 2 + 4 + 4 + 6", RollFormat.Breakdown(fireball));

            SetResult withModifier = Result(2, Dice(DieType.D6, 3, 5));
            Assert.AreEqual("3 + 5 + 2", RollFormat.Breakdown(withModifier));
        }

        [Test]
        public void BreakdownOfSeveralTypesBracketsTheGroupsInFormulaOrder()
        {
            // thrown in a different order than the formula "d8 + 2d6 + 3"
            var dice = new List<DieRoll>();
            dice.AddRange(Dice(DieType.D6, 3));
            dice.AddRange(Dice(DieType.D8, 5));
            dice.AddRange(Dice(DieType.D6, 5));
            SetResult r = Result(3, dice.ToArray());

            Assert.AreEqual(16, r.Total);
            Assert.AreEqual("5 + (3 + 5) + 3", RollFormat.Breakdown(r));
        }

        [Test]
        public void BreakdownShowsOneSumPerTypeWhenTheDiceDoNotFit()
        {
            var dice = new List<DieRoll>();
            dice.AddRange(Dice(DieType.D8, 5));
            dice.AddRange(Dice(DieType.D6, 3, 5));
            SetResult r = Result(3, dice.ToArray());

            Assert.AreEqual("5 + (3 + 5) + 3", RollFormat.Breakdown(r, 15)); // exactly fits
            Assert.AreEqual("5 + 8 + 3", RollFormat.Breakdown(r, 14));

            SetResult fireball = Result(0, Dice(DieType.D6, 3, 5, 1, 6, 2, 4, 4, 6));
            Assert.AreEqual("31", RollFormat.Breakdown(fireball, 10));
        }

        [Test]
        public void BreakdownShowsANegativeModifierAndPercentDice()
        {
            var dice = new List<DieRoll>();
            dice.AddRange(Dice(DieType.D100, 57));
            dice.AddRange(Dice(DieType.D4, 2));
            Assert.AreEqual("57 + 2 - 3", RollFormat.Breakdown(Result(-3, dice.ToArray())));
        }

        [Test]
        public void BreakdownShowsKeptDiceModifierAndDroppedPass()
        {
            var cfg = new RollSetConfig { Advantage = AdvantageMode.Advantage, Modifier = 2 };
            cfg.Pool.TryAdd(DieType.D20, new DiceLimits());
            var low = new PoolRoll();
            low.Dice.Add(new DieRoll(DieType.D20, 4));
            var high = new PoolRoll();
            high.Dice.Add(new DieRoll(DieType.D20, 17));

            SetResult r = RollEngine.Evaluate(cfg, new[] { low, high });

            Assert.AreEqual(19, r.Total);
            Assert.AreEqual("17 + 2   (4 dropped)", RollFormat.Breakdown(r));
            Assert.AreEqual("", RollFormat.Breakdown(null));
        }
    }
}
