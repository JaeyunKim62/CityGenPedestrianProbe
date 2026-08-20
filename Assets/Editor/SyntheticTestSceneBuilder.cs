using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityGenPedestrianProbe.Editor
{
    internal static class SyntheticTestSceneBuilder
    {
        private const string MapFolder = "Assets/Generated/SyntheticMaps";
        private const string SceneFolder = "Assets/Scenes/Synthetic";
        private const string CatalogPath =
            "Assets/Generated/Signals/SignalCatalog_C002_DirectionRange.asset";
        private const float EyeHeight = 1.7f;

        [MenuItem("Tools/Pedestrian Probe/Build Synthetic Test Scenes", priority = 40)]
        private static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(MapFolder);
            Directory.CreateDirectory(SceneFolder);
            var catalog = AssetDatabase.LoadAssetAtPath<SignalCatalog>(CatalogPath);
            if (catalog == null)
                Debug.LogWarning($"Signal catalog not found at {CatalogPath}. " +
                                 "Synthetic scenes will be silent until one is assigned.");

            foreach (SyntheticLayout layout in SyntheticTestMaps.Layouts)
                BuildOne(layout, catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Built {SyntheticTestMaps.Layouts.Length} synthetic test scenes in " +
                      $"{SceneFolder}. Open one, press Play, then G to silence semantic.audio.");
        }

        private static void BuildOne(SyntheticLayout layout, SignalCatalog catalog)
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
            runtime.Configure(map, controller, null, catalog);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{layout.Id}.unity");
        }
    }
}
