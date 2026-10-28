using System;

namespace DiceThrower.Core
{
    /// <summary>The dice a player adds to a roll set (logical dice).</summary>
    public enum DieType
    {
        D4 = 0,
        D6 = 1,
        D8 = 2,
        D10 = 3,
        D12 = 4,
        D20 = 5,
        D100 = 6, // d% : thrown as a tens die + a units die
    }

    /// <summary>The dice that physically exist in the tray (physical dice).</summary>
    public enum DieKind
    {
        D4 = 0,
        D6 = 1,
        D8 = 2,
        D10 = 3,      // faces 1..10, the "10" face is printed "0"
        D12 = 4,
        D20 = 5,
        D10Tens = 6,  // faces 00,10,20..90 (values 0..90)
        D10Units = 7, // faces 0..9 (values 0..9) - the d10 used together with the tens die
    }

    public static class DieInfo
    {
        public const int TypeCount = 7;

        public static readonly DieType[] AllTypes =
        {
            DieType.D4, DieType.D6, DieType.D8, DieType.D10, DieType.D12, DieType.D20, DieType.D100
        };

        /// <summary>Highest value the logical die can roll (d% = 100).</summary>
        public static int Sides(DieType t)
        {
            switch (t)
            {
                case DieType.D4: return 4;
                case DieType.D6: return 6;
                case DieType.D8: return 8;
                case DieType.D10: return 10;
                case DieType.D12: return 12;
                case DieType.D20: return 20;
                case DieType.D100: return 100;
                default: throw new ArgumentOutOfRangeException("t");
            }
        }

        public static string Label(DieType t)
        {
            return t == DieType.D100 ? "d%" : "d" + Sides(t);
        }

        public static int FaceCount(DieKind k)
        {
            switch (k)
            {
                case DieKind.D4: return 4;
                case DieKind.D6: return 6;
                case DieKind.D8: return 8;
                case DieKind.D10:
                case DieKind.D10Tens:
                case DieKind.D10Units: return 10;
                case DieKind.D12: return 12;
                case DieKind.D20: return 20;
                default: throw new ArgumentOutOfRangeException("k");
            }
        }

        /// <summary>The values a physical die can show, ascending.</summary>
        public static int[] FaceValues(DieKind k)
        {
            int n = FaceCount(k);
            var v = new int[n];
            switch (k)
            {
                case DieKind.D10Tens:
                    for (int i = 0; i < n; i++) v[i] = i * 10;
                    break;
                case DieKind.D10Units:
                    for (int i = 0; i < n; i++) v[i] = i;
                    break;
                default:
                    for (int i = 0; i < n; i++) v[i] = i + 1;
                    break;
            }
            return v;
        }

        /// <summary>The text printed on a face showing <paramref name="value"/>.</summary>
        public static string FaceLabel(DieKind k, int value)
        {
            switch (k)
            {
                case DieKind.D10: return value == 10 ? "0" : value.ToString();
                case DieKind.D10Tens: return value == 0 ? "00" : value.ToString();
                default: return value.ToString();
            }
        }
    }
}
