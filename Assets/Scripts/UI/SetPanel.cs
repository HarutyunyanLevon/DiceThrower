using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using DiceThrower.Core;
using DiceThrower.Game;

namespace DiceThrower.UI
{
    /// <summary>
    /// The Roll tab's controls for one roll set: the header (caption with the range, formula, total, on/off switch, advantage, modifier, hide,
    /// and the breakdown under them) and the dice row. Gestures turn into RollSession operations; the resulting TrayChange goes out through the callbacks.
    /// A set with a <see cref="DiceRowTypes"/> list (the Attack row) offers only those die types plus a "+" button that opens the picker;
    /// a long press on a type removes it. Without a list (the Damage row) every type is offered.
    /// The die buttons are flat silhouettes of the die shape in the set's colour (attack or damage), drawn by DieIcon.
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
        private readonly DiceRowTypes _rowTypes;
        private readonly DiePicker _picker;
        private readonly Func<DiceSkin> _skinOf;
        private readonly Action<TrayChange> _apply;
        private readonly Action<DieType, AddResult> _blocked;
        private readonly Action _refreshAll;

        private VisualElement _switch, _advButton, _modPill;
        private Label _caption, _formula, _total, _breakdown, _modLabel;
        private DieIcon _advIcon;
        private LineIcon _chevron;
        private bool _rowHidden;
        private DiceSkin _rowSkin; // the dice set the row was built for
        private readonly List<DieType> _shown = new List<DieType>();   // the die types the row is built for
        private readonly List<DieType> _wanted = new List<DieType>();  // scratch: the types the row should show now
        private readonly List<DieType> _missing = new List<DieType>(); // scratch: the types the picker offers
        private readonly Dictionary<DieType, VisualElement> _badge = new Dictionary<DieType, VisualElement>();
        private readonly Dictionary<DieType, Label> _badgeText = new Dictionary<DieType, Label>();

        /// <param name="rowTypes">The die types this row offers, or null for every type.</param>
        /// <param name="picker">The "+" popover; required when <paramref name="rowTypes"/> is given, ignored otherwise.</param>
        /// <param name="skinOf">The dice set this roll set uses right now. Its atlas gives the die buttons' pictures.</param>
        /// <param name="apply">Applies a change to the tray (and refreshes the screen).</param>
        /// <param name="blocked">Reports a die that could not be added.</param>
        /// <param name="refreshAll">Redraws every set; used while the modifier slider moves and after the die type list changed.</param>
        public SetPanel(int set, RollSession session, RollUiConfig config, SliderOverlay overlay, DiceRowTypes rowTypes, DiePicker picker,
                        Func<DiceSkin> skinOf, Action<TrayChange> apply, Action<DieType, AddResult> blocked, Action refreshAll)
        {
            _set = set;
            _session = session;
            _config = config;
            _overlay = overlay;
            _rowTypes = rowTypes;
            _picker = picker;
            _skinOf = skinOf;
            _apply = apply;
            _blocked = blocked;
            _refreshAll = refreshAll;

            BuildHeader();
            SyncShown();
            _rowSkin = _skinOf();
            BuildRow();
        }

        // ------------------------------------------------------------------ build

        private void BuildHeader()
        {
            Header.AddToClassList("set-header");

            _caption = UiFactory.Label("", "caption");
            _formula = UiFactory.Label("", "formula");
            VisualElement textBlock = UiFactory.Column("set-header__text");
            textBlock.Add(_caption);
            textBlock.Add(_formula);

            _total = UiFactory.Label("", "total");

            VisualElement top = UiFactory.Column("set-header__top");
            top.Add(textBlock);
            top.Add(_total);

            _switch = new VisualElement();
            _switch.AddToClassList("switch");
            _switch.Add(UiFactory.Column("switch__knob"));
            PressGesture.Tappable(_switch, () => _apply(_session.SetEnabled(_set, !_session.Sets[_set].Enabled)));
            top.Add(_switch);

            _advButton = new VisualElement();
            _advButton.AddToClassList("adv-btn");
            _advIcon = DieIcon.ForType(DieType.D20);
            _advButton.Add(_advIcon);
            PressGesture.Tappable(_advButton, () => _apply(_session.CycleAdvantage(_set)));
            top.Add(_advButton);

            BuildModifier(top);
            BuildHideButton(top);
            Header.Add(top);

            _breakdown = UiFactory.Label("", "breakdown");
            Header.Add(_breakdown);
        }

