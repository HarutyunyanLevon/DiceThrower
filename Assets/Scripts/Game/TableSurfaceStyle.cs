using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// Size and look of a table-style play surface (the Dvarven Tavern's oak table). The editor builder reads this
    /// to generate the theme scene prefab; change a value and run the builder again to regenerate it.
    /// </summary>
    [CreateAssetMenu(menuName = "DiceThrower/Table Surface Style", fileName = "TableSurfaceStyle")]
    public sealed class TableSurfaceStyle : ScriptableObject
    {
        [Header("Table")]
        [Tooltip("Outer width (x) and depth (z) including the rim. Keep it equal to the other surfaces so dice caps and spawn spots fit.")]
        public Vector2 outerSize = new Vector2(8.2f, 11.7f);
        public float rimWidth = 0.7f;
        public float rimHeight = 0.55f;
        public float topThickness = 0.5f;
        [Tooltip("Height of the invisible walls that stop dice flying out over the low rim, and of the ceiling.")]
        public float wallHeight = 14f;

        [Header("Surface physics")]
        [Range(0f, 1f)] public float bounciness = 0.3f;
        public float dynamicFriction = 0.5f;
        public float staticFriction = 0.6f;

        [Header("Props (visual only)")]
        public float mugRadius = 0.6f;
        public float mugHeight = 1f;
        [Tooltip("Distance of a mug's centre from the two outer edges of its corner. Radius + inset = rim width keeps the mug off the play area.")]
        public float mugInset = 0.1f;
        public int coinCount = 7;
        public float coinRadius = 0.26f;
        public float coinThickness = 0.06f;
        public int coinSeed = 5;

        [Header("Backdrop")]
        [Tooltip("Depth of the stone floor below the table top.")]
        public float floorDepth = 3f;
        public float floorSize = 80f;
        [Tooltip("How many times the stone texture repeats across the floor.")]
        public float floorTiling = 18f;

        [Header("Lanterns")]
        public Color lanternColor = new Color(1f, 0.7f, 0.32f);
        public float lanternIntensity = 14f;
        public float lanternRange = 16f;
        public float lanternHeight = 3.2f;
        [Tooltip("Distance of each lantern from the table centre along x and z.")]
        public Vector2 lanternOffset = new Vector2(5.4f, 7.2f);

        [Header("Scene light and camera")]
        public Color lightColor = new Color(1f, 0.82f, 0.56f);
        public float lightIntensity = 0.8f;
        public Vector3 lightEuler = new Vector3(60f, -30f, 0f);
        public Color cameraBackground = new Color(0.07f, 0.05f, 0.04f);
        public Color ambientColor = new Color(0.2f, 0.14f, 0.1f);

        [Header("Colours")]
        public Color oakTint = new Color(1f, 0.92f, 0.85f);
        public Color goldColor = new Color(0.85f, 0.62f, 0.2f);
        public Color mugColor = new Color(0.48f, 0.29f, 0.12f);
        public Color foamColor = new Color(0.95f, 0.85f, 0.55f);
    }
}
