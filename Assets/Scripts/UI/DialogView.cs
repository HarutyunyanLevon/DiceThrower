using System;
using UnityEngine.UIElements;

namespace DiceThrower.UI
{
    /// <summary>
    /// The app's confirmation modal (UXML element "dialog"): a title, a message and Cancel / OK.
    /// Used to confirm deletions. Naming and editing presets is the PresetEditorView's job.
    /// </summary>
    public sealed class DialogView
    {
        private const string OpenClass = "dialog--open";

        private readonly VisualElement _root;
        private readonly Label _title, _message, _okText;
        private Action _onOk;

        public DialogView(VisualElement uiRoot, DataUiConfig cfg)
        {
            _root = uiRoot.Q<VisualElement>("dialog");
            _title = uiRoot.Q<Label>("dialog-title");
            _message = uiRoot.Q<Label>("dialog-message");
            _okText = uiRoot.Q<Label>("dialog-ok-text");
            uiRoot.Q<Label>("dialog-cancel-text").text = cfg.cancelText;

            PressGesture.Tappable(uiRoot.Q<VisualElement>("dialog-cancel"), Close);
            PressGesture.Tappable(uiRoot.Q<VisualElement>("dialog-ok"), Submit);
        }

        public void Confirm(string title, string message, string okText, Action onOk)
        {
            _title.text = title;
            _message.text = message;
            _okText.text = okText;
            _onOk = onOk;
            _root.AddToClassList(OpenClass);
        }

        private void Submit()
        {
            Action onOk = _onOk;
            Close();
            if (onOk != null) onOk();
        }

        private void Close()
        {
            _root.RemoveFromClassList(OpenClass);
            _onOk = null;
        }
    }
}
