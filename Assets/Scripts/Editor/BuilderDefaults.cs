using UnityEngine;

namespace DiceThrower.EditorTools
{
    /// <summary>Asset locations and the starting values written into NEW assets by the builders.</summary>
    internal static class BuilderPaths
    {
        public const string ConfigDir = "Assets/Config";
        public const string PrefabDir = "Assets/Prefabs";
        public const string MaterialDir = "Assets/Materials";
        public const string UiDir = "Assets/UI";
        public const string SceneDir = "Assets/Scenes";

        public const string DiceMaterial = MaterialDir + "/DiceLit.mat";
        public const string FloorMaterial = MaterialDir + "/TrayFloor.mat";
        public const string RailMaterial = MaterialDir + "/TrayRail.mat";
        public const string Physics = ConfigDir + "/DicePhysics.asset";
        public const string TrayConfig = ConfigDir + "/TrayConfig.asset";
        public const string CameraFit = ConfigDir + "/CameraFitConfig.asset";
        public const string Rules = ConfigDir + "/RulesConfig.asset";
        public const string UiConfig = ConfigDir + "/RollUiConfig.asset";
        public const string AttackSkin = ConfigDir + "/Skin_Attack.asset";
        public const string DamageSkin = ConfigDir + "/Skin_Damage.asset";
        public const string ObsidianSkin = ConfigDir + "/Skin_Attack_Obsidian.asset";
        public const string EmeraldSkin = ConfigDir + "/Skin_Damage_Emerald.asset";
        public const string DataUiConfig = ConfigDir + "/DataUiConfig.asset";
        public const string FeedbackConfig = ConfigDir + "/FeedbackConfig.asset";
        public const string DiePrefab = PrefabDir + "/Die.prefab";
        public const string TrayPrefab = PrefabDir + "/DiceTray.prefab";
        public const string ScreenPrefab = PrefabDir + "/RollScreen.prefab";
        public const string PanelSettings = UiDir + "/RollPanelSettings.asset";
        public const string TavernTheme = UiDir + "/Tavern.uss";
        public const string MoonlightTheme = UiDir + "/Moonlight.uss";
        public const string Uxml = UiDir + "/RollScreen.uxml";
        public const string Scene = SceneDir + "/Roll.unity";
    }

    internal static class BuilderDefaults
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

        // themes
        public const string TavernThemeName = "Tavern & Leather", MoonlightThemeName = "Moonlight";

        // scene rig
        public static readonly Color CameraBackground = new Color(0.10f, 0.31f, 0.23f);
        public const float CameraFov = 40f;
        public const float LightIntensity = 1.1f;
        public static readonly Vector3 LightEuler = new Vector3(55f, -25f, 0f);

        // panel: the UI is designed for a phone and always fits the screen
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(390, 844);
    }
}
