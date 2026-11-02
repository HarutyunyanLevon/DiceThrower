using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DiceThrower.Game;
using DiceThrower.UI;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// Builds the DiceFX shader dice (Prismatic, Mercury, Lava, Cosmic, Frost, Void): texture import settings, one material per colour variant, a DiceModelSet per style (it reuses the
    /// glow model's baked geometry, whose atlas layout the numeral mask was made for), one DiceSkin per variant, the DiceFxConfig in
    /// Resources, and the entries the Dice tab lists. Safe to run again: assets keep their GUIDs, and the values written here
    /// (colours and numbers in Styles) are re-applied to existing materials.
    /// </summary>
    public static class DiceFxBuilder
    {
        private const string FxDir = "Assets/DiceModels/FX";
        private const string MaterialDir = FxDir + "/Materials";
        private const string ConfigDir = "Assets/Config";
        private const string ResourcesDir = "Assets/Resources";
        private const string DataUiConfigPath = ConfigDir + "/DataUiConfig.asset";
        private const string GeometryPath = "Assets/DiceModels/Data/glow.json";
        private const string CrystalGeometryPath = "Assets/DiceModels/Data/crystal.json"; // made by Tools/DiceFx/make_crystal_geometry.py
        private const string NumeralsPath = FxDir + "/dicefx_numerals.png";
        private const string NoisePath = FxDir + "/dicefx_noise.png";
        private const int NumeralMaxSize = 2048;   // the numeral SDF needs the resolution
        private const int NoiseMaxSize = 512;
        private const string PrefabDir = FxDir + "/Prefabs";
        private const string EnvPath = FxDir + "/dicefx_studio.asset";   // generated studio reflection cubemap
        private const int EnvSize = 128;
        private const float CloudSize = 2.6f;    // cloud width in die radii
        private const float CloudHeight = 1.9f;  // cloud centre above the die, in die radii (a gap between cloud and die lets the rain show)
        private const float CloudLateral = 1.0f; // cloud shift toward the top of the screen, in die radii (a top-down camera then sees the rain fall)
        private const float RaysSize = 3.4f;     // nat 20 flare width in die radii (Prismatic and Frost)
        private const float BigRaysSize = 4.4f;  // the supernova and the rift flare reach further
        private const float RainDropSize = 0.12f;   // rain streak width, in cloud units: a card picture is only ~100 px, so thinner streaks vanish when it is scaled down
        private const float RainStreakLength = 4f;  // streak length in widths
        private const float RainDropAlpha = 1f;
        private const float RainSoftness = 0.5f;    // Particle shader edge softness: lower = a fuller, more solid streak
        private const int RainMaxParticles = 200;
        private const float RainRate = 110f;
        private const float RainForward = 0.5f;     // rain starts this far toward the preview camera (cloud units), so it falls in front of the die, not hidden behind it
        private const float RainWidth = 0.9f;       // width of the rain curtain (cloud units): a little wider than the die
        private const string RaysShader = "DiceThrower/DiceFX/Rays";
        private const string ParticleShader = "DiceThrower/DiceFX/Particle";

        /// <summary>Skins of the first DiceFX draft; no longer listed in the Dice tab (delete the assets by hand if you like).</summary>
        private static readonly string[] RetiredSkins =
        {
            "Skin_fx_beholder_Default", "Skin_fx_beholder_Azure", "Skin_fx_beholder_Emerald", "Skin_fx_beholder_Amber", "Skin_fx_beholder_Violet", "Skin_fx_beholder_Rose", "Skin_fx_beholder_Smoke",
            "Skin_fx_prismatic_Pearl", "Skin_fx_prismatic_Sunset", "Skin_fx_prismatic_Midnight",
            "Skin_fx_lava_Basalt", "Skin_fx_lava_BlueFire", "Skin_fx_lava_Toxic",
        };

        private sealed class Variant
        {
            public string name;
            public Color body;                       // the colour the dice buttons use
            public Dictionary<string, object> props; // shader property -> Color or number
        }

        private sealed class Style
        {
            public string family;
            public string title;
            public string shader;
            public bool crystal;      // use the crystal geometry (elongated d4 / d8 / d10) instead of the standard one
            public bool titleOnly;    // one look: the Dice tab card is just the title (the variant name is only used for file names)
            public object[] common;   // shader property / value pairs every variant of the style starts from (a variant's own values win)
            public Variant[] variants;
        }

        [MenuItem("DiceThrower/Dice FX/Build")]
        public static void Build()
        {
            Texture2D numerals = ImportTexture(NumeralsPath, TextureWrapMode.Clamp, NumeralMaxSize);
            Texture2D noise = ImportTexture(NoisePath, TextureWrapMode.Repeat, NoiseMaxSize);
            var geometry = AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryPath);
            var crystalGeometry = AssetDatabase.LoadAssetAtPath<TextAsset>(CrystalGeometryPath);
            if (numerals == null || noise == null || geometry == null)
            {
                Debug.LogError("DiceThrower: DiceFX needs " + NumeralsPath + ", " + NoisePath + " and " + GeometryPath + ". Run Tools/DiceFx/make_dicefx_textures.py and DiceThrower > Build Dice Model Sets first.");
                return;
            }
            if (crystalGeometry == null)
                Debug.LogWarning("DiceThrower: " + CrystalGeometryPath + " not found (run Tools/DiceFx/make_crystal_geometry.py); the crystal sets use the standard shapes for now.");

            BuilderAssets.EnsureFolder(MaterialDir);
            BuilderAssets.EnsureFolder(ResourcesDir);
            Cubemap studio = BuildStudioCubemap();
            BuilderAssets.GetOrCreate<DiceFxConfig>(ResourcesDir + "/" + DiceFxConfig.ResourceName + ".asset");

            BuilderAssets.EnsureFolder(PrefabDir);
            var effectsByFamily = new Dictionary<string, DiceFxEffects>
            {
                { "prismatic", BuildPrismaticEffects() },
                { "lava", BuildLavaEffects() },
                { "cosmic", BuildCosmicEffects() },
                { "frost", BuildFrostEffects() },
                { "void", BuildVoidEffects() },
            };

            var specs = new List<DiceSetSpec>();
            foreach (Style style in Styles())
            {
                Shader shader = Shader.Find(style.shader);
                if (shader == null)
                {
                    Debug.LogError("DiceThrower: shader '" + style.shader + "' not found. Check the Console for shader compile errors.");
                    continue;
                }

                var materials = new Material[style.variants.Length];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = BuildMaterial(style, style.variants[i], shader, numerals, noise, studio);

                DiceModelSet set = BuildModelSet(style, style.crystal && crystalGeometry != null ? crystalGeometry : geometry, materials);
                DiceFxEffects effects;
                effectsByFamily.TryGetValue(style.family, out effects); // null for a family without effects (Mercury)
                for (int i = 0; i < materials.Length; i++)
                {
                    DiceSkin skin = BuildSkin(style, style.variants[i], set, i, effects);
                    specs.Add(new DiceSetSpec { name = style.titleOnly ? style.title : style.title + " " + style.variants[i].name, skin = skin });
                }
            }

            AddDiceSets(specs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DiceThrower: DiceFX built (" + specs.Count + " skins). Pick them in the Dice tab.");
        }

        // ------------------------------------------------------------------ the styles (all tunable numbers live here)

        private static Style[] Styles()
        {
            return new[]
            {
                new Style
                {
                    family = "prismatic", title = "Prismatic", shader = "DiceThrower/DiceFX/Prismatic",
                    variants = new[]
                    {
                        // clear glass with rainbow panels and rays; engraved gold numerals
                        V("Clear", C(0.78f, 0.9f, 1f), "_NumeralColor", Gold, "_NumeralGlow", NumeralGlow),
                        // photo look: big saturated panes of colour, one hue per face, almost no noise inside, thin gold numerals
                        // (tuned toward the photo: fewer, larger, more saturated panes - stronger panels, less per-face hue change, bigger pattern)
                        V("Dichroic", C(0.95f, 0.5f, 0.6f), "_NumeralColor", Gold, "_NumeralGlow", NumeralGlow, "_NumeralThick", 0.03f,
                            "_NoiseScale", 0.55f, "_GlassTint", C(0.3f, 0.36f, 0.5f), "_PanelLow", 0.05f, "_PanelHigh", 0.3f, "_PanelStrength", 1.5f,
                            "_PanelHue", 0.2f, "_FacetVariation", 1.0f, "_Bands", 0.9f, "_Drift", 0.015f, "_Saturation", 1.4f,
                            "_LineStrength", 0.45f, "_LineWidth", 0.03f, "_RayStrength", 0.3f, "_RimGlow", 0.5f, "_Sheen", 0.25f),
                    },
                },
                new Style
                {
                    family = "mercury", title = "Mercury", shader = "DiceThrower/DiceFX/Mercury",
                    variants = new[]
                    {
                        V("Silver", C(0.85f, 0.88f, 0.94f),
                            "_Tint", C(0.92f, 0.94f, 0.98f), "_NumeralColor", C(0.04f, 0.05f, 0.08f), "_NumeralGlow", 0f),
                        V("Gold", C(1f, 0.78f, 0.34f),
                            "_Tint", C(1f, 0.78f, 0.34f), "_SkyColor", C(1f, 0.9f, 0.7f), "_NumeralColor", C(0.15f, 0.07f, 0f), "_NumeralGlow", 0f),
                        V("Gunmetal", C(0.45f, 0.52f, 0.64f),
                            "_Tint", C(0.5f, 0.56f, 0.66f), "_SkyColor", C(0.4f, 0.55f, 0.9f), "_EnvStrength", 1.2f, "_NumeralColor", C(0.5f, 0.95f, 1f), "_NumeralGlow", 1.5f),
                    },
                },
                new Style
                {
                    family = "lava", title = "Lava", shader = "DiceThrower/DiceFX/Lava",
                    variants = new[]
                    {
                        // grey speckled stone with flowing orange glow; engraved gold numerals
                        V("Ember", C(0.95f, 0.45f, 0.08f), "_NumeralColor", Gold, "_NumeralGlow", NumeralGlow, "_StoneColor", C(0.85f, 0.85f, 0.9f), "_HeatBase", 0.66f, "_PatchScale", 0.9f, "_Spill", 0.15f, "_SpeckLow", 0.17f, "_SpeckHigh", 0.3f),
                    },
                },
                new Style
                {
                    family = "cosmic", title = "Cosmic", shader = "DiceThrower/DiceFX/Cosmic",
                    // mostly dark space: thin nebula clouds, bigger and brighter stars
                    common = new object[] { "_CloudLow", 0.5f, "_CloudHigh", 0.85f, "_NebulaStrength", 1f, "_StarAmount", 0.5f, "_StarSize", 0.14f, "_StarStrength", 2.6f },
                    variants = new[]
                    {
                        // violet and blue nebula, white-gold stars, gold numerals
                        V("Nebula", C(0.45f, 0.2f, 0.85f), "_NumeralColor", Gold, "_NumeralGlow", NumeralGlow),
                        // green and teal northern-lights nebula
                        V("Aurora", C(0.15f, 0.8f, 0.6f), "_NumeralColor", C(0.88f, 1f, 0.95f), "_NumeralGlow", NumeralGlow,
                            "_NebulaA", C(0.05f, 0.8f, 0.55f), "_NebulaB", C(0.15f, 0.35f, 1f), "_StarColor", C(0.85f, 1f, 0.95f),
                            "_BurstColor", C(0.7f, 1f, 0.9f), "_AccretionColor", C(0.3f, 1f, 0.7f)),
                        // red and orange dying star
                        V("Nova", C(1f, 0.5f, 0.15f), "_NumeralColor", C(1f, 0.95f, 0.8f), "_NumeralGlow", NumeralGlow,
                            "_SpaceColor", C(0.04f, 0.01f, 0.01f), "_NebulaA", C(1f, 0.35f, 0.08f), "_NebulaB", C(0.9f, 0.1f, 0.35f),
                            "_BurstColor", C(1f, 0.95f, 0.7f), "_AccretionColor", C(1f, 0.75f, 0.2f)),
                    },
                },
                new Style
                {
                    family = "frost", title = "Frost", shader = "DiceThrower/DiceFX/Frost",
                    variants = new[]
                    {
                        // clear pale-blue ice with navy numerals
                        V("Glacier", C(0.55f, 0.82f, 1f), "_NumeralColor", C(0.04f, 0.16f, 0.35f), "_NumeralGlow", 0f),
                        // heavily frosted white-silver
                        V("Rime", C(0.9f, 0.96f, 1f), "_NumeralColor", C(0.1f, 0.2f, 0.4f), "_NumeralGlow", 0f,
                            "_IceDeep", C(0.45f, 0.6f, 0.75f), "_IceLight", C(0.9f, 0.96f, 1f), "_FrostAmount", 0.7f),
                        // deep blue ice with cyan glowing numerals
                        V("Abyss", C(0.1f, 0.35f, 0.7f), "_NumeralColor", C(0.6f, 0.95f, 1f), "_NumeralGlow", NumeralGlow,
                            "_IceDeep", C(0.01f, 0.08f, 0.25f), "_IceLight", C(0.15f, 0.55f, 0.9f), "_FrostAmount", 0.3f),
                    },
                },
                new Style
                {
                    family = "void", title = "Void", shader = "DiceThrower/DiceFX/Void",
                    // a dark body: the glow stays on the edges (high power) and is weaker, less mist
                    common = new object[] { "_RimGlow", 0.8f, "_FresnelPower", 4f, "_MistAmount", 0.25f, "_LineStrength", 0.6f },
                    variants = new[]
                    {
                        // purple edge glow
                        V("Amethyst", C(0.45f, 0.15f, 0.8f), "_NumeralColor", C(0.85f, 0.6f, 1f), "_NumeralGlow", NumeralGlow),
                        // cold white-blue edge glow
                        V("Eclipse", C(0.15f, 0.15f, 0.22f), "_NumeralColor", C(0.9f, 0.95f, 1f), "_NumeralGlow", NumeralGlow,
                            "_RimColor", C(0.85f, 0.9f, 1f), "_RiftColor", C(0.7f, 0.8f, 1f), "_MistColor", C(0.12f, 0.16f, 0.3f), "_FlareColor", C(1f, 1f, 1f)),
                        // red edge glow
                        V("Crimson", C(0.8f, 0.1f, 0.2f), "_NumeralColor", C(1f, 0.75f, 0.6f), "_NumeralGlow", NumeralGlow,
                            "_RimColor", C(1f, 0.15f, 0.2f), "_RiftColor", C(1f, 0.3f, 0.25f), "_MistColor", C(0.4f, 0.04f, 0.08f), "_FlareColor", C(1f, 0.8f, 0.7f)),
                    },
                },

                // ---- gemstone and resin sets (photo sets): material only, no nat 20 / nat 1 effects yet
                new Style
                {
                    // glows cyan in a dark theme (ThemeScene.darkGlow, the Cultist Hideout); the daylight eye elsewhere
                    family = "beholder", title = "Beholder", shader = "DiceThrower/DiceFX/Beholder",
                    common = new object[] {
                        "_NumeralColor", Gold, "_NumeralGlow", 0.1f, "_NumeralThick", 0.02f,
                        "_VeinColor", C(0.75f, 0.04f, 0.04f), "_EyeVein", 0.3f,
                        "_IrisRadius", 0.7f, "_PupilWidth", 0.22f, "_PupilHeight", 0.85f, "_EyeGlow", 1.8f, "_Smoothness", 0.97f,
                        "_EnvStrength", 1.0f, "_BevelWidth", 0.35f, "_BevelTilt", 1f, "_EngraveDepth", 1.6f },
                    variants = new[]
                    {
                        // piercing blue slit eye with a scar, behind blue glass
                        V("Scarred", C(0.1f, 0.55f, 0.65f), "_ShellColor", C(0.4f, 0.66f, 0.9f), "_ScleraColor", C(0.76f, 0.87f, 0.97f), "_SelfLit", 0.4f,
                            "_IrisInner", C(0.25f, 0.85f, 1f), "_IrisOuter", C(0.02f, 0.2f, 0.95f), "_PupilShape", 0f, "_ScarAmount", 1f, "_EyeVein", 0.4f),
                        // yellow goat eye with a horizontal bar pupil, behind green glass
                        V("Goat", C(0.1f, 0.6f, 0.3f), "_ShellColor", C(0.12f, 0.7f, 0.32f), "_ScleraColor", C(0.82f, 0.92f, 0.72f), "_SelfLit", 0.5f,
                            "_IrisInner", C(1f, 0.85f, 0.2f), "_IrisOuter", C(0.8f, 0.5f, 0.05f), "_PupilShape", 1f, "_PupilWidth", 0.28f, "_PupilHeight", 0.95f,
                            "_FibreContrast", 0.3f, "_IrisRadius", 0.78f),
                        // streaked gold and orange tiger eye with a round pupil, behind violet glass
                        V("Tiger", C(0.4f, 0.2f, 0.7f), "_ShellColor", C(0.5f, 0.2f, 0.95f), "_ScleraColor", C(0.88f, 0.76f, 0.97f), "_SelfLit", 0.5f,
                            "_IrisInner", C(1f, 0.78f, 0.12f), "_IrisOuter", C(0.95f, 0.3f, 0.02f), "_PupilShape", 2f, "_PupilWidth", 0.3f,
                            "_FibreScale", 26f, "_FibreContrast", 0.95f, "_IrisRadius", 0.78f),
                    },
                },
                new Style
                {
                    // frosted orange jelly with a cream stripe; wobbles after impacts
                    family = "jelly", title = "Orange Jelly", shader = "DiceThrower/DiceFX/Jelly", titleOnly = true,
                    variants = new[]
                    {
                        V("Default", C(1f, 0.5f, 0.1f), "_NumeralColor", C(1f, 0.82f, 0.4f), "_NumeralGlow", 0.9f, "_NumeralThick", 0.02f, "_NumeralMetal", 0.5f,
                            "_DeepColor", C(1f, 0.4f, 0.04f), "_LightColor", C(1f, 0.68f, 0.28f), "_StripeScale", 0.55f, "_StripeWarp", 0.3f, "_StripeSoft", 0.4f,
                            "_ShadeColor", C(0.95f, 0.14f, 0.03f), "_ShadeAmount", 0.8f,
                            "_Subsurface", 0.45f, "_RimColor", C(1f, 0.55f, 0.25f), "_RimGlow", 0.15f, "_Smoothness", 0.35f, "_Grain", 0.05f,
                            "_EnvStrength", 0.55f, "_BevelWidth", 0.35f, "_BevelTilt", 1f, "_EngraveDepth", 1.6f),
                    },
                },
                new Style
                {
                    // deep violet crystal with a pale tip, gold sun and moon engraving
                    family = "amethyst", title = "Amethyst Tarot", shader = "DiceThrower/DiceFX/Gem", crystal = true, titleOnly = true,
                    variants = new[]
                    {
                        V("Default", C(0.2f, 0.07f, 0.4f), "_NumeralColor", Gold, "_NumeralGlow", 0.15f, "_NumeralThick", 0.02f,
                            "_ColorA", C(0.03f, 0.008f, 0.08f), "_ColorB", C(0.2f, 0.05f, 0.4f), "_ColorC", C(0.5f, 0.3f, 0.78f),
                            "_Cloudiness", 0.75f, "_BandScale", 2f, "_NoiseScale", 2.2f, "_Smoothness", 0.95f, "_Depth", 0.9f,
                            "_InnerColor", C(0.35f, 0.1f, 0.7f), "_InnerGlow", 0.18f, "_RimColor", C(0.6f, 0.4f, 0.95f), "_RimGlow", 0.35f,
                            "_FractureColor", C(0.75f, 0.55f, 1f), "_FractureAmount", 0.7f, "_FractureWidth", 0.045f, "_Fire", 0.5f,
                            "_DecorMode", 3f, "_DecorStrength", 1f, "_DecorSize", 0.95f,
                            "_EnvStrength", 1.0f, "_BevelWidth", 0.3f, "_BevelTilt", 0.9f, "_EngraveDepth", 2.2f),
                    },
                },
                new Style
                {
                    // milky stone with green moss, a compass rose on every face
                    family = "moss", title = "Moss Agate", shader = "DiceThrower/DiceFX/Gem", titleOnly = true,
                    variants = new[]
                    {
                        V("Default", C(0.7f, 0.78f, 0.72f), "_NumeralColor", Gold, "_NumeralGlow", 0.15f, "_NumeralThick", 0.03f,
                            "_ColorA", C(0.9f, 0.92f, 0.9f), "_ColorB", C(0.76f, 0.82f, 0.78f), "_ColorC", C(0.97f, 0.98f, 0.97f),
                            "_Cloudiness", 0.6f, "_NoiseScale", 3.2f, "_Smoothness", 0.95f, "_Depth", 0.7f,
                            "_MossColor", C(0.03f, 0.18f, 0.07f), "_MossAmount", 0.85f, "_MossLow", 0.3f, "_MossHigh", 0.58f, "_MossFilament", 1f,
                            "_PerDie", 0.4f, "_TintA", C(0.07f, 0.24f, 0.12f), "_TintB", C(0.7f, 0.76f, 0.73f), "_TintC", C(0.4f, 0.5f, 0.22f), "_TintD", C(0.25f, 0.45f, 0.32f),
                            "_InnerColor", C(0.7f, 0.9f, 0.75f), "_InnerGlow", 0.12f, "_RimColor", C(0.85f, 1f, 0.9f), "_RimGlow", 0.2f,
                            "_DecorMode", 2f, "_DecorStrength", 1f, "_DecorSize", 1f,
                            "_EnvStrength", 1.2f, "_BevelWidth", 0.3f, "_BevelTilt", 0.9f, "_EngraveDepth", 2.2f),
                    },
                },
                new Style
                {
                    // clear resin with pink and navy smoke and iridescent glitter
                    family = "twilight", title = "Resin Twilight Tourmaline", shader = "DiceThrower/DiceFX/Twilight", titleOnly = true,
                    variants = new[]
                    {
                        V("Default", C(0.55f, 0.6f, 0.9f), "_NumeralColor", Gold, "_NumeralGlow", 0.15f, "_NumeralThick", 0.02f,
                            "_ClearColor", C(0.5f, 0.58f, 0.82f), "_PinkColor", C(1f, 0.5f, 0.78f), "_BlueColor", C(0.02f, 0.07f, 0.4f),
                            "_SmokeLow", 0.2f, "_SmokeHigh", 0.45f, "_NoiseScale", 2.2f, "_Glow", 0.55f, "_Smoothness", 0.98f, "_GlitterAmount", 0.6f, "_RimGlow", 0.3f,
                            "_Refract", 1.4f, "_EnvStrength", 0.9f, "_BevelWidth", 0.38f, "_BevelTilt", 1.1f, "_EngraveDepth", 1.5f),
                    },
                },
                new Style
                {
                    // banded brown, orange and cream agate, crystal points
                    family = "super7", title = "Super Seven Agate", shader = "DiceThrower/DiceFX/Gem", crystal = true, titleOnly = true,
                    variants = new[]
                    {
                        V("Default", C(0.85f, 0.45f, 0.12f), "_NumeralColor", Gold, "_NumeralGlow", 0.5f,
                            "_ColorA", C(0.2f, 0.09f, 0.08f), "_ColorB", C(0.85f, 0.42f, 0.1f), "_ColorC", C(0.92f, 0.82f, 0.7f),
                            "_Cloudiness", 0.25f, "_BandScale", 2.2f, "_BandWarp", 1.6f, "_BandSoft", 0.1f,
                            "_VeinColor", C(0.95f, 0.9f, 0.85f), "_VeinAmount", 0.5f, "_VeinWidth", 0.04f,
                            "_InnerColor", C(0.9f, 0.45f, 0.15f), "_InnerGlow", 0.3f, "_RimColor", C(1f, 0.7f, 0.4f), "_RimGlow", 0.3f,
                            "_EnvStrength", 1.4f, "_BevelWidth", 0.3f, "_BevelTilt", 0.9f, "_EngraveDepth", 2.2f),
                    },
                },
                new Style
                {
                    // golden-brown with a bright band that slides as the die turns
                    family = "tigereye", title = "Yellow Tiger's Eye", shader = "DiceThrower/DiceFX/Gem", crystal = true, titleOnly = true,
                    variants = new[]
                    {
                        V("Default", C(0.8f, 0.5f, 0.08f), "_NumeralColor", Gold, "_NumeralGlow", 0.5f,
                            "_ColorA", C(0.1f, 0.04f, 0.01f), "_ColorB", C(0.62f, 0.36f, 0.05f), "_ColorC", C(0.95f, 0.7f, 0.2f),
                            "_Cloudiness", 0.2f, "_BandScale", 4f, "_BandSoft", 0.3f,
                            "_Chatoyance", 1.6f, "_ChatoScale", 2.5f, "_ChatoPower", 5f, "_SheenColor", C(1f, 0.8f, 0.35f),
                            "_InnerColor", C(0.8f, 0.45f, 0.05f), "_InnerGlow", 0.2f, "_RimGlow", 0.15f,
                            "_EnvStrength", 1.4f, "_BevelWidth", 0.3f, "_BevelTilt", 0.9f, "_EngraveDepth", 2.2f),
                    },
                },
                new Style
                {
                    // clear quartz with black rutile needles
                    family = "quartz", title = "Black Rutilated Quartz", shader = "DiceThrower/DiceFX/Gem", crystal = true, titleOnly = true,
                    variants = new[]
                    {
                        V("Default", C(0.45f, 0.48f, 0.54f), "_NumeralColor", Gold, "_NumeralGlow", 0.4f,
                            "_ColorA", C(0.42f, 0.45f, 0.5f), "_ColorB", C(0.62f, 0.66f, 0.72f), "_ColorC", C(0.3f, 0.33f, 0.38f),
                            "_Cloudiness", 0.8f, "_NeedleAmount", 1f, "_NeedleWidth", 0.035f, "_Smoothness", 0.95f,
                            "_InnerColor", C(0.6f, 0.72f, 0.9f), "_InnerGlow", 0.25f, "_RimColor", C(1f, 1f, 1f), "_RimGlow", 0.2f,
                            // photoreal pilot: studio reflections, rounded polished edges, engraved numerals
                            "_EnvStrength", 1.8f, "_BevelWidth", 0.3f, "_BevelTilt", 0.9f, "_EngraveDepth", 2.2f),
                    },
                },
                new Style
                {
                    // every die has its own colour: green, red-brown, yellow or pink-grey
                    family = "indian", title = "Indian Agate", shader = "DiceThrower/DiceFX/Gem", crystal = true, titleOnly = true,
                    variants = new[]
                    {
                        V("Default", C(0.45f, 0.5f, 0.25f), "_NumeralColor", Gold, "_NumeralGlow", 0.5f,
                            "_ColorA", C(0.15f, 0.3f, 0.2f), "_ColorB", C(0.45f, 0.35f, 0.25f), "_ColorC", C(0.8f, 0.6f, 0.35f),
                            "_Cloudiness", 0.55f, "_PerDie", 1f, "_TintMix", 0.95f, "_Fire", 0.15f, "_Cloudiness", 0.8f, "_Smoothness", 0.85f,
                            // the photo's five: dark moss green, brick-red jasper, mustard yellow, mauve pink, teal green
                            "_TintA", C(0.05f, 0.24f, 0.12f), "_TintB", C(0.6f, 0.09f, 0.04f), "_TintC", C(0.82f, 0.5f, 0.06f), "_TintD", C(0.5f, 0.25f, 0.33f), "_TintE", C(0.1f, 0.45f, 0.34f),
                            "_VeinColor", C(0.9f, 0.8f, 0.7f), "_VeinAmount", 0.25f,
                            "_InnerColor", C(0.8f, 0.5f, 0.3f), "_InnerGlow", 0f, "_RimGlow", 0f,
                            "_EnvStrength", 0.6f, "_BevelWidth", 0.3f, "_BevelTilt", 0.9f, "_EngraveDepth", 2.2f),
                    },
                },
            };
        }

        private static readonly Color Gold = new Color(1f, 0.84f, 0.4f, 1f);
        private const float NumeralGlow = 1.2f; // how bright the engraved numerals are (they also glow a little)

        private static Color C(float r, float g, float b)
        {
            return new Color(r, g, b, 1f);
        }

        /// <summary>A variant: name, button colour, then shader property / value pairs (Color or number).</summary>
        private static Variant V(string name, Color body, params object[] pairs)
        {
            var v = new Variant { name = name, body = body, props = new Dictionary<string, object>() };
            for (int i = 0; i + 1 < pairs.Length; i += 2) v.props[(string)pairs[i]] = pairs[i + 1];
            return v;
        }


        // ------------------------------------------------------------------ nat 20 / nat 1 effects

        private static DiceFxEffects BuildPrismaticEffects()
        {
            GameObject rays = BuildRaysPrefab("Fx_Rays", "DiceFX_Rays");
            GameObject cloud = BuildRainCloudPrefab();
            return BuildEffects("prismatic", rays, RaysSize, cloud);
        }

        private static DiceFxEffects BuildLavaEffects()
        {
            return BuildEffects("lava", null, RaysSize, null); // lava only changes the die's own look
        }

        // The Cosmic, Frost and Void dice change their own look for a nat 1 (black hole, melt, fade-out), so only the nat 20 has an object.
        // All three nat 20 objects are the Rays quad with another material: many rays = supernova, six sharp ones = snowflake, two = a rift.

        private static DiceFxEffects BuildCosmicEffects()
        {
            GameObject supernova = BuildRaysPrefab("Fx_Supernova", "DiceFX_Supernova",
                "_Intensity", 2.2f, "_RayCount", 10f, "_RaySharp", 10f, "_Spin", 0.25f, "_Wobble", 0.8f, "_CoreGlow", 1.6f,
                "_Rainbow", 0f, "_Tint", C(1f, 0.8f, 0.5f));
            return BuildEffects("cosmic", supernova, BigRaysSize, null);
        }

        private static DiceFxEffects BuildFrostEffects()
        {
            GameObject bloom = BuildRaysPrefab("Fx_FrostBloom", "DiceFX_FrostBloom",
                "_Intensity", 1.8f, "_RayCount", 6f, "_RaySharp", 14f, "_Spin", 0.06f, "_Wobble", 0.2f, "_CoreGlow", 0.9f,
                "_Rainbow", 0f, "_Tint", C(0.65f, 0.88f, 1f));
            return BuildEffects("frost", bloom, RaysSize, null);
        }

        private static DiceFxEffects BuildVoidEffects()
        {
            GameObject flare = BuildRaysPrefab("Fx_RiftFlare", "DiceFX_RiftFlare",
                "_Intensity", 2f, "_RayCount", 2f, "_RaySharp", 18f, "_Spin", 0.05f, "_Wobble", 0.4f, "_CoreGlow", 1.1f,
                "_Rainbow", 0f, "_Tint", C(0.75f, 0.4f, 1f));
            return BuildEffects("void", flare, BigRaysSize, null);
        }

        /// <summary>Both effects exist for every family. The sizes are only written when there is an object (else the asset keeps its own).</summary>
        private static DiceFxEffects BuildEffects(string family, GameObject nat20Prefab, float nat20Size, GameObject nat1Prefab)
        {
            var effects = BuilderAssets.GetOrCreate<DiceFxEffects>(ConfigDir + "/DiceFxEffects_" + family + ".asset");
            var so = new SerializedObject(effects);
            so.FindProperty("hasNat20").boolValue = true;
            so.FindProperty("nat20Prefab").objectReferenceValue = nat20Prefab;
            so.FindProperty("hasNat1").boolValue = true;
            so.FindProperty("nat1Prefab").objectReferenceValue = nat1Prefab;
            if (nat20Prefab != null) so.FindProperty("nat20Size").floatValue = nat20Size;
            if (nat1Prefab != null)
            {
                so.FindProperty("nat1Size").floatValue = CloudSize;
                so.FindProperty("nat1Height").floatValue = CloudHeight;
                so.FindProperty("nat1Lateral").floatValue = CloudLateral;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effects);
            return effects;
        }

        private static Material EffectMaterial(string name, string shaderName, params object[] props)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError("DiceThrower: shader '" + shaderName + "' not found."); return null; }
            string path = MaterialDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            for (int i = 0; i + 1 < props.Length; i += 2) SetProperty(mat, (string)props[i], props[i + 1]);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetProperty(Material mat, string name, object value)
        {
            if (value is Color) mat.SetColor(name, (Color)value);
            else mat.SetFloat(name, System.Convert.ToSingle(value));
        }

        private static void NoShadows(Renderer r)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>A camera-facing quad with a Rays material (shader values given as name / value pairs), 1 unit wide (DieFxDriver scales it).</summary>
        private static GameObject BuildRaysPrefab(string prefabName, string materialName, params object[] props)
        {
            Material mat = EffectMaterial(materialName, RaysShader, props);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = prefabName;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            NoShadows(r);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + prefabName + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>A puffy grey cloud (a few spheres in one mesh, about 1 unit wide) with rain falling from it.</summary>
        private static GameObject BuildRainCloudPrefab()
        {
            Mesh cloudMesh = BuildCloudMesh();

            string cloudMatPath = MaterialDir + "/DiceFX_Cloud.mat";
            var cloudMat = AssetDatabase.LoadAssetAtPath<Material>(cloudMatPath);
            if (cloudMat == null)
            {
                cloudMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(cloudMat, cloudMatPath);
            }
            cloudMat.SetColor("_BaseColor", new Color(0.5f, 0.55f, 0.68f, 1f));
            cloudMat.SetFloat("_Smoothness", 0.1f);
            cloudMat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(cloudMat);

            var root = new GameObject("Fx_RainCloud");
            var body = new GameObject("Cloud");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = cloudMesh;
            var mr = body.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cloudMat;
            NoShadows(mr);

            var rain = new GameObject("Rain");
            rain.transform.SetParent(root.transform, false);
            rain.transform.localPosition = new Vector3(0f, -0.12f, -RainForward);
            var ps = rain.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // the drops follow the cloud and scale with it
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.startSize = RainDropSize;
            main.startColor = new Color(0.85f, 0.95f, 1f, RainDropAlpha);
            main.maxParticles = RainMaxParticles;

            var emission = ps.emission;
            emission.rateOverTime = RainRate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(RainWidth, 0.01f, 0.25f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0f;
            velocity.y = -2.4f;
            velocity.z = 0f;

            var pr = rain.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.lengthScale = RainStreakLength;
            pr.velocityScale = 0f;
            pr.sharedMaterial = EffectMaterial("DiceFX_Rain", ParticleShader, "_Softness", RainSoftness);
            NoShadows(pr);

            ps.Play(true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Fx_RainCloud.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Mesh BuildCloudMesh()
        {
            const string path = FxDir + "/Fx_CloudMesh.asset";
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);

            // x, y, radius (the cloud is about 1 unit wide, flat underneath)
            float[,] puffs = { { -0.28f, 0f, 0.2f }, { 0f, 0.08f, 0.27f }, { 0.28f, 0f, 0.2f }, { -0.1f, -0.02f, 0.2f }, { 0.12f, -0.02f, 0.2f } };
            var combine = new CombineInstance[puffs.GetLength(0)];
            for (int i = 0; i < combine.Length; i++)
            {
                float d = puffs[i, 2] * 2f; // the primitive sphere has diameter 1
                combine[i].mesh = sphere;
                combine[i].transform = Matrix4x4.TRS(new Vector3(puffs[i, 0], puffs[i, 1], 0f), Quaternion.identity, new Vector3(d, d * 0.8f, d));
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "Fx_CloudMesh" };
            mesh.Clear();
            mesh.CombineMeshes(combine, true, true);
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ------------------------------------------------------------------ assets

        private static Material BuildMaterial(Style style, Variant v, Shader shader, Texture2D numerals, Texture2D noise, Cubemap studio)
        {
            string path = MaterialDir + "/DiceFX_" + style.family + "_" + Slug(v.name) + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_NumeralMap", numerals);
            mat.SetTexture("_NoiseMap", noise);
            if (mat.HasProperty("_EnvMap")) mat.SetTexture("_EnvMap", studio);
            ResetOutOfRange(mat, shader);
            if (style.common != null)
            {
                for (int i = 0; i + 1 < style.common.Length; i += 2) SetProperty(mat, (string)style.common[i], style.common[i + 1]);
            }
            foreach (KeyValuePair<string, object> kv in v.props) SetProperty(mat, kv.Key, kv.Value);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// A Range property that holds a value outside its range (left over from an older version of the shader) goes back to the shader's default.
        /// This is what turned the Mercury dice black: _NumeralHalo was 4 (range 0.1..0.5), which masked out all of the chrome reflection.
        /// Values inside the range, tuned in the Inspector, are kept.
        /// </summary>
        private static void ResetOutOfRange(Material mat, Shader shader)
        {
            for (int p = 0; p < shader.GetPropertyCount(); p++)
            {
                if (shader.GetPropertyType(p) != UnityEngine.Rendering.ShaderPropertyType.Range) continue;
                string name = shader.GetPropertyName(p);
                Vector2 limits = shader.GetPropertyRangeLimits(p);
                float value = mat.GetFloat(name);
                if (value >= limits.x && value <= limits.y) continue;
                float fallback = shader.GetPropertyDefaultFloatValue(p);
                Debug.Log("DiceThrower: " + mat.name + " " + name + " was " + value + " (range " + limits.x + ".." + limits.y + "); reset to " + fallback + ".");
                mat.SetFloat(name, fallback);
            }
        }

        private static DiceModelSet BuildModelSet(Style style, TextAsset geometry, Material[] materials)
        {
            var set = BuilderAssets.GetOrCreate<DiceModelSet>(ConfigDir + "/DiceModel_fx_" + style.family + ".asset");
            var so = new SerializedObject(set);
            so.FindProperty("geometry").objectReferenceValue = geometry;
            SerializedProperty list = so.FindProperty("variants");
            list.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = style.variants[i].name;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("material").objectReferenceValue = materials[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            return set;
        }

        private static DiceSkin BuildSkin(Style style, Variant v, DiceModelSet set, int index, DiceFxEffects effects)
        {
            var skin = BuilderAssets.GetOrCreate<DiceSkin>(ConfigDir + "/Skin_fx_" + style.family + "_" + Slug(v.name) + ".asset");
            Color ink = v.props.ContainsKey("_NumeralColor") ? (Color)v.props["_NumeralColor"] : Color.black;
            var so = new SerializedObject(skin);
            so.FindProperty("body").colorValue = v.body;
            so.FindProperty("ink").colorValue = ink;
            so.FindProperty("model").objectReferenceValue = set;
            so.FindProperty("variant").intValue = index;
            so.FindProperty("effects").objectReferenceValue = effects;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skin);
            return skin;
        }

        /// <summary>Appends the dice sets the Dice tab does not list yet; the saved choice is an index, so the existing order never changes.</summary>
        private static void AddDiceSets(List<DiceSetSpec> found)
        {
            var config = AssetDatabase.LoadAssetAtPath<DataUiConfig>(DataUiConfigPath);
            if (config == null) { Debug.LogWarning("DiceThrower: " + DataUiConfigPath + " not found; run DiceThrower > Build Config, Prefabs and Scene first."); return; }

            var list = new List<DiceSetSpec>(config.diceSets);
            list.RemoveAll(d => d == null || d.skin == null || System.Array.IndexOf(RetiredSkins, d.skin.name) >= 0);
            foreach (DiceSetSpec spec in found)
            {
                if (!list.Exists(d => d.skin == spec.skin)) list.Add(spec);
            }
            config.diceSets = list.ToArray();
            EditorUtility.SetDirty(config);
        }

        private static Texture2D ImportTexture(string path, TextureWrapMode wrap, int maxSize)
        {
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            bool changed = importer.sRGBTexture || !importer.mipmapEnabled || importer.wrapMode != wrap
                           || importer.maxTextureSize != maxSize || importer.alphaIsTransparency;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;                 // data, not colour
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.wrapMode = wrap;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = maxSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ studio reflection cubemap

        /// <summary>
        /// A small HDR studio made in code: a dark floor, a soft sky, a big warm key softbox, a cool strip light and a rim light.
        /// Shiny dice reflect it, which is most of what makes a polished stone or resin look real. Rebuilt on every run (cheap).
        /// </summary>
        private static Cubemap BuildStudioCubemap()
        {
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(EnvPath);
            if (cube == null)
            {
                cube = new Cubemap(EnvSize, TextureFormat.RGBAHalf, true);
                AssetDatabase.CreateAsset(cube, EnvPath);
            }

            var pixels = new Color[EnvSize * EnvSize];
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < EnvSize; y++)
                {
                    for (int x = 0; x < EnvSize; x++)
                    {
                        float u = ((x + 0.5f) / EnvSize) * 2f - 1f;
                        float v = ((y + 0.5f) / EnvSize) * 2f - 1f;
                        Vector3 dir;
                        switch (face)
                        {
                            case 0: dir = new Vector3(1f, -v, -u); break;   // +X
                            case 1: dir = new Vector3(-1f, -v, u); break;   // -X
                            case 2: dir = new Vector3(u, 1f, v); break;     // +Y
                            case 3: dir = new Vector3(u, -1f, -v); break;   // -Y
                            case 4: dir = new Vector3(u, -v, 1f); break;    // +Z
                            default: dir = new Vector3(-u, -v, -1f); break; // -Z
                        }
                        pixels[y * EnvSize + x] = StudioColor(dir.normalized);
                    }
                }
                cube.SetPixels(pixels, (CubemapFace)face);
            }
            cube.Apply(true, false);
            EditorUtility.SetDirty(cube);
            return cube;
        }

        private static Color StudioColor(Vector3 d)
        {
            // soft sky above, dim floor below
            float up = Mathf.Clamp01(d.y * 0.5f + 0.5f);
            Color c = Color.Lerp(new Color(0.03f, 0.03f, 0.035f), new Color(0.2f, 0.22f, 0.26f), Mathf.SmoothStep(0f, 1f, up));
            c += Softbox(d, new Vector3(-0.5f, 0.8f, 0.35f), 0.78f, 0.88f, new Color(7f, 6.7f, 6.2f));   // key, warm white (big, bright)
            c += Softbox(d, new Vector3(0.95f, 0.15f, -0.25f), 0.86f, 0.93f, new Color(3f, 3.8f, 5f));   // strip, cool
            c += Softbox(d, new Vector3(0.05f, 0.3f, -1f), 0.84f, 0.92f, new Color(3.5f, 3.1f, 2.6f));   // rim from behind
            c += Softbox(d, new Vector3(0.1f, 0.35f, 0.95f), 0.9f, 0.96f, new Color(2.2f, 2.2f, 2.4f));  // front fill so the camera side shows a highlight
            c += Softbox(d, new Vector3(-0.9f, -0.1f, -0.3f), 0.9f, 0.95f, new Color(1.2f, 1.3f, 1.6f)); // dim side card
            c += Softbox(d, new Vector3(0f, 1f, 0f), 0.55f, 0.95f, new Color(0.5f, 0.5f, 0.55f));       // broad top fill
            c.a = 1f;
            return c;
        }

        private static Color Softbox(Vector3 d, Vector3 centre, float lo, float hi, Color colour)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lo, hi, Vector3.Dot(d, centre.normalized)));
            return colour * t;
        }

        private static string Slug(string text)
        {
            return text.Replace(" ", "");
        }
    }
}
