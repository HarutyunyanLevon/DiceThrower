using UnityEditor;
using UnityEngine;
using DiceThrower.Game;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Builds the Armenian Tavla theme: a walnut backgammon board (the painted board is the play surface) lying on a red kilim carpet, with
    /// brass studs on the corners, a pair of tea glasses and apricots on the rim, and two warm lamps. The play area is rectangular like the
    /// Dvarven Tavern's; the modelled walnut rim is static (it does not flex), dice bounce off the invisible walls on its inner face.
    /// Sizes and physics come from the TableSurfaceStyle asset. Run it again after changing the style or the art.
    /// </summary>
    public static class ArmenianTavlaBuilder
    {
        public const string ThemeName = "Armenian Tavla";
        private const string Dir = "Assets/Themes/ArmenianTavla";
        private const string MatDir = Dir + "/Materials";
        private const string StylePath = Dir + "/TableSurfaceStyle.asset";
        private const string PhysicsPath = Dir + "/Walnut_Physics.asset";
        private const string PrefabPath = Dir + "/Theme_ArmenianTavla.prefab";
        private const string TexDir = "Assets/Textures/ArmenianTavla";
        private const string UiTexDir = "Assets/UI/Tavla";
        private const string SheetPath = "Assets/UI/Tavla.uss";

        // the look of the props (visual only)
        private const float StudSize = 0.34f, StudHeight = 0.05f;
        private const float GlassRadius = 0.27f, GlassHeight = 0.5f, GlassFoot = 0.05f;
        private const int ApricotCount = 3;
        private const float ApricotRadius = 0.2f;
        private const float LampOrbSize = 0.4f;

        private static readonly Color WalnutTint = new Color(1f, 0.95f, 0.9f);
        private static readonly Color BrassColor = new Color(0.8f, 0.62f, 0.25f);
        private static readonly Color TeaColor = new Color(0.62f, 0.25f, 0.08f, 1f);
        private static readonly Color ApricotColor = new Color(0.93f, 0.53f, 0.17f);
        private static readonly Color CarpetTint = new Color(0.9f, 0.85f, 0.85f);

        [MenuItem("DiceThrower/Themes/Build Armenian Tavla")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            ThemeKit.EnsureFolders(MatDir);

            foreach (string name in new[] { "Ui_TavlaPanel", "Ui_TavlaButton", "Ui_TavlaButtonApricot", "Ui_TavlaRollCoin" })
                ThemeKit.SetupUiTexture(UiTexDir + "/" + name + ".png");
            ThemeKit.SetupTexture(TexDir + "/Tex_TavlaBoard.png", true);
            ThemeKit.SetupTexture(TexDir + "/Tex_TavlaCarpet.png", false);
            ThemeKit.SetupTexture(TexDir + "/Tex_TavlaWalnut.png", false);

            var style = BuilderAssets.GetOrCreate<TableSurfaceStyle>(StylePath, s =>
            {
                s.bounciness = 0.25f;
                s.dynamicFriction = 0.45f;
                s.staticFriction = 0.55f;
                s.floorDepth = 1.2f;
                s.floorTiling = 10f;
                s.lanternColor = new Color(1f, 0.74f, 0.4f);
                s.lanternIntensity = 12f;
                s.lightColor = new Color(1f, 0.88f, 0.68f);
                s.lightIntensity = 0.9f;
                s.cameraBackground = new Color(0.09f, 0.03f, 0.03f);
                s.ambientColor = new Color(0.24f, 0.15f, 0.12f);
            });
            PhysicsMaterial walnut = ThemeKit.Physics(PhysicsPath, style.bounciness, style.dynamicFriction, style.staticFriction);

            Texture2D boardTex = ThemeKit.Tex(TexDir + "/Tex_TavlaBoard.png");
            Texture2D carpetTex = ThemeKit.Tex(TexDir + "/Tex_TavlaCarpet.png");
            Texture2D walnutTex = ThemeKit.Tex(TexDir + "/Tex_TavlaWalnut.png");
            if (boardTex == null || carpetTex == null || walnutTex == null) return;

            Material boardMat = ThemeKit.Mat(MatDir, "Board", Color.white, boardTex, Vector2.one, 0.3f, 0f);
            Material walnutMat = ThemeKit.Mat(MatDir, "Walnut", WalnutTint, walnutTex, new Vector2(2f, 2f), 0.3f, 0f);
            Material carpetMat = ThemeKit.Mat(MatDir, "Carpet", CarpetTint, carpetTex, Vector2.one * style.floorTiling, 0.05f, 0f);
            Material brassMat = ThemeKit.Mat(MatDir, "Brass", BrassColor, null, Vector2.one, 0.65f, 0.8f);
            Material teaMat = ThemeKit.Mat(MatDir, "Tea", TeaColor, null, Vector2.one, 0.8f, 0f);
            Material apricotMat = ThemeKit.Mat(MatDir, "Apricot", ApricotColor, null, Vector2.one, 0.35f, 0f);
            Material lampMat = ThemeKit.Mat(MatDir, "LampGlow", style.lanternColor, null, Vector2.one, 0.2f, 0f);
            ThemeKit.Emit(lampMat, style.lanternColor, 3f);

            GameObject root = BuildScene(style, walnut, boardMat, walnutMat, carpetMat, brassMat, teaMat, apricotMat, lampMat);
            ThemeScene prefab = ThemeKit.SavePrefab(root, PrefabPath);
            ThemeKit.RegisterTheme(ThemeName, SheetPath, prefab);
            ThemeKit.Finish(ThemeName, PrefabPath);
        }

        // ------------------------------------------------------------------ scene

        private static GameObject BuildScene(TableSurfaceStyle st, PhysicsMaterial walnut, Material boardMat, Material walnutMat, Material carpetMat,
                                              Material brassMat, Material teaMat, Material apricotMat, Material lampMat)
        {
            Vector2 size = st.outerSize;
            float rw = st.rimWidth, rh = st.rimHeight;
            float cx = size.x * 0.5f - rw * 0.5f, cz = size.y * 0.5f - rw * 0.5f; // the middle of the rim

            var root = new GameObject("Theme_ArmenianTavla");
            var scene = root.AddComponent<ThemeScene>();
            Transform table;
            Transform surface = ThemeKit.TableShell(root, st, walnut, walnutMat, boardMat, walnutMat, out table);

            // ---- brass studs on the four rim corners
            Transform props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            foreach (Vector2 c in new[] { new Vector2(-cx, -cz), new Vector2(cx, -cz), new Vector2(-cx, cz), new Vector2(cx, cz) })
                ThemeKit.Prim(PrimitiveType.Cylinder, props, "Stud", new Vector3(c.x, rh + StudHeight, c.y), new Vector3(StudSize, StudHeight, StudSize), brassMat, false);

            // ---- two tea glasses on opposite rim corners, and apricots beside them
            AddTeaGlass(props, teaMat, brassMat, new Vector3(-cx, rh, cz));
            AddTeaGlass(props, teaMat, brassMat, new Vector3(cx, rh, -cz));
            var rng = new System.Random(st.coinSeed);
            for (int i = 0; i < ApricotCount; i++)
            {
                float t = (float)(rng.NextDouble() * 2.0 - 1.0);
                float side = i % 2 == 0 ? 1f : -1f;
                ThemeKit.Prim(PrimitiveType.Sphere, props, "Apricot", new Vector3(side * cx, rh + ApricotRadius, t * (size.y * 0.5f - rw * 2f)), Vector3.one * (2f * ApricotRadius), apricotMat, false);
            }

            // ---- the carpet far below the table
            Transform backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(root.transform, false);
            ThemeKit.FlatQuad(backdrop, "Carpet", new Vector3(0f, -st.floorDepth, 0f), new Vector2(st.floorSize, st.floorSize), 0f, carpetMat);

            // ---- two warm lamps over opposite corners
            Transform lights = new GameObject("Lamps").transform;
            lights.SetParent(root.transform, false);
            AddLamp(lights, st, lampMat, new Vector3(-st.lanternOffset.x, st.lanternHeight, st.lanternOffset.y));
            AddLamp(lights, st, lampMat, new Vector3(st.lanternOffset.x, st.lanternHeight, -st.lanternOffset.y));

            ThemeKit.ConfigureScene(scene, surface, rh, false, st.lightColor, st.lightIntensity, st.lightEuler, st.cameraBackground, st.ambientColor);
            return root;
        }

        private static void AddTeaGlass(Transform parent, Material tea, Material brass, Vector3 baseAt)
        {
            var glass = new GameObject("TeaGlass").transform;
            glass.SetParent(parent, false);
            glass.localPosition = baseAt;
            ThemeKit.Prim(PrimitiveType.Cylinder, glass, "Foot", new Vector3(0f, GlassFoot, 0f), new Vector3(1.4f * GlassRadius, GlassFoot, 1.4f * GlassRadius), brass, false);
            ThemeKit.Prim(PrimitiveType.Cylinder, glass, "Glass", new Vector3(0f, 2f * GlassFoot + GlassHeight * 0.5f, 0f), new Vector3(2f * GlassRadius, GlassHeight * 0.5f, 2f * GlassRadius), tea, false);
        }

        private static void AddLamp(Transform parent, TableSurfaceStyle st, Material glow, Vector3 pos)
        {
            var lamp = new GameObject("Lamp").transform;
            lamp.SetParent(parent, false);
            lamp.localPosition = pos;
            ThemeKit.Prim(PrimitiveType.Sphere, lamp, "Orb", Vector3.zero, Vector3.one * LampOrbSize, glow, false);
            ThemeKit.PointLight(lamp, "Light", Vector3.zero, st.lanternColor, st.lanternIntensity, st.lanternRange, true);
        }
    }
}
