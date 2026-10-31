using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// Size, physics and lighting of a round play surface (the Cultist hideout's floor circle, the Cathedral's postament).
    /// The editor builders read this to generate the theme scene prefab; change a value and run the builder again.
    /// The containment is an invisible ring of colliders, so nothing about the drawing has to be solid.
    /// </summary>
    [CreateAssetMenu(menuName = "DiceThrower/Round Surface Style", fileName = "RoundSurfaceStyle")]
    public sealed class RoundSurfaceStyle : ScriptableObject
    {
        [Header("Play area")]
        [Tooltip("Diameter the camera fits to the screen (the surface marker's x / z scale). The same as the table's width keeps dice caps and spawn spots valid.")]
        public float outerDiameter = 8.2f;
        [Tooltip("Distance from the outer edge to the invisible wall's inner face: the dice's play radius is outerDiameter / 2 - wallInset.")]
        public float wallInset = 0.5f;
        [Tooltip("The drawn disc is this much smaller than the outer diameter, per side.")]
        public float discInset = 0.2f;
        public float wallThickness = 0.6f;
        public float wallHeight = 14f;
        [Min(8)] public int wallSegments = 32;
        public float groundThickness = 0.5f;
        [Tooltip("How high a modelled rim stands above the surface (the camera keeps it in view). 0 = a flat surface.")]
        public float rimHeight = 0f;
        [Tooltip("Where the scenery of the camera's four corners sits (x, z of one corner; the others mirror it). The corners of the round surface's bounding square stay empty, so this is where tentacles, ferns and flourishes go.")]
        public Vector2 cornerAnchor = new Vector2(3.2f, 4.8f);
        [Tooltip("Seed for the scattered scenery (clouds, ferns, stones): the same seed gives the same scene every build.")]
        public int sceneSeed = 7;

        [Header("Surface physics")]
        [Range(0f, 1f)] public float bounciness = 0.3f;
        public float dynamicFriction = 0.5f;
        public float staticFriction = 0.6f;

        [Header("Backdrop")]
        [Tooltip("How far the backdrop floor lies below the play surface (0 = the same plane).")]
        public float floorDepth = 0f;
        public float floorSize = 80f;
        [Tooltip("How many times the floor texture repeats across the backdrop floor.")]
        public float floorTiling = 14f;

        [Header("Scene light and camera")]
        public Color lightColor = Color.white;
        public float lightIntensity = 1f;
        public Vector3 lightEuler = new Vector3(60f, -30f, 0f);
        public Color cameraBackground = Color.black;
        public Color ambientColor = new Color(0.1f, 0.1f, 0.1f);

        public float OuterRadius { get { return outerDiameter * 0.5f; } }
        public float DiscRadius { get { return outerDiameter * 0.5f - discInset; } }
        public float PlayRadius { get { return outerDiameter * 0.5f - wallInset; } }
    }
}
