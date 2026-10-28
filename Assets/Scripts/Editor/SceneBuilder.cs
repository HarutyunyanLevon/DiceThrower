using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;
using DiceThrower.Game;
using DiceThrower.UI;
using Object = UnityEngine.Object;

namespace DiceThrower.EditorTools
{
    /// <summary>Builds the Roll scene: camera, light, tray, event system and the RollScreen.</summary>
    internal static class SceneBuilder
    {
        internal static void BuildScene(float cameraPitch)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene unloads unused assets, so the prefabs are loaded again here instead of passed in
            var trayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuilderPaths.TrayPrefab);
            var screenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuilderPaths.ScreenPrefab);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BuilderDefaults.CameraBackground;
            cam.fieldOfView = BuilderDefaults.CameraFov;
            camGo.AddComponent<AudioListener>();
            camGo.transform.rotation = Quaternion.Euler(cameraPitch, 0f, 0f); // the view moves it into place

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = BuilderDefaults.LightIntensity;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(BuilderDefaults.LightEuler);

            PrefabUtility.InstantiatePrefab(trayPrefab);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            PrefabUtility.InstantiatePrefab(screenPrefab);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), BuilderPaths.Scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BuilderPaths.Scene, true) };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        }
    }
}
