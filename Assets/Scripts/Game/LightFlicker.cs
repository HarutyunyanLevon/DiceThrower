using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>Makes a light waver like a flame: its strength follows smooth noise. One float lookup per frame.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightFlicker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("How far the strength strays from its start value, as a share of it.")] private float amount = 0.25f;
        [SerializeField, Tooltip("How fast the flame wavers.")] private float speed = 2.2f;

        private Light _light;
        private float _base, _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * speed);
            _light.intensity = _base * (1f + (n - 0.5f) * 2f * amount);
        }
    }
}
