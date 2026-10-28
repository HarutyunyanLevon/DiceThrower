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

        internal static GameObject BuildTray(TrayConfig config, DieBody diePrefab, Material diceMat, Material floorMat, Material railMat)
        {
            float w = BuilderDefaults.TrayWidth, d = BuilderDefaults.TrayDepth, t = BuilderDefaults.WallThickness;
            float wallH = BuilderDefaults.WallHeight, railH = BuilderDefaults.RailHeight;

            var root = new GameObject("DiceTray");

            Transform floor = MakeCube(root.transform, "Floor", new Vector3(0f, -BuilderDefaults.FloorThickness * 0.5f, 0f),
                new Vector3(w + 2f * t, BuilderDefaults.FloorThickness, d + 2f * t), floorMat, true).transform;

            // four sides: a tall invisible collider plus a low visible rail
            MakeSide(root.transform, "Left", new Vector3(-(w + t) * 0.5f, 0f, 0f), new Vector3(t, wallH, d + 2f * t), railH, railMat);
            MakeSide(root.transform, "Right", new Vector3((w + t) * 0.5f, 0f, 0f), new Vector3(t, wallH, d + 2f * t), railH, railMat);
            MakeSide(root.transform, "Near", new Vector3(0f, 0f, -(d + t) * 0.5f), new Vector3(w + 2f * t, wallH, t), railH, railMat);
            MakeSide(root.transform, "Far", new Vector3(0f, 0f, (d + t) * 0.5f), new Vector3(w + 2f * t, wallH, t), railH, railMat);

            // ceiling so nothing escapes upward
            var ceiling = new GameObject("Ceiling");
            ceiling.transform.SetParent(root.transform, false);
            ceiling.transform.localPosition = new Vector3(0f, wallH, 0f);
            ceiling.AddComponent<BoxCollider>().size = new Vector3(w + 2f * t, BuilderDefaults.FloorThickness, d + 2f * t);

            var dice = new GameObject("Dice");
            dice.transform.SetParent(root.transform, false);

            var tray = root.AddComponent<DiceTray>();
            var so = new SerializedObject(tray);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("diePrefab").objectReferenceValue = diePrefab;
            so.FindProperty("dieMaterial").objectReferenceValue = diceMat;
            so.FindProperty("floor").objectReferenceValue = floor;
            so.FindProperty("diceRoot").objectReferenceValue = dice.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void MakeSide(Transform parent, string name, Vector3 center, Vector3 colliderSize, float railHeight, Material railMat)
        {
            var wall = new GameObject(name + "Wall");
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = center + new Vector3(0f, colliderSize.y * 0.5f, 0f);
            wall.AddComponent<BoxCollider>().size = colliderSize;

            MakeCube(parent, name + "Rail", center + new Vector3(0f, railHeight * 0.5f, 0f),
                new Vector3(colliderSize.x, railHeight, colliderSize.z), railMat, false);
        }

        private static GameObject MakeCube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
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
        internal static void WireScreenPrefab(RollUiConfig ui, RulesConfig rules, CameraFitConfig cameraFit, DataUiConfig data, FeedbackConfig feedback)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BuilderPaths.ScreenPrefab);
            try
            {
                var so = new SerializedObject(root.GetComponent<RollScreenView>());
                bool changed = BuilderAssets.AssignIfEmpty(so, "config", ui);
                changed |= BuilderAssets.AssignIfEmpty(so, "rules", rules);
                changed |= BuilderAssets.AssignIfEmpty(so, "cameraFit", cameraFit);
                changed |= BuilderAssets.AssignIfEmpty(so, "dataConfig", data);
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
