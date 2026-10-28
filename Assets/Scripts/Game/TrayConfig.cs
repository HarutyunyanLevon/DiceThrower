using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// Tuning for how dice are thrown and how the tray decides they have stopped.
    /// The tray's size and look live in the DiceTray prefab, the dice's own physics in the Die prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "DiceThrower/Tray Config", fileName = "TrayConfig")]
    public sealed class TrayConfig : ScriptableObject
    {
        [Header("World")]
        [Tooltip("Downward acceleration (world units / s^2). Higher feels heavier and settles faster.")]
        public float gravity = 32f;

        [Header("Spawn grid (above the near end of the tray)")]
        public int spawnColumns = 4;
        public int spawnRowsPerLayer = 3;
        public float spawnSpacing = 1.5f;
        [Tooltip("Random offset added to each spawn position.")]
        public float spawnJitter = 0.15f;
        public float spawnHeight = 2.6f;
        [Tooltip("Extra height per layer when more dice than one grid layer are thrown.")]
        public float spawnLayerHeight = 1.7f;
        [Tooltip("Distance from the tray's near edge to the first spawn row.")]
        public float spawnEdgeInset = 2.4f;

        [Header("Throw")]
        public Vector3 launchVelocityMin = new Vector3(-2.5f, 2f, 5f);
        public Vector3 launchVelocityMax = new Vector3(2.5f, 5f, 10f);
        public float spinMin = 12f;
        public float spinMax = 30f;

        [Header("Throwing more dice into a tray that already has dice")]
        [Tooltip("The new dice start at a random point this far inside the tray's edge and fly towards the middle.")]
        public float randomSpawnInset = 0.9f;

        [Header("Settling")]
        [Tooltip("How long the player waits before dice that still move are set where they are is in Settings (RulesConfig.settleSeconds).")]
        public float minDieAgeSeconds = 0.4f;
        [Tooltip("Squared speed below which a die counts as still.")]
        public float stillLinearSqr = 0.04f;
        [Tooltip("Squared spin below which a die counts as still.")]
        public float stillAngularSqr = 0.25f;
        [Tooltip("A die must stay still this long to count as settled.")]
        public float stillSeconds = 0.25f;
        [Tooltip("Dot(face normal, up) above which a die lies flat. Below it the die is leaning on something.")]
        [Range(0f, 1f)] public float flatDot = 0.97f;

        [Header("Leaning dice get a small hop")]
        public int maxNudges = 2;
        public float nudgeSideSpeed = 1f;
        public float nudgeUpSpeed = 7f;
        public float nudgeSpin = 8f;

        [Header("Setting dice by hand (timer ran out, or the player tapped the tray)")]
        [Tooltip("Dice are never placed closer than this to the tray's inner edge (on top of their own radius).")]
        public float placeEdgeInset = 0.9f;
        [Tooltip("Share of a die's circumradius that counts as its footprint when dice are pushed apart.")]
        [Range(0.5f, 1.2f)] public float footprintScale = 0.9f;
        [Tooltip("Extra gap kept between dice that were pushed apart.")]
        public float placeGap = 0.1f;
        [Tooltip("Random positions tried for a free spot before the roomiest one is used.")]
        public int placeAttempts = 40;
        [Tooltip("Passes of pushing overlapping dice apart.")]
        public int separatePasses = 24;

        [Header("Dice that leave the tray (the losing copy with advantage / disadvantage)")]
        public float dismissSeconds = 0.3f;

        [Header("Dice that fall out of the world")]
        [Tooltip("How far below the tray floor a die may fall before it is put back.")]
        public float fallMargin = 3f;
        public float respawnHeight = 3f;
    }
}
