using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class AppDataTests
    {
        private string _dir;
        private string _path;
        private DiceLimits _limits;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dicethrower_tests_" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, "save.json");
            _limits = new DiceLimits { HistoryCap = 3, PresetNameMax = 8 };
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            File.Delete(_dir + ".tmp"); // left behind by the failing-save test
        }

        private AppData Open() { return new AppData(new SaveFile(_path), _limits); }

        private static RollSetConfig[] Sets(params DieType[] firstSetDice)
        {
            var sets = new[] { new RollSetConfig(), new RollSetConfig() };
            foreach (DieType t in firstSetDice) sets[0].Pool.TryAdd(t, new DiceLimits());
            return sets;
        }

        private static ThrowResult Throw(int seed)
        {
            var session = new RollSession(new SystemRng(seed), new DiceLimits());
            AddResult r;
            session.AddDie(0, DieType.D20, out r);
            return session.GetThrow();
        }

        [Test]
        public void PresetsAreAddedRenamedDeletedAndSurviveARestart()
        {
            AppData data = Open();
            Preset a = data.AddPreset("  Sneak  ", Sets(DieType.D6, DieType.D6));
            Preset b = data.AddPreset("Smite", Sets(DieType.D8));

            Assert.AreEqual("Sneak", a.Name);          // trimmed
            Assert.AreNotEqual(a.Id, b.Id);
            Assert.IsTrue(data.UpdatePreset(a.Id, "Backstab", Sets(DieType.D6, DieType.D6)));
            Assert.IsTrue(data.DeletePreset(b.Id));

            AppData reopened = Open();
            Assert.AreEqual(1, reopened.Presets.Count);
            Assert.AreEqual("Backstab", reopened.Presets[0].Name);
            Assert.AreEqual(2, reopened.Presets[0].Sets[0].Pool.Get(DieType.D6));
        }

        [Test]
        public void UpdatePresetReplacesTheSetsAndKeepsTheId()
        {
            AppData data = Open();
            Preset p = data.AddPreset("Sneak", Sets(DieType.D6));

            Assert.IsTrue(data.UpdatePreset(p.Id, "Sneak+", Sets(DieType.D8, DieType.D8)));

            Preset reopened = Open().Presets[0];
            Assert.AreEqual(p.Id, reopened.Id);
            Assert.AreEqual("Sneak+", reopened.Name);
            Assert.AreEqual(0, reopened.Sets[0].Pool.Get(DieType.D6));
            Assert.AreEqual(2, reopened.Sets[0].Pool.Get(DieType.D8));
        }

        [Test]
        public void HistoryRemembersWhichPresetAThrowCameFrom()
        {
            AppData data = Open();
            data.AddHistory(Throw(1), 1, "abc");
            data.AddHistory(Throw(2), 2);

            AppData reopened = Open();
            Assert.IsNull(reopened.History[0].PresetId);
            Assert.AreEqual("abc", reopened.History[1].PresetId);
        }

        [Test]
        public void PresetStoresACopyNotTheLiveSets()
        {
            AppData data = Open();
            RollSetConfig[] sets = Sets(DieType.D6);
            Preset p = data.AddPreset("Copy", sets);
            sets[0].Pool.TryAdd(DieType.D6, new DiceLimits());
            Assert.AreEqual(1, p.Sets[0].Pool.Get(DieType.D6));
        }

        [Test]
        public void BlankNamesAndUnknownIdsAreRefused()
        {
            AppData data = Open();
            Assert.IsNull(data.AddPreset("   ", Sets()));
            Preset p = data.AddPreset("Ok", Sets());
            Assert.IsFalse(data.UpdatePreset(p.Id, "", Sets()));
            Assert.IsFalse(data.UpdatePreset("nope", "Name", Sets()));
            Assert.IsFalse(data.DeletePreset("nope"));
            Assert.AreEqual("Ok", data.Presets[0].Name);
        }

        [Test]
        public void LongNamesAreCutToTheLimit()
        {
            AppData data = Open();
            Preset p = data.AddPreset("Extremely long name", Sets());
            Assert.LessOrEqual(p.Name.Length, _limits.PresetNameMax);
            Assert.IsTrue(p.Name.StartsWith("Extremel"));
        }

        [Test]
        public void HistoryIsNewestFirstAndCappedByTheLimit()
        {
            AppData data = Open();
            for (int i = 1; i <= 5; i++) data.AddHistory(Throw(i), 1000 + i);

            Assert.AreEqual(3, data.History.Count);
            Assert.AreEqual(1005, data.History[0].UnixSeconds);
            Assert.AreEqual(1003, data.History[2].UnixSeconds);
            Assert.AreEqual(3, Open().History.Count);
        }

        [Test]
        public void LoweringTheCapTrimsAnOldFileOnOpen()
        {
            AppData data = Open();
            for (int i = 1; i <= 3; i++) data.AddHistory(Throw(i), i);

            _limits = new DiceLimits { HistoryCap = 1, PresetNameMax = 8 };
            AppData reopened = Open();

            Assert.AreEqual(1, reopened.History.Count);
            Assert.AreEqual(3, reopened.History[0].UnixSeconds); // the newest survives
        }

        [Test]
        public void ClearHistoryEmptiesItOnDisk()
        {
            AppData data = Open();
            data.AddHistory(Throw(1), 1);
            data.AddPreset("Keep", Sets());

            data.ClearHistory();

            AppData reopened = Open();
            Assert.AreEqual(0, reopened.History.Count);
            Assert.AreEqual(1, reopened.Presets.Count);
        }

        [Test]
        public void SettingsPersistAfterSave()
        {
            AppData data = Open();
            data.Settings.Sound = false;
            data.Settings.SkinSet = 2;
            data.Save();

            AppData reopened = Open();
            Assert.IsFalse(reopened.Settings.Sound);
            Assert.AreEqual(2, reopened.Settings.SkinSet);
        }

        [Test]
        public void UnwritableLocationRaisesSaveFailedAndKeepsTheChange()
        {
            Directory.CreateDirectory(_dir);
            var data = new AppData(new SaveFile(_dir), _limits); // a folder, not a file
            Exception failure = null;
            data.SaveFailed += e => failure = e;

            data.AddPreset("Still here", Sets());

            Assert.IsNotNull(failure);
            Assert.AreEqual(1, data.Presets.Count);
        }
    }
}
