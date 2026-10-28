using System;
using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>Decides which presets are pinned as chips on the Roll screen.</summary>
    public static class PresetUsage
    {
        /// <summary>
        /// Presets used in the last <paramref name="window"/> throws come first (most used first, ties by the more recent use).
        /// Presets that were not used follow, newest saved first, so a fresh preset shows up at once. At most <paramref name="max"/>.
        /// </summary>
        /// <param name="history">Newest first.</param>
        public static List<Preset> Rank(IReadOnlyList<Preset> presets, IReadOnlyList<HistoryEntry> history, int window, int max)
        {
            var uses = new Dictionary<string, int>();
            var lastUse = new Dictionary<string, int>(); // index in history: smaller = more recent
            int span = Math.Min(Math.Max(0, window), history.Count);
            for (int i = 0; i < span; i++)
            {
                string id = history[i].PresetId;
                if (id == null) continue;
                int n;
                uses.TryGetValue(id, out n);
                uses[id] = n + 1;
                if (!lastUse.ContainsKey(id)) lastUse[id] = i;
            }

            var used = new List<Preset>();
            var unused = new List<Preset>();
            for (int i = 0; i < presets.Count; i++)
                (uses.ContainsKey(presets[i].Id) ? used : unused).Add(presets[i]);

            used.Sort((a, b) =>
            {
                int byCount = uses[b.Id].CompareTo(uses[a.Id]);
                return byCount != 0 ? byCount : lastUse[a.Id].CompareTo(lastUse[b.Id]);
            });
            unused.Reverse();

            used.AddRange(unused);
            if (used.Count > max) used.RemoveRange(Math.Max(0, max), used.Count - Math.Max(0, max));
            return used;
        }
    }
}
