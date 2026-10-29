using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// Everything that makes one leather tray variant: its shape, stitching and hardware, colours, how the surface feels to the dice
    /// and how the walls flex when a die hits them. The mesh, the collision mesh and the materials are generated from this asset
    /// (DiceThrower > Tray > Build Leather Tray Variants); change a value, run the menu item again.
    /// Starting values for the shipped variants are in <see cref="LeatherTrayPresets"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "DiceThrower/Leather Tray Style", fileName = "LeatherTrayStyle")]
    public sealed class LeatherTrayStyle : ScriptableObject
    {
        [Header("Size")]
        [Tooltip("Outer width (x) and depth (z) at the rim, in world units. The playable floor is what is left inside the walls.")]
        public Vector2 outerSize = new Vector2(8.2f, 11.7f);
        [Tooltip("Radius of the floor's corners seen from above. The walls follow it.")]
        public float cornerRadius = 1.2f;

        [Header("Wall")]
        [Tooltip("Length of the wall measured along its slope.")]
        public float wallLength = 1.9f;
        [Range(0f, 50f), Tooltip("How far the wall leans outwards from vertical.")]
        public float flareDegrees = 14f;
        [Tooltip("Radius of the bend where the floor turns into the wall.")]
        public float floorFillet = 0.5f;
        [Tooltip("Thickness of the leather.")]
        public float thickness = 0.16f;
        [Tooltip("Extra thickness of the rolled edge, bulging outwards at the rim.")]
        public float rimBead = 0f;
        [Range(0f, 1f), Tooltip("Where the bead starts, as a share of the wall height measured from the floor.")]
        public float beadStart = 0.55f;
        [Tooltip("Tooled grooves: distance of each from the rim as a share of the wall length. Empty = none.")]
        public float[] grooveFromRim = new float[0];
        public float grooveWidth = 0.12f;
        public float grooveDepth = 0.04f;

        [Header("Hand-made shape")]
        [Range(0f, 0.7f), Tooltip("How much the wall sags in the middle of a side (share of its height).")]
        public float droop;
        [Range(0f, 0.8f), Tooltip("How much taller the wall is at the corners.")]
        public float cornerRise;
        [Range(0f, 40f), Tooltip("Extra outward lean at the corners (folded leather splays there).")]
        public float cornerFlareDegrees;
        [Range(0.5f, 6f), Tooltip("Higher = the corner shaping stays closer to the corner itself.")]
        public float cornerSharpness = 2f;
        [Range(0f, 0.2f), Tooltip("Hand-made wobble of the wall height and lean.")]
        public float irregularity;
        public int seed = 1;

        [Header("Stitching")]
        [Tooltip("One row of stitches per entry: distance from the rim as a share of the wall length. Empty = none.")]
        public float[] stitchRowsFromRim = new float[0];
        public float stitchLength = 0.3f;
        public float stitchWidth = 0.07f;
        public float stitchHeight = 0.035f;
        [Tooltip("Distance from the start of one stitch to the start of the next.")]
        public float stitchPitch = 0.46f;

        [Header("Hardware (snap rivets at the four corners)")]
        public bool rivets;
        [Range(0f, 1f), Tooltip("Distance from the rim as a share of the wall length.")]
        public float rivetFromRim = 0.4f;
        public float rivetRadius = 0.17f;

        [Header("Look")]
        public Color topColor = new Color(0.33f, 0.18f, 0.08f);
        [Tooltip("The rim and the outside of the tray.")]
        public Color liningColor = new Color(0.8f, 0.56f, 0.27f);
        public Color threadColor = new Color(0.93f, 0.84f, 0.62f);
        public Color hardwareColor = new Color(0.85f, 0.62f, 0.24f);
        [Tooltip("Pebbles per texture tile along one axis. The texture is generated from this (and the seed).")]
        public int grainCells = 14;
        [Tooltip("Size of one pebble in world units. Grain tile size = cells x pebble size.")]
        public float pebbleSize = 0.16f;
        [Range(0f, 2f)] public float grainBump = 0.7f;
        [Range(0f, 1f)] public float topSmoothness = 0.55f;
        [Range(0f, 1f)] public float liningSmoothness = 0.12f;
        [Tooltip("World size of one tile of the suede texture used on the rim and outside.")]
        public float liningTileSize = 2f;
        [Range(0f, 2f)] public float liningBump = 0.6f;

        [Header("Feel (what the dice meet)")]
        [Range(0f, 1f)] public float bounciness = 0.1f;
        [Range(0f, 2f)] public float dynamicFriction = 0.7f;
        [Range(0f, 2f)] public float staticFriction = 0.8f;
        [Tooltip("Thickness of the collision shell. Thicker than the leather so a fast die cannot slip through it.")]
        public float collisionThickness = 0.4f;

        [Header("Wall flex (visual only: the collider stays put)")]
        [Tooltip("How far around the hit the leather moves.")]
        public float flexRadius = 1.7f;
        [Tooltip("Largest outward movement of the wall.")]
        public float flexMaxAmplitude = 0.18f;
        [Tooltip("Outward movement per unit of impact speed.")]
        public float flexAmplitudePerSpeed = 0.016f;
        [Tooltip("Impacts slower than this do not move the wall.")]
        public float flexMinSpeed = 1.5f;
        [Tooltip("Wobbles per second.")]
        public float flexFrequency = 9f;
        [Tooltip("Time for the wobble to fade to about a third.")]
        public float flexDecaySeconds = 0.28f;
        [Tooltip("Hits that wobble at the same time; the oldest is dropped when more arrive.")]
        public int flexMaxHits = 6;
        [Tooltip("Hits lower than this above the floor are floor hits and do not move the wall.")]
        public float flexMinHitHeight = 0.2f;

        [Header("Mesh detail")]
        public int sideSegments = 8;
        public int cornerSegments = 8;
        public int filletSegments = 4;
        public int wallSegments = 4;
        public int rimSegments = 6;

        /// <summary>World size of one tile of the grain texture.</summary>
        public float GrainTileSize { get { return Mathf.Max(0.01f, grainCells * pebbleSize); } }
    }
}
