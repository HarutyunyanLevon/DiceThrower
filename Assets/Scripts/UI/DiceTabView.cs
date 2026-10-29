using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Core;
using DiceThrower.Game;

namespace DiceThrower.UI
{
    /// <summary>
    /// The Dice tab. Two slots ("ATTACK DICE SET", "DAMAGE DICE SET") show the sets the roll sets use now, each with a live rotating d20
    /// (one shared preview camera, see <see cref="DicePreviewRig"/>). Below them a two-column grid lists the model dice sets: tap a card,
    /// then press "Use for Attack" or "Use for Damage". A set the other roll set uses is swapped over. Without a preview config, or while a
    /// set has no model, a slot shows the set's d20 sprite (or a plain d20 shape) instead.
    /// </summary>
    public sealed class DiceTabView : IDisposable
    {
        private const string SelectedClass = "dice-card--selected";
        private const string DisabledClass = "action--disabled";

        private readonly DataUiConfig _cfg;
        private readonly RollUiConfig _ui;
        private readonly DicePreviewConfig _preview;
        private readonly DiceSetChoice _choice;
        private readonly Action _onChanged;

        private readonly VisualElement[] _slotPicture = new VisualElement[RollSession.SetCount];
        private readonly Label[] _slotName = new Label[RollSession.SetCount];
        private readonly VisualElement[] _useButton = new VisualElement[RollSession.SetCount];
        private readonly List<VisualElement> _cards = new List<VisualElement>();
        private readonly List<Label> _tags = new List<Label>();

        private DicePreviewRig _rig;
        private bool _visible;
        private int _selected = -1; // the card that is tapped, or -1

        /// <param name="preview">Look of the live dice; null shows still pictures only.</param>
        /// <param name="choice">The dice set of each roll set (the app setting).</param>
        /// <param name="onChanged">Called after a roll set got another dice set (the owner applies it and shows a toast).</param>
        public DiceTabView(VisualElement root, DataUiConfig cfg, RollUiConfig ui, DicePreviewConfig preview, DiceSetChoice choice, Action onChanged)
        {
            _cfg = cfg;
            _ui = ui;
            _preview = preview;
            _choice = choice;
            _onChanged = onChanged;

            BuildSlots(root.Q<VisualElement>("dice-slots"));
            BuildCards(root.Q<ScrollView>("dice-grid"));
            BuildButtons(root.Q<VisualElement>("dice-actions"));
        }

        // ------------------------------------------------------------------ build

        private void BuildSlots(VisualElement parent)
        {
            for (int s = 0; s < RollSession.SetCount; s++)
            {
                RollSetSpec spec = _ui.sets[s];
                VisualElement slot = UiFactory.Column("dice-slot", spec.rowClass);
                slot.Add(UiFactory.Label(string.Format(_cfg.diceSlotFormat, spec.caption), "dice-slot__caption"));
                _slotPicture[s] = UiFactory.Column("dice-slot__picture");
                slot.Add(_slotPicture[s]);
                _slotName[s] = UiFactory.Label("", "dice-slot__name");
                slot.Add(_slotName[s]);
                parent.Add(slot);
            }
        }

        private void BuildCards(ScrollView grid)
        {
            for (int i = 0; i < _cfg.diceSets.Length; i++)
            {
                int index = i;
                VisualElement card = new VisualElement();
                card.AddToClassList("dice-card");

                VisualElement body = UiFactory.Column("dice-card__body");
                VisualElement picture = UiFactory.Column("dice-card__picture");
                picture.Add(DieIcon.ForType(DieType.D20));
                body.Add(picture);
                body.Add(UiFactory.Label(_cfg.diceSets[i].name, "dice-card__name"));

                Label tag = UiFactory.Label("", "dice-card__tag");
                tag.style.display = DisplayStyle.None;
                body.Add(tag);

                card.Add(body);
                ListTap.Attach(card, () => Select(index), _ui.slideThresholdPx, false);
                grid.Add(card);
                _cards.Add(card);
                _tags.Add(tag);
            }
        }

        private void BuildButtons(VisualElement bar)
        {
            for (int s = 0; s < RollSession.SetCount; s++)
            {
                int set = s;
                RollSetSpec spec = _ui.sets[s];
                VisualElement button = new VisualElement();
                button.AddToClassList("action");
                button.AddToClassList("dice-use");
                button.AddToClassList(spec.rowClass);
                button.Add(UiFactory.Label(string.Format(_cfg.useDiceSetFormat, Capitalize(spec.caption)), "dice-use__text"));
                PressGesture.Tappable(button, () => Use(set));
                bar.Add(button);
                _useButton[s] = button;
            }
        }

        // ------------------------------------------------------------------ actions

        private void Select(int index)
        {
            _selected = index;
            Refresh();
        }

        private void Use(int rollSet)
        {
            if (_selected < 0 || !_choice.Assign(rollSet, _selected)) return;
            Refresh();
            _onChanged();
        }

        // ------------------------------------------------------------------ show

        /// <summary>Redraws the slots, the card marks and the buttons. Call when the tab is opened.</summary>
        public void Refresh()
        {
            for (int s = 0; s < RollSession.SetCount; s++)
            {
                _slotName[s].text = _cfg.diceSets[Mathf.Clamp(_choice.Get(s), 0, _cfg.diceSets.Length - 1)].name;
                UpdatePicture(s);
                _useButton[s].EnableInClassList(DisabledClass, _selected < 0 || _choice.Get(s) == _selected);
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].EnableInClassList(SelectedClass, i == _selected);
                int user = _choice.UsedBy(i);
                Label tag = _tags[i];
                tag.style.display = user >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                tag.text = user >= 0 ? _ui.sets[user].caption : "";
                for (int s = 0; s < RollSession.SetCount; s++) tag.EnableInClassList(_ui.sets[s].rowClass, s == user);
            }
        }

        /// <summary>The live dice only turn and are drawn while the tab is on screen.</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible && _rig == null && _preview != null)
                _rig = new DicePreviewRig(_preview, RollSession.SetCount, _preview.textureSize, false);
        }

        public void Tick(float deltaSeconds)
        {
            if (!_visible || _rig == null) return;
            _rig.Spin(deltaSeconds);
            for (int s = 0; s < RollSession.SetCount; s++) _rig.Render(s);
        }

        public void Dispose()
        {
            if (_rig != null) _rig.Dispose();
            _rig = null;
        }

        private void UpdatePicture(int rollSet)
        {
            DiceSkin skin = _cfg.SkinOf(_choice.Get(rollSet));
            VisualElement picture = _slotPicture[rollSet];
            picture.Clear();

            bool live = _rig != null && (_rig.Shows(rollSet, skin, DieKind.D20) || _rig.SetDie(rollSet, skin, DieKind.D20));
            if (live)
            {
                picture.style.backgroundImage = Background.FromRenderTexture(_rig.TextureOf(rollSet));
                return;
            }
            picture.style.backgroundImage = StyleKeyword.Null;
            picture.Add(DieIcon.ForType(DieType.D20));
        }

        private static string Capitalize(string text)
        {
            return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1).ToLowerInvariant();
        }
    }
}
