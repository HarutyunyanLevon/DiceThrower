using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using DiceThrower.Core;

namespace DiceThrower.UI
{
    /// <summary>
    /// The popup that names a preset and sets its dice and modifiers (UXML element "editor"). It reuses the Roll tab's
    /// SetPanels on a RollSession of its own, so the dice rows, the modifier slider, advantage and the on/off switch work the same
    /// way. Nothing is thrown here. Used both to save the current setup and to edit a saved preset.
    /// </summary>
    public sealed class PresetEditorView
    {
        private const string OpenClass = "dialog--open";
        private const string DisabledClass = "action--disabled";

        private readonly RollSession _session;
        private readonly SetPanel[] _panels = new SetPanel[RollSession.SetCount];
        private readonly VisualElement _root, _ok;
        private readonly Label _title, _okText;
        private readonly TextField _name;
        private Action<string, RollSetConfig[]> _onOk;

        /// <param name="onBlocked">A dice limit was hit (the view flashes the count, the caller adds feedback).</param>
        public PresetEditorView(VisualElement uiRoot, RollUiConfig config, DataUiConfig data, DiceLimits limits, Action onBlocked)
        {
            _root = uiRoot.Q<VisualElement>("editor");
            _title = uiRoot.Q<Label>("editor-title");
            _name = uiRoot.Q<TextField>("editor-name");
            _ok = uiRoot.Q<VisualElement>("editor-ok");
            _okText = uiRoot.Q<Label>("editor-ok-text");
            uiRoot.Q<Label>("editor-cancel-text").text = data.cancelText;
            _name.maxLength = limits.PresetNameMax;

            _session = new RollSession(new SystemRng(), limits);
            var overlay = new SliderOverlay(_root, config.sliderTrackPad); // inside the popup, so it is drawn above it
            VisualElement sets = uiRoot.Q<VisualElement>("editor-sets");
            for (int s = 0; s < _panels.Length; s++)
            {
                int set = s;
                _panels[s] = new SetPanel(s, _session, config, overlay, change => Refresh(), (type, result) =>
                {
                    _panels[set].FlashBadge(type);
                    onBlocked();
                }, Refresh);
                sets.Add(_panels[s].Header);
                sets.Add(_panels[s].Row);
            }

            _name.RegisterValueChangedCallback(e => UpdateOk());
            PressGesture.Tappable(uiRoot.Q<VisualElement>("editor-cancel"), Close);
            PressGesture.Tappable(_ok, Submit);
        }

        /// <param name="sets">The setup to start from; copied, so cancelling changes nothing.</param>
        /// <param name="onOk">Gets the name and the edited sets.</param>
        public void Open(string title, string okText, string name, IList<RollSetConfig> sets, Action<string, RollSetConfig[]> onOk)
        {
            _title.text = title;
            _okText.text = okText;
            _onOk = onOk;
            _session.Configure(sets);
            _name.SetValueWithoutNotify(name);
            Refresh();
            _root.AddToClassList(OpenClass);
        }

        private void Refresh()
        {
            for (int s = 0; s < _panels.Length; s++) _panels[s].Refresh();
            UpdateOk();
        }

        /// <summary>A preset needs a name and at least one die.</summary>
        private bool CanSave { get { return !string.IsNullOrWhiteSpace(_name.value) && _session.HasDice; } }

        private void UpdateOk()
        {
            _ok.EnableInClassList(DisabledClass, !CanSave);
        }

        private void Submit()
        {
            if (!CanSave) return;
            Action<string, RollSetConfig[]> onOk = _onOk;
            string name = _name.value;
            RollSetConfig[] sets = _session.Snapshot();
            Close();
            if (onOk != null) onOk(name, sets);
        }

        private void Close()
        {
            _root.RemoveFromClassList(OpenClass);
            _onOk = null;
            _name.Blur();
        }
    }
}
