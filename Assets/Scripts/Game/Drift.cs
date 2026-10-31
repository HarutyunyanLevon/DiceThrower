using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>Moves an object slowly back and forth around where it was placed (drifting clouds, swaying light shafts). Visual only.</summary>
    public sealed class Drift : MonoBehaviour
    {
        [SerializeField, Tooltip("How far it travels each way, in its parent's space.")] private Vector3 amplitude = Vector3.right;
        [SerializeField, Tooltip("Full back-and-forth cycles per second.")] private float speed = 0.05f;

        private Vector3 _start;
        private float _phase;

        private void Awake()
        {
            _start = transform.localPosition;
            _phase = Random.value;
        }

        private void Update()
        {
            transform.localPosition = _start + amplitude * Mathf.Sin((Time.time * speed + _phase) * Mathf.PI * 2f);
        }
    }
}
