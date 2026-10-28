using System;
using System.Collections.Generic;

namespace DiceThrower.Core
{
    public enum AdvantageMode
    {
        Normal = 0,
        Advantage = 1,
        Disadvantage = 2,
    }

    /// <summary>One roll set (e.g. Attack or Damage): its dice, modifier and advantage mode.</summary>
    public sealed class RollSetConfig
    {
        public readonly DicePool Pool = new DicePool();
        public AdvantageMode Advantage = AdvantageMode.Normal;
        private const int AdvantageModeCount = 3;

        /// <summary>A disabled set keeps its pool but is not thrown.</summary>
        public bool Enabled = true;

        /// <summary>Added once to the total. Clamping to the allowed range is the session's job.</summary>
        public int Modifier;

        /// <summary>Normal -> Advantage -> Disadvantage -> Normal (the d20 header button).</summary>
        public void CycleAdvantage()
        {
            Advantage = (AdvantageMode)(((int)Advantage + 1) % AdvantageModeCount);
        }

        public RollSetConfig Clone()
        {
            var c = new RollSetConfig();
            DiceLimits unlimited = DiceLimits.Unlimited;
            for (int i = 0; i < DieInfo.TypeCount; i++)
            {
                DieType t = DieInfo.AllTypes[i];
                c.Pool.SetCount(t, Pool.Get(t), unlimited);
            }
            c.Advantage = Advantage;
            c.Enabled = Enabled;
            c.Modifier = Modifier;
            return c;
        }
    }

    public struct DieRoll
    {
        public DieType Type;
        /// <summary>1..Sides. For d% this is 1..100.</summary>
        public int Value;

        public DieRoll(DieType type, int value) { Type = type; Value = value; }
    }

    /// <summary>The whole pool rolled once.</summary>
    public sealed class PoolRoll
    {
        public readonly List<DieRoll> Dice = new List<DieRoll>();

        public PoolRoll Clone()
        {
            var copy = new PoolRoll();
            copy.Dice.AddRange(Dice);
            return copy;
        }

        public int Subtotal
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < Dice.Count; i++) sum += Dice[i].Value;
                return sum;
            }
        }
    }

    /// <summary>Everything that came out of rolling one set.</summary>
    public sealed class SetResult
    {
        /// <summary>Snapshot of the set as it was rolled.</summary>
        public RollSetConfig Config;
        /// <summary>One pass normally; two with advantage or disadvantage.</summary>
        public PoolRoll[] Passes;
        /// <summary>Which pass was kept.</summary>
        public int ChosenPass;
        public int Modifier;
        public int Total;
        public bool HasNat20;
        public bool HasNat1;

        public PoolRoll Chosen { get { return Passes[ChosenPass]; } }
        public AdvantageMode Advantage { get { return Config.Advantage; } }
    }

    /// <summary>The result of one throw: a result per roll set, null where the set was disabled.</summary>
    public sealed class ThrowResult
    {
        public SetResult[] Sets;

        /// <summary>True when any set shows a natural 20.</summary>
        public bool HasNat20
        {
            get
            {
                for (int i = 0; i < Sets.Length; i++)
                    if (Sets[i] != null && Sets[i].HasNat20) return true;
                return false;
            }
        }

        /// <summary>True when any set shows a natural 1.</summary>
        public bool HasNat1
        {
            get
            {
                for (int i = 0; i < Sets.Length; i++)
                    if (Sets[i] != null && Sets[i].HasNat1) return true;
                return false;
            }
        }
    }
}
