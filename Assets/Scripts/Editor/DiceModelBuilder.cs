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
    /// variant, and the list of dice sets the Dice tab offers (every colour variant of every model). Nothing that already exists is overwritten.
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

        [MenuItem("DiceThrower/Build Dice Model Sets")]
        public static void Build()
        {
            BuilderAssets.EnsureFolder(MaterialDir);
            var sets = new Dictionary<string, DiceModelSet>();
            var diceSets = new List<DiceSetSpec>();         // every colour variant, in file order

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
                    DiceSkin skin = BuilderAssets.GetOrCreate<DiceSkin>(skinPath, s =>
                    {
                        var so = new SerializedObject(s);
                        so.FindProperty("body").colorValue = ToColor(v.body);
                        so.FindProperty("ink").colorValue = ToColor(v.ink);
                        so.FindProperty("model").objectReferenceValue = set;
                        so.FindProperty("variant").intValue = index;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    });
                    diceSets.Add(new DiceSetSpec { name = Capitalize(file.family) + " " + v.name, skin = skin });
                }
            }

            AddDiceSets(diceSets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DiceThrower: dice model sets built (" + sets.Count + " models, " + diceSets.Count + " skins). The Dice tab lists " + diceSets.Count + " dice sets. Next: DiceThrower > Render Dice Sprite Atlases.");
        }

        /// <summary>Appends the dice sets that the Dice tab does not list yet; the saved choice is an index, so the existing order never changes.</summary>
        private static void AddDiceSets(List<DiceSetSpec> found)
        {
            var config = AssetDatabase.LoadAssetAtPath<DataUiConfig>(DataUiConfigPath);
            if (config == null) { Debug.LogWarning("DiceThrower: " + DataUiConfigPath + " not found; run DiceThrower > Build Config, Prefabs and Scene first."); return; }

            var list = new List<DiceSetSpec>(config.diceSets);
            list.RemoveAll(d => d == null || d.skin == null); // a skin deleted by hand
            foreach (DiceSetSpec spec in found)
            {
                if (!list.Exists(d => d.skin == spec.skin)) list.Add(spec);
            }
            config.diceSets = list.ToArray();
            EditorUtility.SetDirty(config);
        }

        private static string Capitalize(string text)
        {
            return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
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
