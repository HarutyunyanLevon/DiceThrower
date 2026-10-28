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
    }
}
