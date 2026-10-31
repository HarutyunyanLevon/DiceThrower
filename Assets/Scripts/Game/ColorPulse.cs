using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>
    /// Slowly swells a colour on some renderers between two values (a halo breathing, light rays fading in and out).
    /// Uses a property block, so the shared material asset is never changed.
    /// </summary>
    public sealed class ColorPulse : MonoBehaviour
    {
        [SerializeField] private Renderer[] targets = new Renderer[0];
        [SerializeField, Tooltip("Shader colour property to drive: _BaseColor for URP Lit / Unlit.")] private string property = "_BaseColor";
        [SerializeField] private Color colorA = Color.black;
        [SerializeField] private Color colorB = Color.white;
        [SerializeField, Tooltip("Full cycles per second.")] private float speed = 0.25f;

        private MaterialPropertyBlock _block;
        private int _id;
        private float _phase;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _id = Shader.PropertyToID(property);
            _phase = Random.value;
        }

        private void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin((Time.time * speed + _phase) * Mathf.PI * 2f);
            Color c = Color.Lerp(colorA, colorB, k);
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                targets[i].GetPropertyBlock(_block);
                _block.SetColor(_id, c);
                targets[i].SetPropertyBlock(_block);
            }
        }
    }
}
