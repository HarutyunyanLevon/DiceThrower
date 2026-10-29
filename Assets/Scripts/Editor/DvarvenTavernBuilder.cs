using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Game;
using DiceThrower.UI;
using Object = UnityEngine.Object;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Builds the Dvarven Tavern theme: the oak table surface, the stone-floor backdrop, the lanterns and the props as one ThemeScene prefab,
    /// and registers the theme (style sheet + scene) in the DataUiConfig. Sizes and colours come from the TableSurfaceStyle asset.
    /// Run it again after changing the style: the prefab, materials and the theme entry are updated in place, so references stay valid.
    /// </summary>
    public static class DvarvenTavernBuilder
    {
        private const string ThemeName = "Dvarven Tavern";
        private const string Dir = "Assets/Themes/DvarvenTavern";
        private const string MatDir = Dir + "/Materials";
        private const string StylePath = Dir + "/TableSurfaceStyle.asset";
        private const string PhysicsPath = Dir + "/Wood_Physics.asset";
        private const string PrefabPath = Dir + "/Theme_DvarvenTavern.prefab";
        private const string TexDir = "Assets/Textures/Dvarven";
        private const string UiTexDir = "Assets/UI/Dvarven";
        private const string SheetPath = "Assets/UI/Dvarven.uss";
        private const string LitShader = "Universal Render Pipeline/Lit";

        [MenuItem("DiceThrower/Themes/Build Dvarven Tavern")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            foreach (string dir in new[] { "Assets/Themes", Dir, MatDir }) BuilderAssets.EnsureFolder(dir);

            foreach (string name in new[] { "Ui_DvarvenPanel", "Ui_DvarvenButton", "Ui_DvarvenButtonGold", "Ui_DvarvenRollCoin" })
                SetupUiTexture(UiTexDir + "/" + name + ".png");

            var style = BuilderAssets.GetOrCreate<TableSurfaceStyle>(StylePath);
            PhysicsMaterial wood = GetOrCreateWoodPhysics(style);

            var topTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/Tex_DvarvenTableTop.png");
            var oakTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/Tex_DvarvenOak.png");
            var stoneTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/Tex_DvarvenStone.png");
            if (topTex == null || oakTex == null || stoneTex == null)
            {
                Debug.LogError("Dvarven Tavern: the textures in " + TexDir + " are missing.");
                return;
            }

            Material topMat = Mat("TableTop", Color.white, topTex, Vector2.one, 0.25f, 0f);
            Material oakMat = Mat("Oak", style.oakTint, oakTex, new Vector2(2f, 2f), 0.2f, 0f);
            Material stoneMat = Mat("StoneFloor", new Color(0.8f, 0.78f, 0.74f), stoneTex, Vector2.one * style.floorTiling, 0.08f, 0f);
            Material goldMat = Mat("Gold", style.goldColor, null, Vector2.one, 0.6f, 0.7f);
            Material mugMat = Mat("MugWood", style.mugColor, oakTex, Vector2.one, 0.2f, 0f);
            Material foamMat = Mat("Foam", style.foamColor, null, Vector2.one, 0.3f, 0f);
            Material lanternMat = Mat("LanternGlow", style.lanternColor, null, Vector2.one, 0.2f, 0f);
            lanternMat.EnableKeyword("_EMISSION");
            lanternMat.SetColor("_EmissionColor", style.lanternColor * 3f);

            GameObject root = BuildScene(style, wood, topMat, oakMat, stoneMat, goldMat, mugMat, foamMat, lanternMat);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            RegisterTheme(prefab.GetComponent<ThemeScene>());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DiceThrower: Dvarven Tavern built (" + PrefabPath + ") and added to the themes. Press Play and pick it in Settings > Theme.");
        }

        // ------------------------------------------------------------------ scene

        private static GameObject BuildScene(TableSurfaceStyle st, PhysicsMaterial wood, Material topMat, Material oakMat, Material stoneMat,
                                              Material goldMat, Material mugMat, Material foamMat, Material lanternMat)
        {
            Vector2 size = st.outerSize;
            float rw = st.rimWidth, rh = st.rimHeight, th = st.topThickness;

            var root = new GameObject("Theme_DvarvenTavern");
            var scene = root.AddComponent<ThemeScene>();

            var surface = new GameObject("Surface").transform;
            surface.SetParent(root.transform, false);
            surface.localScale = new Vector3(size.x, 1f, size.y); // only its scale matters: the outer size the camera fits

            // ---- table: the slab dice land on, the deco top, the visible rim
            Transform table = new GameObject("Table").transform;
            table.SetParent(root.transform, false);
            GameObject body = Prim(PrimitiveType.Cube, table, "Body", new Vector3(0f, -th * 0.5f, 0f), new Vector3(size.x, th, size.y), oakMat, true);
            body.GetComponent<Collider>().sharedMaterial = wood;

            Vector2 inner = new Vector2(size.x - 2f * rw, size.y - 2f * rw);
            GameObject top = Prim(PrimitiveType.Quad, table, "Top", new Vector3(0f, 0.002f, 0f), new Vector3(inner.x, inner.y, 1f), topMat, false);
            top.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // faces up; texture u = x, v = z

            float sx = size.x * 0.5f - rw * 0.5f, sz = size.y * 0.5f - rw * 0.5f;
            Prim(PrimitiveType.Cube, table, "RimLeft", new Vector3(-sx, rh * 0.5f, 0f), new Vector3(rw, rh, size.y), oakMat, false);
            Prim(PrimitiveType.Cube, table, "RimRight", new Vector3(sx, rh * 0.5f, 0f), new Vector3(rw, rh, size.y), oakMat, false);
            Prim(PrimitiveType.Cube, table, "RimNear", new Vector3(0f, rh * 0.5f, -sz), new Vector3(inner.x, rh, rw), oakMat, false);
            Prim(PrimitiveType.Cube, table, "RimFar", new Vector3(0f, rh * 0.5f, sz), new Vector3(inner.x, rh, rw), oakMat, false);

            // gold corner brackets on the rim
            foreach (Vector2 c in Corners(size.x * 0.5f - rw * 0.5f, size.y * 0.5f - rw * 0.5f))
            {
                GameObject b = Prim(PrimitiveType.Cube, table, "Bracket", new Vector3(c.x, rh + 0.03f, c.y), new Vector3(rw * 0.9f, 0.06f, rw * 0.9f), goldMat, false);
                b.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            }

            // ---- invisible walls: the rim's inner face, rising to the ceiling, so no die flies out over the low rim
            Transform walls = new GameObject("Walls").transform;
            walls.SetParent(root.transform, false);
            Wall(walls, "Left", new Vector3(-sx, st.wallHeight * 0.5f, 0f), new Vector3(rw, st.wallHeight, size.y), wood);
            Wall(walls, "Right", new Vector3(sx, st.wallHeight * 0.5f, 0f), new Vector3(rw, st.wallHeight, size.y), wood);
            Wall(walls, "Near", new Vector3(0f, st.wallHeight * 0.5f, -sz), new Vector3(inner.x, st.wallHeight, rw), wood);
            Wall(walls, "Far", new Vector3(0f, st.wallHeight * 0.5f, sz), new Vector3(inner.x, st.wallHeight, rw), wood);
            Wall(walls, "Ceiling", new Vector3(0f, st.wallHeight, 0f), new Vector3(size.x, 0.5f, size.y), wood);

            // ---- props (visual only)
            Transform props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            AddMug(props, st, mugMat, foamMat, new Vector2(-1f, 1f));
            AddMug(props, st, mugMat, foamMat, new Vector2(1f, -1f));
            AddCoins(props, st, goldMat);

            // ---- backdrop: a stone floor far below the table; the table's shadow falls on it
            Transform backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(root.transform, false);
            GameObject floor = Prim(PrimitiveType.Quad, backdrop, "StoneFloor", new Vector3(0f, -st.floorDepth, 0f), new Vector3(st.floorSize, st.floorSize, 1f), stoneMat, false);
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // ---- lanterns: a glowing orb and a flickering warm light, two opposite corners
            Transform lights = new GameObject("Lanterns").transform;
            lights.SetParent(root.transform, false);
            AddLantern(lights, st, lanternMat, new Vector3(-st.lanternOffset.x, st.lanternHeight, st.lanternOffset.y));
            AddLantern(lights, st, lanternMat, new Vector3(st.lanternOffset.x, st.lanternHeight, -st.lanternOffset.y));

            var so = new SerializedObject(scene);
            so.FindProperty("surface").objectReferenceValue = surface;
            so.FindProperty("rimHeight").floatValue = rh;
            so.FindProperty("lightColor").colorValue = st.lightColor;
            so.FindProperty("lightIntensity").floatValue = st.lightIntensity;
            so.FindProperty("lightEuler").vector3Value = st.lightEuler;
            so.FindProperty("cameraBackground").colorValue = st.cameraBackground;
            so.FindProperty("ambientColor").colorValue = st.ambientColor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void Wall(Transform parent, string name, Vector3 pos, Vector3 size, PhysicsMaterial mat)
        {
            var go = new GameObject("Wall" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = mat;
        }

        private static void AddMug(Transform parent, TableSurfaceStyle st, Material mugMat, Material foamMat, Vector2 corner)
        {
            float r = st.mugRadius, h = st.mugHeight, baseY = st.rimHeight;
            float x = corner.x * (st.outerSize.x * 0.5f - st.mugInset);
            float z = corner.y * (st.outerSize.y * 0.5f - st.mugInset);
            var mug = new GameObject("Mug").transform;
            mug.SetParent(parent, false);
            mug.localPosition = new Vector3(x, 0f, z);

            Prim(PrimitiveType.Cylinder, mug, "Body", new Vector3(0f, baseY + h * 0.5f, 0f), new Vector3(2f * r, h * 0.5f, 2f * r), mugMat, false);
            Prim(PrimitiveType.Cylinder, mug, "Foam", new Vector3(0f, baseY + h + 0.02f, 0f), new Vector3(2f * (r - 0.07f), 0.03f, 2f * (r - 0.07f)), foamMat, false);
            // handle: a small block on the outer side
            Prim(PrimitiveType.Cube, mug, "Handle", new Vector3(corner.x * (r + 0.08f), baseY + h * 0.5f, 0f), new Vector3(0.16f, h * 0.5f, 0.2f), mugMat, false);
        }

        private static void AddCoins(Transform parent, TableSurfaceStyle st, Material goldMat)
        {
            var rng = new System.Random(st.coinSeed);
            float rw = st.rimWidth;
            for (int i = 0; i < st.coinCount; i++)
            {
                // along the rim's middle line
                bool alongX = rng.NextDouble() < 0.5;
                float sign = rng.NextDouble() < 0.5 ? -1f : 1f;
                float t = (float)(rng.NextDouble() * 2.0 - 1.0);
                float x = alongX ? t * (st.outerSize.x * 0.5f - rw) : sign * (st.outerSize.x * 0.5f - rw * 0.5f);
                float z = alongX ? sign * (st.outerSize.y * 0.5f - rw * 0.5f) : t * (st.outerSize.y * 0.5f - rw);
                GameObject coin = Prim(PrimitiveType.Cylinder, parent, "Coin", new Vector3(x, st.rimHeight + st.coinThickness * 0.5f, z),
                                       new Vector3(2f * st.coinRadius, st.coinThickness * 0.5f, 2f * st.coinRadius), goldMat, false);
                coin.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            }
        }

        private static void AddLantern(Transform parent, TableSurfaceStyle st, Material glow, Vector3 pos)
        {
            var lantern = new GameObject("Lantern").transform;
            lantern.SetParent(parent, false);
            lantern.localPosition = pos;
            Prim(PrimitiveType.Sphere, lantern, "Orb", Vector3.zero, Vector3.one * 0.45f, glow, false);
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(lantern, false);
            light.type = LightType.Point;
            light.color = st.lanternColor;
            light.intensity = st.lanternIntensity;
            light.range = st.lanternRange;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<LightFlicker>();
        }

        private static Vector2[] Corners(float x, float z)
        {
            return new[] { new Vector2(-x, -z), new Vector2(x, -z), new Vector2(-x, z), new Vector2(x, z) };
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // ------------------------------------------------------------------ assets

        private static Material Mat(string name, Color color, Texture tex, Vector2 tiling, float smoothness, float metallic)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(LitShader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static PhysicsMaterial GetOrCreateWoodPhysics(TableSurfaceStyle st)
        {
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsPath);
            if (pm == null)
            {
                pm = new PhysicsMaterial("Wood_Physics");
                AssetDatabase.CreateAsset(pm, PhysicsPath);
            }
            pm.bounciness = st.bounciness;
            pm.dynamicFriction = st.dynamicFriction;
            pm.staticFriction = st.staticFriction;
            pm.bounceCombine = PhysicsMaterialCombine.Average;
            pm.frictionCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        /// <summary>Crisp UI textures: no mip maps, no compression, transparent edges stay transparent.</summary>
        private static void SetupUiTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        /// <summary>Adds the theme to the DataUiConfig, or updates its entry. The other themes and their order stay as they are.</summary>
        private static void RegisterTheme(ThemeScene scene)
        {
            var config = AssetDatabase.LoadAssetAtPath<DataUiConfig>(BuilderPaths.DataUiConfig);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);
            if (config == null || sheet == null)
            {
                Debug.LogError("Dvarven Tavern: " + BuilderPaths.DataUiConfig + " or " + SheetPath + " not found; the theme was not registered.");
                return;
            }

            var list = new System.Collections.Generic.List<ThemeSpec>(config.themes ?? new ThemeSpec[0]);
            ThemeSpec spec = list.Find(t => t.name == ThemeName);
            if (spec == null)
            {
                spec = new ThemeSpec { name = ThemeName };
                list.Add(spec);
            }
            spec.sheet = sheet;
            spec.scene = scene;
            config.themes = list.ToArray();
            EditorUtility.SetDirty(config);
        }
    }
}
