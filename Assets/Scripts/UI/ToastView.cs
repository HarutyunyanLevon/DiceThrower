using UnityEngine.UIElements;

namespace DiceThrower.UI
{
    /// <summary>The short message that fades in over every tab. Shows for a configured time, a new message replaces the old one.</summary>
    public sealed class ToastView
    {
        private const string ShowClass = "toast--show";

        private readonly Label _label;
        private readonly float _seconds;
        private IVisualElementScheduledItem _hide;

        public ToastView(Label label, float seconds)
        {
            _label = label;
            _label.pickingMode = PickingMode.Ignore;
            _seconds = seconds;
        }

        public void Show(string message)
        {
            _label.text = message;
            _label.AddToClassList(ShowClass);
            if (_hide != null) _hide.Pause();
            _hide = _label.schedule.Execute(() => _label.RemoveFromClassList(ShowClass)).StartingIn(UiFactory.Millis(_seconds));
        }
    }
}
