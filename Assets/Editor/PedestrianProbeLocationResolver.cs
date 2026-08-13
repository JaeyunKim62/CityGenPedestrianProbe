using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CityGen3D;
using CityGenPedestrianProbe;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityGenPedestrianProbe.Editor
{
    internal sealed class PedestrianProbeLocationContext
    {
        public Scene Scene { get; }
        public Database Database { get; }
        public string DatabasePath { get; }
        public string LocationId { get; }
        public Terrain Terrain { get; }
        public string SemanticMapPath { get; }
        public string StatisticsPath { get; }

        public PedestrianProbeLocationContext(Scene scene, Database database,
            string databasePath, string locationId, Terrain terrain,
            string semanticMapPath, string statisticsPath)
        {
            Scene = scene;
            Database = database;
            DatabasePath = databasePath;
            LocationId = locationId;
            Terrain = terrain;
            SemanticMapPath = semanticMapPath;
            StatisticsPath = statisticsPath;
        }
    }

    internal static class PedestrianProbeLocationResolver
    {
        public const string OutputFolder = "Assets/Generated/Semantics";

        [MenuItem("Tools/Pedestrian Probe/Show Current Location", priority = 30)]
        private static void ShowCurrentLocation()
        {
            if (!TryResolve(SceneManager.GetActiveScene(), out
                    PedestrianProbeLocationContext location))
                return;
            Vector3 size = location.Terrain.terrainData.size;
            Debug.Log($"Pedestrian Probe location: {location.LocationId}\n" +
                      $"Scene={location.Scene.path}\n" +
                      $"Database={location.DatabasePath}\n" +
                      $"Origin=({location.Database.origin.latitude:F8}, " +
                      $"{location.Database.origin.longitude:F8})\n" +
                      $"Terrain={size.x:F0}m x {size.z:F0}m at " +
                      $"{location.Terrain.transform.position}\n" +
                      $"Semantic map={location.SemanticMapPath}\n" +
                      $"Statistics={location.StatisticsPath}");
        }

        public static bool TryResolve(Scene scene,
            out PedestrianProbeLocationContext context, bool logErrors = true)
        {
            context = null;
            if (!scene.IsValid() || !scene.isLoaded)
                return Fail("Open a generated CityGen scene first.", logErrors);

            Database database = FindSceneDatabase(scene, out string databaseError);
            if (database == null)
                return Fail(databaseError, logErrors);

            string databasePath = AssetDatabase.GetAssetPath(database);
            if (string.IsNullOrEmpty(databasePath))
                return Fail("The current CityGen Database is not a saved project asset.",
                    logErrors);

            Terrain terrain = FindStudyTerrain(scene);
            if (terrain == null || terrain.terrainData == null)
                return Fail("The current scene needs an active Terrain with TerrainData.",
                    logErrors);

            string locationId = SanitizeLocationId(
                string.IsNullOrWhiteSpace(database.filename) ? scene.name : database.filename);
            int size = Mathf.RoundToInt(Mathf.Min(terrain.terrainData.size.x,
                terrain.terrainData.size.z));
            string semanticMapPath = FindExistingSemanticMap(databasePath, locationId) ??
                                     $"{OutputFolder}/{locationId}_{size}m.asset";
            string statisticsPath = $"{OutputFolder}/" +
                                    Path.GetFileNameWithoutExtension(semanticMapPath) +
                                    "_SourceClassStatistics.csv";
            context = new PedestrianProbeLocationContext(scene, database, databasePath,
                locationId, terrain, semanticMapPath, statisticsPath);
            return true;
        }

        internal static string SanitizeLocationId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "location";
            var result = new StringBuilder(value.Length);
            bool previousSeparator = false;
            foreach (char character in value.Trim())
            {
                bool valid = char.IsLetterOrDigit(character) || character == '-' ||
                             character == '_';
                if (valid)
                {
                    result.Append(character);
                    previousSeparator = false;
                }
                else if (!previousSeparator)
                {
                    result.Append('_');
                    previousSeparator = true;
                }
            }
            string locationId = result.ToString().Trim('_');
            return string.IsNullOrEmpty(locationId) ? "location" : locationId;
        }

        public static Terrain FindStudyTerrain(Scene scene)
        {
            Terrain best = null;
            float bestArea = -1f;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                if (!terrain.gameObject.activeInHierarchy || terrain.terrainData == null)
                    continue;
                Vector3 size = terrain.terrainData.size;
                float area = size.x * size.z;
                if (area <= bestArea)
                    continue;
                best = terrain;
                bestArea = area;
            }
            return best;
        }

        private static Database FindSceneDatabase(Scene scene, out string error)
        {
            var databases = new HashSet<Database>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (CityGen3D.Data data in root.GetComponentsInChildren<CityGen3D.Data>(true))
                if (data != null && data.db != null)
                    databases.Add(data.db);

            if (databases.Count == 1)
            {
                error = string.Empty;
                return databases.First();
            }
            if (databases.Count > 1)
            {
                error = "The scene references more than one CityGen Database. " +
                        "Keep one CityGen3D.Data location per probe scene.";
                return null;
            }

            Database fallback = FindDatabaseBySceneName(scene.name, out bool ambiguous);
            if (fallback != null)
            {
                error = string.Empty;
                Debug.LogWarning("Pedestrian Probe found the Database by matching the scene " +
                                 "name because CityGen3D.Data.db was empty. Assign Data.db to " +
                                 "make the location link explicit.");
                return fallback;
            }
            error = ambiguous
                ? "More than one CityGen Database matches the scene name. Assign the intended " +
                  "asset to CityGen3D.Data.db."
                : "No CityGen Database is assigned to this scene. Download or load the location " +
                  "in Tools > CityGen3D > Data first.";
            return null;
        }

        private static Database FindDatabaseBySceneName(string sceneName, out bool ambiguous)
        {
            ambiguous = false;
            string sceneKey = ComparableName(sceneName);
            if (string.IsNullOrEmpty(sceneKey))
                return null;
            var matches = new List<Database>();
            foreach (string guid in AssetDatabase.FindAssets("t:Database",
                         new[] { "Assets/Database" }))
            {
                Database candidate = AssetDatabase.LoadAssetAtPath<Database>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (candidate == null)
                    continue;
                string candidateKey = ComparableName(candidate.filename);
                if (candidateKey == sceneKey || candidateKey.Contains(sceneKey) ||
                    sceneKey.Contains(candidateKey))
                    matches.Add(candidate);
            }
            ambiguous = matches.Count > 1;
            return matches.Count == 1 ? matches[0] : null;
        }

        private static string FindExistingSemanticMap(string databasePath, string locationId)
        {
            string databaseId = AssetDatabase.AssetPathToGUID(databasePath);
            var matches = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:SemanticSpaceMap",
                         new[] { OutputFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SemanticSpaceMap map = AssetDatabase.LoadAssetAtPath<SemanticSpaceMap>(path);
                if (map != null && string.Equals(map.SourceDatabaseId, databaseId,
                        StringComparison.Ordinal))
                    matches.Add(path);
            }
            if (matches.Count == 0)
                return null;
            string preferred = matches.FirstOrDefault(path => ComparableName(
                Path.GetFileNameWithoutExtension(path)).Contains(ComparableName(locationId)));
            return preferred ?? matches.OrderBy(path => path, StringComparer.Ordinal).First();
        }

        private static string ComparableName(string value) =>
            new string((value ?? string.Empty).Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());

        private static bool Fail(string message, bool logErrors)
        {
            if (logErrors)
                Debug.LogError(message);
            return false;
        }
    }
}
