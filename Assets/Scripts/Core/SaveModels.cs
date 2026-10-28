using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>The Settings tab. Plain values; the views read and write them.</summary>
    public sealed class AppSettings
    {
        public bool Sound = true;
        public bool Haptics = true;
        /// <summary>Index into the dice skin sets of the UI config.</summary>
        public int SkinSet;
    }

    /// <summary>A named dice configuration: both roll sets with their modifier, advantage and enabled state.</summary>
    public sealed class Preset
    {
        public string Id;
        public string Name;
        public RollSetConfig[] Sets;
    }

    /// <summary>One past throw: when it happened and what every set rolled.</summary>
    public sealed class HistoryEntry
    {
        public long UnixSeconds;
        public ThrowResult Throw;
        /// <summary>The preset this throw came from (loaded from the Presets tab or a chip), or null.</summary>
        public string PresetId;
    }

    /// <summary>Everything that is written to the save file.</summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public AppSettings Settings = new AppSettings();
        public readonly List<Preset> Presets = new List<Preset>();
        /// <summary>Newest first.</summary>
        public readonly List<HistoryEntry> History = new List<HistoryEntry>();
    }
}
