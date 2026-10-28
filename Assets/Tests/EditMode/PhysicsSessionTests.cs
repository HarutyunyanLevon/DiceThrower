using System.Collections.Generic;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    /// <summary>Physics mode: dice are thrown without a decided value and the result is what they show once they lie still.</summary>
    public class PhysicsSessionTests
    {
        private static RollSession New()
        {
            return new RollSession(new SystemRng(5), new DiceLimits()) { UsePhysics = true };
        }

        private static List<DieReading> Read(TrayChange change, int value)
        {
            var list = new List<DieReading>();
            for (int i = 0; i < change.Add.Count; i++)
            {
                PhysicalDie d = change.Add[i];
                list.Add(new DieReading(d.SetIndex, d.Pass, d.Kind, value));
            }
            return list;
        }

        [Test]
        public void ThrownDiceHaveNoTargetAndNoResultUntilSettled()
        {
            var s = New();
            AddResult r;
            TrayChange c = s.AddDie(0, DieType.D8, out r);

            Assert.AreEqual(1, c.Add.Count);
            Assert.IsFalse(c.Add[0].HasTarget);
            Assert.IsFalse(s.HasRoll(0));
            Assert.IsTrue(s.HasThrown(0));
            Assert.IsTrue(s.HasPendingResult);
            Assert.IsNull(s.GetThrow());
        }

        [Test]
        public void SettleTurnsReadingsIntoTheResult()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D6, out r);
            TrayChange c = s.AddDie(0, DieType.D6, out r);
            Assert.AreEqual(1, c.Add.Count); // only the new die is thrown

            var readings = new List<DieReading>
            {
                new DieReading(0, 0, DieKind.D6, 4),
                new DieReading(0, 0, DieKind.D6, 5),
            };
            s.Settle(readings);

            Assert.IsFalse(s.HasPendingResult);
            Assert.AreEqual(9, s.GetResult(0).Total);
        }

        [Test]
        public void ModifierIsAddedToWhatTheDiceShow()
        {
            var s = New();
            AddResult r;
            TrayChange c = s.AddDie(0, DieType.D20, out r);
            s.SetModifier(0, 3);
            s.Settle(Read(c, 17));
            Assert.AreEqual(20, s.GetResult(0).Total);
        }

        [Test]
        public void PercentDieIsATensAndAUnitsDie()
        {
            var s = New();
            AddResult r;
            TrayChange c = s.AddDie(0, DieType.D100, out r);
            Assert.AreEqual(2, c.Add.Count);
            Assert.AreEqual(DieKind.D10Tens, c.Add[0].Kind);
            Assert.AreEqual(DieKind.D10Units, c.Add[1].Kind);

            s.Settle(new List<DieReading> { new DieReading(0, 0, DieKind.D10Tens, 40), new DieReading(0, 0, DieKind.D10Units, 7) });
            Assert.AreEqual(47, s.GetResult(0).Total);
        }

        [Test]
        public void TwoZerosOnAPercentDieMeanOneHundred()
        {
            var readings = new List<DieReading> { new DieReading(0, 0, DieKind.D10Tens, 0), new DieReading(0, 0, DieKind.D10Units, 0) };
            PoolRoll[] pools = DicePlanner.ToPools(readings, 0, 1);
            Assert.AreEqual(100, pools[0].Dice[0].Value);
        }

        [Test]
        public void AdvantageThrowsThePoolTwiceAndKeepsTheBetterCopy()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D20, out r);
            TrayChange c = s.CycleAdvantage(0);
            Assert.AreEqual(2, c.Add.Count);
            Assert.AreEqual(0, c.Add[0].Pass);
            Assert.AreEqual(1, c.Add[1].Pass);

            s.Settle(new List<DieReading> { new DieReading(0, 0, DieKind.D20, 7), new DieReading(0, 1, DieKind.D20, 15) });

            Assert.AreEqual(15, s.GetResult(0).Total);
            Assert.AreEqual(0, s.TakeDiscard(0)); // the first copy lost
            Assert.AreEqual(-1, s.TakeDiscard(0)); // reported once
        }

        [Test]
        public void DisadvantageKeepsTheLowerCopy()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D20, out r);
            s.CycleAdvantage(0);
            s.CycleAdvantage(0);
            Assert.AreEqual(AdvantageMode.Disadvantage, s.Sets[0].Advantage);
            s.Settle(new List<DieReading> { new DieReading(0, 0, DieKind.D20, 7), new DieReading(0, 1, DieKind.D20, 15) });
            Assert.AreEqual(7, s.GetResult(0).Total);
            Assert.AreEqual(1, s.TakeDiscard(0));
        }

        [Test]
        public void SettlingAgainKeepsAnAdvantageResultWhoseLosingDiceAreGone()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D20, out r);
            s.CycleAdvantage(0);
            s.Settle(new List<DieReading> { new DieReading(0, 0, DieKind.D20, 7), new DieReading(0, 1, DieKind.D20, 15) });
            s.TakeDiscard(0);

            s.Settle(new List<DieReading> { new DieReading(0, 1, DieKind.D20, 15) }); // the tray only has the kept die now
            Assert.AreEqual(15, s.GetResult(0).Total);
        }

        [Test]
        public void RemovingADieTypeKeepsTheOthersResult()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D6, out r);
            s.AddDie(0, DieType.D8, out r);
            s.Settle(new List<DieReading> { new DieReading(0, 0, DieKind.D6, 2), new DieReading(0, 0, DieKind.D8, 6) });

            TrayChange c = s.SetDieCount(0, DieType.D6, 0, out r);
            Assert.AreEqual(1, c.RemoveTypes.Count);
            s.Settle(new List<DieReading> { new DieReading(0, 0, DieKind.D8, 6) });
            Assert.AreEqual(6, s.GetResult(0).Total);
        }

        [Test]
        public void WithPhysicsOffTheRngDecidesAndDiceCarryTheirTarget()
        {
            var s = new RollSession(new SystemRng(5), new DiceLimits()) { UsePhysics = false };
            AddResult r;
            TrayChange c = s.AddDie(0, DieType.D20, out r);
            Assert.IsTrue(c.Add[0].HasTarget);
            Assert.IsTrue(s.HasRoll(0));
            Assert.AreEqual(c.Add[0].TargetValue, s.GetResult(0).Total);
        }

        [Test]
        public void ThrowAllThrowsEverythingAgain()
        {
            var s = New();
            AddResult r;
            s.AddDie(0, DieType.D6, out r);
            s.AddDie(1, DieType.D8, out r);
            TrayChange c = s.ThrowAll();
            Assert.IsTrue(c.ClearAll);
            Assert.AreEqual(2, c.Add.Count);
            Assert.IsFalse(s.HasRoll(0));
            Assert.IsFalse(s.HasRoll(1));
        }
    }
}
