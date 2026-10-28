using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using DiceThrower.Core;

namespace DiceThrower.UI
{
    /// <summary>
    /// The Presets tab: a list of saved setups. Tap a row to load and throw it; the pencil edits it, the bin deletes.
    /// </summary>
    public sealed class PresetsView
    {
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly DataUiConfig _cfg;
        private readonly string[] _captions;
        private readonly float _tapSlopPx;
        private readonly AppData _data;
        private readonly DialogView _dialog;
        private readonly Action<Preset> _onLoad, _onEdit;

        public PresetsView(VisualElement root, DataUiConfig cfg, string[] setCaptions, float tapSlopPx,
                           AppData data, DialogView dialog, Action<Preset> onLoad, Action<Preset> onEdit)
        {
            _cfg = cfg;
            _captions = setCaptions;
            _tapSlopPx = tapSlopPx;
            _data = data;
            _dialog = dialog;
            _onLoad = onLoad;
            _onEdit = onEdit;
            _list = root.Q<ScrollView>("presets-list");
            _empty = root.Q<Label>("presets-empty");
            _empty.text = cfg.presetsEmptyText;
        }

        public void Refresh()
        {
            _list.contentContainer.Clear();
            IReadOnlyList<Preset> presets = _data.Presets;
            _empty.style.display = presets.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < presets.Count; i++) _list.Add(BuildRow(presets[i]));
        }

        private VisualElement BuildRow(Preset preset)
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");

            VisualElement text = UiFactory.Column("list-row__main");
            text.Add(UiFactory.Label(preset.Name, "list-row__title"));
            for (int s = 0; s < preset.Sets.Length; s++)
                text.Add(UiFactory.Label(string.Format(_cfg.setLineFormat, _captions[s], Describe(preset.Sets[s])), "list-row__line"));
            row.Add(text);

            row.Add(UiFactory.IconButton(LineIcon.Edit, null, _tapSlopPx, () => _onEdit(preset)));
            row.Add(UiFactory.IconButton(LineIcon.Trash, "icon-btn--danger", _tapSlopPx, () => Delete(preset)));
            ListTap.Attach(row, () => _onLoad(preset), _tapSlopPx, false);
            return row;
        }

        private string Describe(RollSetConfig set)
        {
            string dice = RollFormat.Notation(set);
            if (dice.Length == 0) return _cfg.noDiceText;
            return set.Enabled ? dice : string.Format(_cfg.setOffFormat, dice);
        }

        private void Delete(Preset preset)
        {
            _dialog.Confirm(_cfg.deletePresetTitle, string.Format(_cfg.deletePresetMessageFormat, preset.Name), _cfg.deletePresetOk, () =>
            {
                _data.DeletePreset(preset.Id);
                Refresh();
            });
        }
    }
}
