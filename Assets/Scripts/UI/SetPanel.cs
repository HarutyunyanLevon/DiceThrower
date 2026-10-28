using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using DiceThrower.Core;

namespace DiceThrower.UI
{
    /// <summary>
    /// The Roll tab's controls for one roll set: the header (caption, total, on/off switch, advantage, modifier, hide)
    /// and the dice row. Gestures turn into RollSession operations; the resulting TrayChange goes out through the callbacks.
    /// </summary>
    public sealed class SetPanel
    {
        private const string FlashClass = "badge--flash";

        public readonly VisualElement Header = new VisualElement();
        public readonly VisualElement Row = new VisualElement();

        private readonly int _set;
        private readonly RollSession _session;
        private readonly RollUiConfig _config;
        private readonly SliderOverlay _overlay;
        private readonly Action<TrayChange> _apply;
        private readonly Action<DieType, AddResult> _blocked;
        private readonly Action _refreshAll;

        private VisualElement _switch, _advButton, _modPill;
        private Label _caption, _total, _breakdown, _modLabel;
        private DieIcon _advIcon;
        private LineIcon _chevron;
        private bool _rowHidden;
        private readonly Dictionary<DieType, VisualElement> _badge = new Dictionary<DieType, VisualElement>();
        private readonly Dictionary<DieType, Label> _badgeText = new Dictionary<DieType, Label>();

        /// <param name="apply">Applies a change to the tray (and refreshes the screen).</param>
        /// <param name="blocked">Reports a die that could not be added.</param>
        /// <param name="refreshAll">Redraws every set; used while the modifier slider moves.</param>
        public SetPanel(int set, RollSession session, RollUiConfig config, SliderOverlay overlay,
                        Action<TrayChange> apply, Action<DieType, AddResult> blocked, Action refreshAll)
        {
            _set = set;
            _session = session;
            _config = config;
            _overlay = overlay;
            _apply = apply;
            _blocked = blocked;
            _refreshAll = refreshAll;

            BuildHeader();
            BuildRow();
        }

        // ------------------------------------------------------------------ build

        private void BuildHeader()
        {
            Header.AddToClassList("set-header");

            _caption = UiFactory.Label("", "caption");
            _total = UiFactory.Label("", "total");
            _breakdown = UiFactory.Label("", "breakdown");
            VisualElement totalRow = UiFactory.Column("total-row");
            totalRow.Add(_total);
            totalRow.Add(_breakdown);
            VisualElement textBlock = UiFactory.Column("set-header__text");
            textBlock.Add(_caption);
            textBlock.Add(totalRow);
            Header.Add(textBlock);

            _switch = new VisualElement();
            _switch.AddToClassList("switch");
            _switch.Add(UiFactory.Column("switch__knob"));
            PressGesture.Tappable(_switch, () => _apply(_session.SetEnabled(_set, !_session.Sets[_set].Enabled)));
            Header.Add(_switch);

            _advButton = new VisualElement();
            _advButton.AddToClassList("adv-btn");
            _advIcon = DieIcon.ForType(DieType.D20);
            _advButton.Add(_advIcon);
            PressGesture.Tappable(_advButton, () => _apply(_session.CycleAdvantage(_set)));
            Header.Add(_advButton);

            BuildModifier();
            BuildHideButton();
        }

        private void BuildModifier()
        {
            DiceLimits limits = _session.Limits;
            _modPill = new VisualElement();
            _modPill.AddToClassList("mod");
            _modLabel = UiFactory.Label("", "mod__text");
            _modPill.Add(_modLabel);

            var gesture = new PressGesture(_modPill)
            {
                Min = limits.ModifierMin,
                Max = limits.ModifierMax,
                PxPerStep = _config.modifierPxPerStep,
                SlideThreshold = _config.slideThresholdPx,
                GetStart = () => _session.Sets[_set].Modifier,
            };
            gesture.OnSlideBegin = () => _overlay.Show(_modPill, limits.ModifierMin, limits.ModifierMax,
                                                       _session.Sets[_set].Modifier, _config.modifierPxPerStep, RollFormat.Modifier);
            gesture.OnSlide = v =>
            {
                _session.SetModifier(_set, v);
                _overlay.Update(v);
                _refreshAll(); // the total follows the slider live; nothing is thrown
            };
            gesture.OnEnd = () => _overlay.Hide();
            Header.Add(_modPill);
        }

