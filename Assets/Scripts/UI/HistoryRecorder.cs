using System;
using UnityEngine.UIElements;
using DiceThrower.Core;

namespace DiceThrower.UI
{
    /// <summary>
    /// Writes throws to the history. A throw is recorded a moment after its dice lie still (<see cref="Settled"/> starts that wait),
    /// so quick taps on several dice become one entry. Flush records the pending throw now (before a tab switch, clear or app pause);
    /// a throw whose dice still roll stays pending until they settle. Discard drops a throw that is replaced before it finished.
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
            StopTimer(); // waits for the dice to settle
        }

        /// <summary>All dice lie still: the pending throw is recorded after the short wait.</summary>
        public void Settled()
        {
            if (!_pending) return;
            StopTimer();
            _timer = _host.schedule.Execute(Flush).StartingIn(UiFactory.Millis(_delaySeconds));
        }

        /// <summary>Drops the pending throw without recording it.</summary>
        public void Discard()
        {
            _pending = false;
            _presetId = null;
            StopTimer();
        }

        /// <summary>Records the pending throw (whatever is rolled right now), unless its dice are still rolling.</summary>
        public void Flush()
        {
            if (!_pending || _session.HasPendingResult) return;
            _pending = false;
            StopTimer();
            ThrowResult result = _session.GetThrow();
            string presetId = _presetId;
            _presetId = null;
            if (result == null) return;
            _data.AddHistory(result, _unix, presetId);
            if (Recorded != null) Recorded();
        }

        private void StopTimer()
        {
            if (_timer == null) return;
            _timer.Pause();
            _timer = null;
        }
    }
}
