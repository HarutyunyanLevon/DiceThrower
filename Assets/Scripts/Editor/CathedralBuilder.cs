using UnityEditor;
using UnityEngine;
using DiceThrower.Game;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Builds the Cathedral theme: a round marble postament (gold ring, violet star inlay, a glowing cross) standing in a violet nave, a halo
    /// of light dots floating just outside the stone, rays of white-gold light fanning over the floor and two stained-glass light panels.
    /// The play area is round and held in by an invisible ring of colliders sized to the halo; the halo, rays and panels are visual only.
    /// Numbers of the surface come from the RoundSurfaceStyle asset. Run it again after changing the style or the art.
    /// </summary>
    public static class CathedralBuilder
    {
        public const string ThemeName = "Cathedral";
        private const string Dir = "Assets/Themes/Cathedral";
        private const string MatDir = Dir + "/Materials";
        private const string MeshDir = Dir + "/Meshes";
        private const string StylePath = Dir + "/SurfaceStyle.asset";
        private const string PhysicsPath = Dir + "/Marble_Physics.asset";
        private const string PrefabPath = Dir + "/Theme_Cathedral.prefab";
        private const string TexDir = "Assets/Textures/Cathedral";
        private const string UiTexDir = "Assets/UI/Cathedral";
        private const string SheetPath = "Assets/UI/Cathedral.uss";

        // the look of the props (visual only)
        private const int DiscSegments = 64;
        private const float PedestalHeight = 3f;     // the postament stands this high above the nave floor
        private const float PedestalFlare = 0.15f;   // the stone body is this much wider than the drawn disc
        private const float GoldRingFlare = 0.18f, GoldRingHeight = 0.1f;
        private const int HaloDots = 24;
        private const float HaloDotSize = 0.13f, HaloOffset = 0.55f, HaloHeight = 0.12f;
        private const int RayCount = 12;
        private const float RayLength = 7f, RayWidth = 1.1f, RayHidden = 0.5f;
        private const float WindowOffsetZ = 6.6f;
        private static readonly Vector2 WindowSize = new Vector2(3.2f, 5.6f);

        private static readonly Color FloorTint = new Color(0.85f, 0.8f, 0.95f);
        private static readonly Color TopTint = new Color(0.62f, 0.58f, 0.72f); // the marble top is pale: keep it from burning out under the light
        private static readonly Color GoldColor = new Color(0.89f, 0.75f, 0.42f);
        private static readonly Color HaloDim = new Color(0.55f, 0.5f, 0.35f), HaloBright = new Color(1f, 0.98f, 0.85f);
        private static readonly Color RayDim = new Color(1f, 0.97f, 0.8f, 0.3f), RayBright = new Color(1f, 0.97f, 0.8f, 0.85f);
        private static readonly Color WindowTint = new Color(1f, 1f, 1f, 0.7f);

        [MenuItem("DiceThrower/Themes/Build Cathedral")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            ThemeKit.EnsureFolders(MatDir, MeshDir);

            foreach (string name in new[] { "Ui_CathPanel", "Ui_CathButton", "Ui_CathButtonGold", "Ui_CathRollCoin" })
                ThemeKit.SetupUiTexture(UiTexDir + "/" + name + ".png");
            ThemeKit.SetupTexture(TexDir + "/Tex_CathFloor.png", false);
            ThemeKit.SetupTexture(TexDir + "/Tex_CathMarble.png", false);
            ThemeKit.SetupTexture(TexDir + "/Tex_CathPedestal.png", true);
            ThemeKit.SetupTexture(TexDir + "/Tex_CathRay.png", true);
            ThemeKit.SetupTexture(TexDir + "/Tex_CathWindow.png", true);

            var style = BuilderAssets.GetOrCreate<RoundSurfaceStyle>(StylePath, s =>
            {
                s.bounciness = 0.25f;
                s.dynamicFriction = 0.35f;
                s.staticFriction = 0.45f;
                s.floorDepth = PedestalHeight;
                s.floorTiling = 10f;
                s.lightColor = new Color(1f, 0.95f, 0.82f);
                s.lightIntensity = 0.7f;
                s.lightEuler = new Vector3(80f, 15f, 0f);
                s.cameraBackground = new Color(0.04f, 0.02f, 0.09f);
                s.ambientColor = new Color(0.13f, 0.09f, 0.22f);
            });
            PhysicsMaterial marble = ThemeKit.Physics(PhysicsPath, style.bounciness, style.dynamicFriction, style.staticFriction);

            Texture2D floorTex = ThemeKit.Tex(TexDir + "/Tex_CathFloor.png");
            Texture2D marbleTex = ThemeKit.Tex(TexDir + "/Tex_CathMarble.png");
            Texture2D pedestalTex = ThemeKit.Tex(TexDir + "/Tex_CathPedestal.png");
            Texture2D rayTex = ThemeKit.Tex(TexDir + "/Tex_CathRay.png");
            Texture2D windowTex = ThemeKit.Tex(TexDir + "/Tex_CathWindow.png");
            if (floorTex == null || marbleTex == null || pedestalTex == null || rayTex == null || windowTex == null) return;

            Material floorMat = ThemeKit.Mat(MatDir, "NaveFloor", FloorTint, floorTex, Vector2.one * style.floorTiling, 0.45f, 0f);
            Material bodyMat = ThemeKit.Mat(MatDir, "PedestalStone", Color.white, marbleTex, new Vector2(2f, 1f), 0.5f, 0f);
            Material topMat = ThemeKit.Mat(MatDir, "PedestalTop", TopTint, pedestalTex, Vector2.one, 0.2f, 0f);
            Material goldMat = ThemeKit.Mat(MatDir, "Gold", GoldColor, null, Vector2.one, 0.6f, 0.8f);
            Material haloMat = ThemeKit.Unlit(MatDir, "HaloDot", HaloBright, null, SurfaceBlend.Opaque, false);
            Material rayMat = ThemeKit.Unlit(MatDir, "Ray", RayBright, rayTex, SurfaceBlend.Additive, true);
            Material windowMat = ThemeKit.Unlit(MatDir, "GlassPanel", WindowTint, windowTex, SurfaceBlend.Alpha, true);

            Mesh disc = ThemeKit.SaveMesh(ThemeKit.DiscMesh(style.DiscRadius, DiscSegments), MeshDir + "/PedestalDisc.asset");
            Mesh halo = ThemeKit.SaveMesh(ThemeKit.RingOfSpheres(style.DiscRadius + HaloOffset, HaloDots, HaloDotSize, HaloHeight), MeshDir + "/Halo.asset");

            GameObject root = BuildScene(style, marble, disc, halo, floorMat, bodyMat, topMat, goldMat, haloMat, rayMat, windowMat);
            ThemeScene prefab = ThemeKit.SavePrefab(root, PrefabPath);
            ThemeKit.RegisterTheme(ThemeName, SheetPath, prefab);
            ThemeKit.Finish(ThemeName, PrefabPath);
        }

        // ------------------------------------------------------------------ scene

        private static GameObject BuildScene(RoundSurfaceStyle st, PhysicsMaterial marble, Mesh disc, Mesh halo, Material floorMat, Material bodyMat,
                                              Material topMat, Material goldMat, Material haloMat, Material rayMat, Material windowMat)
        {
            var root = new GameObject("Theme_Cathedral");
            var scene = root.AddComponent<ThemeScene>();
            Transform surface = ThemeKit.RoundSurface(root, st, marble);
            float r = st.DiscRadius;
            float floorY = -st.floorDepth;

            // ---- the postament: the drawn top (dice land on y = 0), its stone body down to the nave floor and a gold ring at the top edge
            Transform pedestal = new GameObject("Postament").transform;
            pedestal.SetParent(root.transform, false);
            ThemeKit.MeshObject(pedestal, "Top", disc, topMat, new Vector3(0f, 0.003f, 0f));
            float bodyRadius = r + PedestalFlare;
            ThemeKit.Prim(PrimitiveType.Cylinder, pedestal, "Body", new Vector3(0f, floorY * 0.5f - 0.01f, 0f), new Vector3(2f * bodyRadius, st.floorDepth * 0.5f, 2f * bodyRadius), bodyMat, false);
            float ringRadius = r + GoldRingFlare;
            ThemeKit.Prim(PrimitiveType.Cylinder, pedestal, "GoldRing", new Vector3(0f, -GoldRingHeight * 0.5f, 0f), new Vector3(2f * ringRadius, GoldRingHeight * 0.5f, 2f * ringRadius), goldMat, false);

            // ---- the halo of light dots just outside the stone, breathing
            Transform props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            GameObject haloObj = ThemeKit.MeshObject(props, "Halo", halo, haloMat, Vector3.zero);
            ThemeKit.AddPulse(haloObj, new[] { haloObj.GetComponent<Renderer>() }, HaloDim, HaloBright, 0.35f);

            // ---- backdrop: the nave floor, rays of light fanning across it and two stained-glass light panels
            Transform backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(root.transform, false);
            ThemeKit.FlatQuad(backdrop, "NaveFloor", new Vector3(0f, floorY, 0f), new Vector2(st.floorSize, st.floorSize), 0f, floorMat);

            var rays = new Renderer[RayCount];
            for (int i = 0; i < RayCount; i++)
            {
                float a = i * Mathf.PI * 2f / RayCount;
                float mid = bodyRadius + RayLength * 0.5f - RayHidden; // the near end tucks under the postament
                Vector3 pos = new Vector3(Mathf.Sin(a) * mid, floorY + 0.02f, Mathf.Cos(a) * mid);
                rays[i] = ThemeKit.FlatQuad(backdrop, "Ray", pos, new Vector2(RayWidth, RayLength), a * Mathf.Rad2Deg, rayMat).GetComponent<Renderer>();
            }
            ThemeKit.AddPulse(backdrop.gameObject, rays, RayDim, RayBright, 0.18f);

            ThemeKit.FlatQuad(backdrop, "GlassFar", new Vector3(0f, floorY + 0.03f, WindowOffsetZ), WindowSize, 0f, windowMat);
            ThemeKit.FlatQuad(backdrop, "GlassNear", new Vector3(0f, floorY + 0.03f, -WindowOffsetZ), WindowSize, 180f, windowMat);

            ThemeKit.ConfigureScene(scene, surface, 0f, true, st.lightColor, st.lightIntensity, st.lightEuler, st.cameraBackground, st.ambientColor);
            return root;
        }
    }
}
