using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DiceThrower.EditorTools
{
    /// <summary>
    /// One-click Android setup and build. "Configure Android Player" writes the Player Settings the app needs
    /// (portrait only, ARM64, IL2CPP, package id); "Build Android APK" builds the scenes in Build Settings.
    /// Needs the Android Build Support module (with SDK, NDK and OpenJDK) installed for this Editor version.
    /// </summary>
    public static class AndroidBuild
    {
        private static class Settings
        {
            public const string CompanyName = "ForgeAndTavern"; // change to your own studio name before publishing
            public const string ProductName = "DiceThrower";
            public const string PackageId = "com.forgeandtavern.dicethrower";
            public const string Version = "0.2.0";
            public const int VersionCode = 2;
            public const AndroidSdkVersions MinSdk = AndroidSdkVersions.AndroidApiLevel26;
            public const string OutputDir = "Builds";
            public const string ApkName = "DiceThrower.apk";
        }

        [MenuItem("DiceThrower/Configure Android Player")]
        public static void Configure()
        {
            PlayerSettings.companyName = Settings.CompanyName;
            PlayerSettings.productName = Settings.ProductName;
            PlayerSettings.bundleVersion = Settings.Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, Settings.PackageId);
            PlayerSettings.Android.bundleVersionCode = Settings.VersionCode;

            // portrait only (v1)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = Settings.MinSdk;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            AssetDatabase.SaveAssets();
            Debug.Log("DiceThrower: Android Player Settings written (" + Settings.PackageId + ", portrait, IL2CPP, ARM64).");
        }

        [MenuItem("DiceThrower/Build Android APK")]
        public static void BuildApk()
        {
            Configure();
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("DiceThrower: no enabled scene in Build Settings. Run DiceThrower > Build Config, Prefabs and Scene first.");
                return;
            }

            Directory.CreateDirectory(Settings.OutputDir);
            EditorUserBuildSettings.buildAppBundle = false;
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(Settings.OutputDir, Settings.ApkName),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log("DiceThrower: APK built, " + (summary.totalSize / (1024 * 1024)) + " MB, " + summary.outputPath);
            else
                Debug.LogError("DiceThrower: Android build " + summary.result + " with " + summary.totalErrors + " error(s). See the Console above.");
        }
    }
}
