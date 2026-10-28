using System;

namespace DiceThrower.Core
{
    /// <summary>Source of randomness. Swappable so tests can script rolls.</summary>
    public interface IRng
    {
        /// <summary>Uniform integer in [minInclusive, maxInclusive].</summary>
        int Range(int minInclusive, int maxInclusive);
    }

    public sealed class SystemRng : IRng
    {
        private readonly Random _random;

        public SystemRng() : this(Guid.NewGuid().GetHashCode()) { }

        public SystemRng(int seed)
        {
            _random = new Random(seed);
        }

        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive) throw new ArgumentException("max < min");
            return _random.Next(minInclusive, maxInclusive + 1);
        }
    }
}
