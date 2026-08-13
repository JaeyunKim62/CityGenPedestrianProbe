using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CityGen3D;
using CityGenPedestrianProbe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityGenPedestrianProbe.Editor
{
    internal static class SemanticMapBaker
    {
        private const float MinimumTriangleArea = 0.0025f;

        [MenuItem("Tools/Pedestrian Probe/Bake Semantic Map", priority = 10)]
        public static void BakeSemanticMap() => BakeSemanticMapInternal();

        private static SemanticSpaceMap BakeSemanticMapInternal()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!PedestrianProbeLocationResolver.TryResolve(scene, out
                    PedestrianProbeLocationContext location))
                return null;

            Database database = location.Database;
            Terrain studyTerrain = location.Terrain;
            Vector3 terrainPosition = studyTerrain.transform.position;
            Vector3 terrainSize = studyTerrain.terrainData.size;
            float studySize = Mathf.Min(terrainSize.x, terrainSize.z);
            Rect studyBounds = new Rect(terrainPosition.x, terrainPosition.z,
                terrainSize.x, terrainSize.z);
            Vector2 center = studyBounds.center;
            Vector2 mapWorldOffset = new Vector2(terrainPosition.x, terrainPosition.z);
            Vector3 originLocal = database.origin.GetMapCoord(database.minLocation,
                Vector2.zero).GetPosition();
            Vector2 originWorld = mapWorldOffset + new Vector2(originLocal.x, originLocal.z);
            var features = new List<SemanticFeature>();
            var sourceReport = new Dictionary<SemanticSpaceKind, int>();
            Dictionary<string, int> cityGenComponentCounts = CountCityGenComponents(scene);

            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                SemanticSpaceKind kind = ClassifyMesh(filter.transform);
                string sourceClass = FindCityGenSourceClass(filter.transform);
                if (kind == SemanticSpaceKind.Unknown && string.IsNullOrEmpty(sourceClass))
                    continue;
                Vector2 sourceOffset = UsesMapCoordinates(sourceClass)
                    ? mapWorldOffset
                    : Vector2.zero;
                SemanticFeature feature = BuildFeatureFromMesh(filter, kind, sourceClass,
                    sourceOffset, studyBounds);
                if (feature == null)
                    continue;
                features.Add(feature);
                Increment(sourceReport, kind);
            }

            AddCityGenShapeFallbacks(scene, studyBounds, mapWorldOffset, features, sourceReport);
            AddRoadFallbacks(scene, studyBounds, mapWorldOffset, features, sourceReport);
            AddCrosswalkFallbacks(database, studyBounds, mapWorldOffset, features, sourceReport);
            AddCurbFallbacks(studyBounds, features, sourceReport);

            SemanticSpaceKind[] required =
            {
                SemanticSpaceKind.Sidewalk, SemanticSpaceKind.Curb,
                SemanticSpaceKind.Crosswalk, SemanticSpaceKind.Road
            };
            string summary = string.Join(", ", required.Select(kind =>
                $"{kind}={features.Count(feature => feature.Kind == kind)}"));
            SemanticSpaceKind[] missing = required.Where(kind =>
                features.All(feature => feature.Kind != kind)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError("Semantic bake stopped because required categories are empty: " +
                               string.Join(", ", missing) + ". Candidates: " + summary +
                               ". Confirm CityGen Generate completed and inspect generated object names.");
                return null;
            }

            EnsureFolder("Assets", "Generated");
            EnsureFolder("Assets/Generated", "Semantics");
            SemanticSpaceMap map = AssetDatabase.LoadAssetAtPath<SemanticSpaceMap>(
                location.SemanticMapPath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<SemanticSpaceMap>();
                AssetDatabase.CreateAsset(map, location.SemanticMapPath);
            }
            map.SetData("3", AssetDatabase.AssetPathToGUID(location.DatabasePath),
                database.origin.latitude, database.origin.longitude, originWorld, center,
                studySize, 5f, features, cityGenComponentCounts.Keys,
                cityGenComponentCounts);
            WriteSourceClassStatistics(location.StatisticsPath, map.SourceClassStatistics);
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = map;
            var report = new StringBuilder();
            report.AppendLine($"Semantic map baked for {location.LocationId} to " +
                              $"{location.SemanticMapPath}. {summary}.");
            report.AppendLine($"Database={location.DatabasePath}, " +
                              $"statistics={location.StatisticsPath}.");
            report.AppendLine($"Center=({center.x:F2}, {center.y:F2}), " +
                              $"terrain bounds={studyBounds}, total features={features.Count}.");
            report.AppendLine("CityGen source class statistics:");
            foreach (CityGenSourceClassStatistic statistic in map.SourceClassStatistics)
                report.AppendLine($"- {statistic.SourceClass}: " +
                                  $"components={statistic.ComponentCount}, " +
                                  $"features={statistic.FeatureCount}, " +
                                  $"triangles={statistic.TriangleCount}, " +
                                  $"area={statistic.ProjectedAreaSquareMeters:F1}m2");
            Debug.Log(report.ToString());
            return map;
        }

        [MenuItem("Tools/Pedestrian Probe/Bake && Setup Scene", priority = 0)]
        public static void BakeAndSetupScene()
        {
            SemanticSpaceMap map = BakeSemanticMapInternal();
            if (map == null)
            {
                Debug.LogError("Pedestrian Probe setup was not run because the semantic bake failed. " +
                               "Fix the missing semantic categories shown above and try again.");
                return;
            }
            PedestrianFreeCamSetup.Setup(map);
        }

        public static void BakeAndSetupForAutomation()
        {
            OpenAutomationSceneIfConfigured();
            BakeAndSetupScene();
        }

        public static void BakeForAutomation()
        {
            OpenAutomationSceneIfConfigured();
            BakeSemanticMap();
        }

        private static void OpenAutomationSceneIfConfigured()
        {
            string scenePath = Environment.GetEnvironmentVariable(
                "PEDESTRIAN_PROBE_SCENE");
            if (!string.IsNullOrWhiteSpace(scenePath))
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        private static SemanticSpaceKind ClassifyMesh(Transform transform)
        {
            string path = HierarchyPath(transform).ToLowerInvariant();
            if (ContainsAny(path, "crosswalk", "crossing", "zebra"))
                return SemanticSpaceKind.Crosswalk;
            if (ContainsAny(path, "curb", "kerb"))
                return SemanticSpaceKind.Curb;
            if (ContainsAny(path, "sidewalk", "pavement", "footway"))
                return SemanticSpaceKind.Sidewalk;
            if (!ContainsAny(path, "roadside") && ContainsAny(path, "/roads", "/road ",
                    "/road(", "/highways", "/highway ", "/highway("))
                return SemanticSpaceKind.Road;
            return SemanticSpaceKind.Unknown;
        }

        private static string HierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            while (transform != null)
            {
                names.Push(transform.name);
                transform = transform.parent;
            }
            return "/" + string.Join("/", names);
        }

        private static bool ContainsAny(string value, params string[] candidates) =>
            candidates.Any(value.Contains);

        private static string FindCityGenSourceClass(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
            foreach (Component component in current.GetComponents<Component>())
            {
                if (component == null)
                    continue;
                Type type = component.GetType();
                if (string.Equals(type.Namespace, "CityGen3D", StringComparison.Ordinal))
                    return type.FullName;
            }
            return string.Empty;
        }

        private static bool UsesMapCoordinates(string sourceClass) =>
            !string.IsNullOrEmpty(sourceClass) &&
            (sourceClass.StartsWith("CityGen3D.Map", StringComparison.Ordinal) ||
             string.Equals(sourceClass, "CityGen3D.Highway", StringComparison.Ordinal));

        private static Dictionary<string, int> CountCityGenComponents(Scene scene)
        {
            var counts = new Dictionary<string, int>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;
                Type type = component.GetType();
                if (string.Equals(type.Namespace, "CityGen3D", StringComparison.Ordinal))
                {
                    counts.TryGetValue(type.FullName, out int count);
                    counts[type.FullName] = count + 1;
                }
            }
            return counts;
        }

        private static void WriteSourceClassStatistics(string statisticsPath,
            IEnumerable<CityGenSourceClassStatistic> statistics)
        {
            var csv = new StringBuilder();
            csv.AppendLine("source_class,component_count,feature_count,triangle_count,projected_area_m2");
            foreach (CityGenSourceClassStatistic statistic in statistics)
            {
                csv.Append('"').Append(statistic.SourceClass.Replace("\"", "\"\""))
                    .Append("\",").Append(statistic.ComponentCount)
                    .Append(',').Append(statistic.FeatureCount)
                    .Append(',').Append(statistic.TriangleCount)
                    .Append(',').AppendLine(statistic.ProjectedAreaSquareMeters
                        .ToString("F3", CultureInfo.InvariantCulture));
            }
            File.WriteAllText(statisticsPath, csv.ToString(), new UTF8Encoding(true));
        }

        private static SemanticFeature BuildFeatureFromMesh(MeshFilter filter,
            SemanticSpaceKind kind, string sourceClass, Vector2 sourceOffset, Rect clipBounds)
        {
            Mesh mesh = filter.sharedMesh;
            Vector3[] sourceVertices = mesh.vertices;
            int[] sourceTriangles = mesh.triangles;
            var builder = new Mesh2DBuilder();
            for (int i = 0; i + 2 < sourceTriangles.Length; i += 3)
            {
                Vector3 a3 = filter.transform.TransformPoint(sourceVertices[sourceTriangles[i]]);
                Vector3 b3 = filter.transform.TransformPoint(sourceVertices[sourceTriangles[i + 1]]);
                Vector3 c3 = filter.transform.TransformPoint(sourceVertices[sourceTriangles[i + 2]]);
                var polygon = new List<Vector2>
                {
                    new Vector2(a3.x, a3.z) + sourceOffset,
                    new Vector2(b3.x, b3.z) + sourceOffset,
                    new Vector2(c3.x, c3.z) + sourceOffset
                };
                polygon = ClipToRect(polygon, clipBounds);
                builder.AddPolygon(polygon);
            }
            if (builder.Triangles.Count == 0)
                return null;
            long id = StableId(GlobalObjectId.GetGlobalObjectIdSlow(filter).ToString());
            float confidence = kind == SemanticSpaceKind.Crosswalk ? 0.95f : 1f;
            return builder.CreateFeature(kind, id, confidence, sourceClass);
        }

        private static void AddCityGenShapeFallbacks(Scene scene, Rect bounds, Vector2 mapWorldOffset,
            ICollection<SemanticFeature> features, IDictionary<SemanticSpaceKind, int> report)
        {
            bool needSidewalk = features.All(feature => feature.Kind != SemanticSpaceKind.Sidewalk);
            bool needCurb = features.All(feature => feature.Kind != SemanticSpaceKind.Curb);
            if (!needSidewalk && !needCurb)
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Highway highway in root.GetComponentsInChildren<Highway>(true))
            {
                if (needSidewalk)
                    AddShapeLines(highway.GetSidewalkShapes(), highway.transform, 1.5f,
                        SemanticSpaceKind.Sidewalk, 0.75f, "CityGen3D.Highway", mapWorldOffset, bounds,
                        features, report);
                if (needCurb)
                    AddShapeLines(highway.GetCurbShapes(), highway.transform, 0.2f,
                        SemanticSpaceKind.Curb, 0.75f, "CityGen3D.Highway", mapWorldOffset, bounds,
                        features, report);
            }
        }

        private static void AddShapeLines(IReadOnlyList<ShapeLine> lines, Transform owner,
            float width, SemanticSpaceKind kind, float confidence, string sourceClass,
            Vector2 sourceOffset, Rect bounds,
            ICollection<SemanticFeature> features, IDictionary<SemanticSpaceKind, int> report)
        {
            if (lines == null)
                return;
            for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                ShapeLine line = lines[lineIndex];
                if (line == null || line.positions == null || line.positions.Count < 2)
                    continue;
                var builder = new Mesh2DBuilder();
                for (int i = 0; i + 1 < line.positions.Count; i++)
                {
                    Vector3 a3 = owner.TransformPoint(line.positions[i]);
                    Vector3 b3 = owner.TransformPoint(line.positions[i + 1]);
                    AddSegmentQuad(builder, new Vector2(a3.x, a3.z) + sourceOffset,
                        new Vector2(b3.x, b3.z) + sourceOffset, width, bounds);
                }
                if (builder.Triangles.Count == 0)
                    continue;
                long id = StableId(GlobalObjectId.GetGlobalObjectIdSlow(owner).ToString() +
                                   ":" + kind + ":" + lineIndex);
                features.Add(builder.CreateFeature(kind, id, confidence, sourceClass));
                Increment(report, kind);
            }
        }

        private static void AddRoadFallbacks(Scene scene, Rect bounds, Vector2 mapWorldOffset,
            ICollection<SemanticFeature> features, IDictionary<SemanticSpaceKind, int> report)
        {
            if (features.Any(feature => feature.Kind == SemanticSpaceKind.Road))
                return;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MapRoad road in root.GetComponentsInChildren<MapRoad>(true))
            {
                if (road.layer < 0 || HasTag(road.tags, "tunnel", null))
                    continue;
                List<Vector3> positions = road.GetPositions();
                if (positions == null || positions.Count < 2)
                    continue;
                var builder = new Mesh2DBuilder();
                float width = Mathf.Max(2f, road.GetRoadWidth());
                for (int i = 0; i + 1 < positions.Count; i++)
                {
                    Vector3 a3 = road.transform.TransformPoint(positions[i]);
                    Vector3 b3 = road.transform.TransformPoint(positions[i + 1]);
                    AddSegmentQuad(builder, new Vector2(a3.x, a3.z) + mapWorldOffset,
                        new Vector2(b3.x, b3.z) + mapWorldOffset, width, bounds);
                }
                if (builder.Triangles.Count == 0)
                    continue;
                long id = road.way != null ? unchecked((long)road.way.unique_id) :
                    road.node != null ? unchecked((long)road.node.unique_id) :
                    StableId(GlobalObjectId.GetGlobalObjectIdSlow(road).ToString());
                features.Add(builder.CreateFeature(SemanticSpaceKind.Road, id, 0.85f,
                    "CityGen3D.MapRoad"));
                Increment(report, SemanticSpaceKind.Road);
            }
        }

        private static void AddSegmentQuad(Mesh2DBuilder builder, Vector2 a, Vector2 b,
            float width, Rect bounds)
        {
            Vector2 direction = (b - a).normalized;
            if (direction.sqrMagnitude < 0.5f)
                return;
            Vector2 normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
            builder.AddPolygon(ClipToRect(new List<Vector2>
            {
                a - normal, b - normal, b + normal, a + normal
            }, bounds));
        }

        private static void AddCrosswalkFallbacks(Database database, Rect bounds,
            Vector2 mapWorldOffset,
            ICollection<SemanticFeature> features, IDictionary<SemanticSpaceKind, int> report)
        {
            if (database.mapNodes == null)
            {
                Debug.LogWarning("Crosswalk fallback: database.mapNodes is null.");
                return;
            }
            int tagged = 0;
            int unmarked = 0;
            int outside = 0;
            int duplicate = 0;
            int invalidPolygon = 0;
            int added = 0;
            foreach (OSM_Node node in database.mapNodes)
            {
                if (!HasTag(node.tags, "highway", "crossing"))
                    continue;
                tagged++;
                if (HasTag(node.tags, "crossing", "unmarked"))
                {
                    unmarked++;
                    continue;
                }
                Vector3 world = node.location.GetMapCoord(database.minLocation, Vector2.zero).GetPosition();
                Vector2 center = new Vector2(world.x, world.z) + mapWorldOffset;
                if (!bounds.Contains(center))
                {
                    outside++;
                    continue;
                }
                if (HasCrosswalkNear(features, center, 2f))
                {
                    duplicate++;
                    continue;
                }

                Vector2 tangent = FindRoadTangent(database, node.unique_id);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                var polygon = new List<Vector2>
                {
                    center - tangent * 2f - normal * 5f,
                    center + tangent * 2f - normal * 5f,
                    center + tangent * 2f + normal * 5f,
                    center - tangent * 2f + normal * 5f
                };
                polygon = ClipToRect(polygon, bounds);
                var builder = new Mesh2DBuilder();
                builder.AddPolygon(polygon);
                if (builder.Triangles.Count == 0)
                {
                    invalidPolygon++;
                    continue;
                }
                float confidence = HasTag(node.tags, "crossing:markings", null) ||
                                   HasTag(node.tags, "crossing", "marked") ? 0.85f : 0.65f;
                features.Add(builder.CreateFeature(SemanticSpaceKind.Crosswalk,
                    unchecked((long)node.unique_id), confidence, "CityGen3D.OSM_Node"));
                Increment(report, SemanticSpaceKind.Crosswalk);
                added++;
            }
            Debug.Log($"Crosswalk fallback: OSM nodes={database.mapNodes.Count}, " +
                      $"tagged={tagged}, unmarked={unmarked}, outside={outside}, " +
                      $"duplicate={duplicate}, invalid={invalidPolygon}, added={added}, " +
                      $"bounds={bounds}.");
        }

        private static Vector2 FindRoadTangent(Database database, ulong nodeId)
        {
            if (database.mapWays != null)
            foreach (OSM_Way way in database.mapWays)
            {
                if (way.nodes == null || !way.HasNode(nodeId) || !HasTag(way.tags, "highway", null))
                    continue;
                int index = way.nodes.IndexOf(nodeId);
                int otherIndex = index > 0 ? index - 1 : index + 1;
                if (otherIndex < 0 || otherIndex >= way.nodes.Count)
                    continue;
                OSM_Node node = database.GetNode(nodeId);
                OSM_Node other = database.GetNode(way.nodes[otherIndex]);
                if (node == null || other == null)
                    continue;
                Vector3 a = node.location.GetMapCoord(database.minLocation, Vector2.zero).GetPosition();
                Vector3 b = other.location.GetMapCoord(database.minLocation, Vector2.zero).GetPosition();
                Vector2 direction = new Vector2(b.x - a.x, b.z - a.z).normalized;
                if (direction.sqrMagnitude > 0.5f)
                    return direction;
            }
            return Vector2.right;
        }

        private static bool HasTag(IReadOnlyList<OSM_Tag> tags, string key, string value)
        {
            if (tags == null)
                return false;
            for (int i = 0; i < tags.Count; i++)
                if (string.Equals(tags[i].key, key, StringComparison.OrdinalIgnoreCase) &&
                    (value == null || string.Equals(tags[i].value, value,
                        StringComparison.OrdinalIgnoreCase)))
                    return true;
            return false;
        }

        private static bool HasCrosswalkNear(IEnumerable<SemanticFeature> features,
            Vector2 center, float radius)
        {
            foreach (SemanticFeature feature in features)
            {
                if (feature.Kind != SemanticSpaceKind.Crosswalk)
                    continue;
                if (feature.Bounds.Contains(center))
                    return true;
                Vector2 closest = new Vector2(
                    Mathf.Clamp(center.x, feature.Bounds.xMin, feature.Bounds.xMax),
                    Mathf.Clamp(center.y, feature.Bounds.yMin, feature.Bounds.yMax));
                if ((closest - center).sqrMagnitude <= radius * radius)
                    return true;
            }
            return false;
        }

        private static void AddCurbFallbacks(Rect bounds, ICollection<SemanticFeature> features,
            IDictionary<SemanticSpaceKind, int> report)
        {
            if (features.Any(feature => feature.Kind == SemanticSpaceKind.Curb))
                return;
            SemanticFeature[] sidewalks = features.Where(feature =>
                feature.Kind == SemanticSpaceKind.Sidewalk).ToArray();
            long id = -1;
            foreach (SemanticFeature sidewalk in sidewalks)
            {
                IReadOnlyList<Vector2> vertices = sidewalk.Vertices;
                IReadOnlyList<int> triangles = sidewalk.Triangles;
                var counts = new Dictionary<ulong, int>();
                var edges = new Dictionary<ulong, Vector2Int>();
                for (int i = 0; i + 2 < triangles.Count; i += 3)
                {
                    CountEdge(triangles[i], triangles[i + 1], counts, edges);
                    CountEdge(triangles[i + 1], triangles[i + 2], counts, edges);
                    CountEdge(triangles[i + 2], triangles[i], counts, edges);
                }
                foreach (KeyValuePair<ulong, int> pair in counts)
                {
                    if (pair.Value != 1)
                        continue;
                    Vector2Int edge = edges[pair.Key];
                    Vector2 a = vertices[edge.x];
                    Vector2 b = vertices[edge.y];
                    Vector2 direction = (b - a).normalized;
                    if (direction.sqrMagnitude < 0.5f)
                        continue;
                    Vector2 normal = new Vector2(-direction.y, direction.x) * 0.1f;
                    var polygon = ClipToRect(new List<Vector2>
                    {
                        a - normal, b - normal, b + normal, a + normal
                    }, bounds);
                    var builder = new Mesh2DBuilder();
                    builder.AddPolygon(polygon);
                    if (builder.Triangles.Count == 0)
                        continue;
                    string sourceClass = string.IsNullOrEmpty(sidewalk.SourceClass)
                        ? "CityGen3D.Highway"
                        : sidewalk.SourceClass;
                    features.Add(builder.CreateFeature(SemanticSpaceKind.Curb, id--, 0.55f,
                        sourceClass));
                    Increment(report, SemanticSpaceKind.Curb);
                }
            }
        }

        private static void CountEdge(int a, int b, IDictionary<ulong, int> counts,
            IDictionary<ulong, Vector2Int> edges)
        {
            uint min = (uint)Mathf.Min(a, b);
            uint max = (uint)Mathf.Max(a, b);
            ulong key = ((ulong)min << 32) | max;
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
            if (count == 0)
                edges[key] = new Vector2Int(a, b);
        }

        private static List<Vector2> ClipToRect(List<Vector2> polygon, Rect bounds)
        {
            polygon = ClipEdge(polygon, p => p.x >= bounds.xMin,
                (a, b) => IntersectX(a, b, bounds.xMin));
            polygon = ClipEdge(polygon, p => p.x <= bounds.xMax,
                (a, b) => IntersectX(a, b, bounds.xMax));
            polygon = ClipEdge(polygon, p => p.y >= bounds.yMin,
                (a, b) => IntersectY(a, b, bounds.yMin));
            return ClipEdge(polygon, p => p.y <= bounds.yMax,
                (a, b) => IntersectY(a, b, bounds.yMax));
        }

        private static List<Vector2> ClipEdge(List<Vector2> input, Func<Vector2, bool> inside,
            Func<Vector2, Vector2, Vector2> intersection)
        {
            var output = new List<Vector2>();
            if (input.Count == 0)
                return output;
            Vector2 previous = input[input.Count - 1];
            bool previousInside = inside(previous);
            foreach (Vector2 current in input)
            {
                bool currentInside = inside(current);
                if (currentInside != previousInside)
                    output.Add(intersection(previous, current));
                if (currentInside)
                    output.Add(current);
                previous = current;
                previousInside = currentInside;
            }
            return output;
        }

        private static Vector2 IntersectX(Vector2 a, Vector2 b, float x)
        {
            float t = Mathf.Abs(b.x - a.x) < 0.000001f ? 0f : (x - a.x) / (b.x - a.x);
            return new Vector2(x, Mathf.Lerp(a.y, b.y, t));
        }

        private static Vector2 IntersectY(Vector2 a, Vector2 b, float y)
        {
            float t = Mathf.Abs(b.y - a.y) < 0.000001f ? 0f : (y - a.y) / (b.y - a.y);
            return new Vector2(Mathf.Lerp(a.x, b.x, t), y);
        }

        private static long StableId(string text)
        {
            unchecked
            {
                const long offset = 1469598103934665603L;
                const long prime = 1099511628211L;
                long hash = offset;
                foreach (char character in text)
                    hash = (hash ^ character) * prime;
                return hash;
            }
        }

        private static void Increment(IDictionary<SemanticSpaceKind, int> counts,
            SemanticSpaceKind kind)
        {
            counts.TryGetValue(kind, out int count);
            counts[kind] = count + 1;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private sealed class Mesh2DBuilder
        {
            private readonly List<Vector2> vertices = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            private readonly Dictionary<Vector2Int, int> welded = new Dictionary<Vector2Int, int>();
            public IReadOnlyList<int> Triangles => triangles;

            public void AddPolygon(IReadOnlyList<Vector2> polygon)
            {
                if (polygon == null || polygon.Count < 3)
                    return;
                for (int pointIndex = 0; pointIndex < polygon.Count; pointIndex++)
                {
                    Vector2 point = polygon[pointIndex];
                    if (float.IsNaN(point.x) || float.IsNaN(point.y) ||
                        float.IsInfinity(point.x) || float.IsInfinity(point.y))
                        return;
                }
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    Vector2 a = polygon[0];
                    Vector2 b = polygon[i];
                    Vector2 c = polygon[i + 1];
                    float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) -
                                           (b.y - a.y) * (c.x - a.x)) * 0.5f;
                    if (area < MinimumTriangleArea)
                        continue;
                    triangles.Add(AddVertex(a));
                    triangles.Add(AddVertex(b));
                    triangles.Add(AddVertex(c));
                }
            }

            public SemanticFeature CreateFeature(SemanticSpaceKind kind, long id, float confidence,
                string sourceClass = "") => new SemanticFeature(kind, id, confidence,
                vertices.ToArray(), triangles.ToArray(), sourceClass);

            private int AddVertex(Vector2 vertex)
            {
                var key = new Vector2Int(Mathf.RoundToInt(vertex.x * 1000f),
                    Mathf.RoundToInt(vertex.y * 1000f));
                if (welded.TryGetValue(key, out int index))
                    return index;
                index = vertices.Count;
                vertices.Add(vertex);
                welded.Add(key, index);
                return index;
            }
        }
    }
}
