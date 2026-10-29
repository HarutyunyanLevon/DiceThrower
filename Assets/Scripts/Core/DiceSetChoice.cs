using System;
using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>
    /// Which dice set (an index into the list of model sets in the UI config) each roll set rolls with.
    /// No two roll sets share a set: choosing the one the other roll set uses swaps the two.
    /// It is a global app setting; presets never store it. <see cref="Assign"/> raises <see cref="Changed"/> so the owner can save.
    /// </summary>
    public sealed class DiceSetChoice
    {
        private readonly int[] _index = new int[RollSession.SetCount];

        /// <summary>Raised after <see cref="Assign"/> changed a choice.</summary>
        public event Action Changed;

        /// <summary>Roll set i starts with dice set i.</summary>
        public DiceSetChoice()
        {
            for (int i = 0; i < _index.Length; i++) _index[i] = i;
        }

        /// <summary>The dice set index the roll set uses.</summary>
        public int Get(int rollSet)
        {
            return _index[rollSet];
        }

        /// <summary>
        /// Gives the roll set a dice set. If another roll set already uses it, that one takes the set this roll set had (a swap).
        /// False when nothing changed.
        /// </summary>
        public bool Assign(int rollSet, int setIndex)
        {
            if (setIndex < 0 || _index[rollSet] == setIndex) return false;
            for (int other = 0; other < _index.Length; other++)
            {
                if (other != rollSet && _index[other] == setIndex) _index[other] = _index[rollSet];
            }
            _index[rollSet] = setIndex;
            if (Changed != null) Changed();
            return true;
        }

        /// <summary>The roll set that uses the dice set, or -1 when none does.</summary>
        public int UsedBy(int setIndex)
        {
            for (int i = 0; i < _index.Length; i++)
                if (_index[i] == setIndex) return i;
            return -1;
        }

        /// <summary>Replaces the choices (reading a save): one per roll set, missing ones keep their start value. Negative values and repeats are corrected. Raises no event.</summary>
        public void SetAll(IList<int> values)
        {
            for (int i = 0; i < _index.Length; i++)
                _index[i] = values != null && i < values.Count ? values[i] : i;
            Fit(int.MaxValue);
        }

        /// <summary>
        /// Keeps every choice inside the <paramref name="available"/> dice sets and distinct (a repeat takes the lowest unused set).
        /// Raises no event. Choices stay shared when there are fewer sets than roll sets.
        /// </summary>
        public void Fit(int available)
        {
            if (available <= 0) return;
            for (int i = 0; i < _index.Length; i++)
                _index[i] = Math.Max(0, Math.Min(available - 1, _index[i]));

            for (int i = 1; i < _index.Length; i++)
            {
                if (!UsedBefore(i, _index[i])) continue;
                for (int candidate = 0; candidate < available; candidate++)
                {
                    if (UsedBefore(i, candidate)) continue;
                    _index[i] = candidate;
                    break;
                }
            }
        }

        private bool UsedBefore(int roll, int setIndex)
        {
            for (int j = 0; j < roll; j++)
                if (_index[j] == setIndex) return true;
            return false;
        }
    }
}
