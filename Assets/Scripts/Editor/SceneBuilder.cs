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
        /// <summary>
        /// Makes sure the open scene has a working tray: an object whose prefab is gone (an old leather tray; Unity keeps it as an empty placeholder
        /// with no components) is removed, and the DiceTray prefab is put in the scene if no tray is there. A scene that is fine is left as it is.
        /// </summary>
        internal static void RepairTray()
        {
            bool changed = false;
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!PrefabUtility.IsPrefabAssetMissing(root) && PrefabUtility.GetPrefabInstanceStatus(root) != PrefabInstanceStatus.MissingAsset) continue;
                Debug.Log("DiceThrower: removing '" + root.name + "' from the scene (its prefab is missing).");
                Object.DestroyImmediate(root);
                changed = true;
            }
            if (Object.FindAnyObjectByType<DiceTray>() == null)
            {
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BuilderPaths.TrayPrefab));
                changed = true;
            }
            if (changed) EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

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
