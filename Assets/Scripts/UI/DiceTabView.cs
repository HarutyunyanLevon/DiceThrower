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
        private readonly List<VisualElement> _cardPicture = new List<VisualElement>();
        private readonly List<bool> _cardLive = new List<bool>(); // the card shows a live die (else its d20 icon)
        private readonly List<float> _effectEnd = new List<float>(); // Time.unscaledTime at which a card's previewed effect stops, 0 = none
        private bool[] _drawn;           // scratch: cards already drawn this frame

        private ScrollView _grid;
        private DicePreviewRig _rig;     // the two slots
        private DicePreviewRig _gridRig; // one die per card
        private int _gridCursor;         // where the next round of card redraws starts
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
            _grid = grid;
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
                AddEffectChips(body, index);

                card.Add(body);
                ListTap.Attach(card, () => Select(index), _ui.slideThresholdPx, false);
                grid.Add(card);
                _cards.Add(card);
                _tags.Add(tag);
                _cardPicture.Add(picture);
                _cardLive.Add(false);
                _effectEnd.Add(0f);
            }
            _drawn = new bool[_cards.Count];
        }

        /// <summary>Two small chips in the card's corner (nat 20, nat 1) for a set that has those effects. Tapping one plays it on the card's die.</summary>
        private void AddEffectChips(VisualElement body, int index)
        {
            DiceFxEffects effects = _cfg.SkinOf(index) != null ? _cfg.SkinOf(index).Effects : null;
            if (effects == null || (!effects.hasNat20 && !effects.hasNat1)) return;

            VisualElement row = new VisualElement();
            row.AddToClassList("dice-card__fx");
            if (effects.hasNat20) row.Add(EffectChip(index, DieNatEffect.Nat20, _cfg.nat20ChipText, "dice-fx-chip--nat20"));
            if (effects.hasNat1) row.Add(EffectChip(index, DieNatEffect.Nat1, _cfg.nat1ChipText, "dice-fx-chip--nat1"));
            body.Add(row);
        }

        private VisualElement EffectChip(int index, DieNatEffect effect, string text, string styleClass)
        {
            VisualElement chip = new VisualElement();
            chip.AddToClassList("dice-fx-chip");
            chip.AddToClassList(styleClass);
            chip.Add(UiFactory.Label(text, "dice-fx-chip__text"));
            ListTap.Attach(chip, () => PlayEffect(index, effect), _ui.slideThresholdPx, true); // consume: the card underneath is not selected
            return chip;
        }

        /// <summary>Plays a nat 20 / nat 1 effect on a card's die; it stops by itself after the config's preview time.</summary>
        private void PlayEffect(int index, DieNatEffect effect)
        {
            if (_gridRig == null || !_cardLive[index]) return;
            DieFxDriver fx = _gridRig.FxOf(index);
            if (fx == null) return;
            fx.SetEffect(effect);
            _effectEnd[index] = Time.unscaledTime + _preview.effectPreviewSeconds;
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
            if (visible && _gridRig == null && _preview != null) BuildGridDice();
        }

        public void Tick(float deltaSeconds)
        {
            if (!_visible) return;
            if (_rig != null)
            {
                _rig.Spin(deltaSeconds);
                for (int s = 0; s < RollSession.SetCount; s++) _rig.Render(s);
            }
            if (_gridRig != null) TickGrid(deltaSeconds);
        }

        public void Dispose()
        {
            if (_rig != null) _rig.Dispose();
            _rig = null;
            if (_gridRig != null) _gridRig.Dispose();
            _gridRig = null;
        }

        // ------------------------------------------------------------------ the dice on the cards

        /// <summary>One live d20 per card, like the slots above. A set without a model keeps its flat d20 icon.</summary>
        private void BuildGridDice()
        {
            _gridRig = new DicePreviewRig(_preview, _cards.Count, _preview.gridTextureSize, false, _preview.gridStageOffset);
            for (int i = 0; i < _cards.Count; i++)
            {
                if (!_gridRig.SetDie(i, _cfg.SkinOf(i), DieKind.D20)) continue;
                VisualElement picture = _cardPicture[i];
                picture.Clear();
                picture.style.backgroundImage = Background.FromRenderTexture(_gridRig.TextureOf(i));
                _cardLive[i] = true;
            }
        }

        /// <summary>
        /// All dice turn together, but only the cards inside the scroll view are drawn, a few per frame in turn
        /// (a card that is not drawn keeps its last picture), so 24 sets cost no more than a screenful.
        /// </summary>
        private void TickGrid(float deltaSeconds)
        {
            _gridRig.Spin(deltaSeconds);

            // previewed effects end by themselves
            float now = Time.unscaledTime;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_effectEnd[i] <= 0f || now < _effectEnd[i]) continue;
                _effectEnd[i] = 0f;
                DieFxDriver ended = _gridRig.FxOf(i);
                if (ended != null) ended.SetEffect(DieNatEffect.None);
            }
            _gridRig.TickEffects(deltaSeconds);

            Rect view = _grid.worldBound;
            int count = _cards.Count;

            // a card that plays an effect is drawn every frame (its particles only move while it is drawn); it does not use the budget
            for (int i = 0; i < count; i++)
            {
                DieFxDriver fx = _cardLive[i] ? _gridRig.FxOf(i) : null;
                _drawn[i] = fx != null && fx.Busy && _cards[i].worldBound.Overlaps(view);
                if (_drawn[i]) _gridRig.Render(i);
            }

            int budget = Mathf.Max(1, _preview.gridRendersPerFrame);
            int start = _gridCursor;
            int last = -1;
            for (int step = 0; step < count && budget > 0; step++)
            {
                int i = (start + step) % count;
                if (_drawn[i] || !_cardLive[i] || !_cards[i].worldBound.Overlaps(view)) continue;
                _gridRig.Render(i);
                last = i;
                budget--;
            }
            if (last >= 0) _gridCursor = (last + 1) % count;
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
