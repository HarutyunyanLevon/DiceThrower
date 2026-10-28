using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>How the camera is placed so the whole tray fits its slot on the screen.</summary>
    [CreateAssetMenu(menuName = "DiceThrower/Camera Fit Config", fileName = "CameraFitConfig")]
    public sealed class CameraFitConfig : ScriptableObject
    {
        [Tooltip("Camera tilt in degrees; 90 looks straight down.")]
        [Range(30f, 90f)] public float pitchDegrees = 76f;
        [Tooltip("Margin kept around the tray inside its slot, as a fraction of the screen.")]
        public float slotInset = 0.01f;
        [Tooltip("After the screen changes (and at start) the fit is repeated for this long, while layout settles.")]
        public float settleSeconds = 2f;

        [Header("Distance search")]
        public float minDistance = 4f;
        public float maxDistance = 160f;
        public float distanceStep = 0.1f;
        [Tooltip("Passes that slide the camera until the tray is centred in its slot.")]
        public int centeringPasses = 4;
    }
}
