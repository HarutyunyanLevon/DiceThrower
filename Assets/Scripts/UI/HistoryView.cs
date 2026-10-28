using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;
using DiceThrower.Core;

namespace DiceThrower.UI
{
    /// <summary>The History tab: past throws, newest first, with the time, each set's total and its breakdown. Tap one to throw that setup again.</summary>
    public sealed class HistoryView
    {
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly DataUiConfig _cfg;
        private readonly string[] _captions;
        private readonly float _tapSlopPx;
        private readonly AppData _data;
        private readonly Action<HistoryEntry> _onRethrow;

        public HistoryView(VisualElement root, DataUiConfig cfg, string[] setCaptions, float tapSlopPx, AppData data, Action<HistoryEntry> onRethrow)
        {
            _cfg = cfg;
            _captions = setCaptions;
            _tapSlopPx = tapSlopPx;
            _data = data;
            _onRethrow = onRethrow;
            _list = root.Q<ScrollView>("history-list");
            _empty = root.Q<Label>("history-empty");
            _empty.text = cfg.historyEmptyText;
        }

        public void Refresh()
        {
            _list.contentContainer.Clear();
            IReadOnlyList<HistoryEntry> entries = _data.History;
            _empty.style.display = entries.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < entries.Count; i++) _list.Add(BuildRow(entries[i]));
        }

        private VisualElement BuildRow(HistoryEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("list-row");

            VisualElement text = UiFactory.Column("list-row__main");
            text.Add(UiFactory.Label(TimeText(entry.UnixSeconds), "list-row__time"));
            for (int s = 0; s < entry.Throw.Sets.Length; s++)
            {
                SetResult result = entry.Throw.Sets[s];
                if (result == null) continue; // that set was off or empty in this throw

                VisualElement line = UiFactory.Column("hist-set");
                line.Add(UiFactory.Label(_captions[s], "hist-set__cap"));
                Label total = UiFactory.Label(result.Total.ToString(), "hist-set__total");
                total.EnableInClassList("hist-set__total--nat20", result.HasNat20);
                total.EnableInClassList("hist-set__total--nat1", result.HasNat1);
                line.Add(total);
                line.Add(UiFactory.Label(RollFormat.Breakdown(result), "hist-set__detail"));
                text.Add(line);
            }
            row.Add(text);

            ListTap.Attach(row, () => _onRethrow(entry), _tapSlopPx, false);
            return row;
        }

        private string TimeText(long unixSeconds)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString(_cfg.historyTimeFormat, CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return ""; // a damaged time value in the file
            }
        }
    }
}
