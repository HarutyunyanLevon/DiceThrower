using System;
using UnityEngine;

namespace DiceThrower.UI
{
    /// <summary>Per roll set: its label and its USS row class. The dice skins are chosen in the Settings tab (DataUiConfig).</summary>
    [Serializable]
    public sealed class RollSetSpec
    {
        public string caption = "SET";
        [Tooltip("USS class added to this set's dice row, e.g. dice-row--atk (colours come from the stylesheet).")]
        public string rowClass = "dice-row--atk";
    }

    /// <summary>Numbers and texts of the Roll screen. Looks (colours, sizes) are in the USS files.</summary>
    [CreateAssetMenu(menuName = "DiceThrower/Roll UI Config", fileName = "RollUiConfig")]
    public sealed class RollUiConfig : ScriptableObject
    {
        [Header("Roll sets (the app supports exactly two)")]
        public RollSetSpec[] sets = new RollSetSpec[2];

        [Header("App")]
        public int targetFrameRate = 60;

        [Header("Press and slide")]
        [Tooltip("Pointer travel (UI px) before a press becomes a slide, or a list press becomes a scroll.")]
        public float slideThresholdPx = 10f;
        [Tooltip("UI px of travel per +1 die while sliding a die button.")]
        public float countPxPerStep = 24f;
        [Tooltip("UI px of travel per +1 while sliding the modifier.")]
        public float modifierPxPerStep = 14f;
        [Tooltip("Extra track length above and below the slide range (UI px).")]
        public float sliderTrackPad = 22f;
        [Tooltip("Seconds a die button is held still before its type is removed from the Attack row.")]
        public float longPressSeconds = 0.5f;

        [Header("Feedback")]
        public float toastSeconds = 1.6f;
        public float badgeFlashSeconds = 0.35f;

        [Header("Texts")]
        public string clearText = "Clear";
        public string saveText = "Save";
        [Tooltip("{0} = max dice per set")]
        public string limitTotalFormat = "Limit reached: {0} dice per set";
        [Tooltip("{0} = max per type, {1} = die label")]
        public string limitTypeFormat = "Limit reached: {0} x {1}";
        [Tooltip("{0} = caption, {1} = lowest, {2} = highest possible total")]
        public string captionRangeFormat = "{0}: {1} - {2}";
        [Tooltip("Longest breakdown line (characters) that still lists every die; a longer one shows one sum per die type.")]
        public int breakdownMaxChars = 44;
    }
}
