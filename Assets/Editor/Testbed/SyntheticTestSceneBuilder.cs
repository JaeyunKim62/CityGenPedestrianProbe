using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityGenPedestrianProbe.Editor
{
    public static class SyntheticTestSceneBuilder
    {
        private const string MapFolder = "Assets/Testbed/Maps";
        private const string SceneFolder = "Assets/Testbed/Scenes";
        private const string CatalogPath =
            "Assets/Generated/Signals/SignalCatalog_C101_PulseCategory.asset";
        private const float EyeHeight = 1.7f;

        [MenuItem("Tools/Pedestrian Probe/Testbed/Build Synthetic Scenes", priority = 40)]
        public static void BuildAll()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(MapFolder);
            Directory.CreateDirectory(SceneFolder);
            AssetDatabase.Refresh();

            if (FindCatalog() == null)
                return;
            foreach (SyntheticLayout layout in SyntheticTestMaps.Layouts)
                BuildOne(layout);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Built {SyntheticTestMaps.Layouts.Length} synthetic test scenes in " +
                      $"{SceneFolder}. Open one, press Play, then G to silence semantic.audio.");
        }

        private static SignalCatalog FindCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SignalCatalog>(CatalogPath);
            if (catalog != null)
                return catalog;

            foreach (string assetGuid in AssetDatabase.FindAssets("t:SignalCatalog"))
            {
                catalog = AssetDatabase.LoadAssetAtPath<SignalCatalog>(
                    AssetDatabase.GUIDToAssetPath(assetGuid));
                if (catalog != null)
                    return catalog;
            }

            Debug.LogError("No SignalCatalog asset found, so the synthetic scenes would " +
                           "be silent. Aborting.");
            return null;
        }

        private static SignalCatalog[] FindComparisonCatalogs()
        {
            var found = new System.Collections.Generic.List<SignalCatalog>();
            foreach (string assetGuid in AssetDatabase.FindAssets("t:SignalCatalog"))
            {
                var comparison = AssetDatabase.LoadAssetAtPath<SignalCatalog>(
                    AssetDatabase.GUIDToAssetPath(assetGuid));
                if (comparison != null)
                    found.Add(comparison);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.CatalogId, b.CatalogId));
            return found.ToArray();
        }

        private static void BuildOne(SyntheticLayout layout)
        {
            SemanticSpaceMap map = SyntheticTestMaps.Create(layout.Id);
            string mapPath = $"{MapFolder}/{layout.Id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SemanticSpaceMap>(mapPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(mapPath);
            AssetDatabase.CreateAsset(map, mapPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            var playerObject = new GameObject("Synthetic Pedestrian");
            SceneManager.MoveGameObjectToScene(playerObject, scene);
            playerObject.transform.SetPositionAndRotation(new Vector3(0f, EyeHeight, 0f),
                Quaternion.identity);
            playerObject.AddComponent<Camera>();
            SemanticPedestrianController controller =
                playerObject.AddComponent<SemanticPedestrianController>();
            controller.Configure(map, null);

            var runtimeObject = new GameObject("Pedestrian Probe Runtime");
            SceneManager.MoveGameObjectToScene(runtimeObject, scene);
            PedestrianProbeRuntime runtime =
                runtimeObject.AddComponent<PedestrianProbeRuntime>();
            SignalCatalog liveCatalog = FindCatalog();
            if (liveCatalog == null)
                Debug.LogError($"{layout.Id} was built without a catalog and will be silent.");
            runtime.Configure(map, controller, null, liveCatalog, FindComparisonCatalogs());
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(runtime);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{layout.Id}.unity");
        }
    }
}
