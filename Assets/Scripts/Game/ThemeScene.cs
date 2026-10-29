using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// Root of a theme's 3D scene prefab: the play surface, the backdrop, the props and the lights of one theme.
    /// The <see cref="ThemeSceneHost"/> instantiates it under the DiceTray and hands the surface marker to the tray.
    /// Besides the objects in the prefab, a theme sets the colour, strength and angle of the scene's main light,
    /// the camera background and the ambient light, so the dice match the scene around them.
    /// </summary>
    public sealed class ThemeScene : MonoBehaviour
    {
        [Header("Play surface")]
        [SerializeField, Tooltip("Empty marker at the middle of the surface. Its x / z scale is the outer size the camera fits to the screen.")]
        private Transform surface;
        [SerializeField, Tooltip("How high the rim stands above the surface (the camera keeps the rim in view). 0 = ignore.")]
        private float rimHeight;

        [Header("Main light")]
        [SerializeField] private Color lightColor = Color.white;
        [SerializeField] private float lightIntensity = 1.1f;
        [SerializeField] private Vector3 lightEuler = new Vector3(55f, -25f, 0f);

        [Header("Camera and ambient")]
        [SerializeField] private Color cameraBackground = Color.black;
        [SerializeField] private Color ambientColor = new Color(0.2f, 0.2f, 0.2f);

        public Transform Surface { get { return surface; } }
        public float RimHeight { get { return rimHeight; } }
        public Color LightColor { get { return lightColor; } }
        public float LightIntensity { get { return lightIntensity; } }
        public Vector3 LightEuler { get { return lightEuler; } }
        public Color CameraBackground { get { return cameraBackground; } }
        public Color AmbientColor { get { return ambientColor; } }
    }
}
