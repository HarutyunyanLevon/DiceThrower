using System.Collections.Generic;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class PresetUsageTests
    {
        private static Preset P(string id) { return new Preset { Id = id, Name = id, Sets = new RollSetConfig[RollSession.SetCount] }; }

        private static HistoryEntry H(string presetId) { return new HistoryEntry { PresetId = presetId }; }

        private static string Ids(List<Preset> list)
        {
            var ids = new List<string>();
            foreach (Preset p in list) ids.Add(p.Id);
            return string.Join(",", ids);
        }

        [Test]
        public void MostUsedComesFirstAndTiesGoToTheMoreRecentUse()
        {
            var presets = new[] { P("a"), P("b"), P("c") };
            // newest first: c, b, a, a  -> a used twice; b and c once, c more recently
            var history = new[] { H("c"), H("b"), H("a"), H("a") };

            Assert.AreEqual("a,c,b", Ids(PresetUsage.Rank(presets, history, 30, 10)));
        }

        [Test]
        public void OnlyTheLastWindowThrowsCount()
        {
            var presets = new[] { P("a"), P("b") };
            var history = new[] { H("b"), H("a"), H("a"), H("a") };

            Assert.AreEqual("a,b", Ids(PresetUsage.Rank(presets, history, 4, 10)));
            Assert.AreEqual("b,a", Ids(PresetUsage.Rank(presets, history, 1, 10))); // a is outside the window and ranks as unused
        }

        [Test]
        public void UnusedPresetsFollowNewestSavedFirst()
        {
            var presets = new[] { P("old"), P("mid"), P("new"), P("used") };
            var history = new[] { H("used") };

            Assert.AreEqual("used,new,mid,old", Ids(PresetUsage.Rank(presets, history, 30, 10)));
        }

        [Test]
        public void ThrowsWithoutAPresetAndDeletedPresetsAreIgnored()
        {
            var presets = new[] { P("a") };
            var history = new[] { H(null), H("gone"), H("a") };

            Assert.AreEqual("a", Ids(PresetUsage.Rank(presets, history, 30, 10)));
        }

        [Test]
        public void ResultIsCappedAndAZeroWindowRanksNothingAsUsed()
        {
            var presets = new[] { P("a"), P("b"), P("c") };
            var history = new[] { H("a") };

            Assert.AreEqual("a,c", Ids(PresetUsage.Rank(presets, history, 30, 2)));
            Assert.AreEqual("c,b,a", Ids(PresetUsage.Rank(presets, history, 0, 10)));
            Assert.AreEqual(0, PresetUsage.Rank(presets, history, 30, 0).Count);
        }
    }
}
