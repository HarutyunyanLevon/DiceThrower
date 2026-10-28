using System;

namespace DiceThrower.Core
{
    /// <summary>Rule limits. The live values come from the RulesConfig asset; these are the fallbacks.</summary>
    public sealed class DiceLimits
    {
        public int MaxTotal = 20;
        public int MaxPerType = 10;
        public int ModifierMin = -10;
        public int ModifierMax = 10;
        /// <summary>Throws kept in the history; the oldest are dropped.</summary>
        public int HistoryCap = 100;
        /// <summary>Longest preset name.</summary>
        public int PresetNameMax = 24;

        /// <summary>No caps at all: for data that was valid when it was saved. The session applies the real limits when it loads it.</summary>
        public static DiceLimits Unlimited
        {
            get
            {
                return new DiceLimits
                {
                    MaxTotal = int.MaxValue,
                    MaxPerType = int.MaxValue,
                    ModifierMin = int.MinValue,
                    ModifierMax = int.MaxValue,
                    HistoryCap = int.MaxValue,
                    PresetNameMax = int.MaxValue,
                };
            }
        }

        public int ClampModifier(int value)
        {
            return Math.Max(ModifierMin, Math.Min(ModifierMax, value));
        }
    }

    public enum AddResult
    {
        Added,
        BlockedTotal,   // pool already holds MaxTotal dice
        BlockedType,    // that die type already holds MaxPerType dice
    }

    /// <summary>How many dice of each type a roll set holds.</summary>
    public sealed class DicePool
    {
        private readonly int[] _counts = new int[DieInfo.TypeCount];

        public int Get(DieType t) { return _counts[(int)t]; }

        public int Total
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _counts.Length; i++) sum += _counts[i];
                return sum;
            }
        }

        public bool IsEmpty { get { return Total == 0; } }

        /// <summary>Adds one die. Refuses (and changes nothing) when a cap is hit.</summary>
        public AddResult TryAdd(DieType t, DiceLimits limits)
        {
            if (Total >= limits.MaxTotal) return AddResult.BlockedTotal;
            if (_counts[(int)t] >= limits.MaxPerType) return AddResult.BlockedType;
            _counts[(int)t]++;
            return AddResult.Added;
        }

        /// <summary>
        /// Sets the count of one die type (used by the press-and-slide count slider).
        /// Returns the first cap that clamped the request, or Added if it fit exactly.
        /// </summary>
        public AddResult SetCount(DieType t, int count, DiceLimits limits)
        {
            if (count < 0) count = 0;
            AddResult result = AddResult.Added;
            if (count > limits.MaxPerType)
            {
                count = limits.MaxPerType;
                result = AddResult.BlockedType;
            }
            int others = Total - _counts[(int)t];
            if (others + count > limits.MaxTotal)
            {
                count = Math.Max(0, limits.MaxTotal - others);
                result = AddResult.BlockedTotal;
            }
            _counts[(int)t] = count;
            return result;
        }

        public bool Remove(DieType t)
        {
            if (_counts[(int)t] <= 0) return false;
            _counts[(int)t]--;
            return true;
        }

        public void Clear()
        {
            Array.Clear(_counts, 0, _counts.Length);
        }

        public DicePool Clone()
        {
            var p = new DicePool();
            Array.Copy(_counts, p._counts, _counts.Length);
            return p;
        }
    }
}
