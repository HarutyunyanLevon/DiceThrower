using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using DiceThrower.Game;
using Object = UnityEngine.Object;

namespace DiceThrower.EditorTools
{
    /// <summary>Create-if-missing helpers shared by the builders. Nothing that already exists is overwritten.</summary>
    internal static class BuilderAssets
    {
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        public static T GetOrCreate<T>(string path, Action<T> init = null) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            if (init != null) init(created);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        public static T GetOrCreatePrefab<T>(string path, Func<GameObject> build) where T : Component
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null)
            {
                GameObject go = build();
                existing = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }
            return existing.GetComponent<T>();
        }

        public static Material GetOrCreateMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static PhysicsMaterial GetOrCreatePhysicsMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BuilderPaths.Physics);
            if (existing != null) return existing;
            var pm = new PhysicsMaterial("DicePhysics")
            {
                bounciness = BuilderDefaults.Bounciness,
                dynamicFriction = BuilderDefaults.DynamicFriction,
                staticFriction = BuilderDefaults.StaticFriction,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Average,
            };
            AssetDatabase.CreateAsset(pm, BuilderPaths.Physics);
            return pm;
        }

        public static PanelSettings GetOrCreatePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(BuilderPaths.PanelSettings);
            if (panel != null) return panel;
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = BuilderDefaults.ReferenceResolution;
            panel.screenMatchMode = PanelScreenMatchMode.Shrink;
            string[] themes = AssetDatabase.FindAssets("UnityDefaultRuntimeTheme t:ThemeStyleSheet");
            if (themes.Length > 0)
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(themes[0]));
            AssetDatabase.CreateAsset(panel, BuilderPaths.PanelSettings);
            return panel;
        }

        public static void SetSkin(DiceSkin skin, Color body, Color ink)
        {
            var so = new SerializedObject(skin);
            so.FindProperty("body").colorValue = body;
            so.FindProperty("ink").colorValue = ink;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static bool AssignIfEmpty(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property.objectReferenceValue != null) return false;
            property.objectReferenceValue = value;
            return true;
        }
    }
}