        private void BuildModifier(VisualElement parent)
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
            parent.Add(_modPill);
        }

        private void BuildHideButton(VisualElement parent)
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
            parent.Add(button);
        }

        private void BuildRow()
        {
            Row.AddToClassList("dice-row");
            Row.AddToClassList(_config.sets[_set].rowClass);
            RebuildRow();
        }

        /// <summary>(Re)creates the bar, one button per shown die type and, on a row with a type list, the "+" button.</summary>
        private void RebuildRow()
        {
            Row.Clear();
            _badge.Clear();
            _badgeText.Clear();
            Row.Add(UiFactory.Column("dice-row__bar"));
            for (int i = 0; i < _shown.Count; i++) Row.Add(BuildDieButton(_shown[i]));
            if (_rowTypes != null) Row.Add(BuildAddButton());
        }

        /// <summary>Works out which die types the row should show. True when that differs from what the row was built for.</summary>
        private bool SyncShown()
        {
            DiceRowTypes.Shown(_rowTypes, _session.Sets[_set].Pool, _wanted);
            if (SameTypes(_wanted, _shown)) return false;
            _shown.Clear();
            _shown.AddRange(_wanted);
            return true;
        }

        private static bool SameTypes(List<DieType> a, List<DieType> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private VisualElement BuildDieButton(DieType type)
        {
            int max = _session.Limits.MaxPerType;
            var button = new VisualElement();
            button.AddToClassList("die-btn");
            button.Add(DieIcon.ForType(type)); // flat silhouette in the set's colour, not a picture of the 3D die

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
            if (_rowTypes != null)
            {
                gesture.LongPressMillis = UiFactory.Millis(_config.longPressSeconds);
                gesture.OnLongPress = () => RemoveType(type);
            }
            return button;
        }

        private VisualElement BuildAddButton()
        {
            var button = new VisualElement();
            button.AddToClassList("die-btn");
            button.AddToClassList("die-btn--add");
            var plus = new LineIcon(LineIcon.Plus);
            plus.AddToClassList("die-btn__plus");
            button.Add(plus);
            PressGesture.Tappable(button, () => OpenPicker(button));

            DiceRowTypes.Missing(_shown, _missing);
            button.style.display = _missing.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None; // every type is on the row already
            return button;
        }

        private void OpenPicker(VisualElement anchor)
        {
            DiceRowTypes.Missing(_shown, _missing);
            _picker.Show(anchor, _missing, _config.sets[_set].rowClass, _rowSkin, type =>
            {
                _rowTypes.Add(type); // saved by the owner of the list
                _refreshAll();
            });
        }

        /// <summary>Long press on a die type: it leaves the row's list, and its dice leave the set.</summary>
        private void RemoveType(DieType type)
        {
            AddResult result;
            TrayChange change = _session.SetDieCount(_set, type, 0, out result);
            _rowTypes.Remove(type);
            _apply(change); // redraws, which rebuilds the row without the type
        }

        private static string FormatCount(int v) { return "x" + v; }

        // ------------------------------------------------------------------ show state

        /// <summary>Flashes the count badge of a die type that hit a limit.</summary>
        public void FlashBadge(DieType type)
        {
            VisualElement badge;
            if (!_badge.TryGetValue(type, out badge)) return;
            badge.style.display = DisplayStyle.Flex;
            badge.AddToClassList(FlashClass);
            badge.schedule.Execute(() => badge.RemoveFromClassList(FlashClass)).StartingIn(UiFactory.Millis(_config.badgeFlashSeconds));
        }

        public void Refresh()
        {
            bool rebuild = SyncShown(); // a setup was loaded, or the type list changed
            DiceSkin skin = _skinOf();
            if (skin != _rowSkin) // another dice set: other pictures
            {
                _rowSkin = skin;
                rebuild = true;
            }
            if (rebuild) RebuildRow();

            RollSetConfig set = _session.Sets[_set];
            SetResult result = _session.GetResult(_set);

            int min, max;
            _session.GetRange(_set, out min, out max);
            string caption = _config.sets[_set].caption;
            _caption.text = set.Pool.IsEmpty ? caption : string.Format(_config.captionRangeFormat, caption, min, max);
            _formula.text = RollFormat.Formula(set);

            _total.text = result != null ? result.Total.ToString() : "-";
            _total.EnableInClassList("total--nat20", result != null && result.HasNat20);
            _total.EnableInClassList("total--nat1", result != null && result.HasNat1);
            _breakdown.text = RollFormat.Breakdown(result, _config.breakdownMaxChars);

            Header.EnableInClassList("set-header--off", !set.Enabled);
            _switch.EnableInClassList("switch--on", set.Enabled);

            _advButton.EnableInClassList("adv-btn--a", set.Advantage == AdvantageMode.Advantage);
            _advButton.EnableInClassList("adv-btn--d", set.Advantage == AdvantageMode.Disadvantage);
            string tag = RollFormat.AdvantageTag(set.Advantage);
            _advIcon.SetText(tag.Length == 0 ? DieInfo.Sides(DieType.D20).ToString() : tag);

            _modLabel.text = RollFormat.Modifier(set.Modifier);

            for (int i = 0; i < _shown.Count; i++)
            {
                DieType t = _shown[i];
                int n = set.Pool.Get(t);
                _badge[t].style.display = n > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _badgeText[t].text = n.ToString();
            }
        }
    }
}
