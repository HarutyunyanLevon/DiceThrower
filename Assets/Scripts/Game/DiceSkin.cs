using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>Colours and surface of a die set. One asset per set (Attack, Damage, ...).</summary>
    [CreateAssetMenu(menuName = "DiceThrower/Dice Skin", fileName = "DiceSkin")]
    public sealed class DiceSkin : ScriptableObject
    {
        [SerializeField] private Color body = Color.white;
        [SerializeField] private Color ink = Color.black;
        [SerializeField, Range(0f, 1f)] private float smoothness = 0.45f;
        [SerializeField, Range(0f, 1f)] private float metallic;

        public Color32 Body { get { return body; } }
        public Color32 Ink { get { return ink; } }
        public float Smoothness { get { return smoothness; } }
        public float Metallic { get { return metallic; } }

        /// <summary>Cache key: dice visuals are shared between skins that look the same.</summary>
        public string Key
        {
            get { return ColorUtility.ToHtmlStringRGBA(body) + "|" + ColorUtility.ToHtmlStringRGBA(ink) + "|" + smoothness + "|" + metallic; }
        }
    }
}
