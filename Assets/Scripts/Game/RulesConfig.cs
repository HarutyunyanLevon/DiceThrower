using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>Game rules that are numbers: dice caps and the modifier range.</summary>
    [CreateAssetMenu(menuName = "DiceThrower/Rules Config", fileName = "RulesConfig")]
    public sealed class RulesConfig : ScriptableObject
    {
        [Tooltip("Dice allowed in one roll set.")]
        public int maxDicePerSet = 20;
        [Tooltip("Dice of one type allowed in one roll set.")]
        public int maxDicePerType = 10;
        public int modifierMin = -10;
        public int modifierMax = 10;

        [Header("Data")]
        [Tooltip("Throws kept in the history. The oldest are dropped.")]
        public int historyCap = 100;
        [Tooltip("Longest preset name. Longer names are cut.")]
        public int presetNameMaxLength = 24;

        [Header("Pinned presets")]
        [Tooltip("The chips on the Roll screen are ordered by use in this many of the latest throws.")]
        public int usageWindow = 30;
        [Tooltip("Most chips shown on the Roll screen.")]
        public int pinnedChipsMax = 6;

        public DiceLimits ToLimits()
        {
            return new DiceLimits
            {
                MaxTotal = maxDicePerSet,
                MaxPerType = maxDicePerType,
                ModifierMin = modifierMin,
                ModifierMax = modifierMax,
                HistoryCap = historyCap,
                PresetNameMax = presetNameMaxLength,
            };
        }
    }
}
