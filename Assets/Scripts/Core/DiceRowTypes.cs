using System;
using System.Collections.Generic;

namespace DiceThrower.Core
{
    /// <summary>
    /// The die types a dice row offers, in the order they are shown. Only the Attack row has such a list (the Damage row always
    /// offers every type). It is one global app setting: presets never store it. Add and Remove raise <see cref="Changed"/>
    /// so the owner can save.
    /// </summary>
    public sealed class DiceRowTypes
    {
        private readonly List<DieType> _types = new List<DieType>(DieInfo.DefaultAttackTypes);

        /// <summary>Raised after <see cref="Add"/> or <see cref="Remove"/> changed the list.</summary>
        public event Action Changed;

        public IReadOnlyList<DieType> Types { get { return _types; } }

        public bool Contains(DieType type) { return _types.Contains(type); }

        /// <summary>Appends a type. False when it is already in the list.</summary>
        public bool Add(DieType type)
        {
            if (_types.Contains(type)) return false;
            _types.Add(type);
            if (Changed != null) Changed();
            return true;
        }

        /// <summary>Removes a type; any type may go, d20 too. False when it was not in the list.</summary>
        public bool Remove(DieType type)
        {
            if (!_types.Remove(type)) return false;
            if (Changed != null) Changed();
            return true;
        }

        /// <summary>Replaces the list (reading a save). Undefined values and repeats are dropped. Raises no event.</summary>
        public void SetAll(IEnumerable<DieType> types)
        {
            var wanted = new List<DieType>();
            foreach (DieType t in types)
                if (Enum.IsDefined(typeof(DieType), t) && !wanted.Contains(t)) wanted.Add(t);
            _types.Clear();
            _types.AddRange(wanted);
        }

        /// <summary>
        /// The types a row shows for a pool: the row's list in its order, then (in die order) any other type the pool holds,
        /// so a loaded setup is never hidden. <paramref name="row"/> null = every type, in die order (the Damage row).
        /// <paramref name="into"/> is cleared first.
        /// </summary>
        public static void Shown(DiceRowTypes row, DicePool pool, List<DieType> into)
        {
            into.Clear();
            if (row == null)
            {
                into.AddRange(DieInfo.AllTypes);
                return;
            }
            into.AddRange(row._types);
            for (int i = 0; i < DieInfo.TypeCount; i++)
            {
                DieType t = DieInfo.AllTypes[i];
                if (pool.Get(t) > 0 && !into.Contains(t)) into.Add(t);
            }
        }

        /// <summary>The types that are not in <paramref name="shown"/>, in die order: what the "+" picker offers. <paramref name="into"/> is cleared first.</summary>
        public static void Missing(List<DieType> shown, List<DieType> into)
        {
            into.Clear();
            for (int i = 0; i < DieInfo.TypeCount; i++)
            {
                DieType t = DieInfo.AllTypes[i];
                if (!shown.Contains(t)) into.Add(t);
            }
        }
    }
}
