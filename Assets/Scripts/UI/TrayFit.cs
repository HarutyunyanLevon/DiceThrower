using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Game;

namespace DiceThrower.UI
{
    /// <summary>
    /// Keeps the layout and the 3D tray in step: applies the device safe area to the screen and fits the fixed-size tray
    /// into the transparent tray area. Call Tick every frame.
    /// </summary>
    public sealed class TrayFit
    {
        private readonly VisualElement _root, _safeArea, _trayArea;
        private readonly Camera _cam;
        private readonly DiceTray _tray;
        private readonly CameraFitConfig _config;

        private int _screenW, _screenH;
        private bool _fitted;
        private float _settleUntil;
        private Rect _fitRoot;
        private float _fitAspect;

        public TrayFit(VisualElement root, VisualElement safeArea, VisualElement trayArea, Camera cam, DiceTray tray, CameraFitConfig config)
        {
            _root = root;
            _safeArea = safeArea;
            _trayArea = trayArea;
            _cam = cam;
            _tray = tray;
            _config = config;
            _settleUntil = Time.unscaledTime + config.settleSeconds;
        }

        /// <summary>The tray's size or the scene around it changed: fit the camera again for a while, as after a screen change.</summary>
        public void Invalidate()
        {
            _settleUntil = Time.unscaledTime + _config.settleSeconds;
            _fitted = false;
        }

        public void Tick()
        {
            Rect rootBound = _root.worldBound;
            bool screenChanged = Screen.width != _screenW || Screen.height != _screenH
                || !Mathf.Approximately(rootBound.width, _fitRoot.width) || !Mathf.Approximately(rootBound.height, _fitRoot.height);
            if (screenChanged)
            {
                _screenW = Screen.width;
                _screenH = Screen.height;
                _settleUntil = Time.unscaledTime + _config.settleSeconds; // layout and panel scale may still be settling
                ApplySafeArea();
            }
            // While settling, follow every layout change; afterwards only a real screen change refits,
            // so hiding a dice row later does not resize the tray.
            bool settling = Time.unscaledTime < _settleUntil;
            if (screenChanged || settling || !_fitted || !Mathf.Approximately(_cam.aspect, _fitAspect)) Refit();
        }

        private void ApplySafeArea()
        {
            if (Screen.width <= 0 || _root.worldBound.width <= 0f) return;
            float scale = _root.worldBound.width / Screen.width; // UI px per screen px
            Rect safe = Screen.safeArea;
            _safeArea.style.paddingTop = (Screen.height - safe.yMax) * scale;
            _safeArea.style.paddingBottom = safe.yMin * scale;
        }

        private void Refit()
        {
            Rect area = _trayArea.worldBound;
            Rect root = _root.worldBound;
            if (root.width <= 0f || root.height <= 0f || area.width <= 0f || area.height <= 0f) return;

            float inset = _config.slotInset;
            var slot = new Rect(
                area.xMin / root.width + inset,
                1f - area.yMax / root.height + inset, // viewport y runs bottom-up, UI y top-down
                area.width / root.width - 2f * inset,
                area.height / root.height - 2f * inset);
            TrayCamera.Fit(_cam, _tray.transform.position, _tray.Extent, _config, slot, _tray.Height);
            _fitRoot = root;
            _fitAspect = _cam.aspect;
            _fitted = true;
        }
    }
}
