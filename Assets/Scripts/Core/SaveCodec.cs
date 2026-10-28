using System;
using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>
    /// SaveData to JSON text and back. Reading is forgiving: missing or unknown parts fall back to
    /// defaults. Only text that is not JSON, or that comes from a newer version, is rejected.
    /// Enums are written by name so reordering them later does not break old files.
    /// </summary>
    public static class SaveCodec
    {
        public static string Serialize(SaveData data)
        {
            return Json.Write(WriteData(data));
        }

        /// <summary>Throws FormatException for malformed text or a save from a newer version.</summary>
        public static SaveData Deserialize(string json)
        {
            var root = Json.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("The save file must hold a JSON object.");

            int version = GetInt(root, "version", SaveData.CurrentVersion);
            if (version > SaveData.CurrentVersion)
                throw new FormatException("The save file is from a newer version (" + version + ").");

            var data = new SaveData();
            Dictionary<string, object> settings = GetObject(root, "settings");
            data.Settings.Sound = GetBool(settings, "sound", data.Settings.Sound);
            data.Settings.Haptics = GetBool(settings, "haptics", data.Settings.Haptics);
            data.Settings.SkinSet = Math.Max(0, GetInt(settings, "skinSet", data.Settings.SkinSet));

            foreach (object item in GetList(root, "presets"))
            {
                Preset preset = ReadPreset(item as Dictionary<string, object>);
                if (preset != null) data.Presets.Add(preset);
            }
            foreach (object item in GetList(root, "history"))
            {
                HistoryEntry entry = ReadEntry(item as Dictionary<string, object>);
                if (entry != null) data.History.Add(entry);
            }
            return data;
        }

        // ------------------------------------------------------------------ writing

        private static Dictionary<string, object> WriteData(SaveData data)
        {
            var settings = new Dictionary<string, object>
            {
                { "sound", data.Settings.Sound },
                { "haptics", data.Settings.Haptics },
                { "skinSet", data.Settings.SkinSet },
            };

            var presets = new List<object>();
            foreach (Preset p in data.Presets)
            {
                var sets = new List<object>();
                foreach (RollSetConfig c in p.Sets) sets.Add(WriteConfig(c));
                presets.Add(new Dictionary<string, object> { { "id", p.Id }, { "name", p.Name }, { "sets", sets } });
            }

            var history = new List<object>();
            foreach (HistoryEntry e in data.History)
            {
                var sets = new List<object>();
                foreach (SetResult r in e.Throw.Sets) sets.Add(WriteResult(r));
                var item = new Dictionary<string, object> { { "time", e.UnixSeconds }, { "sets", sets } };
                if (e.PresetId != null) item.Add("preset", e.PresetId);
                history.Add(item);
            }

            return new Dictionary<string, object>
            {
                { "version", SaveData.CurrentVersion },
                { "settings", settings },
                { "presets", presets },
                { "history", history },
            };
        }

        /// <summary>Only dice types that are actually in the pool are written.</summary>
        private static Dictionary<string, object> WriteConfig(RollSetConfig c)
        {
            var dice = new Dictionary<string, object>();
            for (int i = 0; i < DieInfo.TypeCount; i++)
            {
                DieType t = DieInfo.AllTypes[i];
                if (c.Pool.Get(t) > 0) dice.Add(t.ToString(), c.Pool.Get(t));
            }
            return new Dictionary<string, object>
            {
                { "dice", dice },
                { "advantage", c.Advantage.ToString() },
                { "enabled", c.Enabled },
                { "modifier", c.Modifier },
            };
        }

        /// <summary>The set and what it rolled. Total and chosen pass are worked out again when reading.</summary>
        private static object WriteResult(SetResult r)
        {
            if (r == null) return null;
            var passes = new List<object>();
            foreach (PoolRoll pass in r.Passes)
            {
                var dice = new List<object>();
                foreach (DieRoll d in pass.Dice)
                    dice.Add(new Dictionary<string, object> { { "type", d.Type.ToString() }, { "value", d.Value } });
                passes.Add(dice);
            }
            return new Dictionary<string, object> { { "config", WriteConfig(r.Config) }, { "passes", passes } };
        }

        // ------------------------------------------------------------------ reading

        private static Preset ReadPreset(Dictionary<string, object> d)
        {
            if (d == null) return null;
            var preset = new Preset
            {
                Id = GetString(d, "id", null) ?? Guid.NewGuid().ToString("N"),
                Name = GetString(d, "name", ""),
                Sets = new RollSetConfig[RollSession.SetCount],
            };
            List<object> sets = GetList(d, "sets");
            for (int i = 0; i < preset.Sets.Length; i++)
                preset.Sets[i] = ReadConfig(i < sets.Count ? sets[i] as Dictionary<string, object> : null);
            return preset;
        }

        private static RollSetConfig ReadConfig(Dictionary<string, object> d)
        {
            var c = new RollSetConfig();
            Dictionary<string, object> dice = GetObject(d, "dice");
            DiceLimits unlimited = DiceLimits.Unlimited;
            for (int i = 0; i < DieInfo.TypeCount; i++)
            {
                DieType t = DieInfo.AllTypes[i];
                c.Pool.SetCount(t, Math.Max(0, GetInt(dice, t.ToString(), 0)), unlimited);
            }
            c.Advantage = GetEnum(d, "advantage", AdvantageMode.Normal);
            c.Enabled = GetBool(d, "enabled", true);
            c.Modifier = GetInt(d, "modifier", 0);
            return c;
        }

        private static HistoryEntry ReadEntry(Dictionary<string, object> d)
        {
            if (d == null) return null;
            var throwResult = new ThrowResult { Sets = new SetResult[RollSession.SetCount] };
            List<object> sets = GetList(d, "sets");
            bool any = false;
            for (int i = 0; i < throwResult.Sets.Length && i < sets.Count; i++)
            {
                throwResult.Sets[i] = ReadResult(sets[i] as Dictionary<string, object>);
                any |= throwResult.Sets[i] != null;
            }
            if (!any) return null;
            return new HistoryEntry { UnixSeconds = GetLong(d, "time", 0), Throw = throwResult, PresetId = GetString(d, "preset", null) };
        }

        private static SetResult ReadResult(Dictionary<string, object> d)
        {
            if (d == null) return null;
            RollSetConfig config = ReadConfig(GetObject(d, "config"));
            List<object> rawPasses = GetList(d, "passes");
            if (rawPasses.Count < 1 || rawPasses.Count > 2) return null; // a set is rolled once, or twice with advantage

            var passes = new PoolRoll[rawPasses.Count];
            for (int i = 0; i < passes.Length; i++)
            {
                var pass = new PoolRoll();
                var dice = rawPasses[i] as List<object>;
                if (dice != null)
                {
                    foreach (object item in dice)
                    {
                        var die = item as Dictionary<string, object>;
                        DieType type;
                        int value = GetInt(die, "value", 0);
                        if (TryGetEnum(die, "type", out type) && value >= 1) pass.Dice.Add(new DieRoll(type, value));
                    }
                }
                passes[i] = pass;
            }
            return RollEngine.Evaluate(config, passes);
        }

        // ------------------------------------------------------------------ tolerant getters (a null dictionary reads as empty)

        private static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> d, string key)
        {
            return Get(d, key) as Dictionary<string, object>;
        }

        private static List<object> GetList(Dictionary<string, object> d, string key)
        {
            return Get(d, key) as List<object> ?? new List<object>();
        }

        private static bool GetBool(Dictionary<string, object> d, string key, bool fallback)
        {
            object v = Get(d, key);
            return v is bool ? (bool)v : fallback;
        }

        private static string GetString(Dictionary<string, object> d, string key, string fallback)
        {
            return Get(d, key) as string ?? fallback;
        }

        private static long GetLong(Dictionary<string, object> d, string key, long fallback)
        {
            object v = Get(d, key);
            if (v is long) return (long)v;
            if (v is double) return (long)Math.Round((double)v);
            return fallback;
        }

        private static int GetInt(Dictionary<string, object> d, string key, int fallback)
        {
            long v = GetLong(d, key, fallback);
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, v));
        }

        private static T GetEnum<T>(Dictionary<string, object> d, string key, T fallback) where T : struct
        {
            T value;
            return TryGetEnum(d, key, out value) ? value : fallback;
        }

        private static bool TryGetEnum<T>(Dictionary<string, object> d, string key, out T value) where T : struct
        {
            value = default(T);
            string name = Get(d, key) as string;
            return name != null && Enum.TryParse(name, out value) && Enum.IsDefined(typeof(T), value);
        }
    }
}
