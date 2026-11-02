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
        [SerializeField, Tooltip("Optional imported 3D dice. When set, the dice look like this model variant; Body and Ink then only colour the dice the model lacks (the d% tens die).")] private DiceModelSet model;
        [SerializeField, Tooltip("Colour variant of the model.")] private int variant;
        [SerializeField, Tooltip("Optional nat 20 / nat 1 effects of this set (DiceFX shader sets). The Dice tab shows a preview button for each.")] private DiceFxEffects effects;

        public Color32 Body { get { return body; } }
        public Color32 Ink { get { return ink; } }
        public float Smoothness { get { return smoothness; } }
        public float Metallic { get { return metallic; } }
        public DiceModelSet Model { get { return model; } }
        public int Variant { get { return variant; } }
        public DiceFxEffects Effects { get { return effects; } }

        /// <summary>Cache key: dice visuals are shared between skins that look the same.</summary>
        public string Key
        {
            get
            {
                if (model != null) return model.name + "#" + variant + "|" + ColorUtility.ToHtmlStringRGBA(body) + ColorUtility.ToHtmlStringRGBA(ink);
                return ColorUtility.ToHtmlStringRGBA(body) + "|" + ColorUtility.ToHtmlStringRGBA(ink) + "|" + smoothness + "|" + metallic;
            }
        }
    }
}
