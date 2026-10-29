using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>The Settings tab. Plain values; the views read and write them.</summary>
    public sealed class AppSettings
    {
        public bool Sound = true;
        public bool Haptics = true;
        /// <summary>The model dice set (index into the UI config's list) each roll set rolls with. A global setting, never stored in presets.</summary>
        public readonly DiceSetChoice DiceSets = new DiceSetChoice();
        /// <summary>Index into the themes of the UI config.</summary>
        public int Theme;
        /// <summary>How many of the latest throws rank the pinned chips. 0 = not chosen yet, the RulesConfig value applies.</summary>
        public int UsageWindow;
        /// <summary>Dice are simulated and the result is what they show. Off: the result is random and the dice just appear showing it.</summary>
        public bool PhysicsDice = true;
        /// <summary>Seconds after a throw before dice that still move are set where they are. 0 = not chosen yet, the RulesConfig value applies.</summary>
        public int SettleSeconds;
        /// <summary>The die types on the Attack row (d20, d4, d6, d% until changed). A global setting, never stored in presets.</summary>
        public readonly DiceRowTypes AttackDice = new DiceRowTypes();

        public int SettleSecondsOr(int configDefault)
        {
            return SettleSeconds > 0 ? SettleSeconds : configDefault;
        }

        public int UsageWindowOr(int configDefault)
        {
            return UsageWindow > 0 ? UsageWindow : configDefault;
        }
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
