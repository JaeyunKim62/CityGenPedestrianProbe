using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CityGenPedestrianProbe;

namespace CityGenPedestrianProbe.Editor
{
    internal static class PedestrianFreeCamSetup
    {
        private const string PrefabPath =
            "Assets/CityGen3D/Resources/Prefabs/URP/FreeCam URP.prefab";
        private const string CameraName = "Pedestrian FreeCam";

        [MenuItem("Tools/Pedestrian Probe/Setup Scene", priority = 20)]
        public static void SetupFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!PedestrianProbeLocationResolver.TryResolve(scene, out
                    PedestrianProbeLocationContext location))
                return;

            SemanticSpaceMap map = AssetDatabase.LoadAssetAtPath<SemanticSpaceMap>(
                location.SemanticMapPath);
            if (map == null)
            {
                Debug.LogError($"Semantic map is missing for {location.LocationId}: " +
                               $"{location.SemanticMapPath}. Run Tools > Pedestrian Probe > " +
                               "Bake Semantic Map first.");
                return;
            }
            Setup(map, location);
        }

        internal static void Setup(SemanticSpaceMap map)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!PedestrianProbeLocationResolver.TryResolve(scene, out
                    PedestrianProbeLocationContext location))
                return;
            Setup(map, location);
        }

        private static void Setup(SemanticSpaceMap map,
            PedestrianProbeLocationContext location)
        {
            Scene scene = location.Scene;
            string expectedDatabaseId = AssetDatabase.AssetPathToGUID(location.DatabasePath);
            if (!string.Equals(map.SourceDatabaseId, expectedDatabaseId,
                    System.StringComparison.Ordinal))
            {
                Debug.LogError("The selected semantic map belongs to a different CityGen " +
                               "Database. Bake the current scene again.");
                return;
            }

            GameObject cameraObject = FindInScene(scene, CameraName);
            if (cameraObject == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"FreeCam prefab not found: {PrefabPath}");
                    return;
                }
                cameraObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                cameraObject.name = CameraName;
            }

            CityGen3D.FreeCam oldController = cameraObject.GetComponent<CityGen3D.FreeCam>();
            if (oldController != null)
                oldController.enabled = false;
            CityGen3D.Screenshot screenshot = cameraObject.GetComponent<CityGen3D.Screenshot>();
            if (screenshot != null)
            {
                screenshot.enabled = false;
                EditorUtility.SetDirty(screenshot);
            }
            SemanticPedestrianController controller =
                cameraObject.GetComponent<SemanticPedestrianController>();
            if (controller == null)
                controller = Undo.AddComponent<SemanticPedestrianController>(cameraObject);

            Terrain terrain = location.Terrain;
            controller.Configure(map, terrain);
            cameraObject.transform.SetPositionAndRotation(FindSpawn(map, terrain),
                Quaternion.Euler(0f, 45f, 0f));
            DisableOtherCameras(scene, cameraObject);

            GameObject runtimeObject = FindInScene(scene, "Pedestrian Probe Runtime");
            if (runtimeObject == null)
            {
                runtimeObject = new GameObject("Pedestrian Probe Runtime");
                SceneManager.MoveGameObjectToScene(runtimeObject, scene);
                Undo.RegisterCreatedObjectUndo(runtimeObject, "Create Pedestrian Probe Runtime");
            }
            PedestrianProbeRuntime runtime = runtimeObject.GetComponent<PedestrianProbeRuntime>();
            if (runtime == null)
                runtime = Undo.AddComponent<PedestrianProbeRuntime>(runtimeObject);
            runtime.Configure(map, controller, terrain);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(runtime);
            EditorUtility.SetDirty(cameraObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = cameraObject;
            Debug.Log($"Pedestrian Probe scene setup complete for {location.LocationId}. " +
                      "Play starts blind; B or F1 toggles debug view in Editor.");
        }

        private static Vector3 FindSpawn(SemanticSpaceMap map, Terrain terrain)
        {
            Vector3 position = new Vector3(map.StudyCenterXZ.x, 0f, map.StudyCenterXZ.y);
            foreach (SemanticFeature feature in map.Features)
            {
                if (feature.Kind != SemanticSpaceKind.Sidewalk ||
                    feature.Vertices.Count == 0 || feature.Triangles.Count < 3)
                    continue;
                int ia = feature.Triangles[0];
                int ib = feature.Triangles[1];
                int ic = feature.Triangles[2];
                if (ia < 0 || ib < 0 || ic < 0 || ia >= feature.Vertices.Count ||
                    ib >= feature.Vertices.Count || ic >= feature.Vertices.Count)
                    continue;
                Vector2 interior = (feature.Vertices[ia] + feature.Vertices[ib] +
                                    feature.Vertices[ic]) / 3f;
                position.x = interior.x;
                position.z = interior.y;
                break;
            }
            if (terrain != null)
                position.y = terrain.SampleHeight(position) + terrain.transform.position.y + 1.7f;
            else
                position.y = 1.7f;
            return position;
        }

        private static GameObject FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName)
                    return candidate.gameObject;
            return null;
        }

        private static void DisableOtherCameras(Scene scene, GameObject player)
        {
            Camera playerCamera = player.GetComponentInChildren<Camera>(true);
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
                AudioListener ownListener = playerCamera.GetComponent<AudioListener>();
                if (ownListener != null)
                    ownListener.enabled = true;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (camera == playerCamera)
                    continue;
                camera.enabled = false;
                AudioListener listener = camera.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = false;
                EditorUtility.SetDirty(camera);
            }
        }
    }
}
