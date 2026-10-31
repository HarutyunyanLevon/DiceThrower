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
            PhysicsMaterial dicePhysics = BuilderAssets.GetOrCreatePhysicsMaterial();

            var trayConfig = BuilderAssets.GetOrCreate<TrayConfig>(BuilderPaths.TrayConfig);
            var cameraFit = BuilderAssets.GetOrCreate<CameraFitConfig>(BuilderPaths.CameraFit);
            var rules = BuilderAssets.GetOrCreate<RulesConfig>(BuilderPaths.Rules);
            var uiConfig = BuilderAssets.GetOrCreate<RollUiConfig>(BuilderPaths.UiConfig, c =>
            {
                c.sets = new[]
                {
                    new RollSetSpec { caption = "ATTACK", rowClass = "dice-row--atk" },
                    new RollSetSpec { caption = "DAMAGE", rowClass = "dice-row--dmg" },
                };
            });
            var dataConfig = BuilderAssets.GetOrCreate<DataUiConfig>(BuilderPaths.DataUiConfig); // its dice sets come from DiceThrower > Build Dice Model Sets, its themes from DiceThrower > Themes > Build All Themes
            var preview = BuilderAssets.GetOrCreate<DicePreviewConfig>(BuilderPaths.PreviewConfig);

            FeedbackConfig feedback = FeedbackAssets.GetOrCreate(BuilderPaths.FeedbackConfig);

            DieBody diePrefab = BuilderAssets.GetOrCreatePrefab<DieBody>(BuilderPaths.DiePrefab, () => PrefabBuilder.BuildDie(dicePhysics));
            BuilderAssets.GetOrCreatePrefab<DiceTray>(BuilderPaths.TrayPrefab, () => PrefabBuilder.BuildTray(trayConfig, diePrefab, diceMat));
            BuilderAssets.GetOrCreatePrefab<RollScreenView>(BuilderPaths.ScreenPrefab, PrefabBuilder.BuildScreen);
            PrefabBuilder.WireScreenPrefab(uiConfig, rules, cameraFit, dataConfig, preview, feedback); // also fills references and components added after the prefab was first built

            if (!File.Exists(BuilderPaths.Scene)) SceneBuilder.BuildScene(cameraFit.pitchDegrees);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(BuilderPaths.Scene);
            SceneBuilder.RepairTray();
            Debug.Log("DiceThrower: configs in " + BuilderPaths.ConfigDir + ", prefabs in " + BuilderPaths.PrefabDir + ", scene " + BuilderPaths.Scene + ". Run DiceThrower > Themes > Build All Themes, then press Play.");
        }
    }
}
