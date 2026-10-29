using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class DiceSetChoiceTests
    {
        private const int Attack = RollSession.AttackSet, Damage = RollSession.DamageSet;

        [Test]
        public void EachRollSetStartsWithItsOwnSet()
        {
            var choice = new DiceSetChoice();

            Assert.AreEqual(0, choice.Get(Attack));
            Assert.AreEqual(1, choice.Get(Damage));
        }

        [Test]
        public void AssigningAFreeSetChangesOnlyThatRollSet()
        {
            var choice = new DiceSetChoice();

            Assert.IsTrue(choice.Assign(Attack, 7));

            Assert.AreEqual(7, choice.Get(Attack));
            Assert.AreEqual(1, choice.Get(Damage));
        }

        [Test]
        public void AssigningTheSetTheOtherRollSetUsesSwapsThem()
        {
            var choice = new DiceSetChoice();
            choice.Assign(Attack, 4);
            choice.Assign(Damage, 9);

            choice.Assign(Attack, 9);

            Assert.AreEqual(9, choice.Get(Attack));
            Assert.AreEqual(4, choice.Get(Damage));
        }

        [Test]
        public void AssigningTheCurrentSetChangesNothingAndRaisesNoEvent()
        {
            var choice = new DiceSetChoice();
            int raised = 0;
            choice.Changed += () => raised++;

            Assert.IsFalse(choice.Assign(Attack, 0));
            Assert.IsFalse(choice.Assign(Attack, -1));

            Assert.AreEqual(0, raised);
            Assert.AreEqual(0, choice.Get(Attack));
        }

        [Test]
        public void AChangeRaisesTheEventOnce()
        {
            var choice = new DiceSetChoice();
            int raised = 0;
            choice.Changed += () => raised++;

            choice.Assign(Damage, 0); // a swap is one change

            Assert.AreEqual(1, raised);
            Assert.AreEqual(1, choice.Get(Attack));
            Assert.AreEqual(0, choice.Get(Damage));
        }

        [Test]
        public void UsedByNamesTheRollSetOrMinusOne()
        {
            var choice = new DiceSetChoice();
            choice.Assign(Damage, 5);

            Assert.AreEqual(Attack, choice.UsedBy(0));
            Assert.AreEqual(Damage, choice.UsedBy(5));
            Assert.AreEqual(-1, choice.UsedBy(3));
        }

        [Test]
        public void SetAllCorrectsNegativeAndRepeatedValuesWithoutAnEvent()
        {
            var choice = new DiceSetChoice();
            int raised = 0;
            choice.Changed += () => raised++;

            choice.SetAll(new[] { -4, -4 });

            Assert.AreEqual(0, choice.Get(Attack));
            Assert.AreEqual(1, choice.Get(Damage));
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void SetAllWithMissingValuesKeepsTheStartValues()
        {
            var choice = new DiceSetChoice();
            choice.Assign(Attack, 8);

            choice.SetAll(new int[0]);

            Assert.AreEqual(0, choice.Get(Attack));
            Assert.AreEqual(1, choice.Get(Damage));
        }

        [Test]
        public void FitPullsChoicesIntoTheAvailableSetsAndKeepsThemDistinct()
        {
            var choice = new DiceSetChoice();
            choice.Assign(Attack, 14);
            choice.Assign(Damage, 12);

            choice.Fit(10); // both land on 9, the second moves to the lowest free set

            Assert.AreEqual(9, choice.Get(Attack));
            Assert.AreEqual(0, choice.Get(Damage));
        }

        [Test]
        public void FitWithNoSetsOrOneSetDoesNotThrow()
        {
            var choice = new DiceSetChoice();

            choice.Fit(0);
            Assert.AreEqual(1, choice.Get(Damage)); // untouched

            choice.Fit(1); // cannot be distinct, both clamp to 0
            Assert.AreEqual(0, choice.Get(Attack));
            Assert.AreEqual(0, choice.Get(Damage));
        }
    }
}
