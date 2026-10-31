using UnityEditor;
using UnityEngine;
using DiceThrower.Game;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Builds the Druid Cove theme: a woven straw nest with a bark rim on a mossy forest floor, ferns in the four corners of the camera frame,
    /// a few speckled eggs, and slow shafts of green-gold light. The play area is round and held in by an invisible ring of colliders; the
    /// modelled bark rim is static (it does not flex). Numbers of the surface come from the RoundSurfaceStyle asset.
    /// Run it again after changing the style or the art: assets are updated in place, so references stay valid.
    /// </summary>
    public static class DruidCoveBuilder
    {
        public const string ThemeName = "Druid Cove";
        private const string Dir = "Assets/Themes/DruidCove";
        private const string MatDir = Dir + "/Materials";
        private const string MeshDir = Dir + "/Meshes";
        private const string StylePath = Dir + "/SurfaceStyle.asset";
        private const string PhysicsPath = Dir + "/Straw_Physics.asset";
        private const string PrefabPath = Dir + "/Theme_DruidCove.prefab";
        private const string TexDir = "Assets/Textures/DruidCove";
        private const string UiTexDir = "Assets/UI/Druid";
        private const string SheetPath = "Assets/UI/Druid.uss";

        // the look of the props (visual only)
        private const int DiscSegments = 64, RimSegments = 64;
        private const float RimWidth = 0.45f, RimRepeat = 6f, RimOverhang = 0.25f;
        private const float BowlThickness = 0.5f;
        private const float FernSize = 3.4f, FernLift = 0.05f;
        private const int EggCount = 3;
        private static readonly Vector3 EggSize = new Vector3(0.34f, 0.46f, 0.34f);
        private const float EggSpread = 0.7f;               // share of the corner anchor where the eggs lie
        private const int ShaftCount = 2;
        private static readonly Vector2 ShaftSize = new Vector2(1.6f, 9f);
        private static readonly Vector3 ShaftDrift = new Vector3(1.2f, 0f, 0f);
        private const float ShaftDriftSpeed = 0.05f, ShaftYaw = 22f, ShaftOffset = 2.2f;
        private const float WarmLightHeight = 3f, WarmLightRange = 12f, WarmLightIntensity = 5f;

        private static readonly Color StrawTint = new Color(1f, 0.97f, 0.9f);
        private static readonly Color BarkTint = new Color(0.9f, 0.82f, 0.75f);
        private static readonly Color MossTint = new Color(0.9f, 1f, 0.9f);
        private static readonly Color FernTint = new Color(0.8f, 0.95f, 0.8f, 0.95f);
        private static readonly Color ShaftDim = new Color(0.9f, 1f, 0.7f, 0.15f), ShaftBright = new Color(0.9f, 1f, 0.7f, 0.5f);
        private static readonly Color WarmLightColor = new Color(1f, 0.9f, 0.6f);

        [MenuItem("DiceThrower/Themes/Build Druid Cove")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            ThemeKit.EnsureFolders(MatDir, MeshDir);

            foreach (string name in new[] { "Ui_DruidPanel", "Ui_DruidButton", "Ui_DruidButtonAmber", "Ui_DruidRollCoin" })
                ThemeKit.SetupUiTexture(UiTexDir + "/" + name + ".png");
            ThemeKit.SetupTexture(TexDir + "/Tex_DruidNest.png", true);
            ThemeKit.SetupTexture(TexDir + "/Tex_DruidBark.png", false);
            ThemeKit.SetupTexture(TexDir + "/Tex_DruidMoss.png", false);
            ThemeKit.SetupTexture(TexDir + "/Tex_DruidShaft.png", true);
            ThemeKit.SetupTexture(TexDir + "/Tex_DruidFern.png", true);
            ThemeKit.SetupTexture(TexDir + "/Tex_DruidEgg.png", false);

            var style = BuilderAssets.GetOrCreate<RoundSurfaceStyle>(StylePath, s =>
            {
                s.bounciness = 0.2f;
                s.dynamicFriction = 0.7f;
                s.staticFriction = 0.8f;
                s.rimHeight = 0.3f;
                s.floorDepth = 1.5f;
                s.floorTiling = 12f;
                s.lightColor = new Color(1f, 0.93f, 0.7f);
                s.lightIntensity = 0.8f;
                s.lightEuler = new Vector3(65f, 25f, 0f);
                s.cameraBackground = new Color(0.03f, 0.07f, 0.04f);
                s.ambientColor = new Color(0.17f, 0.24f, 0.17f);
            });
            PhysicsMaterial straw = ThemeKit.Physics(PhysicsPath, style.bounciness, style.dynamicFriction, style.staticFriction);

            Texture2D nestTex = ThemeKit.Tex(TexDir + "/Tex_DruidNest.png");
            Texture2D barkTex = ThemeKit.Tex(TexDir + "/Tex_DruidBark.png");
            Texture2D mossTex = ThemeKit.Tex(TexDir + "/Tex_DruidMoss.png");
            Texture2D shaftTex = ThemeKit.Tex(TexDir + "/Tex_DruidShaft.png");
            Texture2D fernTex = ThemeKit.Tex(TexDir + "/Tex_DruidFern.png");
            Texture2D eggTex = ThemeKit.Tex(TexDir + "/Tex_DruidEgg.png");
            if (nestTex == null || barkTex == null || mossTex == null || shaftTex == null || fernTex == null || eggTex == null) return;

            Material nestMat = ThemeKit.Mat(MatDir, "Nest", StrawTint, nestTex, Vector2.one, 0.1f, 0f);
            Material bowlMat = ThemeKit.Mat(MatDir, "Bowl", BarkTint, barkTex, new Vector2(4f, 1f), 0.15f, 0f);
            Material rimMat = ThemeKit.DoubleSided(ThemeKit.Mat(MatDir, "BarkRim", BarkTint, barkTex, Vector2.one, 0.2f, 0f));
            Material mossMat = ThemeKit.Mat(MatDir, "MossFloor", MossTint, mossTex, Vector2.one * style.floorTiling, 0.1f, 0f);
            Material fernMat = ThemeKit.Unlit(MatDir, "Fern", FernTint, fernTex, SurfaceBlend.Alpha, true);
            Material eggMat = ThemeKit.Mat(MatDir, "Egg", Color.white, eggTex, Vector2.one, 0.45f, 0f);
            Material shaftMat = ThemeKit.Unlit(MatDir, "Shaft", ShaftBright, shaftTex, SurfaceBlend.Additive, true);

            Mesh disc = ThemeKit.SaveMesh(ThemeKit.DiscMesh(style.DiscRadius, DiscSegments), MeshDir + "/NestDisc.asset");
            Mesh rim = ThemeKit.SaveMesh(ThemeKit.RingMesh(style.PlayRadius, style.DiscRadius + RimOverhang, style.rimHeight, RimSegments, RimRepeat), MeshDir + "/Rim.asset");

            GameObject root = BuildScene(style, straw, disc, rim, nestMat, bowlMat, rimMat, mossMat, fernMat, eggMat, shaftMat);
            ThemeScene prefab = ThemeKit.SavePrefab(root, PrefabPath);
            ThemeKit.RegisterTheme(ThemeName, SheetPath, prefab);
            ThemeKit.Finish(ThemeName, PrefabPath);
        }

        // ------------------------------------------------------------------ scene

        private static GameObject BuildScene(RoundSurfaceStyle st, PhysicsMaterial straw, Mesh disc, Mesh rim, Material nestMat, Material bowlMat, Material rimMat,
                                              Material mossMat, Material fernMat, Material eggMat, Material shaftMat)
        {
            var root = new GameObject("Theme_DruidCove");
            var scene = root.AddComponent<ThemeScene>();
            Transform surface = ThemeKit.RoundSurface(root, st, straw);
            float floorY = -st.floorDepth;

            // ---- the nest: the woven top (dice land on y = 0), the bowl under it down to the moss, and the bark rim
            Transform nest = new GameObject("Nest").transform;
            nest.SetParent(root.transform, false);
            ThemeKit.MeshObject(nest, "Top", disc, nestMat, new Vector3(0f, 0.003f, 0f));
            float bowlRadius = st.DiscRadius + RimOverhang;
            ThemeKit.Prim(PrimitiveType.Cylinder, nest, "Bowl", new Vector3(0f, floorY * 0.5f - 0.01f, 0f), new Vector3(2f * bowlRadius, st.floorDepth * 0.5f, 2f * bowlRadius), bowlMat, false);
            ThemeKit.MeshObject(nest, "BarkRim", rim, rimMat, Vector3.zero);

            // ---- the forest floor, with a fern in each corner of the camera frame
            Transform backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(root.transform, false);
            ThemeKit.FlatQuad(backdrop, "Moss", new Vector3(0f, floorY - 0.01f, 0f), new Vector2(st.floorSize, st.floorSize), 0f, mossMat);
            for (int i = 0; i < 4; i++)
            {
                float x = i == 0 || i == 3 ? -st.cornerAnchor.x : st.cornerAnchor.x;
                float z = i < 2 ? st.cornerAnchor.y : -st.cornerAnchor.y;
                ThemeKit.FlatQuad(backdrop, "Fern", new Vector3(x, floorY + FernLift, z), Vector2.one * FernSize, Mathf.Atan2(-x, -z) * Mathf.Rad2Deg, fernMat);
            }

            // ---- speckled eggs on the moss between the ferns
            var rng = new System.Random(st.sceneSeed);
            Transform props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            for (int i = 0; i < EggCount; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float r = st.cornerAnchor.magnitude * EggSpread;
                GameObject egg = ThemeKit.Prim(PrimitiveType.Sphere, props, "Egg", new Vector3(Mathf.Sin(a) * r, floorY + EggSize.y * 0.5f, Mathf.Cos(a) * r), EggSize, eggMat, false);
                egg.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            }

            // ---- slow shafts of light lying across the moss
            Transform shafts = new GameObject("Shafts").transform;
            shafts.SetParent(root.transform, false);
            var shaftRenderers = new Renderer[ShaftCount];
            for (int i = 0; i < ShaftCount; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                GameObject shaft = ThemeKit.FlatQuad(shafts, "Shaft", new Vector3(side * ShaftOffset, floorY + 0.03f * (i + 1), 0f), ShaftSize, side * ShaftYaw, shaftMat);
                ThemeKit.AddDrift(shaft, ShaftDrift, ShaftDriftSpeed * side);
                shaftRenderers[i] = shaft.GetComponent<Renderer>();
            }
            ThemeKit.AddPulse(shafts.gameObject, shaftRenderers, ShaftDim, ShaftBright, 0.15f);

            Transform lights = new GameObject("Lights").transform;
            lights.SetParent(root.transform, false);
            ThemeKit.PointLight(lights, "Warmth", new Vector3(0f, WarmLightHeight, 0f), WarmLightColor, WarmLightIntensity, WarmLightRange, false);

            ThemeKit.ConfigureScene(scene, surface, st.rimHeight, true, st.lightColor, st.lightIntensity, st.lightEuler, st.cameraBackground, st.ambientColor);
            return root;
        }
    }
}
