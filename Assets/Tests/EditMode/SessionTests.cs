using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class SessionTests
    {
        private static RollSession New() { return new RollSession(new SystemRng(11), new DiceLimits()); }

        [Test]
        public void AddDieOnNormalSetThrowsOnlyThatDie()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D6, out r);
            TrayChange c = s.AddDie(0, DieType.D6, out r);
            Assert.AreEqual(1, c.Add.Count);
            Assert.AreEqual(0, c.ClearSets.Count);
            Assert.AreEqual(2, s.GetResult(0).Chosen.Dice.Count);
        }

        [Test]
        public void AdvantageSetRethrowsWholeSetOnAdd()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D6, out r);
            s.CycleAdvantage(0);
            TrayChange c = s.AddDie(0, DieType.D6, out r);
            Assert.Contains(0, c.ClearSets);
            Assert.AreEqual(2, c.Add.Count);
            Assert.AreEqual(2, s.GetResult(0).Passes.Length);
        }

        [Test]
        public void SlidingCountDownClearsTypeAndRethrowsNewAmount()
        {
            var s = New();
            AddResult r;
            for (int i = 0; i < 4; i++) s.AddDie(1, DieType.D8, out r);
            s.AddDie(1, DieType.D6, out r);
            TrayChange c = s.SetDieCount(1, DieType.D8, 2, out r);
            Assert.AreEqual(1, c.RemoveTypes.Count);
            Assert.AreEqual(2, c.Add.Count);
            Assert.AreEqual(3, s.GetResult(1).Chosen.Dice.Count); // 2 d8 + the untouched d6
        }

        [Test]
        public void SlidingCountUpThrowsOnlyExtras()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D20, out r);
            TrayChange c = s.SetDieCount(0, DieType.D20, 4, out r);
            Assert.AreEqual(3, c.Add.Count);
            Assert.AreEqual(0, c.RemoveTypes.Count);
        }

        [Test]
        public void DisabledSetKeepsPoolButHasNoRoll()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D6, out r);
            TrayChange off = s.SetEnabled(0, false);
            Assert.Contains(0, off.ClearSets);
            Assert.IsNull(s.GetResult(0));
            Assert.AreEqual(1, s.Sets[0].Pool.Total);
            TrayChange c = s.AddDie(0, DieType.D6, out r);
            Assert.IsTrue(c.IsEmpty);
            s.SetEnabled(0, true);
            Assert.AreEqual(2, s.GetResult(0).Chosen.Dice.Count);
        }

        [Test]
        public void ModifierChangesTotalWithoutThrowing()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D6, out r);
            int before = s.GetResult(0).Total;
            s.SetModifier(0, 5);
            Assert.AreEqual(before + 5, s.GetResult(0).Total);
        }

        [Test]
        public void CapFlashesAndAddsNothing()
        {
            var s = New();
            AddResult r = AddResult.Added;
            for (int i = 0; i < 11; i++) s.AddDie(0, DieType.D6, out r);
            Assert.AreEqual(AddResult.BlockedType, r);
            Assert.AreEqual(10, s.Sets[0].Pool.Get(DieType.D6));
        }

        [Test]
        public void RangeIncludesModifier()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D20, out r);
            s.AddDie(0, DieType.D6, out r);
            s.SetModifier(0, 3);
            int min, max;
            s.GetRange(0, out min, out max);
            Assert.AreEqual(5, min);
            Assert.AreEqual(29, max);
        }
    }
}
