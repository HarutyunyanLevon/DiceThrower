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
        public const string Physics = ConfigDir + "/DicePhysics.asset";
        public const string TrayConfig = ConfigDir + "/TrayConfig.asset";
        public const string CameraFit = ConfigDir + "/CameraFitConfig.asset";
        public const string Rules = ConfigDir + "/RulesConfig.asset";
        public const string UiConfig = ConfigDir + "/RollUiConfig.asset";
        public const string DataUiConfig = ConfigDir + "/DataUiConfig.asset";
        public const string PreviewConfig = ConfigDir + "/DicePreviewConfig.asset";
        public const string FeedbackConfig = ConfigDir + "/FeedbackConfig.asset";
        public const string DiePrefab = PrefabDir + "/Die.prefab";
        public const string TrayPrefab = PrefabDir + "/DiceTray.prefab";
        public const string ScreenPrefab = PrefabDir + "/RollScreen.prefab";
        public const string PanelSettings = UiDir + "/RollPanelSettings.asset";
        public const string Uxml = UiDir + "/RollScreen.uxml";
        public const string Scene = SceneDir + "/Roll.unity";
    }

    internal static class BuilderDefaults
    {
        // die rigidbody and surface
        public const float DieMass = 1f, LinearDamping = 0.05f, AngularDamping = 0.15f, SleepThreshold = 0.3f;
        public const float Bounciness = 0.25f, DynamicFriction = 0.45f, StaticFriction = 0.55f;

        // scene rig
        public static readonly Color CameraBackground = new Color(0.10f, 0.31f, 0.23f);
        public const float CameraFov = 40f;
        public const float LightIntensity = 1.1f;
        public static readonly Vector3 LightEuler = new Vector3(55f, -25f, 0f);

        // panel: the UI is designed for a phone and always fits the screen
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(390, 844);
    }
}
