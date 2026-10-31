using UnityEditor;
using UnityEngine;
using DiceThrower.Game;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Builds the Dvarven Tavern theme: the oak table surface, the stone-floor backdrop, the lanterns and the props as one ThemeScene prefab,
    /// and registers the theme (style sheet + scene) in the DataUiConfig. Sizes and colours come from the TableSurfaceStyle asset.
    /// Run it again after changing the style: the prefab, materials and the theme entry are updated in place, so references stay valid.
    /// </summary>
    public static class DvarvenTavernBuilder
    {
        public const string ThemeName = "Dvarven Tavern";
        private const string Dir = "Assets/Themes/DvarvenTavern";
        private const string MatDir = Dir + "/Materials";
        private const string StylePath = Dir + "/TableSurfaceStyle.asset";
        private const string PhysicsPath = Dir + "/Wood_Physics.asset";
        private const string PrefabPath = Dir + "/Theme_DvarvenTavern.prefab";
        private const string TexDir = "Assets/Textures/Dvarven";
        private const string UiTexDir = "Assets/UI/Dvarven";
        private const string SheetPath = "Assets/UI/Dvarven.uss";

        [MenuItem("DiceThrower/Themes/Build Dvarven Tavern")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            ThemeKit.EnsureFolders(MatDir);

            foreach (string name in new[] { "Ui_DvarvenPanel", "Ui_DvarvenButton", "Ui_DvarvenButtonGold", "Ui_DvarvenRollCoin" })
                ThemeKit.SetupUiTexture(UiTexDir + "/" + name + ".png");

            var style = BuilderAssets.GetOrCreate<TableSurfaceStyle>(StylePath);
            PhysicsMaterial wood = ThemeKit.Physics(PhysicsPath, style.bounciness, style.dynamicFriction, style.staticFriction);

            Texture2D topTex = ThemeKit.Tex(TexDir + "/Tex_DvarvenTableTop.png");
            Texture2D oakTex = ThemeKit.Tex(TexDir + "/Tex_DvarvenOak.png");
            Texture2D stoneTex = ThemeKit.Tex(TexDir + "/Tex_DvarvenStone.png");
            if (topTex == null || oakTex == null || stoneTex == null) return;

            Material topMat = ThemeKit.Mat(MatDir, "TableTop", Color.white, topTex, Vector2.one, 0.25f, 0f);
            Material oakMat = ThemeKit.Mat(MatDir, "Oak", style.oakTint, oakTex, new Vector2(2f, 2f), 0.2f, 0f);
            Material stoneMat = ThemeKit.Mat(MatDir, "StoneFloor", new Color(0.8f, 0.78f, 0.74f), stoneTex, Vector2.one * style.floorTiling, 0.08f, 0f);
            Material goldMat = ThemeKit.Mat(MatDir, "Gold", style.goldColor, null, Vector2.one, 0.6f, 0.7f);
            Material mugMat = ThemeKit.Mat(MatDir, "MugWood", style.mugColor, oakTex, Vector2.one, 0.2f, 0f);
            Material foamMat = ThemeKit.Mat(MatDir, "Foam", style.foamColor, null, Vector2.one, 0.3f, 0f);
            Material lanternMat = ThemeKit.Mat(MatDir, "LanternGlow", style.lanternColor, null, Vector2.one, 0.2f, 0f);
            ThemeKit.Emit(lanternMat, style.lanternColor, 3f);

            GameObject root = BuildScene(style, wood, topMat, oakMat, stoneMat, goldMat, mugMat, foamMat, lanternMat);
            ThemeScene prefab = ThemeKit.SavePrefab(root, PrefabPath);
            ThemeKit.RegisterTheme(ThemeName, SheetPath, prefab);
            ThemeKit.Finish(ThemeName, PrefabPath);
        }

        // ------------------------------------------------------------------ scene

        private static GameObject BuildScene(TableSurfaceStyle st, PhysicsMaterial wood, Material topMat, Material oakMat, Material stoneMat,
                                              Material goldMat, Material mugMat, Material foamMat, Material lanternMat)
        {
            Vector2 size = st.outerSize;
            float rw = st.rimWidth, rh = st.rimHeight;

            var root = new GameObject("Theme_DvarvenTavern");
            var scene = root.AddComponent<ThemeScene>();
            Transform table;
            Transform surface = ThemeKit.TableShell(root, st, wood, oakMat, topMat, oakMat, out table);

            // gold corner brackets on the rim
            foreach (Vector2 c in Corners(size.x * 0.5f - rw * 0.5f, size.y * 0.5f - rw * 0.5f))
            {
                GameObject b = ThemeKit.Prim(PrimitiveType.Cube, table, "Bracket", new Vector3(c.x, rh + 0.03f, c.y), new Vector3(rw * 0.9f, 0.06f, rw * 0.9f), goldMat, false);
                b.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            }

            // ---- props (visual only)
            Transform props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            AddMug(props, st, mugMat, foamMat, new Vector2(-1f, 1f));
            AddMug(props, st, mugMat, foamMat, new Vector2(1f, -1f));
            AddCoins(props, st, goldMat);

            // ---- backdrop: a stone floor far below the table; the table's shadow falls on it
            Transform backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(root.transform, false);
            ThemeKit.FlatQuad(backdrop, "StoneFloor", new Vector3(0f, -st.floorDepth, 0f), new Vector2(st.floorSize, st.floorSize), 0f, stoneMat);

            // ---- lanterns: a glowing orb and a flickering warm light, two opposite corners
            Transform lights = new GameObject("Lanterns").transform;
            lights.SetParent(root.transform, false);
            AddLantern(lights, st, lanternMat, new Vector3(-st.lanternOffset.x, st.lanternHeight, st.lanternOffset.y));
            AddLantern(lights, st, lanternMat, new Vector3(st.lanternOffset.x, st.lanternHeight, -st.lanternOffset.y));

            ThemeKit.ConfigureScene(scene, surface, rh, false, st.lightColor, st.lightIntensity, st.lightEuler, st.cameraBackground, st.ambientColor);
            return root;
        }

        private static void AddMug(Transform parent, TableSurfaceStyle st, Material mugMat, Material foamMat, Vector2 corner)
        {
            float r = st.mugRadius, h = st.mugHeight, baseY = st.rimHeight;
            float x = corner.x * (st.outerSize.x * 0.5f - st.mugInset);
            float z = corner.y * (st.outerSize.y * 0.5f - st.mugInset);
            var mug = new GameObject("Mug").transform;
            mug.SetParent(parent, false);
            mug.localPosition = new Vector3(x, 0f, z);

            ThemeKit.Prim(PrimitiveType.Cylinder, mug, "Body", new Vector3(0f, baseY + h * 0.5f, 0f), new Vector3(2f * r, h * 0.5f, 2f * r), mugMat, false);
            ThemeKit.Prim(PrimitiveType.Cylinder, mug, "Foam", new Vector3(0f, baseY + h + 0.02f, 0f), new Vector3(2f * (r - 0.07f), 0.03f, 2f * (r - 0.07f)), foamMat, false);
            // handle: a small block on the outer side
            ThemeKit.Prim(PrimitiveType.Cube, mug, "Handle", new Vector3(corner.x * (r + 0.08f), baseY + h * 0.5f, 0f), new Vector3(0.16f, h * 0.5f, 0.2f), mugMat, false);
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
                GameObject coin = ThemeKit.Prim(PrimitiveType.Cylinder, parent, "Coin", new Vector3(x, st.rimHeight + st.coinThickness * 0.5f, z),
                                                new Vector3(2f * st.coinRadius, st.coinThickness * 0.5f, 2f * st.coinRadius), goldMat, false);
                coin.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            }
        }

        private static void AddLantern(Transform parent, TableSurfaceStyle st, Material glow, Vector3 pos)
        {
            var lantern = new GameObject("Lantern").transform;
            lantern.SetParent(parent, false);
            lantern.localPosition = pos;
            ThemeKit.Prim(PrimitiveType.Sphere, lantern, "Orb", Vector3.zero, Vector3.one * 0.45f, glow, false);
            ThemeKit.PointLight(lantern, "Light", Vector3.zero, st.lanternColor, st.lanternIntensity, st.lanternRange, true);
        }

        private static Vector2[] Corners(float x, float z)
        {
            return new[] { new Vector2(-x, -z), new Vector2(x, -z), new Vector2(-x, z), new Vector2(x, z) };
        }
    }
}
