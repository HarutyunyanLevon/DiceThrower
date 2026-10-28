using System;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Core;
using DiceThrower.Game;

namespace DiceThrower.UI
{
    /// <summary>The Settings tab: sound and haptics switches, physics dice and settle time, dice skin, colour theme, the chip usage window and a clear-history button. Every change is saved at once.</summary>
    public sealed class SettingsView
    {
        private const string SwitchOnClass = "switch--on";
        private const string DisabledClass = "action--disabled";
        private const string StepperDisabledClass = "stepper__btn--disabled";

        private readonly DataUiConfig _cfg;
        private readonly RulesConfig _rules;
        private readonly AppData _data;
        private readonly ThemeApplier _theme;
        private readonly DialogView _dialog;
        private readonly Action _onSkinChanged;
        private readonly Action _onThrowChanged;
        private readonly Action<string> _toast;
        private readonly VisualElement _soundSwitch, _hapticsSwitch, _clearButton, _windowMinus, _windowPlus, _physicsSwitch, _settleMinus, _settlePlus;
        private readonly Label _skinValue, _themeValue, _windowValue, _settleValue;

        public SettingsView(VisualElement root, DataUiConfig cfg, RulesConfig rules, AppData data, ThemeApplier theme,
                            DialogView dialog, Action onSkinChanged, Action onThrowChanged, Action<string> toast)
        {
            _cfg = cfg;
            _rules = rules;
            _data = data;
            _theme = theme;
            _dialog = dialog;
            _onSkinChanged = onSkinChanged;
            _onThrowChanged = onThrowChanged;
            _toast = toast;

            _soundSwitch = root.Q<VisualElement>("switch-sound");
            _hapticsSwitch = root.Q<VisualElement>("switch-haptics");
            _physicsSwitch = root.Q<VisualElement>("switch-physics");
            _settleMinus = root.Q<VisualElement>("settle-minus");
            _settlePlus = root.Q<VisualElement>("settle-plus");
            _settleValue = root.Q<Label>("settle-value");
            _clearButton = root.Q<VisualElement>("btn-clear-history");
            _windowMinus = root.Q<VisualElement>("window-minus");
            _windowPlus = root.Q<VisualElement>("window-plus");
            _skinValue = root.Q<Label>("skin-value");
            _themeValue = root.Q<Label>("theme-value");
            _windowValue = root.Q<Label>("window-value");

            PressGesture.Tappable(root.Q<VisualElement>("row-sound"), () => { _data.Settings.Sound = !_data.Settings.Sound; Changed(); });
            PressGesture.Tappable(root.Q<VisualElement>("row-haptics"), () => { _data.Settings.Haptics = !_data.Settings.Haptics; Changed(); });
            PressGesture.Tappable(root.Q<VisualElement>("row-physics"), () => { _data.Settings.PhysicsDice = !_data.Settings.PhysicsDice; Changed(); _onThrowChanged(); });
            PressGesture.Tappable(_settleMinus, () => StepSettle(-_rules.settleSecondsStep));
            PressGesture.Tappable(_settlePlus, () => StepSettle(_rules.settleSecondsStep));
            PressGesture.Tappable(root.Q<VisualElement>("row-skin"), NextSkin);
            PressGesture.Tappable(root.Q<VisualElement>("row-theme"), NextTheme);
            PressGesture.Tappable(_windowMinus, () => StepWindow(-_rules.usageWindowStep));
            PressGesture.Tappable(_windowPlus, () => StepWindow(_rules.usageWindowStep));
            PressGesture.Tappable(_clearButton, AskClearHistory);
        }

        public void Refresh()
        {
            int window = Window();
            _soundSwitch.EnableInClassList(SwitchOnClass, _data.Settings.Sound);
            _hapticsSwitch.EnableInClassList(SwitchOnClass, _data.Settings.Haptics);
            int settle = SettleSeconds();
            _physicsSwitch.EnableInClassList(SwitchOnClass, _data.Settings.PhysicsDice);
            _settleValue.text = string.Format(_cfg.settleSecondsFormat, settle);
            _settleMinus.EnableInClassList(StepperDisabledClass, !_data.Settings.PhysicsDice || settle <= _rules.settleSecondsMin);
            _settlePlus.EnableInClassList(StepperDisabledClass, !_data.Settings.PhysicsDice || settle >= _rules.settleSecondsMax);
            _skinValue.text = _cfg.skinSets[SkinIndex()].name;
            _themeValue.text = _theme.NameOf(_data.Settings.Theme);
            _windowValue.text = string.Format(_cfg.usageWindowFormat, window);
            _windowMinus.EnableInClassList(StepperDisabledClass, window <= _rules.usageWindowMin);
            _windowPlus.EnableInClassList(StepperDisabledClass, window >= _rules.usageWindowMax);
            _clearButton.EnableInClassList(DisabledClass, _data.History.Count == 0);
        }

        private int SkinIndex()
        {
            return Mathf.Clamp(_data.Settings.SkinSet, 0, _cfg.skinSets.Length - 1);
        }

        /// <summary>The saved window, or the config default, kept inside the allowed range.</summary>
        private int Window()
        {
            return Mathf.Clamp(_data.Settings.UsageWindowOr(_rules.usageWindow), _rules.usageWindowMin, _rules.usageWindowMax);
        }

        /// <summary>The saved settle time, or the config default, kept inside the allowed range.</summary>
        private int SettleSeconds()
        {
            return Mathf.Clamp(_data.Settings.SettleSecondsOr(_rules.settleSeconds), _rules.settleSecondsMin, _rules.settleSecondsMax);
        }

        private void StepSettle(int delta)
        {
            if (!_data.Settings.PhysicsDice) return;
            _data.Settings.SettleSeconds = Mathf.Clamp(SettleSeconds() + delta, _rules.settleSecondsMin, _rules.settleSecondsMax);
            Changed();
            _onThrowChanged();
        }

        private void Changed()
        {
            _data.Save();
            Refresh();
        }

        private void NextSkin()
        {
            _data.Settings.SkinSet = (SkinIndex() + 1) % _cfg.skinSets.Length;
            Changed();
            _onSkinChanged();
        }

        private void NextTheme()
        {
            _data.Settings.Theme = (_theme.Clamp(_data.Settings.Theme) + 1) % _theme.Count;
            _theme.Apply(_data.Settings.Theme);
            Changed();
        }

        private void StepWindow(int delta)
        {
            _data.Settings.UsageWindow = Mathf.Clamp(Window() + delta, _rules.usageWindowMin, _rules.usageWindowMax);
            Changed();
        }

        private void AskClearHistory()
        {
            if (_data.History.Count == 0) return;
            _dialog.Confirm(_cfg.clearHistoryTitle, _cfg.clearHistoryMessage, _cfg.clearHistoryOk, () =>
            {
                _data.ClearHistory();
                Refresh();
                _toast(_cfg.historyClearedToast);
            });
        }
    }
}
