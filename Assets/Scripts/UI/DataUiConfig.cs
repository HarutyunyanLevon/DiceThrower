using System;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Core;
using DiceThrower.Game;

namespace DiceThrower.UI
{
    /// <summary>A named look for the dice: one skin per roll set, in set order (Attack, Damage).</summary>
    [Serializable]
    public sealed class SkinSetSpec
    {
        public string name = "Skins";
        public DiceSkin[] skins = new DiceSkin[RollSession.SetCount];
    }

    /// <summary>A named colour theme: a style sheet that redefines the colour variables of Tavern.uss.</summary>
    [Serializable]
    public sealed class ThemeSpec
    {
        public string name = "Theme";
        public StyleSheet sheet;
    }

    /// <summary>Numbers and texts of the Presets, History and Settings tabs. Limits (history length, name length) are in RulesConfig.</summary>
    [CreateAssetMenu(menuName = "DiceThrower/Data UI Config", fileName = "DataUiConfig")]
    public sealed class DataUiConfig : ScriptableObject
    {
        [Header("Storage")]
        [Tooltip("Name of the save file in Application.persistentDataPath.")]
        public string saveFileName = "dicethrower_save.json";

        [Header("History")]
        [Tooltip("Seconds after the last roll before it is written to the history. Tapping several dice quickly makes one entry.")]
        public float historyCommitDelaySeconds = 2f;
        [Tooltip(".NET date format of an entry's time (local time).")]
        public string historyTimeFormat = "dd MMM  HH:mm";

        [Header("Dice skins (the Settings tab cycles through these)")]
        public SkinSetSpec[] skinSets = new SkinSetSpec[0];

        [Header("Themes (the Settings tab cycles through these)")]
        public ThemeSpec[] themes = new ThemeSpec[0];

        [Header("Settings texts")]
        [Tooltip("{0} = number of throws")]
        public string usageWindowFormat = "{0} throws";

        [Header("Preset editor and dialog texts")]
        public string cancelText = "Cancel";
        public string savePresetTitle = "Save preset";
        public string savePresetOk = "Save";
        public string editPresetTitle = "Edit preset";
        public string editPresetOk = "Save";
        public string deletePresetTitle = "Delete preset?";
        [Tooltip("{0} = preset name")]
        public string deletePresetMessageFormat = "\"{0}\" will be removed.";
        public string deletePresetOk = "Delete";
        public string clearHistoryTitle = "Clear history?";
        public string clearHistoryMessage = "All recorded throws will be removed.";
        public string clearHistoryOk = "Clear";

        [Header("Toasts")]
        public string saveEmptyToast = "Add some dice first";
        [Tooltip("{0} = preset name")]
        public string presetSavedFormat = "Saved \"{0}\"";
        public string historyClearedToast = "History cleared";
        public string skinChangedToast = "The new skin applies from the next throw";

        [Header("Lists")]
        [Tooltip("{0} = number of the new preset")]
        public string defaultPresetNameFormat = "Preset {0}";
        [Tooltip("{0} = set caption, {1} = its dice, e.g. ATTACK: 1d20 + 5")]
        public string setLineFormat = "{0}: {1}";
        [Tooltip("{0} = a set's dice")]
        public string setOffFormat = "{0} (off)";
        public string noDiceText = "no dice";
        [TextArea] public string presetsEmptyText = "No presets yet. Set up your dice on the Roll tab and press Save.";
        [TextArea] public string historyEmptyText = "No throws yet.";

        /// <summary>At least one theme with a style sheet, and every skin set needs one skin per roll set.</summary>
        public bool IsValid()
        {
            if (themes == null || themes.Length == 0) return false;
            for (int i = 0; i < themes.Length; i++)
                if (themes[i].sheet == null) return false;
            if (skinSets == null || skinSets.Length == 0) return false;
            for (int i = 0; i < skinSets.Length; i++)
            {
                DiceSkin[] skins = skinSets[i].skins;
                if (skins == null || skins.Length != RollSession.SetCount) return false;
                for (int s = 0; s < skins.Length; s++)
                    if (skins[s] == null) return false;
            }
            return true;
        }
    }
}
