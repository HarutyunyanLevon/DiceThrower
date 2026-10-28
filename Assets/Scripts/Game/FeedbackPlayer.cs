using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// Plays the cues of the FeedbackConfig. The Sound and Haptics switches of the settings are read at play time,
    /// so flipping a switch takes effect at once. Lives on the RollScreen prefab next to its AudioSource.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class FeedbackPlayer : MonoBehaviour
    {
        [SerializeField] private FeedbackConfig config;

        private AudioSource _source;
        private AppSettings _settings;
        private Throttle _hitThrottle;

        /// <summary>True when a config is assigned. The screen refuses to start without one.</summary>
        public bool HasConfig { get { return config != null; } }

        /// <summary>Connects the settings the switches live in. Nothing plays before this.</summary>
        public void Bind(AppSettings settings)
        {
            _settings = settings;
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // plain 2D sound
            if (config != null) _hitThrottle = new Throttle(config.hitMinIntervalSeconds);
        }

        public void Play(FeedbackKind kind)
        {
            Fire(kind, 1f);
        }

        /// <summary>A die hit something at this speed. Soft touches are ignored, harder ones are louder.</summary>
        public void PlayHit(float speed)
        {
            if (config == null || speed < config.hitMinSpeed || !_hitThrottle.TryPass(Time.unscaledTime)) return;
            float loudness = Mathf.InverseLerp(config.hitMinSpeed, config.hitMaxSpeed, speed);
            Fire(FeedbackKind.DieHit, Mathf.Lerp(config.hitMinVolume, 1f, loudness));
        }

        private void Fire(FeedbackKind kind, float intensity)
        {
            if (config == null || _settings == null) return;
            FeedbackCue cue = config.Cue(kind);

            if (_settings.Sound && cue.clip != null)
                _source.PlayOneShot(cue.clip, config.masterVolume * cue.volume * intensity);
            if (_settings.Haptics && cue.vibrateMs > 0)
                Haptics.Vibrate(cue.vibrateMs, cue.vibrateStrength);

            if (config.logCues)
                Debug.Log("Feedback " + kind + " x" + intensity.ToString("0.00") + " sound=" + _settings.Sound + " haptics=" + _settings.Haptics, this);
        }
    }
}
