using System.Collections.Generic;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class CoreTests
    {
        [TestCase(1, 0, 1)]
        [TestCase(7, 0, 7)]
        [TestCase(10, 10, 0)]
        [TestCase(40, 40, 0)]
        [TestCase(99, 90, 9)]
        [TestCase(100, 0, 0)]
        public void PercentSplit(int value, int tens, int units)
        {
            DicePlanner.SplitPercent(value, out int t, out int u);
            Assert.AreEqual(tens, t);
            Assert.AreEqual(units, u);
        }

        [Test]
        public void CapsBlockTotalAndPerType()
        {
            var lim = new DiceLimits();
            var pool = new DicePool();
            int added = 0;
            for (int i = 0; i < 40; i++)
                if (pool.TryAdd((DieType)(i % 6), lim) == AddResult.Added) added++;
            Assert.AreEqual(20, added);

            var p2 = new DicePool();
            for (int i = 0; i < 10; i++) Assert.AreEqual(AddResult.Added, p2.TryAdd(DieType.D6, lim));
            Assert.AreEqual(AddResult.BlockedType, p2.TryAdd(DieType.D6, lim));
        }

        [Test]
        public void AdvantageKeepsHigherDisadvantageKeepsLower()
        {
            var rng = new SystemRng(7);
            var cfg = new RollSetConfig { Advantage = AdvantageMode.Advantage };
            cfg.Pool.TryAdd(DieType.D20, new DiceLimits());
            for (int i = 0; i < 500; i++)
            {
                var r = RollEngine.Roll(cfg, rng);
                Assert.GreaterOrEqual(r.Chosen.Subtotal, r.Passes[1 - r.ChosenPass].Subtotal);
            }
            cfg.Advantage = AdvantageMode.Disadvantage;
            for (int i = 0; i < 500; i++)
            {
                var r = RollEngine.Roll(cfg, rng);
                Assert.LessOrEqual(r.Chosen.Subtotal, r.Passes[1 - r.ChosenPass].Subtotal);
            }
        }

        [Test]
        public void ModifierIsClampedBySessionAndAddedOnce()
        {
            var limits = new DiceLimits();
            var session = new RollSession(new SystemRng(3), limits);
            session.SetModifier(0, limits.ModifierMax + 89);
            Assert.AreEqual(limits.ModifierMax, session.Sets[0].Modifier);

            var cfg = new RollSetConfig();
            cfg.Pool.TryAdd(DieType.D6, limits);
            cfg.Modifier = limits.ModifierMax;
            var r = RollEngine.Roll(cfg, new SystemRng(3));
            Assert.AreEqual(r.Chosen.Subtotal + limits.ModifierMax, r.Total);
        }

        [Test]
        public void DisabledSetIsNotRolledAndPoolIsKept()
        {
            var a = new RollSetConfig { Enabled = false };
            a.Pool.TryAdd(DieType.D8, new DiceLimits());
            var res = RollEngine.RollThrow(new List<RollSetConfig> { a }, new SystemRng(1));
            Assert.IsNull(res.Sets[0]);
            Assert.AreEqual(1, a.Pool.Total);
        }

        [Test]
        public void PercentDieBecomesTwoPhysicalDice()
        {
            var cfg = new RollSetConfig();
            cfg.Pool.TryAdd(DieType.D100, new DiceLimits());
            var plan = new List<PhysicalDie>();
            DicePlanner.Plan(RollEngine.Roll(cfg, new SystemRng(5)), 0, plan);
            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual(DieKind.D10Tens, plan[0].Kind);
            Assert.AreEqual(DieKind.D10Units, plan[1].Kind);
        }
    }
}
