using UnityEditor;
using UnityEngine;
using DiceThrower.Game;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Builds the Old One Cultist Hideout theme: a black cracked-stone floor with a pentagram circle drawn on it, five candles on the star points,
    /// tentacles with watching eyes creeping in from the corners, and a flickering red light. The play area is round and held in by an invisible
    /// ring of colliders sized to the outer circle; candles and tentacles are visual only. Numbers of the surface come from the RoundSurfaceStyle asset.
    /// Run it again after changing the style or the art: assets are updated in place, so references stay valid.
    /// </summary>
    public static class CultistHideoutBuilder
    {
        public const string ThemeName = "Old One Cultist Hideout";
        private const string Dir = "Assets/Themes/CultistHideout";
        private const string MatDir = Dir + "/Materials";
        private const string MeshDir = Dir + "/Meshes";
        private const string StylePath = Dir + "/SurfaceStyle.asset";
        private const string PhysicsPath = Dir + "/Stone_Physics.asset";
        private const string PrefabPath = Dir + "/Theme_CultistHideout.prefab";
        private const string TexDir = "Assets/Textures/CultistHideout";
        private const string UiTexDir = "Assets/UI/Cultist";
        private const string SheetPath = "Assets/UI/Cultist.uss";

        // the look of the props (visual only)
        private const int DiscSegments = 64;
        private const int StarPoints = 5;
        private const float StarRadiusShare = 0.88f; // share of the disc radius where the star points lie: the same as in Tex_CultSigil
        private const float CandleRadius = 0.2f, CandleHeight = 0.85f;
        private const float GlowLightHeight = 2.4f, GlowLightRange = 11f, GlowLightIntensity = 6f;
        private const int TentacleCount = 4, TentacleSteps = 18, TentacleSides = 8;
        private const float TentacleRadiusBase = 0.26f, TentacleRadiusTip = 0.03f;
        private const float TentacleReach = 0.7f; // how far a tentacle's tip creeps over the edge of the sigil disc
        private const float EyeAlong = 0.78f, EyeRadius = 0.14f;

        private static readonly Color FloorTint = new Color(0.9f, 0.85f, 0.85f);
        private static readonly Color WaxColor = new Color(0.91f, 0.87f, 0.77f);
        private static readonly Color FlameDim = new Color(1f, 0.5f, 0.18f), FlameBright = new Color(1f, 0.78f, 0.34f);
        private static readonly Color TentacleColor = new Color(0.11f, 0.04f, 0.13f);
        private static readonly Color EyeColor = new Color(0.91f, 0.85f, 0.54f);
        private static readonly Color GlowLightColor = new Color(1f, 0.32f, 0.2f);

        [MenuItem("DiceThrower/Themes/Build Old One Cultist Hideout")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            ThemeKit.EnsureFolders(MatDir, MeshDir);

            foreach (string name in new[] { "Ui_CultPanel", "Ui_CultButton", "Ui_CultButtonRed", "Ui_CultRollCoin" })
                ThemeKit.SetupUiTexture(UiTexDir + "/" + name + ".png");
            ThemeKit.SetupTexture(TexDir + "/Tex_CultFloor.png", false);
            ThemeKit.SetupTexture(TexDir + "/Tex_CultSigil.png", true);

            var style = BuilderAssets.GetOrCreate<RoundSurfaceStyle>(StylePath, s =>
            {
                s.bounciness = 0.35f;
                s.dynamicFriction = 0.5f;
                s.staticFriction = 0.6f;
                s.floorDepth = 0f;
                s.floorTiling = 14f;
                s.lightColor = new Color(0.85f, 0.3f, 0.25f);
                s.lightIntensity = 0.3f;
                s.lightEuler = new Vector3(65f, -25f, 0f);
                s.cameraBackground = new Color(0.03f, 0.01f, 0.02f);
                s.ambientColor = new Color(0.08f, 0.035f, 0.045f);
            });
            PhysicsMaterial stone = ThemeKit.Physics(PhysicsPath, style.bounciness, style.dynamicFriction, style.staticFriction);

            Texture2D floorTex = ThemeKit.Tex(TexDir + "/Tex_CultFloor.png");
            Texture2D sigilTex = ThemeKit.Tex(TexDir + "/Tex_CultSigil.png");
            if (floorTex == null || sigilTex == null) return;

            Material floorMat = ThemeKit.Mat(MatDir, "Floor", FloorTint, floorTex, Vector2.one * style.floorTiling, 0.15f, 0f);
            Material sigilMat = ThemeKit.Mat(MatDir, "Sigil", Color.white, sigilTex, Vector2.one, 0.2f, 0f);
            Material waxMat = ThemeKit.Mat(MatDir, "Wax", WaxColor, null, Vector2.one, 0.35f, 0f);
            Material flameMat = ThemeKit.Unlit(MatDir, "Flame", FlameBright, null, SurfaceBlend.Opaque, false);
            Material tentacleMat = ThemeKit.DoubleSided(ThemeKit.Mat(MatDir, "Tentacle", TentacleColor, null, Vector2.one, 0.55f, 0f));
            Material eyeMat = ThemeKit.Unlit(MatDir, "Eye", EyeColor, null, SurfaceBlend.Opaque, false);
            Material pupilMat = ThemeKit.Unlit(MatDir, "Pupil", new Color(0.02f, 0f, 0.02f), null, SurfaceBlend.Opaque, false);

            Mesh disc = ThemeKit.SaveMesh(ThemeKit.DiscMesh(style.DiscRadius, DiscSegments), MeshDir + "/SigilDisc.asset");
            Mesh tentacle = ThemeKit.SaveMesh(ThemeKit.TubeMesh(TentaclePath(TentacleLength(style)), TentacleRadiusBase, TentacleRadiusTip, TentacleSides), MeshDir + "/Tentacle.asset");

            GameObject root = BuildScene(style, stone, disc, tentacle, floorMat, sigilMat, waxMat, flameMat, tentacleMat, eyeMat, pupilMat);
            ThemeScene prefab = ThemeKit.SavePrefab(root, PrefabPath);
            ThemeKit.RegisterTheme(ThemeName, SheetPath, prefab);
            ThemeKit.Finish(ThemeName, PrefabPath);
        }

        // ------------------------------------------------------------------ scene

        private static GameObject BuildScene(RoundSurfaceStyle st, PhysicsMaterial stone, Mesh disc, Mesh tentacle, Material floorMat, Material sigilMat,
                                              Material waxMat, Material flameMat, Material tentacleMat, Material eyeMat, Material pupilMat)
        {
            var root = new GameObject("Theme_CultistHideout");
            var scene = root.AddComponent<ThemeScene>();
            Transform surface = ThemeKit.RoundSurface(root, st, stone);

            // ---- floor: the stone everywhere, and the sigil disc on it (the dice land on y = 0)
            Transform backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(root.transform, false);
            ThemeKit.FlatQuad(backdrop, "StoneFloor", new Vector3(0f, -st.floorDepth - 0.01f, 0f), new Vector2(st.floorSize, st.floorSize), 0f, floorMat);
            ThemeKit.MeshObject(backdrop, "Sigil", disc, sigilMat, new Vector3(0f, 0.003f, 0f));

            // ---- candles on the star points (visual only: dice that reach the rim pass through them)
            Transform props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            var flames = new Renderer[StarPoints];
            float starRadius = st.DiscRadius * StarRadiusShare;
            for (int i = 0; i < StarPoints; i++)
            {
                float a = i * Mathf.PI * 2f / StarPoints; // the first point is "up" on screen (+z), like the texture
                var candle = new GameObject("Candle").transform;
                candle.SetParent(props, false);
                candle.localPosition = new Vector3(Mathf.Sin(a) * starRadius, 0f, Mathf.Cos(a) * starRadius);
                ThemeKit.Prim(PrimitiveType.Cylinder, candle, "Wax", new Vector3(0f, CandleHeight * 0.5f, 0f), new Vector3(2f * CandleRadius, CandleHeight * 0.5f, 2f * CandleRadius), waxMat, false);
                flames[i] = ThemeKit.Prim(PrimitiveType.Sphere, candle, "Flame", new Vector3(0f, CandleHeight + 0.14f, 0f), new Vector3(0.16f, 0.3f, 0.16f), flameMat, false).GetComponent<Renderer>();
            }
            ThemeKit.AddPulse(props.gameObject, flames, FlameDim, FlameBright, 0.7f);

            // ---- tentacles with a watching eye, one in each corner of the camera frame (the style's corner anchor), curling in towards the circle
            Transform creatures = new GameObject("Tentacles").transform;
            creatures.SetParent(root.transform, false);
            Vector3[] spine = TentaclePath(TentacleLength(st));
            for (int i = 0; i < TentacleCount; i++)
                AddTentacle(creatures, tentacle, spine, tentacleMat, eyeMat, pupilMat, new Vector2(i % 2 == 0 ? st.cornerAnchor.x : -st.cornerAnchor.x, i < 2 ? st.cornerAnchor.y : -st.cornerAnchor.y), i % 3 == 0);

            // ---- the one real light: a wavering red glow above the circle (the candles' light is painted on the stone)
            Transform lights = new GameObject("Lights").transform;
            lights.SetParent(root.transform, false);
            ThemeKit.PointLight(lights, "Glow", new Vector3(0f, GlowLightHeight, 0f), GlowLightColor, GlowLightIntensity, GlowLightRange, true);

            ThemeKit.ConfigureScene(scene, surface, 0f, true, st.lightColor, st.lightIntensity, st.lightEuler, st.cameraBackground, st.ambientColor);
            return root;
        }

        /// <summary>From the corner anchor to just over the edge of the sigil disc.</summary>
        private static float TentacleLength(RoundSurfaceStyle st)
        {
            return st.cornerAnchor.magnitude - st.DiscRadius + TentacleReach;
        }

        /// <summary>The tentacle's spine in its own frame: it runs along +z, wavers sideways and lifts its tip off the floor.</summary>
        private static Vector3[] TentaclePath(float length)
        {
            var path = new Vector3[TentacleSteps + 1];
            for (int i = 0; i <= TentacleSteps; i++)
            {
                float t = i / (float)TentacleSteps;
                path[i] = new Vector3(Mathf.Sin(t * 5f) * 0.55f * t, 0.12f + 0.55f * Mathf.Pow(t, 2.2f), t * length);
            }
            return path;
        }

        private static void AddTentacle(Transform parent, Mesh mesh, Vector3[] path, Material mat, Material eyeMat, Material pupilMat, Vector2 anchor, bool mirror)
        {
            var go = ThemeKit.MeshObject(parent, "Tentacle", mesh, mat, new Vector3(anchor.x, 0f, anchor.y));
            go.transform.rotation = Quaternion.LookRotation(new Vector3(-anchor.x, 0f, -anchor.y)); // +z points at the circle
            go.transform.localScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);

            Vector3 eyeAt = path[Mathf.RoundToInt(EyeAlong * TentacleSteps)] + Vector3.up * (EyeRadius * 0.9f);
            ThemeKit.Prim(PrimitiveType.Sphere, go.transform, "Eye", eyeAt, Vector3.one * (2f * EyeRadius), eyeMat, false);
            ThemeKit.Prim(PrimitiveType.Sphere, go.transform, "Pupil", eyeAt + new Vector3(0f, EyeRadius * 0.45f, EyeRadius * 0.55f), Vector3.one * EyeRadius, pupilMat, false);
        }
    }
}
