namespace DiceThrower.Core
{
    /// <summary>Lets an event through at most once per interval, so a pile of dice hitting at once is not a wall of noise.</summary>
    public sealed class Throttle
    {
        private readonly float _intervalSeconds;
        private float _nextAllowed = float.NegativeInfinity;

        public Throttle(float intervalSeconds)
        {
            _intervalSeconds = intervalSeconds;
        }

        /// <summary>True (and starts the wait) when the event may happen at time <paramref name="now"/> in seconds.</summary>
        public bool TryPass(float now)
        {
            if (now < _nextAllowed) return false;
            _nextAllowed = now + _intervalSeconds;
            return true;
        }
    }
}
