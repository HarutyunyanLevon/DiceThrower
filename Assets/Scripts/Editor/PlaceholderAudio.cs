using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Placeholder sounds made from simple tones and noise, written as WAV files so they can be replaced by real ones later.
    /// Nothing is overwritten: an existing file is loaded as it is.
    /// </summary>
    internal static class PlaceholderAudio
    {
        private const string Dir = "Assets/Audio";
        private const int SampleRate = 22050;
        private const int BitsPerSample = 16;
        private const float Headroom = 0.9f;  // loudest sample after normalising
        private const float TwoPi = Mathf.PI * 2f;

        /// <summary>Gives the value of the wave at time t (seconds) of a sound that lasts <c>seconds</c>.</summary>
        private delegate float Recipe(float t, System.Random noise);

        public static AudioClip Throw() { return Make("throw", 0.30f, (t, n) => Noise(n) * Rise(t, 0.02f) * Fall(t, 7f) * (0.65f + 0.35f * Mathf.Sin(TwoPi * 40f * t))); }

        public static AudioClip Hit()
        {
            return Make("hit", 0.10f, (t, n) => Tone(180f, t) * Fall(t, 50f) + Tone(900f, t) * Fall(t, 70f) * 0.4f + Noise(n) * Fall(t, 90f) * 0.5f);
        }

        public static AudioClip Settled() { return Make("settled", 0.14f, (t, n) => Tone(140f, t) * Fall(t, 35f) * 0.8f + Noise(n) * Fall(t, 100f) * 0.15f); }

        /// <summary>A short rising arpeggio.</summary>
        public static AudioClip Nat20()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            return Make("nat20", 0.65f, (t, n) => Arpeggio(notes, 0.10f, 5f, t));
        }

        /// <summary>A short falling, sad line.</summary>
        public static AudioClip Nat1()
        {
            float[] notes = { 392f, 349.23f, 293.66f };
            return Make("nat1", 0.55f, (t, n) => Arpeggio(notes, 0.16f, 4f, t, true));
        }

        public static AudioClip Blocked() { return Make("blocked", 0.12f, (t, n) => Mathf.Sign(Tone(140f, t)) * 0.4f * Fall(t, 25f)); }

        // ------------------------------------------------------------------ building blocks

        private static float Tone(float hz, float t) { return Mathf.Sin(TwoPi * hz * t); }

        private static float Noise(System.Random n) { return (float)(n.NextDouble() * 2.0 - 1.0); }

        /// <summary>Exponential fade-out.</summary>
        private static float Fall(float t, float rate) { return Mathf.Exp(-rate * t); }

        /// <summary>Linear fade-in over <paramref name="seconds"/>.</summary>
        private static float Rise(float t, float seconds) { return Mathf.Clamp01(t / seconds); }

        private static float Arpeggio(float[] notes, float step, float decay, float t, bool hollow = false)
        {
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float local = t - i * step;
                if (local < 0f) continue;
                float wave = Tone(notes[i], local) + (hollow ? 0.5f * Tone(notes[i] * 0.5f, local) : 0.3f * Tone(notes[i] * 2f, local));
                sum += wave * Fall(local, decay);
            }
            return sum;
        }

        // ------------------------------------------------------------------ file

        private static AudioClip Make(string name, float seconds, Recipe recipe)
        {
            string path = Dir + "/" + name + ".wav";
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Audio");
            File.WriteAllBytes(path, ToWav(Render(seconds, recipe)));
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static float[] Render(float seconds, Recipe recipe)
        {
            var samples = new float[Mathf.CeilToInt(seconds * SampleRate)];
            var noise = new System.Random(samples.Length); // the same sound every time
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = recipe(i / (float)SampleRate, noise);
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }
            if (peak > 0f)
                for (int i = 0; i < samples.Length; i++) samples[i] *= Headroom / peak;
            return samples;
        }

        private static byte[] ToWav(float[] samples)
        {
            const int channels = 1;
            int bytesPerSample = BitsPerSample / 8;
            int dataSize = samples.Length * bytesPerSample;
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataSize);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);                                   // size of the fmt chunk
                w.Write((short)1);                             // PCM
                w.Write((short)channels);
                w.Write(SampleRate);
                w.Write(SampleRate * channels * bytesPerSample);
                w.Write((short)(channels * bytesPerSample));
                w.Write((short)BitsPerSample);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataSize);
                for (int i = 0; i < samples.Length; i++) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
                return stream.ToArray();
            }
        }
    }
}
