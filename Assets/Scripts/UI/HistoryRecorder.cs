using System;
using UnityEngine.UIElements;
using DiceThrower.Core;

namespace DiceThrower.UI
{
    /// <summary>
    /// Writes throws to the history. A throw waits a moment before it is recorded, so quick taps on several dice
    /// become one entry. Flush records the pending throw now (before a new throw, a tab switch, clear or app pause).
    /// </summary>
    public sealed class HistoryRecorder
    {
        private readonly VisualElement _host;
        private readonly RollSession _session;
        private readonly AppData _data;
        private readonly float _delaySeconds;

        private IVisualElementScheduledItem _timer;
        private bool _pending;
        private long _unix;
        private string _presetId;

        /// <summary>Raised after a throw was written to the history.</summary>
        public event Action Recorded;

        public HistoryRecorder(VisualElement scheduleHost, RollSession session, AppData data, float delaySeconds)
        {
            _host = scheduleHost;
            _session = session;
            _data = data;
            _delaySeconds = delaySeconds;
        }

        /// <param name="presetId">The preset the throw came from, or null. Dice added to a pending throw keep its preset.</param>
        public void Queue(string presetId = null)
        {
            if (!_pending) _presetId = presetId;
            _pending = true;
            _unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_timer != null) _timer.Pause();
            _timer = _host.schedule.Execute(Flush).StartingIn(UiFactory.Millis(_delaySeconds));
        }

        /// <summary>Records the pending throw (whatever is rolled right now).</summary>
        public void Flush()
        {
            if (!_pending) return;
            _pending = false;
            if (_timer != null)
            {
                _timer.Pause();
                _timer = null;
            }
            ThrowResult result = _session.GetThrow();
            string presetId = _presetId;
            _presetId = null;
            if (result == null) return;
            _data.AddHistory(result, _unix, presetId);
            if (Recorded != null) Recorded();
        }
    }
}