        private void BuildHideButton()
        {
            var button = new VisualElement();
            button.AddToClassList("hide-btn");
            _chevron = new LineIcon(LineIcon.ChevronDown);
            _chevron.AddToClassList("hide-btn__icon");
            button.Add(_chevron);
            PressGesture.Tappable(button, () =>
            {
                _rowHidden = !_rowHidden;
                Row.style.display = _rowHidden ? DisplayStyle.None : DisplayStyle.Flex;
                _chevron.SetPaths(_rowHidden ? LineIcon.ChevronUp : LineIcon.ChevronDown);
            });
            Header.Add(button);
        }

        private void BuildRow()
        {
            Row.AddToClassList("dice-row");
            Row.AddToClassList(_config.sets[_set].rowClass);
            Row.Add(UiFactory.Column("dice-row__bar"));
            for (int i = 0; i < DieInfo.TypeCount; i++) Row.Add(BuildDieButton(DieInfo.AllTypes[i]));
        }

        private VisualElement BuildDieButton(DieType type)
        {
            int max = _session.Limits.MaxPerType;
            var button = new VisualElement();
            button.AddToClassList("die-btn");
            button.Add(DieIcon.ForType(type));

            VisualElement badge = UiFactory.Column("badge");
            Label badgeText = UiFactory.Label("", "badge__text");
            badge.Add(badgeText);
            button.Add(badge);
            _badge[type] = badge;
            _badgeText[type] = badgeText;

            var gesture = new PressGesture(button)
            {
                Min = 0,
                Max = max,
                PxPerStep = _config.countPxPerStep,
                SlideThreshold = _config.slideThresholdPx,
                GetStart = () => _session.Sets[_set].Pool.Get(type),
            };
            gesture.OnTap = () =>
            {
                AddResult result;
                TrayChange change = _session.AddDie(_set, type, out result);
                if (result != AddResult.Added) _blocked(type, result);
                _apply(change);
            };
            gesture.OnSlideBegin = () => _overlay.Show(button, 0, max, _session.Sets[_set].Pool.Get(type), _config.countPxPerStep, FormatCount);
            gesture.OnSlide = v => _overlay.Update(v);
            gesture.OnCommit = v =>
            {
                AddResult result;
                TrayChange change = _session.SetDieCount(_set, type, v, out result);
                if (result != AddResult.Added) _blocked(type, result);
                _apply(change);
            };
            gesture.OnEnd = () => _overlay.Hide();
            return button;
        }

        private static string FormatCount(int v) { return "x" + v; }

        // ------------------------------------------------------------------ show state

        /// <summary>Flashes the count badge of a die type that hit a limit.</summary>
        public void FlashBadge(DieType type)
        {
            VisualElement badge = _badge[type];
            badge.style.display = DisplayStyle.Flex;
            badge.AddToClassList(FlashClass);
            badge.schedule.Execute(() => badge.RemoveFromClassList(FlashClass)).StartingIn(UiFactory.Millis(_config.badgeFlashSeconds));
        }

        public void Refresh()
        {
            RollSetConfig set = _session.Sets[_set];
            SetResult result = _session.GetResult(_set);

            int min, max;
            _session.GetRange(_set, out min, out max);
            string caption = _config.sets[_set].caption;
            _caption.text = set.Pool.IsEmpty ? caption : string.Format(_config.rangeCaptionFormat, caption, min, max);

            _total.text = result != null ? result.Total.ToString() : "-";
            _total.EnableInClassList("total--nat20", result != null && result.HasNat20);
            _total.EnableInClassList("total--nat1", result != null && result.HasNat1);
            _breakdown.text = RollFormat.Breakdown(result);

            Header.EnableInClassList("set-header--off", !set.Enabled);
            _switch.EnableInClassList("switch--on", set.Enabled);

            _advButton.EnableInClassList("adv-btn--a", set.Advantage == AdvantageMode.Advantage);
            _advButton.EnableInClassList("adv-btn--d", set.Advantage == AdvantageMode.Disadvantage);
            string tag = RollFormat.AdvantageTag(set.Advantage);
            _advIcon.SetText(tag.Length == 0 ? DieInfo.Sides(DieType.D20).ToString() : tag);

            _modLabel.text = RollFormat.Modifier(set.Modifier);

            for (int i = 0; i < DieInfo.TypeCount; i++)
            {
                DieType t = DieInfo.AllTypes[i];
                int n = set.Pool.Get(t);
                _badge[t].style.display = n > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _badgeText[t].text = n.ToString();
            }
        }
    }
}
