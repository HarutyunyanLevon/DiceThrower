using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class RollFormatTests
    {
        [Test]
        public void NotationListsDiceModifierAndAdvantage()
        {
            var c = new RollSetConfig { Advantage = AdvantageMode.Advantage, Modifier = -3 };
            var limits = new DiceLimits();
            c.Pool.TryAdd(DieType.D6, limits);
            c.Pool.TryAdd(DieType.D6, limits);
            c.Pool.TryAdd(DieType.D20, limits);
            c.Pool.TryAdd(DieType.D100, limits);

            Assert.AreEqual("2d6 + 1d20 + 1d% - 3 ADV", RollFormat.Notation(c));
            Assert.AreEqual("", RollFormat.Notation(new RollSetConfig { Modifier = 5 }));
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
