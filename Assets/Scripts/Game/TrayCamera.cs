using UnityEngine;

namespace DiceThrower.Game
{
    public static class TrayCamera
    {
        /// <summary>
        /// Places the camera as close as possible while the whole tray stays inside
        /// <paramref name="slot"/> (viewport units, from the bottom-left), and centres the tray in it.
        /// </summary>
        /// <param name="trayCenter">World position of the tray floor's centre.</param>
        /// <param name="trayExtent">Outer width and depth of the tray.</param>
        /// <param name="trayHeight">How high the rim stands above the floor; the rim is kept in view too.</param>
        public static void Fit(Camera cam, Vector3 trayCenter, Vector2 trayExtent, CameraFitConfig cfg, Rect slot, float trayHeight = 0f)
        {
            if (!(slot.width > 0f) || !(slot.height > 0f)) return;

            cam.ResetAspect(); // the aspect can be stale during the first frames of Play
            Quaternion rot = Quaternion.Euler(cfg.pitchDegrees, 0f, 0f);
            cam.transform.rotation = rot;
            Vector3 forward = rot * Vector3.forward, right = rot * Vector3.right, up = rot * Vector3.up;

            float hw = trayExtent.x * 0.5f, hd = trayExtent.y * 0.5f;
            Vector3[] corners =
            {
                trayCenter + new Vector3(-hw, 0f, -hd), trayCenter + new Vector3(hw, 0f, -hd),
                trayCenter + new Vector3(-hw, 0f, hd), trayCenter + new Vector3(hw, 0f, hd),
                trayCenter + new Vector3(-hw, trayHeight, -hd), trayCenter + new Vector3(hw, trayHeight, -hd),
                trayCenter + new Vector3(-hw, trayHeight, hd), trayCenter + new Vector3(hw, trayHeight, hd),
            };

            float best = cfg.maxDistance;
            for (float dist = cfg.minDistance; dist < cfg.maxDistance; dist += cfg.distanceStep)
            {
                Vector2 min, max;
                if (!Project(cam, trayCenter - forward * dist, corners, out min, out max)) continue;
                if (max.x - min.x <= slot.width && max.y - min.y <= slot.height) { best = dist; break; }
            }

            // slide the camera (not the tray) until the tray's bounding box is centred in the slot
            float tanHalfFov = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float worldHeight = 2f * best * tanHalfFov, worldWidth = worldHeight * cam.aspect;
            Vector3 camPos = trayCenter - forward * best;
            for (int pass = 0; pass < cfg.centeringPasses; pass++)
            {
                Vector2 min, max;
                if (!Project(cam, camPos, corners, out min, out max)) break;
                Vector2 off = (min + max) * 0.5f - slot.center;
                camPos += right * (off.x * worldWidth) + up * (off.y * worldHeight);
            }
            cam.transform.position = camPos;
        }

        /// <summary>Viewport bounding box of the points as seen from camPos. False if any is behind the camera.</summary>
        private static bool Project(Camera cam, Vector3 camPos, Vector3[] points, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            if (float.IsNaN(camPos.x) || float.IsNaN(camPos.y) || float.IsNaN(camPos.z)) return false;

            Vector3 saved = cam.transform.position;
            cam.transform.position = camPos;
            bool ok = true;
            for (int i = 0; i < points.Length && ok; i++)
            {
                Vector3 vp = cam.WorldToViewportPoint(points[i]);
                ok = vp.z > 0f;
                min = Vector2.Min(min, vp);
                max = Vector2.Max(max, vp);
            }
            cam.transform.position = saved;
            return ok;
        }
    }
}
