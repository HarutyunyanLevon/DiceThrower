using UnityEditor;
using UnityEngine;
using DiceThrower.Game;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Builds the Sky Elf Cafe theme: a round white-marble cafe table with a gold rim, standing on a stem in the middle of the sky, drifting clouds
    /// below it and gold scrolls in the four corners of the camera frame. The play area is round and held in by an invisible ring of colliders;
    /// the modelled gold rim is static (it does not flex). Numbers of the surface come from the RoundSurfaceStyle asset.
    /// Run it again after changing the style or the art: assets are updated in place, so references stay valid.
    /// </summary>
    public static class ElfCafeBuilder
    {
        public const string ThemeName = "Sky Elf Cafe";
        private const string Dir = "Assets/Themes/SkyElfCafe";
        private const string MatDir = Dir + "/Materials";
        private const string MeshDir = Dir + "/Meshes";
        private const string StylePath = Dir + "/SurfaceStyle.asset";
        private const string PhysicsPath = Dir + "/Marble_Physics.asset";
        private const string PrefabPath = Dir + "/Theme_SkyElfCafe.prefab";
        private const string TexDir = "Assets/Textures/SkyElfCafe";
        private const string UiTexDir = "Assets/UI/Elf";
        private const string SheetPath = "Assets/UI/Elf.uss";

        // the look of the props (visual only)
        private const int DiscSegments = 64, RimSegments = 64;
        private const float RimWidth = 0.35f, RimRepeat = 24f;
        private const float SlabThickness = 0.3f, SlabFlare = 0.12f, StemRadius = 0.55f;
        private const int CloudCount = 6;
        private const float CloudDepthStep = 0.25f;         // each cloud lies a little lower than the one before: no z-fighting
        private const float CloudRadiusMin = 5f, CloudRadiusMax = 9f;
        private static readonly Vector2 CloudSize = new Vector2(9f, 4.5f);
        private static readonly Vector3 CloudDrift = new Vector3(1.6f, 0f, 0f);
        private const float CloudDriftSpeed = 0.04f;
        private const float ScrollSize = 3.2f;

        private static readonly Color MarbleTint = new Color(1f, 0.97f, 0.9f);
        private static readonly Color GoldColor = new Color(0.79f, 0.64f, 0.29f);
        private static readonly Color CloudTint = new Color(1f, 1f, 1f, 0.8f);
        private static readonly Color ScrollTint = new Color(1f, 1f, 1f, 0.95f);

        [MenuItem("DiceThrower/Themes/Build Sky Elf Cafe")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            ThemeKit.EnsureFolders(MatDir, MeshDir);

            foreach (string name in new[] { "Ui_ElfPanel", "Ui_ElfButton", "Ui_ElfButtonGold", "Ui_ElfRollCoin" })
                ThemeKit.SetupUiTexture(UiTexDir + "/" + name + ".png");
            ThemeKit.SetupTexture(TexDir + "/Tex_ElfTable.png", true);
            ThemeKit.SetupTexture(TexDir + "/Tex_ElfCloud.png", true);
            ThemeKit.SetupTexture(TexDir + "/Tex_ElfCorner.png", true);

            var style = BuilderAssets.GetOrCreate<RoundSurfaceStyle>(StylePath, s =>
            {
                s.bounciness = 0.3f;
                s.dynamicFriction = 0.35f;
                s.staticFriction = 0.45f;
                s.rimHeight = 0.12f;
                s.floorDepth = 2.5f;
                s.lightColor = new Color(1f, 0.97f, 0.88f);
                s.lightIntensity = 1.1f;
                s.lightEuler = new Vector3(70f, -20f, 0f);
                s.cameraBackground = new Color(0.55f, 0.78f, 0.94f);
                s.ambientColor = new Color(0.55f, 0.58f, 0.66f);
            });
            PhysicsMaterial marble = ThemeKit.Physics(PhysicsPath, style.bounciness, style.dynamicFriction, style.staticFriction);

            Texture2D tableTex = ThemeKit.Tex(TexDir + "/Tex_ElfTable.png");
            Texture2D cloudTex = ThemeKit.Tex(TexDir + "/Tex_ElfCloud.png");
            Texture2D scrollTex = ThemeKit.Tex(TexDir + "/Tex_ElfCorner.png");
            if (tableTex == null || cloudTex == null || scrollTex == null) return;

            Material topMat = ThemeKit.Mat(MatDir, "TableTop", MarbleTint, tableTex, Vector2.one, 0.55f, 0f);
            Material slabMat = ThemeKit.Mat(MatDir, "TableSlab", MarbleTint, null, Vector2.one, 0.5f, 0f);
            Material goldMat = ThemeKit.DoubleSided(ThemeKit.Mat(MatDir, "Gold", GoldColor, null, Vector2.one, 0.5f, 0.3f));
            Material cloudMat = ThemeKit.Unlit(MatDir, "Cloud", CloudTint, cloudTex, SurfaceBlend.Alpha, true);
            Material scrollMat = ThemeKit.Unlit(MatDir, "GoldScroll", ScrollTint, scrollTex, SurfaceBlend.Alpha, true);

            Mesh disc = ThemeKit.SaveMesh(ThemeKit.DiscMesh(style.DiscRadius, DiscSegments), MeshDir + "/TableDisc.asset");
            Mesh rim = ThemeKit.SaveMesh(ThemeKit.RingMesh(style.PlayRadius, style.PlayRadius + RimWidth, style.rimHeight, RimSegments, RimRepeat), MeshDir + "/Rim.asset");

            GameObject root = BuildScene(style, marble, disc, rim, topMat, slabMat, goldMat, cloudMat, scrollMat);
            ThemeScene prefab = ThemeKit.SavePrefab(root, PrefabPath);
            ThemeKit.RegisterTheme(ThemeName, SheetPath, prefab);
            ThemeKit.Finish(ThemeName, PrefabPath);
        }

        // ------------------------------------------------------------------ scene

        private static GameObject BuildScene(RoundSurfaceStyle st, PhysicsMaterial marble, Mesh disc, Mesh rim, Material topMat, Material slabMat,
                                              Material goldMat, Material cloudMat, Material scrollMat)
        {
            var root = new GameObject("Theme_SkyElfCafe");
            var scene = root.AddComponent<ThemeScene>();
            Transform surface = ThemeKit.RoundSurface(root, st, marble);
            float floorY = -st.floorDepth;

            // ---- the table: the drawn top (dice land on y = 0), a marble slab under it, a gold rim round the edge and a stem down into the clouds
            Transform table = new GameObject("Table").transform;
            table.SetParent(root.transform, false);
            ThemeKit.MeshObject(table, "Top", disc, topMat, new Vector3(0f, 0.003f, 0f));
            float slabRadius = st.DiscRadius + SlabFlare;
            ThemeKit.Prim(PrimitiveType.Cylinder, table, "Slab", new Vector3(0f, -SlabThickness * 0.5f - 0.01f, 0f), new Vector3(2f * slabRadius, SlabThickness * 0.5f, 2f * slabRadius), slabMat, false);
            ThemeKit.MeshObject(table, "GoldRim", rim, goldMat, Vector3.zero);
            float stemHeight = st.floorDepth - SlabThickness;
            ThemeKit.Prim(PrimitiveType.Cylinder, table, "Stem", new Vector3(0f, -SlabThickness - stemHeight * 0.5f, 0f), new Vector3(2f * StemRadius, stemHeight * 0.5f, 2f * StemRadius), goldMat, false);

            // ---- the sky: clouds drifting below the table, each at its own depth, speed and place
            Transform backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(root.transform, false);
            var rng = new System.Random(st.sceneSeed);
            for (int i = 0; i < CloudCount; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float r = Mathf.Lerp(CloudRadiusMin, CloudRadiusMax, (float)rng.NextDouble());
                Vector3 pos = new Vector3(Mathf.Sin(a) * r, floorY - i * CloudDepthStep, Mathf.Cos(a) * r);
                GameObject cloud = ThemeKit.FlatQuad(backdrop, "Cloud", pos, CloudSize * Mathf.Lerp(0.8f, 1.3f, (float)rng.NextDouble()), (float)(rng.NextDouble() * 360.0), cloudMat);
                ThemeKit.AddDrift(cloud, CloudDrift, CloudDriftSpeed * (i % 2 == 0 ? 1f : -1f));
            }

            // ---- gold scrolls in the four corners of the camera frame (each one turned to point at the table)
            Transform scrolls = new GameObject("Scrolls").transform;
            scrolls.SetParent(root.transform, false);
            for (int i = 0; i < 4; i++)
            {
                float x = i == 0 || i == 3 ? -st.cornerAnchor.x : st.cornerAnchor.x;
                float z = i < 2 ? st.cornerAnchor.y : -st.cornerAnchor.y;
                ThemeKit.FlatQuad(scrolls, "Scroll", new Vector3(x, 0.01f, z), Vector2.one * ScrollSize, 90f * i, scrollMat);
            }

            ThemeKit.ConfigureScene(scene, surface, st.rimHeight, true, st.lightColor, st.lightIntensity, st.lightEuler, st.cameraBackground, st.ambientColor);
            return root;
        }
    }
}
