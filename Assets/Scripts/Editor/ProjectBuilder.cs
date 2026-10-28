using System;
using System.IO;
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
    /// <summary>
    /// Creates the config assets, materials, prefabs and the Roll scene. Nothing that already exists
    /// is overwritten, so tuned assets survive; delete an asset and run the menu item to regenerate it.
    /// The numbers below are only the starting values written into NEW assets.
    /// </summary>
    public static class ProjectBuilder
    {
        private const string ConfigDir = "Assets/Config";
        private const string PrefabDir = "Assets/Prefabs";
        private const string MaterialDir = "Assets/Materials";
        private const string UiDir = "Assets/UI";
        private const string SceneDir = "Assets/Scenes";

        private static readonly string DiceMaterialPath = MaterialDir + "/DiceLit.mat";
        private static readonly string FloorMaterialPath = MaterialDir + "/TrayFloor.mat";
        private static readonly string RailMaterialPath = MaterialDir + "/TrayRail.mat";
        private static readonly string PhysicsPath = ConfigDir + "/DicePhysics.asset";
        private static readonly string TrayConfigPath = ConfigDir + "/TrayConfig.asset";
        private static readonly string CameraFitPath = ConfigDir + "/CameraFitConfig.asset";
        private static readonly string RulesPath = ConfigDir + "/RulesConfig.asset";
        private static readonly string UiConfigPath = ConfigDir + "/RollUiConfig.asset";
        private static readonly string AttackSkinPath = ConfigDir + "/Skin_Attack.asset";
        private static readonly string DamageSkinPath = ConfigDir + "/Skin_Damage.asset";
        private static readonly string ObsidianSkinPath = ConfigDir + "/Skin_Attack_Obsidian.asset";
        private static readonly string EmeraldSkinPath = ConfigDir + "/Skin_Damage_Emerald.asset";
        private static readonly string DataUiConfigPath = ConfigDir + "/DataUiConfig.asset";
        private static readonly string FeedbackConfigPath = ConfigDir + "/FeedbackConfig.asset";
        private static readonly string DiePrefabPath = PrefabDir + "/Die.prefab";
        private static readonly string TrayPrefabPath = PrefabDir + "/DiceTray.prefab";
        private static readonly string ScreenPrefabPath = PrefabDir + "/RollScreen.prefab";
        private static readonly string PanelSettingsPath = UiDir + "/RollPanelSettings.asset";
        private static readonly string UxmlPath = UiDir + "/RollScreen.uxml";
        private static readonly string ScenePath = SceneDir + "/Roll.unity";

        private static class Defaults
        {
            // tray geometry (world units)
            public const float TrayWidth = 7f, TrayDepth = 10.5f;
            public const float WallThickness = 0.6f, RailHeight = 0.5f, WallHeight = 14f, FloorThickness = 0.5f;
            public static readonly Color Floor = new Color(0.11f, 0.33f, 0.25f);
            public static readonly Color Rail = new Color(0.54f, 0.35f, 0.17f);

            // die rigidbody and surface
            public const float DieMass = 1f, LinearDamping = 0.05f, AngularDamping = 0.15f, SleepThreshold = 0.3f;
            public const float Bounciness = 0.25f, DynamicFriction = 0.45f, StaticFriction = 0.55f;

            // dice skins
            public static readonly Color AttackBody = new Color32(239, 230, 208, 255);
            public static readonly Color AttackInk = new Color32(26, 18, 8, 255);
            public static readonly Color DamageBody = new Color32(168, 50, 31, 255);
            public static readonly Color DamageInk = Color.white;
            public static readonly Color ObsidianBody = new Color32(38, 40, 52, 255);
            public static readonly Color ObsidianInk = new Color32(224, 178, 90, 255);
            public static readonly Color EmeraldBody = new Color32(34, 110, 72, 255);
            public static readonly Color EmeraldInk = new Color32(244, 230, 200, 255);
            public const string IvorySetName = "Ivory & Crimson", ObsidianSetName = "Obsidian & Emerald";

            // scene rig
            public static readonly Color CameraBackground = new Color(0.10f, 0.31f, 0.23f);
            public const float CameraFov = 40f;
            public const float LightIntensity = 1.1f;
            public static readonly Vector3 LightEuler = new Vector3(55f, -25f, 0f);

            // panel: the UI is designed for a phone and always fits the screen
            public static readonly Vector2Int ReferenceResolution = new Vector2Int(390, 844);
        }

        [MenuItem("DiceThrower/Build Config, Prefabs and Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (string dir in new[] { ConfigDir, PrefabDir, MaterialDir, UiDir, SceneDir }) EnsureFolder(dir);

            Material diceMat = GetOrCreateMaterial(DiceMaterialPath, Color.white);
            Material floorMat = GetOrCreateMaterial(FloorMaterialPath, Defaults.Floor);
            Material railMat = GetOrCreateMaterial(RailMaterialPath, Defaults.Rail);
            PhysicsMaterial dicePhysics = GetOrCreatePhysicsMaterial();

            var trayConfig = GetOrCreate<TrayConfig>(TrayConfigPath);
            var cameraFit = GetOrCreate<CameraFitConfig>(CameraFitPath);
            var rules = GetOrCreate<RulesConfig>(RulesPath);
            var attack = GetOrCreate<DiceSkin>(AttackSkinPath, s => SetSkin(s, Defaults.AttackBody, Defaults.AttackInk));
            var damage = GetOrCreate<DiceSkin>(DamageSkinPath, s => SetSkin(s, Defaults.DamageBody, Defaults.DamageInk));
            var obsidian = GetOrCreate<DiceSkin>(ObsidianSkinPath, s => SetSkin(s, Defaults.ObsidianBody, Defaults.ObsidianInk));
            var emerald = GetOrCreate<DiceSkin>(EmeraldSkinPath, s => SetSkin(s, Defaults.EmeraldBody, Defaults.EmeraldInk));
            var uiConfig = GetOrCreate<RollUiConfig>(UiConfigPath, c =>
            {
                c.sets = new[]
                {
                    new RollSetSpec { caption = "ATTACK", rowClass = "dice-row--atk" },
                    new RollSetSpec { caption = "DAMAGE", rowClass = "dice-row--dmg" },
                };
            });
            var dataConfig = GetOrCreate<DataUiConfig>(DataUiConfigPath, c =>
            {
                c.skinSets = new[]
                {
                    new SkinSetSpec { name = Defaults.IvorySetName, skins = new[] { attack, damage } },
                    new SkinSetSpec { name = Defaults.ObsidianSetName, skins = new[] { obsidian, emerald } },
                };
            });

            FeedbackConfig feedback = FeedbackAssets.GetOrCreate(FeedbackConfigPath);

            DieBody diePrefab = GetOrCreatePrefab<DieBody>(DiePrefabPath, () => BuildDie(dicePhysics));
            DiceTray trayPrefab = GetOrCreatePrefab<DiceTray>(TrayPrefabPath, () => BuildTray(trayConfig, diePrefab, diceMat, floorMat, railMat));
            GetOrCreatePrefab<RollScreenView>(ScreenPrefabPath, BuildScreen);
            WireScreenPrefab(uiConfig, rules, cameraFit, dataConfig, feedback); // also fills references and components added after the prefab was first built

            if (!File.Exists(ScenePath)) BuildScene(cameraFit.pitchDegrees);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("DiceThrower: configs in " + ConfigDir + ", prefabs in " + PrefabDir + ", scene " + ScenePath + ". Press Play.");
        }

        // ------------------------------------------------------------------ prefabs

        private static GameObject BuildDie(PhysicsMaterial physics)
        {
            var go = new GameObject("Die");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            var col = go.AddComponent<MeshCollider>();
            col.convex = true;
            col.sharedMaterial = physics;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Defaults.DieMass;
            rb.linearDamping = Defaults.LinearDamping;
            rb.angularDamping = Defaults.AngularDamping;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.sleepThreshold = Defaults.SleepThreshold;
            go.AddComponent<DieBody>();
            return go;
        }

        private static GameObject BuildTray(TrayConfig config, DieBody diePrefab, Material diceMat, Material floorMat, Material railMat)
        {
            float w = Defaults.TrayWidth, d = Defaults.TrayDepth, t = Defaults.WallThickness;
            float wallH = Defaults.WallHeight, railH = Defaults.RailHeight;

            var root = new GameObject("DiceTray");

            Transform floor = MakeCube(root.transform, "Floor", new Vector3(0f, -Defaults.FloorThickness * 0.5f, 0f),
                new Vector3(w + 2f * t, Defaults.FloorThickness, d + 2f * t), floorMat, true).transform;

            // four sides: a tall invisible collider plus a low visible rail
            MakeSide(root.transform, "Left", new Vector3(-(w + t) * 0.5f, 0f, 0f), new Vector3(t, wallH, d + 2f * t), railH, railMat);
            MakeSide(root.transform, "Right", new Vector3((w + t) * 0.5f, 0f, 0f), new Vector3(t, wallH, d + 2f * t), railH, railMat);
            MakeSide(root.transform, "Near", new Vector3(0f, 0f, -(d + t) * 0.5f), new Vector3(w + 2f * t, wallH, t), railH, railMat);
            MakeSide(root.transform, "Far", new Vector3(0f, 0f, (d + t) * 0.5f), new Vector3(w + 2f * t, wallH, t), railH, railMat);

            // ceiling so nothing escapes upward
            var ceiling = new GameObject("Ceiling");
            ceiling.transform.SetParent(root.transform, false);
            ceiling.transform.localPosition = new Vector3(0f, wallH, 0f);
            ceiling.AddComponent<BoxCollider>().size = new Vector3(w + 2f * t, Defaults.FloorThickness, d + 2f * t);

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

        private static GameObject BuildScreen()
        {
            var go = new GameObject("RollScreen");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = GetOrCreatePanelSettings();
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            go.AddComponent<RollScreenView>();
            return go;
        }

        /// <summary>Adds what the RollScreen prefab is missing (the FeedbackPlayer) and fills every empty config reference. References you set yourself are left alone.</summary>
        private static void WireScreenPrefab(RollUiConfig ui, RulesConfig rules, CameraFitConfig cameraFit, DataUiConfig data, FeedbackConfig feedback)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ScreenPrefabPath);
            try
            {
                var so = new SerializedObject(root.GetComponent<RollScreenView>());
                bool changed = AssignIfEmpty(so, "config", ui);
                changed |= AssignIfEmpty(so, "rules", rules);
                changed |= AssignIfEmpty(so, "cameraFit", cameraFit);
                changed |= AssignIfEmpty(so, "dataConfig", data);
                so.ApplyModifiedPropertiesWithoutUndo();

                FeedbackPlayer player = root.GetComponent<FeedbackPlayer>();
                if (player == null)
                {
                    player = root.AddComponent<FeedbackPlayer>(); // brings its AudioSource along
                    changed = true;
                }
                var playerSo = new SerializedObject(player);
                changed |= AssignIfEmpty(playerSo, "config", feedback);
                playerSo.ApplyModifiedPropertiesWithoutUndo();

                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool AssignIfEmpty(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property.objectReferenceValue != null) return false;
            property.objectReferenceValue = value;
            return true;
        }

        // ------------------------------------------------------------------ scene

        private static void BuildScene(float cameraPitch)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene unloads unused assets, so the prefabs are loaded again here instead of passed in
            var trayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrayPrefabPath);
            var screenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPrefabPath);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Defaults.CameraBackground;
            cam.fieldOfView = Defaults.CameraFov;
            camGo.AddComponent<AudioListener>();
            camGo.transform.rotation = Quaternion.Euler(cameraPitch, 0f, 0f); // the view moves it into place

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = Defaults.LightIntensity;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(Defaults.LightEuler);

            PrefabUtility.InstantiatePrefab(trayPrefab);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            PrefabUtility.InstantiatePrefab(screenPrefab);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        }

        // ------------------------------------------------------------------ helpers

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        private static T GetOrCreate<T>(string path, Action<T> init = null) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            if (init != null) init(created);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static T GetOrCreatePrefab<T>(string path, Func<GameObject> build) where T : Component
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null)
            {
                GameObject go = build();
                existing = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }
            return existing.GetComponent<T>();
        }

        private static Material GetOrCreateMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static PhysicsMaterial GetOrCreatePhysicsMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsPath);
            if (existing != null) return existing;
            var pm = new PhysicsMaterial("DicePhysics")
            {
                bounciness = Defaults.Bounciness,
                dynamicFriction = Defaults.DynamicFriction,
                staticFriction = Defaults.StaticFriction,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Average,
            };
            AssetDatabase.CreateAsset(pm, PhysicsPath);
            return pm;
        }

        private static PanelSettings GetOrCreatePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel != null) return panel;
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = Defaults.ReferenceResolution;
            panel.screenMatchMode = PanelScreenMatchMode.Shrink;
            string[] themes = AssetDatabase.FindAssets("UnityDefaultRuntimeTheme t:ThemeStyleSheet");
            if (themes.Length > 0)
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(themes[0]));
            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            return panel;
        }

        private static void SetSkin(DiceSkin skin, Color body, Color ink)
        {
            var so = new SerializedObject(skin);
            so.FindProperty("body").colorValue = body;
            so.FindProperty("ink").colorValue = ink;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
