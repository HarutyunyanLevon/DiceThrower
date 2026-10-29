using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class SettingsTests
    {
        private static AppSettings RoundTrip(AppSettings settings)
        {
            var data = new SaveData { Settings = settings };
            return SaveCodec.Deserialize(SaveCodec.Serialize(data)).Settings;
        }

        [Test]
        public void ThemeAndUsageWindowSurviveASaveAndLoad()
        {
            AppSettings loaded = RoundTrip(new AppSettings { Theme = 1, UsageWindow = 45 });

            Assert.AreEqual(1, loaded.Theme);
            Assert.AreEqual(45, loaded.UsageWindow);
        }

        [Test]
        public void AnOldSaveWithoutThemeOrWindowUsesTheDefaults()
        {
            AppSettings loaded = SaveCodec.Deserialize("{\"version\":1,\"settings\":{\"sound\":false}}").Settings;

            Assert.AreEqual(0, loaded.Theme);
            Assert.AreEqual(0, loaded.UsageWindow);
            Assert.AreEqual(30, loaded.UsageWindowOr(30)); // not chosen: the config value applies
        }

        [Test]
        public void NegativeValuesInTheFileAreClampedToZero()
        {
            AppSettings loaded = SaveCodec.Deserialize("{\"settings\":{\"theme\":-3,\"usageWindow\":-10}}").Settings;

            Assert.AreEqual(0, loaded.Theme);
            Assert.AreEqual(0, loaded.UsageWindow);
        }

        [Test]
        public void AChosenWindowOverridesTheConfigValue()
        {
            Assert.AreEqual(10, new AppSettings { UsageWindow = 10 }.UsageWindowOr(30));
        }

        [Test]
        public void PhysicsSwitchAndSettleTimeSurviveASaveAndLoad()
        {
            AppSettings loaded = RoundTrip(new AppSettings { PhysicsDice = false, SettleSeconds = 8 });

            Assert.IsFalse(loaded.PhysicsDice);
            Assert.AreEqual(8, loaded.SettleSeconds);
        }

        [Test]
        public void AnOldSaveHasPhysicsOnAndUsesTheConfiguredSettleTime()
        {
            AppSettings loaded = SaveCodec.Deserialize("{\"version\":1,\"settings\":{\"sound\":false}}").Settings;

            Assert.IsTrue(loaded.PhysicsDice);
            Assert.AreEqual(5, loaded.SettleSecondsOr(5));
            Assert.AreEqual(9, new AppSettings { SettleSeconds = 9 }.SettleSecondsOr(5));
        }

        [Test]
        public void DiceSetChoicesSurviveASaveAndLoad()
        {
            var settings = new AppSettings();
            settings.DiceSets.Assign(RollSession.AttackSet, 9);
            settings.DiceSets.Assign(RollSession.DamageSet, 3);

            AppSettings loaded = RoundTrip(settings);

            Assert.AreEqual(9, loaded.DiceSets.Get(RollSession.AttackSet));
            Assert.AreEqual(3, loaded.DiceSets.Get(RollSession.DamageSet));
        }

        [Test]
        public void ASaveWithoutDiceSetsStartsWithTheFirstTwoSets()
        {
            AppSettings loaded = SaveCodec.Deserialize("{\"version\":1,\"settings\":{\"sound\":false}}").Settings;

            Assert.AreEqual(0, loaded.DiceSets.Get(RollSession.AttackSet));
            Assert.AreEqual(1, loaded.DiceSets.Get(RollSession.DamageSet));
        }

        [Test]
        public void TheOldSkinSetKeyIsIgnoredAndNotWrittenAgain()
        {
            AppSettings loaded = SaveCodec.Deserialize("{\"settings\":{\"skinSet\":4}}").Settings;

            Assert.AreEqual(0, loaded.DiceSets.Get(RollSession.AttackSet));
            Assert.AreEqual(1, loaded.DiceSets.Get(RollSession.DamageSet));
            StringAssert.DoesNotContain("skinSet", SaveCodec.Serialize(new SaveData { Settings = loaded }));
        }

        [Test]
        public void ABrokenFileWithTheSameSetForBothRollSetsIsRepaired()
        {
            AppSettings loaded = SaveCodec.Deserialize("{\"settings\":{\"attackSet\":6,\"damageSet\":6}}").Settings;

            Assert.AreEqual(6, loaded.DiceSets.Get(RollSession.AttackSet));
            Assert.AreEqual(0, loaded.DiceSets.Get(RollSession.DamageSet));
        }
    }
}
