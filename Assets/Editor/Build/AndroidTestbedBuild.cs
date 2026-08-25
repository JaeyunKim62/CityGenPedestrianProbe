using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor
{
    /// <summary>
    /// Builds the synthetic testbed as an Android APK.
    /// </summary>
    /// <remarks>
    /// The testbed carries no terrain, no city meshes and no textures, so it
    /// reaches the phone as the semantic map and the signal catalog alone.
    /// That leaves input and audio as the only things a failure can be, which
    /// is the point of bringing this scene up first rather than a city.
    /// </remarks>
    public static class AndroidTestbedBuild
    {
        private const string ApplicationId = "com.jaeyunkim.pedestrianprobe";
        private const string OutputDirectory = "Builds/Android";
        private const string ApkName = "PedestrianProbe-testbed.apk";

        private const string SceneFolder = "Assets/Testbed/Scenes";

        /// <summary>
        /// Every scene in the testbed folder, sorted by name so SYN01 stays
        /// first and the scene buttons walk them in order.
        /// </summary>
        /// <remarks>
        /// Read from disk rather than listed here. The list was written when
        /// there were five layouts and silently stayed at five when SYN06 and
        /// SYN07 arrived, which left the two stimuli built for the combined
        /// discrimination trial unreachable from the phone.
        /// </remarks>
        private static string[] CollectScenes()
        {
            if (!Directory.Exists(SceneFolder))
                return Array.Empty<string>();

            string[] scenes = Directory.GetFiles(SceneFolder, "*.unity");
            Array.Sort(scenes, StringComparer.Ordinal);
            for (int i = 0; i < scenes.Length; i++)
                scenes[i] = scenes[i].Replace('\\', '/');
            return scenes;
        }

        [MenuItem("Tools/Pedestrian Probe/Testbed/Build Android APK", priority = 41)]
        public static void BuildFromMenu()
        {
            BuildReport report = Run();
            if (report != null && report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(Path.Combine(OutputDirectory, ApkName));
        }

        // Entry point for -executeMethod. Sets the exit code itself so a failed
        // build fails the command rather than reporting success.
        public static void BuildFromCommandLine()
        {
            BuildReport report = null;
            try
            {
                report = Run();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Android build threw: {exception}");
            }

            bool succeeded = report != null &&
                             report.summary.result == BuildResult.Succeeded;
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        private static BuildReport Run()
        {
            string[] scenes = CollectScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError($"No testbed scenes in {SceneFolder}. Run " +
                               "Tools > Pedestrian Probe > Testbed > Build Synthetic " +
                               "Scenes first.");
                return null;
            }

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            EditorUserBuildSettings.buildAppBundle = false;

            Directory.CreateDirectory(OutputDirectory);
            string outputPath = Path.Combine(OutputDirectory, ApkName);

            var options = new BuildPlayerOptions
            {
                // Passed directly rather than through EditorBuildSettings so the
                // project's own scene list is left alone.
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,

                // Development is not optional here: BlindModeOverlay keeps its
                // blind toggle and its status HUD behind DEVELOPMENT_BUILD, so a
                // release build would be permanently blind and silent about it.
                options = BuildOptions.Development,

                // PROBE_DEMO attaches AutoSweepDemo, which is the only reason
                // this build makes any sound with no input device attached.
                extraScriptingDefines = new[] { "PROBE_DEMO" }
            };

            Debug.Log($"Building {scenes.Length} testbed scenes to {outputPath} " +
                      $"as {ApplicationId} (development, PROBE_DEMO).");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"Android build {summary.result}: {summary.totalSize / (1024 * 1024)} MB " +
                      $"in {summary.totalTime}. Errors: {summary.totalErrors}.");
            return report;
        }
    }
}
