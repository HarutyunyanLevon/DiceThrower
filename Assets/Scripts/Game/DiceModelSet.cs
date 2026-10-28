using System;
using System.Collections.Generic;
using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>One die kind of a baked model (see Assets/DiceModels/Data/*.json).</summary>
    [Serializable]
    public sealed class DieModelJson
    {
        public string kind;
        public int faceSize;     // mesh vertices per face; every face owns its vertices (flat shading, own UVs)
        public float[] pos;      // x,y,z per mesh vertex, centred, Unity axes
        public float[] uv;       // u,v per mesh vertex, Unity convention
        public int[] tris;
        public int[] corner;     // welded corner id per mesh vertex
        public int[] values;     // number per face (per corner for the d4)
    }

    [Serializable]
    public sealed class DiceModelJson
    {
        public string family;
        public DieModelJson[] kinds;
    }

    [Serializable]
    public sealed class DiceModelVariant
    {
        public string name;
        public Material material;
    }

    /// <summary>
    /// A set of imported dice (d4 to d20) with several colour variants that share one set of meshes.
    /// The geometry and numbering are baked into a JSON TextAsset (so no mesh read/write flags matter on device);
    /// each variant is just a material. The d% tens die (00-90) has no model; it is generated from the skin colours.
    /// </summary>
    [CreateAssetMenu(menuName = "DiceThrower/Dice Model Set", fileName = "DiceModelSet")]
    public sealed class DiceModelSet : ScriptableObject
    {
        [SerializeField] private TextAsset geometry;
        [SerializeField] private DiceModelVariant[] variants = new DiceModelVariant[0];

        [NonSerialized] private DiceModelJson _parsed;
        [NonSerialized] private readonly Dictionary<DieKind, ModelDie> _solids = new Dictionary<DieKind, ModelDie>();

        public int VariantCount { get { return variants.Length; } }

        public Material MaterialOf(int variant)
        {
            return variants[Mathf.Clamp(variant, 0, variants.Length - 1)].material;
        }

        /// <summary>The model of a die kind, or null when the set has none (the tens die).</summary>
        public ModelDie Solid(DieKind kind)
        {
            if (kind == DieKind.D10Tens || geometry == null || variants.Length == 0) return null;
            ModelDie solid;
            if (_solids.TryGetValue(kind, out solid)) return solid;

            if (_parsed == null) _parsed = JsonUtility.FromJson<DiceModelJson>(geometry.text);
            string wanted = kind == DieKind.D10Units ? DieKind.D10.ToString() : kind.ToString();
            for (int i = 0; i < _parsed.kinds.Length; i++)
            {
                if (_parsed.kinds[i].kind != wanted) continue;
                solid = new ModelDie(kind, _parsed.kinds[i]);
                break;
            }
            _solids[kind] = solid;
            return solid;
        }
    }
}
