using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class SessionDataTests
    {
        private static RollSession New() { return new RollSession(new SystemRng(9), new DiceLimits()); }

        [Test]
        public void SnapshotIsIndependentOfTheSession()
        {
            RollSession s = New();
            AddResult r;
            s.AddDie(0, DieType.D6, out r);
            RollSetConfig[] snap = s.Snapshot();
            s.AddDie(0, DieType.D6, out r);
            s.SetModifier(0, 4);

            Assert.AreEqual(1, snap[0].Pool.Get(DieType.D6));
            Assert.AreEqual(0, snap[0].Modifier);
        }

        [Test]
        public void LoadReplacesBothSetsAndThrowsThem()
        {
            RollSession s = New();
            AddResult r;
            s.AddDie(0, DieType.D4, out r);
            s.AddDie(1, DieType.D12, out r);

            var saved = new[] { new RollSetConfig(), new RollSetConfig { Advantage = AdvantageMode.Disadvantage, Modifier = 2 } };
            saved[0].Pool.TryAdd(DieType.D20, new DiceLimits());
            saved[1].Pool.TryAdd(DieType.D8, new DiceLimits());
            saved[1].Pool.TryAdd(DieType.D8, new DiceLimits());

            TrayChange c = s.Load(saved);

            Assert.IsTrue(c.ClearAll);
            Assert.AreEqual(0, s.Sets[0].Pool.Get(DieType.D4));
            Assert.AreEqual(1, s.Sets[0].Pool.Get(DieType.D20));
            Assert.AreEqual(0, s.Sets[1].Pool.Get(DieType.D12));
            Assert.AreEqual(2, s.Sets[1].Pool.Get(DieType.D8));
            Assert.AreEqual(AdvantageMode.Disadvantage, s.Sets[1].Advantage);
            Assert.AreEqual(2, s.Sets[1].Modifier);
            Assert.AreEqual(1 + 2, c.Add.Count); // d20 and two d8: only the kept pass is shown in the tray
            Assert.IsNotNull(s.GetResult(0));
            Assert.AreEqual(2, s.GetResult(1).Passes.Length);
        }

        [Test]
        public void LoadSqueezesASetupIntoTheCurrentLimits()
        {
            var limits = new DiceLimits { MaxTotal = 3, MaxPerType = 2, ModifierMin = -1, ModifierMax = 1 };
            var s = new RollSession(new SystemRng(1), limits);
            var big = new RollSetConfig { Modifier = 50 };
            var unlimited = DiceLimits.Unlimited;
            big.Pool.SetCount(DieType.D6, 9, unlimited);
            big.Pool.SetCount(DieType.D8, 9, unlimited);

            s.Load(new[] { big, new RollSetConfig() });

            Assert.AreEqual(2, s.Sets[0].Pool.Get(DieType.D6));
            Assert.AreEqual(1, s.Sets[0].Pool.Get(DieType.D8)); // total cap of 3
            Assert.AreEqual(1, s.Sets[0].Modifier);
        }

        [Test]
        public void LoadKeepsADisabledSetOffAndUnrolled()
        {
            RollSession s = New();
            var saved = new[] { new RollSetConfig(), new RollSetConfig { Enabled = false } };
            saved[0].Pool.TryAdd(DieType.D6, new DiceLimits());
            saved[1].Pool.TryAdd(DieType.D6, new DiceLimits());

            s.Load(saved);

            Assert.IsNotNull(s.GetResult(0));
            Assert.IsNull(s.GetResult(1));
            Assert.AreEqual(1, s.Sets[1].Pool.Total);
        }

        [Test]
        public void GetThrowIsNullWhenNothingIsRolledAndIsACopyOtherwise()
        {
            RollSession s = New();
            Assert.IsNull(s.GetThrow());

            AddResult r;
            s.AddDie(0, DieType.D6, out r);
            ThrowResult t = s.GetThrow();
            s.AddDie(0, DieType.D6, out r); // adds a rolled die to the live pass

            Assert.AreEqual(1, t.Sets[0].Chosen.Dice.Count);
            Assert.AreEqual(2, s.GetThrow().Sets[0].Chosen.Dice.Count);
            Assert.IsNull(t.Sets[1]);
        }

        [Test]
        public void ConfigureSetsTheSetupWithoutThrowingAndForgetsOldRolls()
        {
            RollSession s = New();
            AddResult r;
            s.AddDie(0, DieType.D6, out r);
            var saved = new[] { new RollSetConfig { Modifier = 3 }, new RollSetConfig() };
            saved[0].Pool.TryAdd(DieType.D20, new DiceLimits());

            s.Configure(saved);

            Assert.AreEqual(0, s.Sets[0].Pool.Get(DieType.D6));
            Assert.AreEqual(1, s.Sets[0].Pool.Get(DieType.D20));
            Assert.AreEqual(3, s.Sets[0].Modifier);
            Assert.IsNull(s.GetResult(0));
            Assert.IsNull(s.GetThrow());
        }

        [Test]
        public void ThrowResultFindsNaturalTwentiesAndOnesInAnySet()
        {
            var nat20 = new RollSetConfig();
            nat20.Pool.TryAdd(DieType.D20, new DiceLimits());
            var passWith20 = new PoolRoll();
            passWith20.Dice.Add(new DieRoll(DieType.D20, 20));
            var passWith1 = new PoolRoll();
            passWith1.Dice.Add(new DieRoll(DieType.D20, 1));
            var plain = new PoolRoll();
            plain.Dice.Add(new DieRoll(DieType.D20, 9));

            var both = new ThrowResult { Sets = new[] { RollEngine.Evaluate(nat20, new[] { passWith20 }), RollEngine.Evaluate(nat20, new[] { passWith1 }) } };
            var none = new ThrowResult { Sets = new[] { RollEngine.Evaluate(nat20, new[] { plain }), null } };

            Assert.IsTrue(both.HasNat20);
            Assert.IsTrue(both.HasNat1);
            Assert.IsFalse(none.HasNat20);
            Assert.IsFalse(none.HasNat1);
        }

        [Test]
        public void HasDiceLooksAtBothSets()
        {
            RollSession s = New();
            Assert.IsFalse(s.HasDice);
            AddResult r;
            s.AddDie(1, DieType.D6, out r);
            Assert.IsTrue(s.HasDice);
        }
    }
}
