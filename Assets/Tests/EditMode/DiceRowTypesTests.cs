using System.Collections.Generic;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class DiceRowTypesTests
    {
        private static readonly DieType[] Defaults = { DieType.D20, DieType.D4, DieType.D6, DieType.D100 };

        private static AppSettings RoundTrip(AppSettings settings)
        {
            var data = new SaveData { Settings = settings };
            return SaveCodec.Deserialize(SaveCodec.Serialize(data)).Settings;
        }

        // ------------------------------------------------------------------ the list

        [Test]
        public void TheAttackRowStartsWithD20D4D6AndPercent()
        {
            CollectionAssert.AreEqual(Defaults, new DiceRowTypes().Types);
            CollectionAssert.AreEqual(Defaults, new AppSettings().AttackDice.Types);
        }

        [Test]
        public void AddAppendsATypeOnceAndRaisesChanged()
        {
            var row = new DiceRowTypes();
            int changes = 0;
            row.Changed += () => changes++;

            Assert.IsTrue(row.Add(DieType.D8));
            Assert.IsFalse(row.Add(DieType.D8));   // already there
            Assert.IsFalse(row.Add(DieType.D20));

            CollectionAssert.AreEqual(new[] { DieType.D20, DieType.D4, DieType.D6, DieType.D100, DieType.D8 }, row.Types);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void AnyTypeCanBeRemovedIncludingD20()
        {
            var row = new DiceRowTypes();
            int changes = 0;
            row.Changed += () => changes++;

            Assert.IsTrue(row.Remove(DieType.D20));
            Assert.IsFalse(row.Remove(DieType.D20));
            Assert.IsFalse(row.Remove(DieType.D12)); // never was there

            CollectionAssert.AreEqual(new[] { DieType.D4, DieType.D6, DieType.D100 }, row.Types);
            Assert.AreEqual(1, changes);

            foreach (DieType t in new List<DieType>(row.Types)) row.Remove(t);
            Assert.AreEqual(0, row.Types.Count); // the row may end up empty; "+" is still there
        }

        [Test]
        public void SetAllDropsRepeatsAndUnknownValuesAndRaisesNothing()
        {
            var row = new DiceRowTypes();
            int changes = 0;
            row.Changed += () => changes++;

            row.SetAll(new[] { DieType.D8, DieType.D8, (DieType)99, DieType.D12 });

            CollectionAssert.AreEqual(new[] { DieType.D8, DieType.D12 }, row.Types);
            Assert.AreEqual(0, changes);
        }

        // ------------------------------------------------------------------ what a row shows

        [Test]
        public void ARowShowsItsListAndThenAnyOtherTypeThatHasDice()
        {
            var pool = new DicePool();
            DiceLimits unlimited = DiceLimits.Unlimited;
            pool.TryAdd(DieType.D12, unlimited);
            pool.TryAdd(DieType.D8, unlimited);
            pool.TryAdd(DieType.D6, unlimited); // in the list already
            var shown = new List<DieType> { DieType.D10 }; // stale content is cleared

            DiceRowTypes.Shown(new DiceRowTypes(), pool, shown);

            CollectionAssert.AreEqual(new[] { DieType.D20, DieType.D4, DieType.D6, DieType.D100, DieType.D8, DieType.D12 }, shown);
        }

        [Test]
        public void ARowWithoutAListShowsEveryTypeInDieOrder()
        {
            var shown = new List<DieType>();

            DiceRowTypes.Shown(null, new DicePool(), shown);

            CollectionAssert.AreEqual(DieInfo.AllTypes, shown);
        }

        [Test]
        public void AnEmptyListWithAnEmptyPoolShowsNothing()
        {
            var row = new DiceRowTypes();
            row.SetAll(new DieType[0]);
            var shown = new List<DieType> { DieType.D4 };

            DiceRowTypes.Shown(row, new DicePool(), shown);

            Assert.AreEqual(0, shown.Count);
        }

        [Test]
        public void MissingListsWhatThePickerOffersInDieOrder()
        {
            var missing = new List<DieType> { DieType.D4 };

            DiceRowTypes.Missing(new List<DieType> { DieType.D20, DieType.D4 }, missing);
            CollectionAssert.AreEqual(new[] { DieType.D6, DieType.D8, DieType.D10, DieType.D12, DieType.D100 }, missing);

            DiceRowTypes.Missing(new List<DieType>(DieInfo.AllTypes), missing);
            Assert.AreEqual(0, missing.Count); // every type is on the row: no "+"
        }

        // ------------------------------------------------------------------ saving

        [Test]
        public void TheListSurvivesASaveAndLoadInItsOrder()
        {
            var settings = new AppSettings();
            settings.AttackDice.SetAll(new[] { DieType.D8, DieType.D20, DieType.D100 });

            AppSettings loaded = RoundTrip(settings);

            CollectionAssert.AreEqual(new[] { DieType.D8, DieType.D20, DieType.D100 }, loaded.AttackDice.Types);
        }

        [Test]
        public void AnEmptyListStaysEmptyAfterASaveAndLoad()
        {
            var settings = new AppSettings();
            settings.AttackDice.SetAll(new DieType[0]);

            Assert.AreEqual(0, RoundTrip(settings).AttackDice.Types.Count);
        }

        [Test]
        public void AnOldSaveOrAWrongValueGivesTheDefaultList()
        {
            AppSettings old = SaveCodec.Deserialize("{\"version\":1,\"settings\":{\"sound\":false}}").Settings;
            AppSettings wrong = SaveCodec.Deserialize("{\"settings\":{\"attackDice\":5}}").Settings;

            CollectionAssert.AreEqual(Defaults, old.AttackDice.Types);
            CollectionAssert.AreEqual(Defaults, wrong.AttackDice.Types);
        }

        [Test]
        public void JunkInTheSavedListIsSkipped()
        {
            AppSettings loaded = SaveCodec.Deserialize("{\"settings\":{\"attackDice\":[\"D8\",\"D8\",\"D99\",7,null,\"D100\"]}}").Settings;

            CollectionAssert.AreEqual(new[] { DieType.D8, DieType.D100 }, loaded.AttackDice.Types);
        }

        [Test]
        public void PresetsDoNotStoreTheList()
        {
            var data = new SaveData();
            data.Settings.AttackDice.SetAll(new[] { DieType.D12 });
            var preset = new Preset { Id = "p", Name = "x", Sets = new[] { new RollSetConfig(), new RollSetConfig() } };
            preset.Sets[0].Pool.TryAdd(DieType.D8, DiceLimits.Unlimited);
            data.Presets.Add(preset);

            string json = SaveCodec.Serialize(data);
            SaveData back = SaveCodec.Deserialize(json);

            Assert.AreEqual(1, back.Presets[0].Sets[0].Pool.Get(DieType.D8));
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(json, "attackDice").Count); // only in the settings
        }
    }
}
