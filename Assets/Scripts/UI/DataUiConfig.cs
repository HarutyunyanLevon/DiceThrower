using System;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Core;
using DiceThrower.Game;

namespace DiceThrower.UI
{
    /// <summary>A dice set the Dice tab offers: a name and the model skin (colour variant) the dice look like.</summary>
    [Serializable]
    public sealed class DiceSetSpec
    {
        public string name = "Dice";
        public DiceSkin skin;
    }

    /// <summary>
    /// A named theme: a style sheet that redefines the colour variables of Tavern.uss (and may restyle frames and fonts)
    /// and a 3D scene (play surface, backdrop, lights). Both are made by the theme's builder (DiceThrower > Themes).
    /// </summary>
    [Serializable]
    public sealed class ThemeSpec
    {
        public string name = "Theme";
        public StyleSheet sheet;
        [Tooltip("The theme's 3D scene prefab (a ThemeScene on its root).")]
        public ThemeScene scene;
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
        [Tooltip("Longest breakdown line (characters) in an entry that still lists every die; a longer one shows one sum per die type.")]
        public int historyBreakdownMaxChars = 36;

        [Header("Dice sets (the Dice tab lists these, in this order; the saved choice is an index, so new ones go at the end)")]
        public DiceSetSpec[] diceSets = new DiceSetSpec[0];

        [Header("Themes (the theme picker in Settings lists these, in this order; the saved choice is an index)")]
        public ThemeSpec[] themes = new ThemeSpec[0];

        [Header("Settings texts")]
        [Tooltip("{0} = number of throws")]
        public string usageWindowFormat = "{0} throws";
        [Tooltip("{0} = seconds")]
        public string settleSecondsFormat = "{0} s";

        [Header("Theme picker")]
        public string themePickerTitle = "Theme";
        public string themePickerApplyText = "Apply";

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
        public string diceSetChangedToast = "The new dice apply from the next throw";

        [Header("Dice tab")]
        [Tooltip("{0} = roll set caption, e.g. ATTACK")]
        public string diceSlotFormat = "{0} DICE SET";
        [Tooltip("{0} = roll set name, e.g. Attack")]
        public string useDiceSetFormat = "Use for {0}";

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

        /// <summary>The skin of a dice set, with the index kept inside the list.</summary>
        public DiceSkin SkinOf(int diceSetIndex)
        {
            return diceSets[Mathf.Clamp(diceSetIndex, 0, diceSets.Length - 1)].skin;
        }

        /// <summary>At least one theme with a style sheet and a scene, and at least one dice set per roll set, each with a skin.</summary>
        public bool IsValid()
        {
            if (themes == null || themes.Length == 0) return false;
            for (int i = 0; i < themes.Length; i++)
                if (themes[i].sheet == null || themes[i].scene == null) return false;
            if (diceSets == null || diceSets.Length < RollSession.SetCount) return false;
            for (int i = 0; i < diceSets.Length; i++)
                if (diceSets[i] == null || diceSets[i].skin == null) return false;
            return true;
        }
    }
}
