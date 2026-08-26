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
        private const string ApplicationId = "com.jaeyunkim.pedsim.syn";
        private const string ProductName = "합성환경 공간인지";
        private const string OutputDirectory = "Builds/Android";
        private const string ApkName = "PedestrianProbe-testbed.apk";
        private const string AabName = "PedestrianProbe-testbed.aab";
        private const string ExperimenterAabName =
            "PedestrianProbe-testbed-experimenter.aab";
        private const string KeyAlias = "pedestrianprobe";

        private const string SceneFolder = "Assets/Testbed/Scenes";

        /// <summary>
        /// Points Unity at the release keystore, reading both passwords from
        /// the environment.
        /// </summary>
        /// <remarks>
        /// Passwords are taken from PROBE_KEYSTORE_PASS and PROBE_KEYALIAS_PASS
        /// so that nothing secret is written into this file, committed, or left
        /// in a shell history. The upload key decides who is allowed to publish
        /// updates for the lifetime of the app, so it is not the sort of thing
        /// to keep in a repository.
        ///
        /// Set them before building:
        ///   $env:PROBE_KEYSTORE_PASS = '...'
        ///   $env:PROBE_KEYALIAS_PASS = '...'
        /// </remarks>
        private static bool TryApplySigning()
        {
            string keystore = Environment.GetEnvironmentVariable("PROBE_KEYSTORE")
                              ?? Path.Combine(
                                  Environment.GetFolderPath(
                                      Environment.SpecialFolder.UserProfile),
                                  "pedestrianprobe-release.keystore");
            string storePass = Environment.GetEnvironmentVariable("PROBE_KEYSTORE_PASS");
            string aliasPass = Environment.GetEnvironmentVariable("PROBE_KEYALIAS_PASS")
                               ?? storePass;

            if (!File.Exists(keystore))
            {
                Debug.LogError($"No keystore at {keystore}. Create one, or point " +
                               "PROBE_KEYSTORE at it. A release build has to be " +
                               "signed before Play will take it.");
                return false;
            }

            if (string.IsNullOrEmpty(storePass))
            {
                Debug.LogError("PROBE_KEYSTORE_PASS is not set, so the keystore " +
                               "cannot be opened. Set it in the shell that runs " +
                               "the build rather than anywhere it would be saved.");
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = KeyAlias;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            Debug.Log($"Signing with {Path.GetFileName(keystore)}, alias {KeyAlias}.");
            return true;
        }

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

        /// <summary>
        /// True while building something Google Play will accept: no debug
        /// flag, an app bundle rather than an APK, and CityGen's Resources
        /// folder left out so the download fits under Play's limit.
        /// </summary>
        private static bool releaseMode;

        /// <summary>
        /// True while building the signed app for whoever is running the
        /// session rather than for whoever is in it.
        /// </summary>
        /// <remarks>
        /// The debug map, the status text and the way out of the blind
        /// condition used to hang off DEVELOPMENT_BUILD, which conflated two
        /// separate questions: whether a build is a debug build, and whether it
        /// is going to a participant. Only the second should decide those, and
        /// with distribution over Play internal testing there was no way to get
        /// a signed build that had them — a development APK cannot be uploaded,
        /// and a handset on the far end of a test track has no cable.
        ///
        /// Off by default, so the safe build is the one you get without asking.
        /// </remarks>
        private static bool experimenterMode;

        [MenuItem("Tools/Pedestrian Probe/Testbed/Build Android AAB (Play)", priority = 42)]
        public static void BuildReleaseFromMenu()
        {
            releaseMode = true;
            try
            {
                Run();
            }
            finally
            {
                releaseMode = false;
            }
        }

        [MenuItem("Tools/Pedestrian Probe/Testbed/Build Android AAB (experimenter)",
            priority = 43)]
        public static void BuildExperimenterFromMenu()
        {
            experimenterMode = true;
            try
            {
                BuildReleaseFromMenu();
            }
            finally
            {
                experimenterMode = false;
            }
        }

        // Entry point for -executeMethod, release variant.
        public static void BuildReleaseFromCommandLine()
        {
            releaseMode = true;
            BuildFromCommandLine();
        }

        // Entry point for -executeMethod, experimenter variant. Signed and
        // uploadable like the release, and carrying the diagnostics.
        public static void BuildExperimenterFromCommandLine()
        {
            experimenterMode = true;
            BuildReleaseFromCommandLine();
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

        private static string DescribeRecipe()
        {
            if (!releaseMode)
                return "development, PROBE_DEMO";
            return experimenterMode
                ? "experimenter, app bundle, PROBE_EXPERIMENTER"
                : "participant, app bundle";
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

            // Identity belongs to this recipe, not to the project, so both are
            // put back afterwards. Otherwise whichever build ran last decides
            // what ProjectSettings claims the project is.
            string previousId =
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string previousProduct = PlayerSettings.productName;
            bool previousCustomKeystore = PlayerSettings.Android.useCustomKeystore;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.productName = ProductName;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            // Pinned rather than left on automatic, which resolves to whatever
            // SDK the build machine happens to have. Play judges an upload by
            // this number and it should not depend on the desk it was built at.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
            EditorUserBuildSettings.buildAppBundle = releaseMode;

            if (releaseMode && !TryApplySigning())
                return null;

            // A development APK goes onto a handset over a cable, where the
            // debug key Unity generates is all Android asks for. Signing had to
            // be turned off explicitly because the release recipe turns it on
            // and, until now, left it on: the upload keystore stayed selected
            // in the project afterwards, so every later development build died
            // demanding a password with nobody there to type one.
            if (!releaseMode)
                PlayerSettings.Android.useCustomKeystore = false;

            // Play refuses a version code it has seen before, and the failure
            // arrives at upload rather than at build, long after whoever built
            // it has moved on.
            bool keepVersionCode =
                Environment.GetEnvironmentVariable("PROBE_KEEP_VERSION_CODE") == "1";
            if (releaseMode && !keepVersionCode)
            {
                PlayerSettings.Android.bundleVersionCode++;
                Debug.Log($"Version code {PlayerSettings.Android.bundleVersionCode}, " +
                          $"version {PlayerSettings.bundleVersion}.");
            }
            else if (releaseMode)
            {
                Debug.Log($"Keeping version code {PlayerSettings.Android.bundleVersionCode}, " +
                          $"version {PlayerSettings.bundleVersion}.");
            }

            Directory.CreateDirectory(OutputDirectory);
            // Named apart so the two bundles do not overwrite each other on
            // disk. They carry the same application id, because they are the
            // same app going to the same test track — only one can be up there
            // at a time, and which one is a decision somebody makes per session.
            string outputPath = Path.Combine(OutputDirectory,
                releaseMode
                    ? experimenterMode ? ExperimenterAabName : AabName
                    : ApkName);

            var options = new BuildPlayerOptions
            {
                // Passed directly rather than through EditorBuildSettings so the
                // project's own scene list is left alone.
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,

                // A development build carries the debug HUD and the map, and
                // Play refuses anything marked debuggable on every track, so
                // the two cannot be the same build. The settings menu itself
                // survives either way; only the diagnostics and the way out of
                // the blind condition are compiled out of a release.
                options = releaseMode ? BuildOptions.None : BuildOptions.Development,

                // PROBE_DEMO attaches AutoSweepDemo, which is the only reason a
                // development build makes any sound with no input device
                // attached. A release build is driven by a person.
                //
                // PROBE_EXPERIMENTER puts the map, the status text and the way
                // out of the blind condition back into a signed build, for the
                // person running the session rather than the one in it.
                extraScriptingDefines = releaseMode
                    ? experimenterMode
                        ? new[] { "PROBE_EXPERIMENTER" }
                        : Array.Empty<string>()
                    : new[] { "PROBE_DEMO" }
            };

            Debug.Log($"Building {scenes.Length} testbed scenes to {outputPath} " +
                      $"as {ApplicationId} " +
                      $"({DescribeRecipe()}).");

            try
            {
                // Only a release build sheds it. A development build is
                // installed over a cable where half a gigabyte costs thirty
                // seconds, and leaving the project untouched there is worth
                // more than the space.
                using (releaseMode ? new CityGenResourcesExclusion() : null)
                {
                    BuildReport report = BuildPipeline.BuildPlayer(options);
                    BuildSummary summary = report.summary;
                    Debug.Log($"Android build {summary.result}: " +
                              $"{summary.totalSize / (1024 * 1024)} MB in " +
                              $"{summary.totalTime}. Errors: {summary.totalErrors}.");
                    return report;
                }
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,
                    previousId);
                PlayerSettings.productName = previousProduct;
                PlayerSettings.Android.useCustomKeystore = previousCustomKeystore;
            }
        }
    }
}
