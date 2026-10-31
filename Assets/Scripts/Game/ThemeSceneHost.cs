using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DiceThrower.Game
{
    /// <summary>
    /// Swaps the theme's 3D scene. Only the active theme's scene exists: the old one is destroyed before the new one is created,
    /// so memory stays flat however many themes there are. Every theme has a scene; the DiceTray prefab itself has no surface.
    /// Added to the DiceTray's object at runtime; the sun is found automatically.
    /// </summary>
    public sealed class ThemeSceneHost : MonoBehaviour
    {
        private DiceTray _tray;
        private Camera _cam;
        private Light _sun;
        private ThemeScene _shownPrefab;
        private ThemeScene _instance;

        /// <summary>The play surface (its size or rim height) or the lighting changed, so the camera must be fitted again.</summary>
        public event Action Changed;

        /// <summary>Call once before <see cref="Show"/>.</summary>
        public void Bind(DiceTray tray, Camera cam)
        {
            _tray = tray;
            _cam = cam;
            _sun = FindSun();
        }

        /// <summary>Shows a theme's scene (replacing the previous one). Showing the one that is already there does nothing.</summary>
        public void Show(ThemeScene prefab)
        {
            if (_tray == null || prefab == null) return;
            if (prefab == _shownPrefab && _instance != null) return;
            _shownPrefab = prefab;

            if (_instance != null) Destroy(_instance.gameObject);
            Create(prefab);
            Changed?.Invoke();
        }

        private void Create(ThemeScene prefab)
        {
            _instance = Instantiate(prefab, _tray.transform);
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
            _tray.SetSurface(_instance.Surface, _instance.RimHeight, _instance.Round);

            if (_sun != null)
            {
                _sun.color = _instance.LightColor;
                _sun.intensity = _instance.LightIntensity;
                _sun.transform.rotation = Quaternion.Euler(_instance.LightEuler);
            }
            if (_cam != null) _cam.backgroundColor = _instance.CameraBackground;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = _instance.AmbientColor;
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            Light[] lights = FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional) return lights[i];
            return null;
        }
    }
}
