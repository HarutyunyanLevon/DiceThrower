using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DiceThrower.Game;
using DiceThrower.UI;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Turns the baked dice models in Assets/DiceModels (Data/*.json and Textures/*.png, made from the .glb files in Assets/DiceSets)
    /// into Unity assets: texture import settings, one URP Lit material per colour variant, a DiceModelSet per file, one DiceSkin per
    /// variant, and the skin sets the Settings tab cycles through. Nothing that already exists is overwritten.
    /// </summary>
    public static class DiceModelBuilder
    {
        private const string Root = "Assets/DiceModels";
        private const string MaterialDir = Root + "/Materials";
        private const string ConfigDir = "Assets/Config";
        private const string DataUiConfigPath = ConfigDir + "/DataUiConfig.asset";
        private const int TextureMaxSize = 512;
        private const float Smoothness = 0.45f;

        [Serializable] private sealed class FileJson { public string family; public VariantJson[] variants; }
        [Serializable]
        private sealed class VariantJson
        {
            public string name, baseMap, normalMap, emissionMap;
            public float[] body, ink;
        }

        /// <summary>A named skin set: the colour variants (by name) of the Attack and Damage dice.</summary>
        private struct Pair
        {
            public string setName, family, attack, damage;
            public Pair(string setName, string family, string attack, string damage)
            {
                this.setName = setName; this.family = family; this.attack = attack; this.damage = damage;
            }
        }

        private static readonly Pair[] Pairs =
        {
            new Pair("Cosmic Teal & Crimson", "cosmic", "Teal", "Crimson"),
            new Pair("Cosmic Green & Orange", "cosmic", "Green", "Orange"),
            new Pair("Cosmic Purple & Navy", "cosmic", "Purple", "Navy"),
            new Pair("Glow Sky & Red", "glow", "Sky", "Red"),
            new Pair("Glow Black & White", "glow", "Black", "White"),
            new Pair("Glow Green & Lavender", "glow", "Green", "Lavender"),
        };

        [MenuItem("DiceThrower/Build Dice Model Sets")]
        public static void Build()
        {
            BuilderAssets.EnsureFolder(MaterialDir);
            var sets = new Dictionary<string, DiceModelSet>();
            var skins = new Dictionary<string, DiceSkin>(); // key: family/variant

            foreach (string jsonPath in Directory.GetFiles(Root + "/Data", "*.json"))
            {
                string assetPath = jsonPath.Replace('\\', '/');
                var geometry = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                FileJson file = JsonUtility.FromJson<FileJson>(geometry.text);

                var variants = new DiceModelVariant[file.variants.Length];
                for (int i = 0; i < variants.Length; i++)
                    variants[i] = new DiceModelVariant { name = file.variants[i].name, material = GetOrCreateMaterial(file.family, file.variants[i]) };

                string setPath = ConfigDir + "/DiceModel_" + file.family + ".asset";
                DiceModelSet set = BuilderAssets.GetOrCreate<DiceModelSet>(setPath, s =>
                {
                    var so = new SerializedObject(s);
                    so.FindProperty("geometry").objectReferenceValue = geometry;
                    SerializedProperty list = so.FindProperty("variants");
                    list.arraySize = variants.Length;
                    for (int i = 0; i < variants.Length; i++)
                    {
                        list.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = variants[i].name;
                        list.GetArrayElementAtIndex(i).FindPropertyRelative("material").objectReferenceValue = variants[i].material;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                });
                sets[file.family] = set;

                for (int i = 0; i < file.variants.Length; i++)
                {
                    VariantJson v = file.variants[i];
                    int index = i;
                    string skinPath = ConfigDir + "/Skin_" + file.family + "_" + v.name + ".asset";
                    skins[file.family + "/" + v.name] = BuilderAssets.GetOrCreate<DiceSkin>(skinPath, s =>
                    {
                        var so = new SerializedObject(s);
                        so.FindProperty("body").colorValue = ToColor(v.body);
                        so.FindProperty("ink").colorValue = ToColor(v.ink);
                        so.FindProperty("model").objectReferenceValue = set;
                        so.FindProperty("variant").intValue = index;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    });
                }
            }

            AddSkinSets(skins);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DiceThrower: dice model sets built (" + sets.Count + " models, " + skins.Count + " skins). They are in the Settings tab's skin list.");
        }

        private static void AddSkinSets(Dictionary<string, DiceSkin> skins)
        {
            var config = AssetDatabase.LoadAssetAtPath<DataUiConfig>(DataUiConfigPath);
            if (config == null) { Debug.LogWarning("DiceThrower: " + DataUiConfigPath + " not found; run DiceThrower > Build Config, Prefabs and Scene first."); return; }

            var list = new List<SkinSetSpec>(config.skinSets);
            foreach (Pair p in Pairs)
            {
                DiceSkin attack, damage;
                if (!skins.TryGetValue(p.family + "/" + p.attack, out attack) || !skins.TryGetValue(p.family + "/" + p.damage, out damage)) continue;
                if (list.Exists(s => s.name == p.setName)) continue;
                list.Add(new SkinSetSpec { name = p.setName, skins = new[] { attack, damage } });
            }
            config.skinSets = list.ToArray();
            EditorUtility.SetDirty(config);
        }

        // ------------------------------------------------------------------ materials and textures

        private static Material GetOrCreateMaterial(string family, VariantJson v)
        {
            string path = MaterialDir + "/Dice_" + family + "_" + v.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Texture2D baseMap = ImportTexture(Root + "/" + v.baseMap, false);
            Texture2D normalMap = string.IsNullOrEmpty(v.normalMap) ? null : ImportTexture(Root + "/" + v.normalMap, true);
            Texture2D emission = string.IsNullOrEmpty(v.emissionMap) ? null : ImportTexture(Root + "/" + v.emissionMap, false);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = Path.GetFileNameWithoutExtension(path) };
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", Smoothness);
            mat.SetFloat("_Metallic", 0f);
            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (emission != null)
            {
                mat.SetTexture("_EmissionMap", emission);
                mat.SetColor("_EmissionColor", Color.white);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Texture2D ImportTexture(string path, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.maxTextureSize = TextureMaxSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color ToColor(float[] rgb)
        {
            return new Color(rgb[0], rgb[1], rgb[2], 1f);
        }
    }
}
