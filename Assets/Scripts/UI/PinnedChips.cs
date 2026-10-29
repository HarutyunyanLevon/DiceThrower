using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using DiceThrower.Core;

namespace DiceThrower.UI
{
    /// <summary>
    /// The row of preset chips above the Roll tab's buttons. Presets used most in the last few throws come first
    /// (see PresetUsage). A chip shows the preset's name and its formulas. Tap a chip to load the preset and throw it; the row scrolls sideways when it does not fit.
    /// </summary>
    public sealed class PinnedChips
    {
        private readonly ScrollView _row;
        private readonly AppData _data;
        private readonly Func<int> _window;
        private readonly int _max;
        private readonly float _tapSlopPx;
        private readonly Action<Preset> _onTap;

        public PinnedChips(VisualElement root, AppData data, Func<int> usageWindow, int maxChips, float tapSlopPx, Action<Preset> onTap)
        {
            _row = root.Q<ScrollView>("chips");
            _data = data;
            _window = usageWindow;
            _max = maxChips;
            _tapSlopPx = tapSlopPx;
            _onTap = onTap;
        }

        public void Refresh()
        {
            _row.contentContainer.Clear();
            List<Preset> ranked = PresetUsage.Rank(_data.Presets, _data.History, _window(), _max);
            _row.style.display = ranked.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            for (int i = 0; i < ranked.Count; i++) _row.Add(BuildChip(ranked[i]));
        }

        private VisualElement BuildChip(Preset preset)
        {
            var chip = new VisualElement();
            chip.AddToClassList("chip");
            chip.Add(UiFactory.Label(preset.Name, "chip__text"));
            string formula = RollFormat.Summary(preset.Sets);
            if (formula.Length > 0) chip.Add(UiFactory.Label(formula, "chip__formula"));
            ListTap.Attach(chip, () => _onTap(preset), _tapSlopPx, false); // a drag scrolls the row instead
            return chip;
        }
    }
}
