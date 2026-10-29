using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DiceThrower.Core;
using DiceThrower.Game;
using DiceThrower.UI;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Renders every dice set of the Dice tab (DataUiConfig.diceSets) into one PNG sprite atlas: a row of square cells, one per die type
    /// (d4 d6 d8 d10 d12 d20 d%), transparent background. The look (camera, pose, light, cell size, folder) is in the DicePreviewConfig asset (made with defaults when missing).
    /// The PNG is imported as an uncompressed texture and set as the skin's sprite atlas, which the UI cuts into die button pictures.
    /// A die the set's model lacks is left empty (the UI then falls back to a plain die shape). Existing atlases are rendered again.
    /// </summary>
    public static class DiceAtlasBuilder
    {
        private const int MaxTextureSize = 2048;

        [MenuItem("DiceThrower/Render Dice Sprite Atlases")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("DiceThrower: leave Play mode before rendering the dice sprite atlases.");
                return;
            }

            var dataConfig = AssetDatabase.LoadAssetAtPath<DataUiConfig>(BuilderPaths.DataUiConfig);
            if (dataConfig == null)
            {
                Debug.LogWarning("DiceThrower: " + BuilderPaths.DataUiConfig + " not found; run DiceThrower > Build Config, Prefabs and Scene first.");
                return;
            }
            if (dataConfig.diceSets == null || dataConfig.diceSets.Length == 0)
            {
                Debug.LogWarning("DiceThrower: the data config lists no dice sets; run DiceThrower > Build Dice Model Sets first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuilderAssets.EnsureFolder(BuilderPaths.ConfigDir);

            string previousScene = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // no other light or camera disturbs the pictures
            dataConfig = AssetDatabase.LoadAssetAtPath<DataUiConfig>(BuilderPaths.DataUiConfig); // NewScene unloads unused assets: load them again
            DicePreviewConfig preview = BuilderAssets.GetOrCreate<DicePreviewConfig>(BuilderPaths.PreviewConfig); // made here with its defaults when missing
            string folder = preview.atlasFolder; // read now: reopening the scene may unload the asset again
            BuilderAssets.EnsureFolder(folder);

            int done = 0;
            try
            {
                using (var rig = new DicePreviewRig(preview, 1, preview.spriteSize, true))
                {
                    for (int i = 0; i < dataConfig.diceSets.Length; i++)
                    {
                        DiceSkin skin = dataConfig.diceSets[i].skin;
                        if (skin == null) continue;
                        EditorUtility.DisplayProgressBar("Rendering dice sprite atlases", skin.name, (float)i / dataConfig.diceSets.Length);
                        if (RenderAtlas(rig, preview, skin)) done++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DiceThrower: rendered " + done + " dice sprite atlases into " + folder + ". The Dice tab and the dice rows use them from the next Play.");
        }

        /// <summary>Renders one skin's seven dice into a PNG, imports it and sets it as the skin's atlas.</summary>
        private static bool RenderAtlas(DicePreviewRig rig, DicePreviewConfig preview, DiceSkin skin)
        {
            int cell = preview.spriteSize;
            int width = cell * DieInfo.TypeCount;
            var pixels = new Color32[width * cell]; // all transparent

            for (int t = 0; t < DieInfo.TypeCount; t++)
            {
                DieKind kind = DiceSprites.KindOf((DieType)t);
                if (!rig.SetDie(0, skin, kind))
                {
                    Debug.LogWarning("DiceThrower: " + skin.name + " has no " + kind + " model; its cell stays empty.");
                    continue;
                }
                rig.Render(0);
                Color32[] picture = rig.Capture(0);
                for (int y = 0; y < cell; y++) Array.Copy(picture, y * cell, pixels, y * width + t * cell, cell);
            }

            string path = preview.atlasFolder + "/Atlas_" + skin.name.Replace("Skin_", "") + ".png";
            var texture = new Texture2D(width, cell, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("DiceThrower: could not import " + path);
                return false;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var so = new SerializedObject(skin);
            so.FindProperty("spriteAtlas").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skin);
            return true;
        }
    }
}
