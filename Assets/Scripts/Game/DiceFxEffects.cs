using UnityEngine;

namespace DiceThrower.Game
{
    /// <summary>What a die looks like when it rolls its best or worst number (see <see cref="DieFxDriver.SetEffect"/>).</summary>
    public enum DieNatEffect
    {
        None,
        Nat20,
        Nat1,
    }

    /// <summary>
    /// The nat 20 / nat 1 effects of a dice set. The look of the die itself is in its shader (_Highlight for a nat 20, _Gloom for a nat 1);
    /// this asset says which effects exist (the Dice tab shows a button for each) and adds optional objects that appear beside the die
    /// while the effect is on: rays behind it, a rain cloud above it. Sizes are in die radii, so every die kind gets the same proportions.
    /// </summary>
    [CreateAssetMenu(menuName = "DiceThrower/Dice FX Effects", fileName = "DiceFxEffects")]
    public sealed class DiceFxEffects : ScriptableObject
    {
        [Header("Nat 20")]
        public bool hasNat20;
        [Tooltip("Optional object that appears with the effect (unit size: 1 world unit across). Instantiated once per die.")]
        public GameObject nat20Prefab;
        [Tooltip("Width of the object in die radii.")]
        public float nat20Size = 3.4f;
        [Tooltip("Height of its centre above the die centre, in die radii.")]
        public float nat20Height;
        [Tooltip("Shift of its centre toward the top of the screen, in die radii.")]
        public float nat20Lateral;

        [Header("Nat 1")]
        public bool hasNat1;
        public GameObject nat1Prefab;
        public float nat1Size = 1.7f;
        public float nat1Height = 1.9f;
        public float nat1Lateral = 0.3f;

        public bool Has(DieNatEffect effect)
        {
            return (effect == DieNatEffect.Nat20 && hasNat20) || (effect == DieNatEffect.Nat1 && hasNat1);
        }
    }
}
