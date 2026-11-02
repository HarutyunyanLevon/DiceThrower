using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// Numbers behind the DiceFX shader dice (DieFxDriver). Lives in Resources so the driver finds it without a scene reference;
    /// when the asset is missing the defaults below are used.
    /// </summary>
    [CreateAssetMenu(menuName = "DiceThrower/Dice FX Config", fileName = "DiceFxConfig")]
    public sealed class DiceFxConfig : ScriptableObject
    {
        public enum LowFxMode
        {
            Auto,
            ForceOn,
            ForceOff,
        }

        public const string ResourceName = "DiceFxConfig";
        public const string LowFxKeyword = "DICEFX_LOW";

        [Header("Low-FX fallback (cheaper variants of the Cosmic, Frost and Void shaders)")]
        [Tooltip("Auto: weak phones (less RAM than the limit below) use the cheap variants. Force On / Force Off to test. Takes effect at once in the Editor.")]
        public LowFxMode lowFxMode = LowFxMode.Auto;
        [Tooltip("Auto mode: devices with less system RAM (MB) than this get the cheap variants (one noise layer, one star layer, no dissolve).")]
        public int lowFxBelowRamMb = 4000;

        [Header("Energy (how hard the die is moving, 0..1)")]
        [Tooltip("Die speed (m/s of the centre plus the corners' spin speed) that counts as full energy.")]
        public float fullEnergySpeed = 6f;
        [Tooltip("How fast energy climbs when the die speeds up, per second.")]
        public float energyRisePerSecond = 8f;
        [Tooltip("Seconds energy takes to fall from 1 to 0 once the die slows down (the lava cooling time).")]
        public float energyCoolSeconds = 1.5f;

        [Header("Impact flash")]
        [Tooltip("Impact speed (m/s) that gives a full flash.")]
        public float flashImpactSpeed = 5f;
        [Tooltip("Seconds a full flash takes to fade.")]
        public float flashFadeSeconds = 0.35f;

        [Header("Nat 20 / nat 1 effects")]
        [Tooltip("Seconds an effect takes to fade in or out.")]
        public float effectFadeSeconds = 0.5f;

        [Header("Variation")]
        [Tooltip("Each die gets a random seed in 0..this, so two dice of one set do not show the same pattern.")]
        public float seedRange = 100f;

        private static DiceFxConfig _current;

        public static DiceFxConfig Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<DiceFxConfig>(ResourceName);
                    if (_current == null) _current = CreateInstance<DiceFxConfig>();
                    _current.ApplyQuality();
                }
                return _current;
            }
        }

        public bool UseLowFx
        {
            get
            {
                if (lowFxMode == LowFxMode.ForceOn) return true;
                if (lowFxMode == LowFxMode.ForceOff) return false;
                return SystemInfo.systemMemorySize < lowFxBelowRamMb;
            }
        }

        /// <summary>Switches the global shader keyword the DiceFX shaders read (DICEFX_LOW).</summary>
        public void ApplyQuality()
        {
            if (UseLowFx) Shader.EnableKeyword(LowFxKeyword);
            else Shader.DisableKeyword(LowFxKeyword);
        }

        private void OnValidate()
        {
            ApplyQuality();
        }
    }
}
