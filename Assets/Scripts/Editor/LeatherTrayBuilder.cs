using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DiceThrower.Game;
using DiceThrower.UI;
using Object = UnityEngine.Object;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Builds the leather tray variants: a style asset, textures, materials, render + collision meshes, a physics material and a
    /// complete DiceTray prefab for each, plus a preview scene with all variants side by side.
    /// Style assets, textures and prefabs that already exist are kept (tune them freely); meshes, materials and physics materials
    /// are generated from the style every time, so editing a style and running the menu item again is how a change reaches them.
    /// </summary>
    public static class LeatherTrayBuilder
    {
        private const string Root = "Assets/LeatherTrays";
        private const string StyleDir = Root + "/Styles", TextureDir = Root + "/Textures", MaterialDir = Root + "/Materials", MeshDir = Root + "/Meshes";
        private const string SuedeAlbedo = TextureDir + "/Suede_Albedo.png", SuedeNormal = TextureDir + "/Suede_Normal.png";
        private const int SuedeSeed = 77;
        private const string PreviewScene = BuilderPaths.SceneDir + "/TrayVariants.unity";

        // preview scene layout
        private const float PreviewGap = 1.8f, PreviewPitch = 68f, PreviewDistance = 62f;
        private const string ThrowSkinAttack = BuilderPaths.ConfigDir + "/Skin_Attack.asset", ThrowSkinDamage = BuilderPaths.ConfigDir + "/Skin_Damage.asset";

        private const string Menu = "DiceThrower/Tray/";

        // ------------------------------------------------------------------ menu

        [MenuItem(Menu + "Build Leather Tray Variants")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!Build(false)) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildPreviewScene();
            Debug.Log("DiceThrower: leather trays built in " + Root + " (prefabs in " + BuilderPaths.PrefabDir + "), preview scene " + PreviewScene + ". Press Play and use the button to throw dice.");
        }

        [MenuItem(Menu + "Reset Leather Styles to Presets")]
        public static void ResetStyles()
        {
            foreach (LeatherTrayPreset preset in LeatherTrayPresets.All)
            {
                var style = AssetDatabase.LoadAssetAtPath<LeatherTrayStyle>(StylePath(preset));
                if (style == null) continue;
                var fresh = ScriptableObject.CreateInstance<LeatherTrayStyle>();
                LeatherTrayPresets.Fill(fresh, preset);
                string keepName = style.name;
                EditorUtility.CopySerialized(fresh, style); // keeps the asset (and everything that points at it), replaces its values
                style.name = keepName;
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(style);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("DiceThrower: leather styles reset to the preset values. Run Build Leather Tray Variants (or Regenerate Leather Textures) to apply them.");
        }

        [MenuItem(Menu + "Regenerate Leather Textures")]
        public static void RegenerateTextures()
        {
            if (!Build(true)) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DiceThrower: leather textures regenerated.");
        }

        [MenuItem(Menu + "Open Variant Preview Scene")]
        public static void OpenPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScene) == null) { BuildAll(); return; }
            EditorSceneManager.OpenScene(PreviewScene);
        }

        [MenuItem(Menu + "Use in Open Scene/Folded Tan")] private static void UseFoldedTan() { UseInScene(LeatherTrayPreset.FoldedTan); }
        [MenuItem(Menu + "Use in Open Scene/Rolled Chestnut")] private static void UseRolledChestnut() { UseInScene(LeatherTrayPreset.RolledChestnut); }
        [MenuItem(Menu + "Use in Open Scene/Stitched Box")] private static void UseStitchedBox() { UseInScene(LeatherTrayPreset.StitchedBox); }
        [MenuItem(Menu + "Use in Open Scene/Tooled Wine")] private static void UseTooledWine() { UseInScene(LeatherTrayPreset.TooledWine); }

        // ------------------------------------------------------------------ build

        private static bool Build(bool forceTextures)
        {
            var trayConfig = AssetDatabase.LoadAssetAtPath<TrayConfig>(BuilderPaths.TrayConfig);
            var diePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuilderPaths.DiePrefab);
            var diceMat = AssetDatabase.LoadAssetAtPath<Material>(BuilderPaths.DiceMaterial);
            if (trayConfig == null || diePrefab == null || diceMat == null || diePrefab.GetComponent<DieBody>() == null)
            {
                Debug.LogError("DiceThrower: run DiceThrower > Build Config, Prefabs and Scene first (the tray config, Die prefab and dice material are missing).");
                return false;
            }

            foreach (string dir in new[] { Root, StyleDir, TextureDir, MaterialDir, MeshDir, BuilderPaths.PrefabDir })
                BuilderAssets.EnsureFolder(dir);

            if (forceTextures || !System.IO.File.Exists(System.IO.Path.GetFullPath(SuedeAlbedo)))
                LeatherTextures.WriteSuede(SuedeAlbedo, SuedeNormal, SuedeSeed);

            foreach (LeatherTrayPreset preset in LeatherTrayPresets.All)
                BuildVariant(preset, forceTextures, trayConfig, diePrefab.GetComponent<DieBody>(), diceMat);
            return true;
        }

        private static string Name(LeatherTrayPreset p) { return LeatherTrayPresets.AssetName(p); }
        private static string StylePath(LeatherTrayPreset p) { return StyleDir + "/" + Name(p) + ".asset"; }
        private static string PrefabPath(LeatherTrayPreset p) { return BuilderPaths.PrefabDir + "/DiceTray_" + Name(p) + ".prefab"; }

        private static void BuildVariant(LeatherTrayPreset preset, bool forceTextures, TrayConfig trayConfig, DieBody diePrefab, Material diceMat)
        {
            string n = Name(preset);
            var style = BuilderAssets.GetOrCreate<LeatherTrayStyle>(StylePath(preset), s => LeatherTrayPresets.Fill(s, preset));

            string grainAlbedo = TextureDir + "/" + n + "_Grain_Albedo.png", grainNormal = TextureDir + "/" + n + "_Grain_Normal.png";
            if (forceTextures || !System.IO.File.Exists(System.IO.Path.GetFullPath(grainAlbedo)))
                LeatherTextures.WriteGrain(grainAlbedo, grainNormal, style.grainCells, style.seed);

            // meshes
            LeatherTrayMeshData render = LeatherTrayGeometry.BuildRender(style);
            LeatherTrayMeshData collision = LeatherTrayGeometry.BuildCollision(style);
            Mesh renderMesh = SaveMesh(render, MeshDir + "/" + n + "_Render.asset", n + " Render");
            Mesh collisionMesh = SaveMesh(collision, MeshDir + "/" + n + "_Collision.asset", n + " Collision");

            // materials: the lining's texture tiles a whole number of times around the tray so it closes without a seam
            float lining = Mathf.Max(1, Mathf.RoundToInt(render.GrainTiles * style.GrainTileSize / Mathf.Max(0.01f, style.liningTileSize))) / (float)render.GrainTiles;
            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(grainAlbedo);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(grainNormal);
            var suedeAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(SuedeAlbedo);
            var suedeNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(SuedeNormal);
            Material top = LeatherMaterial(MaterialDir + "/" + n + "_Top.mat", style.topColor, albedoTex, normalTex, 1f, style.grainBump, style.topSmoothness);
            Material rim = LeatherMaterial(MaterialDir + "/" + n + "_Lining.mat", style.liningColor, suedeAlbedo, suedeNormal, lining, style.liningBump, style.liningSmoothness);
            Material thread = PlainMaterial(MaterialDir + "/" + n + "_Thread.mat", style.threadColor, 0f, 0.3f);
            Material hardware = PlainMaterial(MaterialDir + "/" + n + "_Hardware.mat", style.hardwareColor, 0.9f, 0.65f);

            PhysicsMaterial physics = PhysicsFor(MaterialDir + "/" + n + "_Physics.asset", style);

            // prefab: created once; afterwards only its size marker and height follow the style
            string prefabPath = PrefabPath(preset);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                GameObject go = BuildPrefabRoot(n, style, render, renderMesh, collisionMesh, new[] { top, rim, thread, hardware }, physics, trayConfig, diePrefab, diceMat);
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);
            }
            else
            {
                RefreshPrefab(prefabPath, render);
            }
        }

        // ------------------------------------------------------------------ assets

        private static Mesh SaveMesh(LeatherTrayMeshData data, string path, string meshName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh mesh = data.ToMesh(existing);
            mesh.name = meshName;
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material LoadOrCreateMaterial(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material LeatherMaterial(string path, Color tint, Texture2D albedo, Texture2D normal, float tiling, float bump, float smoothness)
        {
            Material m = LoadOrCreateMaterial(path);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BaseMap", albedo);
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", bump);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_SmoothnessTextureChannel", 1f); // smoothness from the albedo's alpha
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material PlainMaterial(string path, Color color, float metallic, float smoothness)
        {
            Material m = LoadOrCreateMaterial(path);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static PhysicsMaterial PhysicsFor(string path, LeatherTrayStyle style)
        {
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm == null)
            {
                pm = new PhysicsMaterial(System.IO.Path.GetFileNameWithoutExtension(path));
                AssetDatabase.CreateAsset(pm, path);
            }
            pm.bounciness = style.bounciness;
            pm.dynamicFriction = style.dynamicFriction;
            pm.staticFriction = style.staticFriction;
            pm.bounceCombine = PhysicsMaterialCombine.Average; // same rule as the dice's own material
            pm.frictionCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        // ------------------------------------------------------------------ prefab

        private static GameObject BuildPrefabRoot(string n, LeatherTrayStyle style, LeatherTrayMeshData render, Mesh renderMesh, Mesh collisionMesh,
            Material[] materials, PhysicsMaterial physics, TrayConfig trayConfig, DieBody diePrefab, Material diceMat)
        {
            var root = new GameObject("DiceTray_" + n);

            // the tray's outer size, read by the camera fit and by the dice placement; it has no renderer or collider
            var floor = new GameObject("Floor");
            floor.transform.SetParent(root.transform, false);
            ApplyMarker(floor.transform, render);

            var leather = new GameObject("Leather");
            leather.transform.SetParent(root.transform, false);
            leather.AddComponent<MeshFilter>().sharedMesh = renderMesh;
            var mr = leather.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            var mc = leather.AddComponent<MeshCollider>();
            mc.sharedMesh = collisionMesh;
            mc.convex = false;
            mc.sharedMaterial = physics;
            var body = leather.AddComponent<LeatherTray>();
            var bodySo = new SerializedObject(body);
            bodySo.FindProperty("style").objectReferenceValue = style;
            bodySo.ApplyModifiedPropertiesWithoutUndo();

            var dice = new GameObject("Dice");
            dice.transform.SetParent(root.transform, false);

            var tray = root.AddComponent<DiceTray>();
            var so = new SerializedObject(tray);
            so.FindProperty("config").objectReferenceValue = trayConfig;
            so.FindProperty("diePrefab").objectReferenceValue = diePrefab;
            so.FindProperty("dieMaterial").objectReferenceValue = diceMat;
            so.FindProperty("floor").objectReferenceValue = floor.transform;
            so.FindProperty("diceRoot").objectReferenceValue = dice.transform;
            so.FindProperty("height").floatValue = render.Height;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void ApplyMarker(Transform floor, LeatherTrayMeshData render)
        {
            floor.localPosition = new Vector3(0f, -BuilderDefaults.FloorThickness * 0.5f, 0f);
            floor.localScale = new Vector3(render.OuterExtent.x, BuilderDefaults.FloorThickness, render.OuterExtent.y);
        }

        private static void RefreshPrefab(string path, LeatherTrayMeshData render)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform floor = root.transform.Find("Floor");
                if (floor != null) ApplyMarker(floor, render);
                var tray = root.GetComponent<DiceTray>();
                if (tray != null)
                {
                    var so = new SerializedObject(tray);
                    so.FindProperty("height").floatValue = render.Height;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ scenes

        private static void BuildPreviewScene()
        {
            BuilderAssets.EnsureFolder(BuilderPaths.SceneDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BuilderDefaults.CameraBackground;
            cam.fieldOfView = BuilderDefaults.CameraFov;
            camGo.AddComponent<AudioListener>();
            Quaternion rot = Quaternion.Euler(PreviewPitch, 0f, 0f);
            camGo.transform.rotation = rot;
            camGo.transform.position = -(rot * Vector3.forward) * PreviewDistance;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = BuilderDefaults.LightIntensity;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(BuilderDefaults.LightEuler);

            // 2 x 2 grid
            Vector2 size = Vector2.zero;
            foreach (LeatherTrayPreset p in LeatherTrayPresets.All)
            {
                var st = AssetDatabase.LoadAssetAtPath<LeatherTrayStyle>(StylePath(p));
                if (st != null) size = Vector2.Max(size, st.outerSize);
            }
            for (int i = 0; i < LeatherTrayPresets.All.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(LeatherTrayPresets.All[i]));
                if (prefab == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                int col = i % 2, row = i / 2;
                inst.transform.position = new Vector3((col - 0.5f) * (size.x + PreviewGap), 0f, (row - 0.5f) * (size.y + PreviewGap));
            }

            var demoGo = new GameObject("Demo");
            var demo = demoGo.AddComponent<LeatherTrayDemo>();
            var skins = new[]
            {
                AssetDatabase.LoadAssetAtPath<DiceSkin>(ThrowSkinAttack),
                AssetDatabase.LoadAssetAtPath<DiceSkin>(ThrowSkinDamage),
            };
            var so = new SerializedObject(demo);
            SerializedProperty list = so.FindProperty("skins");
            list.arraySize = skins.Length;
            for (int i = 0; i < skins.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = skins[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), PreviewScene);
        }

        private static void UseInScene(LeatherTrayPreset preset)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(preset));
            if (prefab == null)
            {
                Debug.LogError("DiceThrower: build the variants first (DiceThrower > Tray > Build Leather Tray Variants).");
                return;
            }
            var old = Object.FindFirstObjectByType<DiceTray>();
            if (old == null)
            {
                Debug.LogError("DiceThrower: the open scene has no DiceTray to replace. Open Roll.unity.");
                return;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.gameObject.scene);
            inst.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
            Undo.RegisterCreatedObjectUndo(inst, "Use leather tray");
            var newTray = inst.GetComponent<DiceTray>();

            // a screen that points at the old tray points at the new one
            foreach (RollScreenView view in Object.FindObjectsByType<RollScreenView>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(view);
                SerializedProperty p = so.FindProperty("tray");
                if (p != null && p.objectReferenceValue == old)
                {
                    p.objectReferenceValue = newTray;
                    so.ApplyModifiedProperties();
                }
            }

            Undo.DestroyObjectImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(inst.scene);
            Debug.Log("DiceThrower: the open scene now uses the " + LeatherTrayPresets.DisplayName(preset) + " tray. Save the scene to keep it.");
        }
    }
}
