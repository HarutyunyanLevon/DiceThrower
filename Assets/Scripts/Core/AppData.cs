using System;
using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>
    /// The saved state of the app (settings, presets, history) and the rules for changing it.
    /// Every change is written to the save file straight away.
    /// </summary>
    public sealed class AppData
    {
        private readonly SaveFile _file;
        private readonly DiceLimits _limits;

        public AppData(SaveFile file, DiceLimits limits)
        {
            _file = file;
            _limits = limits;
            Data = file.Load();
            LoadError = file.LastError;
            TrimHistory(); // the cap may have been lowered since the file was written
        }

        public SaveData Data { get; private set; }

        /// <summary>Why the save file could not be read (fresh data is used then), or null.</summary>
        public Exception LoadError { get; private set; }

        /// <summary>Raised when a change could not be written to disk. The change is still kept in memory.</summary>
        public event Action<Exception> SaveFailed;

        public AppSettings Settings { get { return Data.Settings; } }
        public IReadOnlyList<Preset> Presets { get { return Data.Presets; } }
        /// <summary>Newest first.</summary>
        public IReadOnlyList<HistoryEntry> History { get { return Data.History; } }

        // ------------------------------------------------------------------ presets

        /// <summary>Stores a copy of the sets under a new name. Null when the name is blank.</summary>
        public Preset AddPreset(string name, IList<RollSetConfig> sets)
        {
            string clean = CleanName(name);
            if (clean.Length == 0) return null;

            var preset = new Preset { Id = Guid.NewGuid().ToString("N"), Name = clean, Sets = CopySets(sets) };
            Data.Presets.Add(preset);
            Save();
            return preset;
        }

        /// <summary>Replaces a preset's name and sets (the edit popup). False for an unknown id or a blank name.</summary>
        public bool UpdatePreset(string id, string name, IList<RollSetConfig> sets)
        {
            string clean = CleanName(name);
            Preset preset = Find(id);
            if (preset == null || clean.Length == 0) return false;
            preset.Name = clean;
            preset.Sets = CopySets(sets);
            Save();
            return true;
        }

        public bool DeletePreset(string id)
        {
            Preset preset = Find(id);
            if (preset == null) return false;
            Data.Presets.Remove(preset);
            Save();
            return true;
        }

        // ------------------------------------------------------------------ history

        /// <param name="presetId">The preset the throw came from, or null.</param>
        public void AddHistory(ThrowResult result, long unixSeconds, string presetId = null)
        {
            Data.History.Insert(0, new HistoryEntry { UnixSeconds = unixSeconds, Throw = result, PresetId = presetId });
            TrimHistory();
            Save();
        }

        public void ClearHistory()
        {
            if (Data.History.Count == 0) return;
            Data.History.Clear();
            Save();
        }

        // ------------------------------------------------------------------ saving

        /// <summary>Writes everything. Call it after changing Settings.</summary>
        public void Save()
        {
            if (!_file.Save(Data) && SaveFailed != null) SaveFailed(_file.LastError);
        }

        private static RollSetConfig[] CopySets(IList<RollSetConfig> sets)
        {
            var copy = new RollSetConfig[RollSession.SetCount];
            for (int i = 0; i < copy.Length; i++) copy[i] = i < sets.Count ? sets[i].Clone() : new RollSetConfig();
            return copy;
        }

        private Preset Find(string id)
        {
            for (int i = 0; i < Data.Presets.Count; i++)
                if (Data.Presets[i].Id == id) return Data.Presets[i];
            return null;
        }

        private string CleanName(string name)
        {
            string trimmed = (name ?? "").Trim();
            if (trimmed.Length > _limits.PresetNameMax) trimmed = trimmed.Substring(0, Math.Max(0, _limits.PresetNameMax)).TrimEnd();
            return trimmed;
        }

        private void TrimHistory()
        {
            int extra = Data.History.Count - Math.Max(0, _limits.HistoryCap);
            if (extra > 0) Data.History.RemoveRange(Data.History.Count - extra, extra);
        }
    }
}
