using System.Collections.Generic;
using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// The UI sprites of a dice set. A skin's sprite atlas is one row of square cells, one per <see cref="DieType"/> in enum order
    /// (d4 d6 d8 d10 d12 d20 d%). The sprites are cut from the texture once and cached. <see cref="Get"/> returns null when the skin
    /// has no usable atlas, and the UI then draws its plain die shapes instead.
    /// </summary>
    public static class DiceSprites
    {
        private const float PixelsPerUnit = 100f;
        private static readonly Vector2 Pivot = new Vector2(0.5f, 0.5f);

        private static readonly Dictionary<Texture2D, Sprite[]> Cache = new Dictionary<Texture2D, Sprite[]>();

        /// <summary>The sprite of a die type, or null when the skin has no atlas (or the texture is not a row of <see cref="DieInfo.TypeCount"/> square cells).</summary>
        public static Sprite Get(DiceSkin skin, DieType type)
        {
            Texture2D atlas = skin != null ? skin.SpriteAtlas : null;
            if (atlas == null || !IsValid(atlas)) return null;

            Sprite[] sprites;
            if (!Cache.TryGetValue(atlas, out sprites) || sprites[0] == null)
            {
                sprites = Cut(atlas);
                Cache[atlas] = sprites;
            }
            return sprites[(int)type];
        }

        /// <summary>The width must be exactly one square cell (the height) per die type.</summary>
        public static bool IsValid(Texture2D atlas)
        {
            return atlas != null && atlas.height > 0 && atlas.width == atlas.height * DieInfo.TypeCount;
        }

        /// <summary>The physical die a type is drawn with (the d% sprite shows the tens die).</summary>
        public static DieKind KindOf(DieType type)
        {
            return type == DieType.D100 ? DieKind.D10Tens : (DieKind)(int)type;
        }

        private static Sprite[] Cut(Texture2D atlas)
        {
            int cell = atlas.height;
            var sprites = new Sprite[DieInfo.TypeCount];
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i] = Sprite.Create(atlas, new Rect(i * cell, 0f, cell, cell), Pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[i].name = atlas.name + "_" + DieInfo.Label((DieType)i);
            }
            return sprites;
        }
    }
}
