using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// Look of the dice pictures made from the 3D models: the live rotating d20 of the Dice tab.
    /// </summary>
    [CreateAssetMenu(menuName = "DiceThrower/Dice Preview Config", fileName = "DicePreviewConfig")]
    public sealed class DicePreviewConfig : ScriptableObject
    {
        [Header("Live preview (Dice tab)")]
        [Tooltip("Pixels per side of a slot's render texture.")]
        public int textureSize = 256;
        [Tooltip("Degrees per second the die turns around the vertical axis.")]
        public float spinDegreesPerSecond = 40f;

        [Header("Camera")]
        [Tooltip("Where the preview dice stand. Far from the tray, so the tray camera and the preview camera never see each other's dice.")]
        public Vector3 stagePosition = new Vector3(0f, 500f, 0f);
        [Range(5f, 60f)] public float fieldOfView = 25f;
        [Tooltip("The die's circumsphere times this fills the picture (1 = touches the edges).")]
        [Range(1f, 2f)] public float framePadding = 1.12f;
        [Tooltip("MSAA samples of the render texture (1, 2, 4 or 8) for smooth edges.")]
        public int antiAliasing = 4;

        [Header("Pose")]
        [Tooltip("Each die is first turned to its classic silhouette (see DiePose), then tilted by these degrees (around x, around y) so it reads as 3D. Used for a kind that has no entry in Tilts.")]
        public Vector2 defaultTiltDegrees = new Vector2(-20f, 0f);
        [Tooltip("Tilt per die kind. Around x: negative looks from above. Around y: turns the die to show a side face.")]
        public DieTilt[] tilts =
        {
            new DieTilt { kind = DieKind.D4, degrees = new Vector2(-12f, 0f) },
            new DieTilt { kind = DieKind.D6, degrees = new Vector2(-25f, 30f) },
            new DieTilt { kind = DieKind.D8, degrees = new Vector2(-20f, 0f) },
            new DieTilt { kind = DieKind.D10, degrees = new Vector2(-25f, 0f) },
            new DieTilt { kind = DieKind.D10Tens, degrees = new Vector2(-25f, 0f) },
            new DieTilt { kind = DieKind.D10Units, degrees = new Vector2(-25f, 0f) },
            new DieTilt { kind = DieKind.D12, degrees = new Vector2(-20f, 0f) },
            new DieTilt { kind = DieKind.D20, degrees = new Vector2(-20f, 0f) },
        };

        [Header("Light (used when the rig adds its own light)")]
        public Vector3 lightEuler = new Vector3(55f, -25f, 0f);
        public float lightIntensity = 1.1f;

        /// <summary>The tilt of a die kind's picture.</summary>
        public Vector2 TiltOf(DieKind kind)
        {
            if (tilts != null)
            {
                for (int i = 0; i < tilts.Length; i++)
                {
                    if (tilts[i].kind == kind) return tilts[i].degrees;
                }
            }
            return defaultTiltDegrees;
        }
    }

    [System.Serializable]
    public struct DieTilt
    {
        public DieKind kind;
        public Vector2 degrees;
    }
}
