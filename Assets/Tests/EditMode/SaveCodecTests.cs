using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using DiceThrower.Core;

namespace DiceThrower.Tests
{
    public class SaveCodecTests
    {
        private static readonly DiceLimits Limits = new DiceLimits();

        private static RollSetConfig Set(AdvantageMode adv, int modifier, bool enabled, params DieType[] dice)
        {
            var c = new RollSetConfig { Advantage = adv, Modifier = modifier, Enabled = enabled };
            foreach (DieType t in dice) c.Pool.TryAdd(t, Limits);
            return c;
        }

        private static Preset MakePreset()
        {
            return new Preset
            {
                Id = "p1",
                Name = "Fireball \"big\"",
                Sets = new[]
                {
                    Set(AdvantageMode.Advantage, 5, true, DieType.D20),
                    Set(AdvantageMode.Normal, -2, false, DieType.D6, DieType.D6, DieType.D100),
                },
            };
        }

        [Test]
        public void SettingsAndPresetsRoundTrip()
        {
            var data = new SaveData();
            data.Settings.Sound = false;
            data.Settings.Haptics = true;
            data.Settings.SkinSet = 1;
            data.Presets.Add(MakePreset());

            SaveData back = SaveCodec.Deserialize(SaveCodec.Serialize(data));

            Assert.IsFalse(back.Settings.Sound);
            Assert.IsTrue(back.Settings.Haptics);
            Assert.AreEqual(1, back.Settings.SkinSet);
            Assert.AreEqual(1, back.Presets.Count);
            Preset p = back.Presets[0];
            Assert.AreEqual("p1", p.Id);
            Assert.AreEqual("Fireball \"big\"", p.Name);
            Assert.AreEqual(AdvantageMode.Advantage, p.Sets[0].Advantage);
            Assert.AreEqual(5, p.Sets[0].Modifier);
            Assert.IsTrue(p.Sets[0].Enabled);
            Assert.AreEqual(1, p.Sets[0].Pool.Get(DieType.D20));
            Assert.AreEqual(AdvantageMode.Normal, p.Sets[1].Advantage);
            Assert.AreEqual(-2, p.Sets[1].Modifier);
            Assert.IsFalse(p.Sets[1].Enabled);
            Assert.AreEqual(2, p.Sets[1].Pool.Get(DieType.D6));
            Assert.AreEqual(1, p.Sets[1].Pool.Get(DieType.D100));
            Assert.AreEqual(0, p.Sets[1].Pool.Get(DieType.D4));
        }

        [Test]
        public void HistoryRoundTripKeepsEveryPassAndTheChosenOne()
        {
            var session = new RollSession(new SystemRng(21), Limits);
            AddResult r;
            session.AddDie(0, DieType.D20, out r);
            session.CycleAdvantage(0); // two passes
            session.AddDie(1, DieType.D8, out r);
            session.AddDie(1, DieType.D8, out r);
            session.SetModifier(1, 3);

            ThrowResult thrown = session.GetThrow();
            var data = new SaveData();
            data.History.Add(new HistoryEntry { UnixSeconds = 1700000000L, Throw = thrown });

            SaveData back = SaveCodec.Deserialize(SaveCodec.Serialize(data));

            Assert.AreEqual(1, back.History.Count);
            Assert.AreEqual(1700000000L, back.History[0].UnixSeconds);
            for (int s = 0; s < RollSession.SetCount; s++)
            {
                SetResult a = thrown.Sets[s], b = back.History[0].Throw.Sets[s];
                Assert.AreEqual(a.Total, b.Total);
                Assert.AreEqual(a.ChosenPass, b.ChosenPass);
                Assert.AreEqual(a.Passes.Length, b.Passes.Length);
                Assert.AreEqual(RollFormat.Breakdown(a), RollFormat.Breakdown(b));
            }
        }

        [Test]
        public void SetThatWasNotRolledStaysNull()
        {
            var session = new RollSession(new SystemRng(4), Limits);
            AddResult r;
            session.AddDie(1, DieType.D6, out r);
            var data = new SaveData();
            data.History.Add(new HistoryEntry { UnixSeconds = 5, Throw = session.GetThrow() });

            SaveData back = SaveCodec.Deserialize(SaveCodec.Serialize(data));

            Assert.IsNull(back.History[0].Throw.Sets[0]);
            Assert.IsNotNull(back.History[0].Throw.Sets[1]);
        }

        [Test]
        public void MissingAndUnknownPartsFallBackToDefaults()
        {
            const string json = "{\"settings\":{\"skinSet\":-3},"
                + "\"presets\":[{\"name\":\"x\",\"sets\":[{\"dice\":{\"D6\":2,\"D99\":4,\"D8\":-5},\"advantage\":\"Sideways\"}]}, 7, null],"
                + "\"history\":[{\"time\":1,\"sets\":[null,null]}, {\"time\":2,\"sets\":[{\"config\":{},\"passes\":[]}]}, \"junk\"]}";

            SaveData data = SaveCodec.Deserialize(json);

            Assert.IsTrue(data.Settings.Sound);       // default
            Assert.AreEqual(0, data.Settings.SkinSet); // negative becomes 0
            Assert.AreEqual(1, data.Presets.Count);    // 7 and null are skipped
            Preset p = data.Presets[0];
            Assert.IsNotNull(p.Id);                    // a missing id is made up
            Assert.AreEqual(RollSession.SetCount, p.Sets.Length);
            Assert.AreEqual(2, p.Sets[0].Pool.Get(DieType.D6));
            Assert.AreEqual(0, p.Sets[0].Pool.Get(DieType.D8)); // negative count -> 0
            Assert.AreEqual(AdvantageMode.Normal, p.Sets[0].Advantage);
            Assert.IsTrue(p.Sets[1].Pool.IsEmpty);      // missing set -> empty
            Assert.AreEqual(0, data.History.Count);     // nothing usable in any entry
        }

        [Test]
        public void NewerVersionAndWrongRootAreRejected()
        {
            Assert.Throws<FormatException>(() => SaveCodec.Deserialize("{\"version\":" + (SaveData.CurrentVersion + 1) + "}"));
            Assert.Throws<FormatException>(() => SaveCodec.Deserialize("[1,2]"));
        }
    }
}
