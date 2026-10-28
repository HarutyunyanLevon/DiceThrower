using UnityEditor;
using UnityEngine;
using DiceThrower.Game;

namespace DiceThrower.EditorTools
{
    /// <summary>Creates the FeedbackConfig asset with its placeholder sounds. The numbers here are only the starting values of a NEW asset.</summary>
    internal static class FeedbackAssets
    {
        private static class Defaults
        {
            // (volume, vibration ms, vibration strength)
            public static readonly Vector3 Throw = new Vector3(0.7f, 15f, 0.35f);
            public static readonly Vector3 Hit = new Vector3(0.9f, 0f, 0f);
            public static readonly Vector3 Settled = new Vector3(0.8f, 25f, 0.5f);
            public static readonly Vector3 Nat20 = new Vector3(1f, 70f, 1f);
            public static readonly Vector3 Nat1 = new Vector3(1f, 45f, 0.8f);
            public static readonly Vector3 Blocked = new Vector3(0.8f, 20f, 0.6f);
        }

        public static FeedbackConfig GetOrCreate(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<FeedbackConfig>(path);
            if (existing != null) return existing;

            var config = ScriptableObject.CreateInstance<FeedbackConfig>();
            Fill(config.throwCue, PlaceholderAudio.Throw(), Defaults.Throw);
            Fill(config.dieHitCue, PlaceholderAudio.Hit(), Defaults.Hit);
            Fill(config.settledCue, PlaceholderAudio.Settled(), Defaults.Settled);
            Fill(config.nat20Cue, PlaceholderAudio.Nat20(), Defaults.Nat20);
            Fill(config.nat1Cue, PlaceholderAudio.Nat1(), Defaults.Nat1);
            Fill(config.blockedCue, PlaceholderAudio.Blocked(), Defaults.Blocked);
            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        private static void Fill(FeedbackCue cue, AudioClip clip, Vector3 volumeMsStrength)
        {
            cue.clip = clip;
            cue.volume = volumeMsStrength.x;
            cue.vibrateMs = Mathf.RoundToInt(volumeMsStrength.y);
            cue.vibrateStrength = volumeMsStrength.z;
        }
    }
}
