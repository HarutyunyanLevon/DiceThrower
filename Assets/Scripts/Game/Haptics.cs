using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// Vibration through the Android vibrator service. Does nothing in the Editor and on other platforms.
    /// Handheld.Vibrate is the fallback when the service cannot be reached; naming it here also makes Unity add the VIBRATE permission to the manifest.
    /// </summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const int VibrationEffectMinSdk = 26; // Android 8: amplitude control
        private const int MaxAmplitude = 255;         // the Android API's range is 1..255

        private static AndroidJavaObject _vibrator;
        private static AndroidJavaClass _effectClass;
        private static int _sdk;
        private static bool _initialised;
        private static bool _broken;

        public static void Vibrate(int milliseconds, float strength)
        {
            if (milliseconds <= 0 || _broken) return;
            try
            {
                Init();
                if (_vibrator == null)
                {
                    Handheld.Vibrate();
                    return;
                }
                if (_effectClass != null)
                {
                    int amplitude = Mathf.Clamp(Mathf.RoundToInt(strength * MaxAmplitude), 1, MaxAmplitude);
                    using (AndroidJavaObject effect = _effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, amplitude))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", (long)milliseconds);
                }
            }
            catch (System.Exception e)
            {
                _broken = true; // do not try again every throw
                Debug.LogWarning("DiceThrower: vibration is not available. " + e.Message);
            }
        }

        private static void Init()
        {
            if (_initialised) return;
            _initialised = true;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                _sdk = version.GetStatic<int>("SDK_INT");
            if (_sdk >= VibrationEffectMinSdk) _effectClass = new AndroidJavaClass("android.os.VibrationEffect");
        }
#else
        public static void Vibrate(int milliseconds, float strength) { }
#endif
    }
}
