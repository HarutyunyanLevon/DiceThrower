using System;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Core;
using DiceThrower.Game;

namespace DiceThrower.UI
{
    /// <summary>The Settings tab: sound and haptics switches, dice skin, colour theme, the chip usage window and a clear-history button. Every change is saved at once.</summary>
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
        private readonly Action<string> _toast;
        private readonly VisualElement _soundSwitch, _hapticsSwitch, _clearButton, _windowMinus, _windowPlus;
        private readonly Label _skinValue, _themeValue, _windowValue;

        public SettingsView(VisualElement root, DataUiConfig cfg, RulesConfig rules, AppData data, ThemeApplier theme,
                            DialogView dialog, Action onSkinChanged, Action<string> toast)
        {
            _cfg = cfg;
            _rules = rules;
            _data = data;
            _theme = theme;
            _dialog = dialog;
            _onSkinChanged = onSkinChanged;
            _toast = toast;

            _soundSwitch = root.Q<VisualElement>("switch-sound");
            _hapticsSwitch = root.Q<VisualElement>("switch-haptics");
            _clearButton = root.Q<VisualElement>("btn-clear-history");
            _windowMinus = root.Q<VisualElement>("window-minus");
            _windowPlus = root.Q<VisualElement>("window-plus");
            _skinValue = root.Q<Label>("skin-value");
            _themeValue = root.Q<Label>("theme-value");
            _windowValue = root.Q<Label>("window-value");

            PressGesture.Tappable(root.Q<VisualElement>("row-sound"), () => { _data.Settings.Sound = !_data.Settings.Sound; Changed(); });
            PressGesture.Tappable(root.Q<VisualElement>("row-haptics"), () => { _data.Settings.Haptics = !_data.Settings.Haptics; Changed(); });
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
