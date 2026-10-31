using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;
using DiceThrower.Game;
using DiceThrower.UI;
using Object = UnityEngine.Object;

namespace DiceThrower.EditorTools
{
    /// <summary>Builds the Die, DiceTray and RollScreen prefabs and wires config references into them.</summary>
    internal static class PrefabBuilder
    {
        internal static GameObject BuildDie(PhysicsMaterial physics)
        {
            var go = new GameObject("Die");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            var col = go.AddComponent<MeshCollider>();
            col.convex = true;
            col.sharedMaterial = physics;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = BuilderDefaults.DieMass;
            rb.linearDamping = BuilderDefaults.LinearDamping;
            rb.angularDamping = BuilderDefaults.AngularDamping;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.sleepThreshold = BuilderDefaults.SleepThreshold;
            go.AddComponent<DieBody>();
            return go;
        }

        /// <summary>
        /// The tray holds only the dice and the logic: a theme's scene brings the play surface, rim, walls and look (ThemeSceneHost).
        /// </summary>
        internal static GameObject BuildTray(TrayConfig config, DieBody diePrefab, Material diceMat)
        {
            var root = new GameObject("DiceTray");

            var dice = new GameObject("Dice");
            dice.transform.SetParent(root.transform, false);

            var tray = root.AddComponent<DiceTray>();
            var so = new SerializedObject(tray);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("diePrefab").objectReferenceValue = diePrefab;
            so.FindProperty("dieMaterial").objectReferenceValue = diceMat;
            so.FindProperty("diceRoot").objectReferenceValue = dice.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        internal static GameObject BuildScreen()
        {
            var go = new GameObject("RollScreen");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = BuilderAssets.GetOrCreatePanelSettings();
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BuilderPaths.Uxml);
            go.AddComponent<RollScreenView>();
            return go;
        }

        /// <summary>Adds what the RollScreen prefab is missing (the FeedbackPlayer) and fills every empty config reference. References you set yourself are left alone.</summary>
        internal static void WireScreenPrefab(RollUiConfig ui, RulesConfig rules, CameraFitConfig cameraFit, DataUiConfig data, DicePreviewConfig preview, FeedbackConfig feedback)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BuilderPaths.ScreenPrefab);
            try
            {
                var so = new SerializedObject(root.GetComponent<RollScreenView>());
                bool changed = BuilderAssets.AssignIfEmpty(so, "config", ui);
                changed |= BuilderAssets.AssignIfEmpty(so, "rules", rules);
                changed |= BuilderAssets.AssignIfEmpty(so, "cameraFit", cameraFit);
                changed |= BuilderAssets.AssignIfEmpty(so, "dataConfig", data);
                changed |= BuilderAssets.AssignIfEmpty(so, "previewConfig", preview);
                so.ApplyModifiedPropertiesWithoutUndo();

                FeedbackPlayer player = root.GetComponent<FeedbackPlayer>();
                if (player == null)
                {
                    player = root.AddComponent<FeedbackPlayer>(); // brings its AudioSource along
                    changed = true;
                }
                var playerSo = new SerializedObject(player);
                changed |= BuilderAssets.AssignIfEmpty(playerSo, "config", feedback);
                playerSo.ApplyModifiedPropertiesWithoutUndo();

                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, BuilderPaths.ScreenPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
