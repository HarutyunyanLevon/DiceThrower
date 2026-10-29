using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DiceThrower.Game
{
    /// <summary>
    /// Swaps the theme's 3D scene. Only the active theme's scene exists: the old one is destroyed before the new one is created,
    /// so memory stays flat however many themes there are. A theme without a scene (null) brings back what the DiceTray prefab holds itself.
    /// Added to the DiceTray's object at runtime; the sun, the camera and the tray's own surface are found automatically.
    /// </summary>
    public sealed class ThemeSceneHost : MonoBehaviour
    {
        private DiceTray _tray;
        private Camera _cam;
        private Light _sun;
        private ThemeScene _shownPrefab;
        private ThemeScene _instance;
        private readonly List<GameObject> _builtIn = new List<GameObject>();

        // what the scene looked like before any theme scene was shown
        private Transform _builtInFloor;
        private float _builtInHeight;
        private Color _sunColor, _camColor, _ambientColor;
        private float _sunIntensity;
        private Quaternion _sunRotation;
        private AmbientMode _ambientMode;

        /// <summary>The play surface (its size or rim height) or the lighting changed, so the camera must be fitted again.</summary>
        public event Action Changed;

        /// <summary>Remembers the scene as it is now. Call once before <see cref="Show"/>.</summary>
        public void Bind(DiceTray tray, Camera cam)
        {
            _tray = tray;
            _cam = cam;
            _sun = FindSun();

            foreach (Transform child in tray.transform)
                if (child != tray.DiceRoot) _builtIn.Add(child.gameObject);
            _builtInFloor = tray.Floor;
            _builtInHeight = tray.Height;

            _camColor = cam.backgroundColor;
            _ambientMode = RenderSettings.ambientMode;
            _ambientColor = RenderSettings.ambientLight;
            if (_sun != null)
            {
                _sunColor = _sun.color;
                _sunIntensity = _sun.intensity;
                _sunRotation = _sun.transform.rotation;
            }
        }

        /// <summary>Shows a theme's scene; null shows the tray's own surface again.</summary>
        public void Show(ThemeScene prefab)
        {
            if (_tray == null) return;
            if (prefab == _shownPrefab && (prefab == null || _instance != null)) return;
            _shownPrefab = prefab;

            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;

            if (prefab == null) Restore();
            else Create(prefab);
            Changed?.Invoke();
        }

        private void Create(ThemeScene prefab)
        {
            SetBuiltInActive(false);
            _instance = Instantiate(prefab, _tray.transform);
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.identity;
            _tray.SetSurface(_instance.Surface, _instance.RimHeight);

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

        private void Restore()
        {
            SetBuiltInActive(true);
            _tray.SetSurface(_builtInFloor, _builtInHeight);
            if (_sun != null)
            {
                _sun.color = _sunColor;
                _sun.intensity = _sunIntensity;
                _sun.transform.rotation = _sunRotation;
            }
            if (_cam != null) _cam.backgroundColor = _camColor;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientLight = _ambientColor;
        }

        private void SetBuiltInActive(bool active)
        {
            for (int i = 0; i < _builtIn.Count; i++)
                if (_builtIn[i] != null) _builtIn[i].SetActive(active);
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional) return lights[i];
            return null;
        }
    }
}
