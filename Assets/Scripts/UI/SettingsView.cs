using System;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Core;

namespace DiceThrower.UI
{
    /// <summary>The Settings tab: sound and haptics switches, the dice skin and a clear-history button. Every change is saved at once.</summary>
    public sealed class SettingsView
    {
        private const string SwitchOnClass = "switch--on";
        private const string DisabledClass = "action--disabled";

        private readonly DataUiConfig _cfg;
        private readonly AppData _data;
        private readonly DialogView _dialog;
        private readonly Action _onSkinChanged;
        private readonly Action<string> _toast;
        private readonly VisualElement _soundSwitch, _hapticsSwitch, _clearButton;
        private readonly Label _skinValue;

        public SettingsView(VisualElement root, DataUiConfig cfg, AppData data, DialogView dialog, Action onSkinChanged, Action<string> toast)
        {
            _cfg = cfg;
            _data = data;
            _dialog = dialog;
            _onSkinChanged = onSkinChanged;
            _toast = toast;

            _soundSwitch = root.Q<VisualElement>("switch-sound");
            _hapticsSwitch = root.Q<VisualElement>("switch-haptics");
            _clearButton = root.Q<VisualElement>("btn-clear-history");
            _skinValue = root.Q<Label>("skin-value");

            PressGesture.Tappable(root.Q<VisualElement>("row-sound"), () => { _data.Settings.Sound = !_data.Settings.Sound; Changed(); });
            PressGesture.Tappable(root.Q<VisualElement>("row-haptics"), () => { _data.Settings.Haptics = !_data.Settings.Haptics; Changed(); });
            PressGesture.Tappable(root.Q<VisualElement>("row-skin"), NextSkin);
            PressGesture.Tappable(_clearButton, AskClearHistory);
        }

        public void Refresh()
        {
            _soundSwitch.EnableInClassList(SwitchOnClass, _data.Settings.Sound);
            _hapticsSwitch.EnableInClassList(SwitchOnClass, _data.Settings.Haptics);
            _skinValue.text = _cfg.skinSets[SkinIndex()].name;
            _clearButton.EnableInClassList(DisabledClass, _data.History.Count == 0);
        }

        private int SkinIndex()
        {
            return Mathf.Clamp(_data.Settings.SkinSet, 0, _cfg.skinSets.Length - 1);
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
