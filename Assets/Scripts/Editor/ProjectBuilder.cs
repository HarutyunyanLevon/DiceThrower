using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Game;
using DiceThrower.UI;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Menu entry that creates the config assets, materials, prefabs and the Roll scene. Nothing that already
    /// exists is overwritten, so tuned assets survive; delete an asset and run the menu item to regenerate it.
    /// Paths and starting values live in <see cref="BuilderPaths"/> and <see cref="BuilderDefaults"/>;
    /// prefabs are built by <see cref="PrefabBuilder"/>, the scene by <see cref="SceneBuilder"/>.
    /// </summary>
    public static class ProjectBuilder
    {
        [MenuItem("DiceThrower/Build Config, Prefabs and Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (string dir in new[] { BuilderPaths.ConfigDir, BuilderPaths.PrefabDir, BuilderPaths.MaterialDir, BuilderPaths.UiDir, BuilderPaths.SceneDir })
                BuilderAssets.EnsureFolder(dir);

            Material diceMat = BuilderAssets.GetOrCreateMaterial(BuilderPaths.DiceMaterial, Color.white);
            Material floorMat = BuilderAssets.GetOrCreateMaterial(BuilderPaths.FloorMaterial, BuilderDefaults.Floor);
            Material railMat = BuilderAssets.GetOrCreateMaterial(BuilderPaths.RailMaterial, BuilderDefaults.Rail);
            PhysicsMaterial dicePhysics = BuilderAssets.GetOrCreatePhysicsMaterial();

            var trayConfig = BuilderAssets.GetOrCreate<TrayConfig>(BuilderPaths.TrayConfig);
            var cameraFit = BuilderAssets.GetOrCreate<CameraFitConfig>(BuilderPaths.CameraFit);
            var rules = BuilderAssets.GetOrCreate<RulesConfig>(BuilderPaths.Rules);
            var attack = BuilderAssets.GetOrCreate<DiceSkin>(BuilderPaths.AttackSkin, s => BuilderAssets.SetSkin(s, BuilderDefaults.AttackBody, BuilderDefaults.AttackInk));
            var damage = BuilderAssets.GetOrCreate<DiceSkin>(BuilderPaths.DamageSkin, s => BuilderAssets.SetSkin(s, BuilderDefaults.DamageBody, BuilderDefaults.DamageInk));
            var obsidian = BuilderAssets.GetOrCreate<DiceSkin>(BuilderPaths.ObsidianSkin, s => BuilderAssets.SetSkin(s, BuilderDefaults.ObsidianBody, BuilderDefaults.ObsidianInk));
            var emerald = BuilderAssets.GetOrCreate<DiceSkin>(BuilderPaths.EmeraldSkin, s => BuilderAssets.SetSkin(s, BuilderDefaults.EmeraldBody, BuilderDefaults.EmeraldInk));
            var uiConfig = BuilderAssets.GetOrCreate<RollUiConfig>(BuilderPaths.UiConfig, c =>
            {
                c.sets = new[]
                {
                    new RollSetSpec { caption = "ATTACK", rowClass = "dice-row--atk" },
                    new RollSetSpec { caption = "DAMAGE", rowClass = "dice-row--dmg" },
                };
            });
            var dataConfig = BuilderAssets.GetOrCreate<DataUiConfig>(BuilderPaths.DataUiConfig, c =>
            {
                c.skinSets = new[]
                {
                    new SkinSetSpec { name = BuilderDefaults.IvorySetName, skins = new[] { attack, damage } },
                    new SkinSetSpec { name = BuilderDefaults.ObsidianSetName, skins = new[] { obsidian, emerald } },
                };
            });

            FillThemes(dataConfig); // a DataUiConfig made before themes existed gets them here

            FeedbackConfig feedback = FeedbackAssets.GetOrCreate(BuilderPaths.FeedbackConfig);

            DieBody diePrefab = BuilderAssets.GetOrCreatePrefab<DieBody>(BuilderPaths.DiePrefab, () => PrefabBuilder.BuildDie(dicePhysics));
            BuilderAssets.GetOrCreatePrefab<DiceTray>(BuilderPaths.TrayPrefab, () => PrefabBuilder.BuildTray(trayConfig, diePrefab, diceMat, floorMat, railMat));
            BuilderAssets.GetOrCreatePrefab<RollScreenView>(BuilderPaths.ScreenPrefab, PrefabBuilder.BuildScreen);
            PrefabBuilder.WireScreenPrefab(uiConfig, rules, cameraFit, dataConfig, feedback); // also fills references and components added after the prefab was first built

            if (!File.Exists(BuilderPaths.Scene)) SceneBuilder.BuildScene(cameraFit.pitchDegrees);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(BuilderPaths.Scene);
            Debug.Log("DiceThrower: configs in " + BuilderPaths.ConfigDir + ", prefabs in " + BuilderPaths.PrefabDir + ", scene " + BuilderPaths.Scene + ". Press Play.");
        }

        /// <summary>Adds the theme list to a DataUiConfig that has none. A list you edited is left alone.</summary>
        private static void FillThemes(DataUiConfig config)
        {
            if (config.themes != null && config.themes.Length > 0) return;
            config.themes = new[]
            {
                new ThemeSpec { name = BuilderDefaults.TavernThemeName, sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BuilderPaths.TavernTheme) },
                new ThemeSpec { name = BuilderDefaults.MoonlightThemeName, sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BuilderPaths.MoonlightTheme) },
            };
            EditorUtility.SetDirty(config);
        }
    }
}
