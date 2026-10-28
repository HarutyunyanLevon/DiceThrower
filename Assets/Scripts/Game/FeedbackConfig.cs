using System;
using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>The moments that make a sound and/or a vibration.</summary>
    public enum FeedbackKind
    {
        Throw,    // dice are thrown
        DieHit,   // a die hits the floor, a wall or another die
        Settled,  // every die lies still and shows its number
        Nat20,    // settled with a natural 20 in the result
        Nat1,     // settled with a natural 1 in the result
        Blocked,  // a dice limit was hit
    }

    /// <summary>One sound and one vibration, played together.</summary>
    [Serializable]
    public sealed class FeedbackCue
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Vibration length in milliseconds. 0 = no vibration.")]
        public int vibrateMs;
        [Tooltip("Vibration strength, 0 to 1 (Android 8+; older phones use their default strength).")]
        [Range(0f, 1f)] public float vibrateStrength = 0.5f;
    }

    /// <summary>Sounds, volumes and vibration lengths. The Settings tab switches sound and haptics on and off as a whole.</summary>
    [CreateAssetMenu(menuName = "DiceThrower/Feedback Config", fileName = "FeedbackConfig")]
    public sealed class FeedbackConfig : ScriptableObject
    {
        [Range(0f, 1f)] public float masterVolume = 0.8f;

        [Header("Cues")]
        public FeedbackCue throwCue = new FeedbackCue();
        public FeedbackCue dieHitCue = new FeedbackCue();
        public FeedbackCue settledCue = new FeedbackCue();
        public FeedbackCue nat20Cue = new FeedbackCue();
        public FeedbackCue nat1Cue = new FeedbackCue();
        public FeedbackCue blockedCue = new FeedbackCue();

        [Header("Dice hits")]
        [Tooltip("Impacts slower than this (world units / s) make no sound.")]
        public float hitMinSpeed = 1.5f;
        [Tooltip("Impacts at or above this speed play at full volume.")]
        public float hitMaxSpeed = 9f;
        [Tooltip("Volume (0 to 1) of the softest audible hit.")]
        [Range(0f, 1f)] public float hitMinVolume = 0.25f;
        [Tooltip("At most one hit sound per this many seconds, so a pile of dice is not a wall of noise.")]
        public float hitMinIntervalSeconds = 0.04f;

        [Header("Debug")]
        [Tooltip("Writes every cue to the Console. Handy in the Editor, where nothing vibrates.")]
        public bool logCues;

        public FeedbackCue Cue(FeedbackKind kind)
        {
            switch (kind)
            {
                case FeedbackKind.Throw: return throwCue;
                case FeedbackKind.DieHit: return dieHitCue;
                case FeedbackKind.Settled: return settledCue;
                case FeedbackKind.Nat20: return nat20Cue;
                case FeedbackKind.Nat1: return nat1Cue;
                default: return blockedCue;
            }
        }
    }
}
