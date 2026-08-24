using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor
{
    /// <summary>
    /// Builds a real CityGen city scene as an Android APK, meshes and all.
    /// </summary>
    /// <remarks>
    /// This is the shortest path to seeing a city run on a phone, not the one
    /// to measure it with. The scene renders a whole city behind the black
    /// overlay, so a slow frame here could be the map queries, the scene load
    /// or the rendering, and the three cannot be told apart. A run scene
    /// carrying only the baked map answers that question; this answers whether
    /// it runs at all.
    /// </remarks>
    public static class AndroidCityBuild
    {
        // Its own identity, so a city build and a testbed build sit on the
        // phone at once instead of replacing each other, and so their session
        // logs land in separate per-app directories and cannot be mixed up.
        private const string ApplicationId = "com.jaeyunkim.pedestrianprobe.city";
        private const string ProductName = "Probe NYC";
        private const string OutputDirectory = "Builds/Android";
        private const string ApkName = "PedestrianProbe-city.apk";
        private const string CityScene = "Assets/Scenes/NYC_DoyersChinatown.unity";

        [MenuItem("Tools/Pedestrian Probe/Build City APK (NYC Doyers)", priority = 42)]
        public static void BuildFromMenu()
        {
            Run();
        }

        public static void BuildFromCommandLine()
        {
            BuildReport report = null;
            try
            {
                report = Run();
            }
            catch (Exception exception)
            {
                Debug.LogError($"City build threw: {exception}");
            }

            bool succeeded = report != null &&
                             report.summary.result == BuildResult.Succeeded;
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        private static BuildReport Run()
        {
            if (!File.Exists(CityScene))
            {
                Debug.LogError($"City scene is missing: {CityScene}");
                return null;
            }

            // The scene is authored by CityGen and carries no probe of its own,
            // so run the same setup the menu does. It saves the scene itself.
            Debug.Log($"Opening {CityScene} and running Setup Scene.");
            EditorSceneManager.OpenScene(CityScene, OpenSceneMode.Single);
            PedestrianFreeCamSetup.SetupFromMenu();

            // Setup reports its own failures and returns, so confirm rather than
            // shipping a scene whose runtime would disable itself on Awake.
            var runtime = UnityEngine.Object.FindFirstObjectByType<PedestrianProbeRuntime>();
            if (runtime == null)
            {
                Debug.LogError("Setup Scene left no PedestrianProbeRuntime in the " +
                               "scene, so the build would start disabled. Aborting.");
                return null;
            }

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            EditorUserBuildSettings.buildAppBundle = false;

            Directory.CreateDirectory(OutputDirectory);
            string outputPath = Path.Combine(OutputDirectory, ApkName);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { CityScene },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
                extraScriptingDefines = new[] { "PROBE_DEMO" }
            };

            // Identity is a property of this recipe, not of the project, and
            // PlayerSettings writes straight through to ProjectSettings.asset.
            // Restoring afterwards keeps whichever build ran last from deciding
            // what the project claims to be.
            string previousId =
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string previousProduct = PlayerSettings.productName;
            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,
                    ApplicationId);
                PlayerSettings.productName = ProductName;

                Debug.Log($"Building {CityScene} to {outputPath} as {ApplicationId}.");
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log($"City build {summary.result}: " +
                          $"{summary.totalSize / (1024 * 1024)} MB in {summary.totalTime}. " +
                          $"Errors: {summary.totalErrors}.");
                return report;
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,
                    previousId);
                PlayerSettings.productName = previousProduct;
            }
        }
    }
}
