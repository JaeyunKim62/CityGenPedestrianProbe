using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    public enum SemanticSpaceKind
    {
        Unknown,
        Sidewalk,
        Curb,
        Crosswalk,
        Road
    }

    [Flags]
    public enum SemanticSpaceMask
    {
        None = 0,
        Sidewalk = 1 << 0,
        Curb = 1 << 1,
        Crosswalk = 1 << 2,
        Road = 1 << 3,
        All = Sidewalk | Curb | Crosswalk | Road
    }

    public readonly struct SemanticHit
    {
        public readonly SemanticSpaceKind Kind;
        public readonly long FeatureId;
        public readonly float Confidence;
        public readonly bool Contains;
        public readonly float DistanceMeters;
        public readonly Vector3 NearestPointWorld;
        public readonly string SourceClass;

        public bool HasClassification => Kind != SemanticSpaceKind.Unknown ||
                                         !string.IsNullOrEmpty(SourceClass);
        public string DisplayClass
        {
            get
            {
                if (string.IsNullOrEmpty(SourceClass))
                    return Kind.ToString();
                const string prefix = "CityGen3D.";
                return SourceClass.StartsWith(prefix, StringComparison.Ordinal)
                    ? SourceClass.Substring(prefix.Length)
                    : SourceClass;
            }
        }

        public SemanticHit(SemanticSpaceKind kind, long featureId, float confidence,
            bool contains, float distanceMeters, Vector3 nearestPointWorld,
            string sourceClass = "")
        {
            Kind = kind;
            FeatureId = featureId;
            Confidence = confidence;
            Contains = contains;
            DistanceMeters = distanceMeters;
            NearestPointWorld = nearestPointWorld;
            SourceClass = sourceClass ?? string.Empty;
        }

        public static SemanticHit Unknown(Vector3 position) =>
            new SemanticHit(SemanticSpaceKind.Unknown, 0, 0f, false,
                float.PositiveInfinity, position);
    }

    [Serializable]
    public sealed class SemanticFeature
    {
        [SerializeField] private SemanticSpaceKind kind;
        [SerializeField] private long featureId;
        [SerializeField] private string sourceClass;
        [SerializeField, Range(0f, 1f)] private float confidence = 1f;
        [SerializeField] private Vector2[] vertices = Array.Empty<Vector2>();
        [SerializeField] private int[] triangles = Array.Empty<int>();
        [SerializeField] private int[] boundaryEdges = Array.Empty<int>();
        [SerializeField] private Rect bounds;

        public SemanticSpaceKind Kind => kind;
        public long FeatureId => featureId;
        public string SourceClass => sourceClass ?? string.Empty;
        public float Confidence => confidence;
        public IReadOnlyList<Vector2> Vertices => vertices;
        public IReadOnlyList<int> Triangles => triangles;
        public IReadOnlyList<int> BoundaryEdges => boundaryEdges;
        public Rect Bounds => bounds;
        public int TriangleCount => triangles == null ? 0 : triangles.Length / 3;
        public float ProjectedAreaSquareMeters
        {
            get
            {
                float area = 0f;
                if (triangles == null)
                    return area;
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int ia = triangles[i];
                    int ib = triangles[i + 1];
                    int ic = triangles[i + 2];
                    if (!ValidVertex(ia) || !ValidVertex(ib) || !ValidVertex(ic))
                        continue;
                    area += Mathf.Abs(Cross(vertices[ib] - vertices[ia],
                        vertices[ic] - vertices[ia])) * 0.5f;
                }
                return area;
            }
        }

        public SemanticFeature(SemanticSpaceKind kind, long featureId, float confidence,
            Vector2[] vertices, int[] triangles, string sourceClass = "")
        {
            this.kind = kind;
            this.featureId = featureId;
            this.sourceClass = sourceClass ?? string.Empty;
            this.confidence = Mathf.Clamp01(confidence);
            this.vertices = vertices ?? Array.Empty<Vector2>();
            this.triangles = triangles ?? Array.Empty<int>();
            RebuildDerivedData();
        }

        public void RebuildDerivedData()
        {
            if (vertices == null || vertices.Length == 0)
            {
                vertices = Array.Empty<Vector2>();
                triangles = Array.Empty<int>();
                boundaryEdges = Array.Empty<int>();
                bounds = new Rect();
                return;
            }

            float minX = vertices[0].x;
            float maxX = minX;
            float minY = vertices[0].y;
            float maxY = minY;
            for (int i = 1; i < vertices.Length; i++)
            {
                minX = Mathf.Min(minX, vertices[i].x);
                maxX = Mathf.Max(maxX, vertices[i].x);
                minY = Mathf.Min(minY, vertices[i].y);
                maxY = Mathf.Max(maxY, vertices[i].y);
            }

            bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            boundaryEdges = BuildBoundaryEdges(triangles);
        }

        public bool Contains(Vector2 point)
        {
            if (!bounds.Contains(point) || triangles == null)
                return false;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int ia = triangles[i];
                int ib = triangles[i + 1];
                int ic = triangles[i + 2];
                if (!ValidVertex(ia) || !ValidVertex(ib) || !ValidVertex(ic))
                    continue;
                if (PointInTriangle(point, vertices[ia], vertices[ib], vertices[ic]))
                    return true;
            }
            return false;
        }

        public float NearestBoundary(Vector2 point, out Vector2 nearest)
        {
            nearest = point;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i + 1 < boundaryEdges.Length; i += 2)
            {
                int ia = boundaryEdges[i];
                int ib = boundaryEdges[i + 1];
                if (!ValidVertex(ia) || !ValidVertex(ib))
                    continue;
                Vector2 candidate = ClosestPointOnSegment(point, vertices[ia], vertices[ib]);
                float sqr = (candidate - point).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = candidate;
                }
            }
            return Mathf.Sqrt(bestSqr);
        }

        private bool ValidVertex(int index) => index >= 0 && index < vertices.Length;

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p - b, a - b);
            float d2 = Cross(p - c, b - c);
            float d3 = Cross(p - a, c - a);
            bool hasNegative = d1 < -0.0001f || d2 < -0.0001f || d3 < -0.0001f;
            bool hasPositive = d1 > 0.0001f || d2 > 0.0001f || d3 > 0.0001f;
            return !(hasNegative && hasPositive);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denominator = ab.sqrMagnitude;
            if (denominator < 0.000001f)
                return a;
            return a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / denominator);
        }

        private static int[] BuildBoundaryEdges(int[] triangleIndices)
        {
            if (triangleIndices == null)
                return Array.Empty<int>();

            var counts = new Dictionary<ulong, int>();
            var oriented = new Dictionary<ulong, Vector2Int>();
            for (int i = 0; i + 2 < triangleIndices.Length; i += 3)
            {
                AddEdge(triangleIndices[i], triangleIndices[i + 1], counts, oriented);
                AddEdge(triangleIndices[i + 1], triangleIndices[i + 2], counts, oriented);
                AddEdge(triangleIndices[i + 2], triangleIndices[i], counts, oriented);
            }

            var result = new List<int>();
            foreach (KeyValuePair<ulong, int> pair in counts)
            {
                if (pair.Value != 1)
                    continue;
                Vector2Int edge = oriented[pair.Key];
                result.Add(edge.x);
                result.Add(edge.y);
            }
            return result.ToArray();
        }

        private static void AddEdge(int a, int b, IDictionary<ulong, int> counts,
            IDictionary<ulong, Vector2Int> oriented)
        {
            uint min = (uint)Mathf.Min(a, b);
            uint max = (uint)Mathf.Max(a, b);
            ulong key = ((ulong)min << 32) | max;
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
            if (count == 0)
                oriented[key] = new Vector2Int(a, b);
        }
    }

    [Serializable]
    public sealed class CityGenSourceClassStatistic
    {
        [SerializeField] private string sourceClass;
        [SerializeField] private int componentCount;
        [SerializeField] private int featureCount;
        [SerializeField] private int triangleCount;
        [SerializeField] private float projectedAreaSquareMeters;

        public string SourceClass => sourceClass ?? string.Empty;
        public int ComponentCount => componentCount;
        public int FeatureCount => featureCount;
        public int TriangleCount => triangleCount;
        public float ProjectedAreaSquareMeters => projectedAreaSquareMeters;

        public CityGenSourceClassStatistic(string sourceClass, int componentCount,
            int featureCount, int triangleCount, float projectedAreaSquareMeters)
        {
            this.sourceClass = sourceClass ?? string.Empty;
            this.componentCount = Mathf.Max(0, componentCount);
            this.featureCount = Mathf.Max(0, featureCount);
            this.triangleCount = Mathf.Max(0, triangleCount);
            this.projectedAreaSquareMeters = Mathf.Max(0f, projectedAreaSquareMeters);
        }
    }

    [CreateAssetMenu(menuName = "Pedestrian Probe/Semantic Space Map")]
    public sealed class SemanticSpaceMap : ScriptableObject
    {
        [SerializeField] private string bakeVersion = "1";
        [SerializeField] private string sourceDatabaseId;
        [SerializeField] private double originLatitude;
        [SerializeField] private double originLongitude;
        [SerializeField] private Vector2 worldOriginXZ;
        [SerializeField] private Vector2 studyCenterXZ;
        [SerializeField] private float studySizeMeters = 256f;
        [SerializeField] private float gridCellMeters = 5f;
        [SerializeField] private List<SemanticFeature> features = new List<SemanticFeature>();
        [SerializeField] private List<string> sourceClasses = new List<string>();
        [SerializeField] private List<CityGenSourceClassStatistic> sourceClassStatistics =
            new List<CityGenSourceClassStatistic>();

        private readonly Dictionary<Vector2Int, List<int>> grid =
            new Dictionary<Vector2Int, List<int>>();
        private bool gridReady;
        private readonly List<int> rayCandidates = new List<int>();
        private readonly List<SemanticHit> queryBuffer = new List<SemanticHit>();
        private readonly HashSet<int> candidateBuffer = new HashSet<int>();

        public string BakeVersion => bakeVersion;
        public string SourceDatabaseId => sourceDatabaseId;
        public Vector2 StudyCenterXZ => studyCenterXZ;
        public float StudySizeMeters => studySizeMeters;
        public Rect StudyBounds => new Rect(studyCenterXZ - Vector2.one * studySizeMeters * 0.5f,
            Vector2.one * studySizeMeters);
        public IReadOnlyList<SemanticFeature> Features => features;
        public IReadOnlyList<string> SourceClasses => sourceClasses;
        public IReadOnlyList<CityGenSourceClassStatistic> SourceClassStatistics =>
            sourceClassStatistics;

        private void OnEnable() => gridReady = false;

        public void SetData(string version, string databaseId, double latitude, double longitude,
            Vector2 worldOrigin, Vector2 center, float size, float cellSize,
            List<SemanticFeature> bakedFeatures, IEnumerable<string> cityGenClasses = null,
            IReadOnlyDictionary<string, int> componentCounts = null)
        {
            bakeVersion = version;
            sourceDatabaseId = databaseId;
            originLatitude = latitude;
            originLongitude = longitude;
            worldOriginXZ = worldOrigin;
            studyCenterXZ = center;
            studySizeMeters = size;
            gridCellMeters = Mathf.Max(1f, cellSize);
            features = bakedFeatures ?? new List<SemanticFeature>();
            sourceClasses = (cityGenClasses ?? Enumerable.Empty<string>())
                .Concat(features.Select(feature => feature.SourceClass))
                .Where(sourceClass => !string.IsNullOrEmpty(sourceClass))
                .Distinct()
                .OrderBy(sourceClass => sourceClass)
                .ToList();
            foreach (SemanticFeature feature in features)
                feature.RebuildDerivedData();
            sourceClassStatistics = BuildSourceClassStatistics(componentCounts);
            gridReady = false;
        }

        private List<CityGenSourceClassStatistic> BuildSourceClassStatistics(
            IReadOnlyDictionary<string, int> componentCounts)
        {
            var result = new List<CityGenSourceClassStatistic>();
            foreach (string sourceClass in sourceClasses)
            {
                SemanticFeature[] classFeatures = features.Where(feature =>
                    string.Equals(feature.SourceClass, sourceClass,
                        StringComparison.Ordinal)).ToArray();
                int components = 0;
                componentCounts?.TryGetValue(sourceClass, out components);
                result.Add(new CityGenSourceClassStatistic(sourceClass, components,
                    classFeatures.Length, classFeatures.Sum(feature => feature.TriangleCount),
                    classFeatures.Sum(feature => feature.ProjectedAreaSquareMeters)));
            }
            return result;
        }

        public SemanticHit ClassifyWorld(Vector3 position)
        {
            EnsureGrid();
            Vector2 point = new Vector2(position.x, position.z);
            CollectCandidates(point, 0f);
            SemanticFeature winner = null;
            foreach (int index in candidateBuffer)
            {
                SemanticFeature feature = features[index];
                if (!feature.Contains(point))
                    continue;
                if (winner == null || ClassificationPriority(feature.Kind) >
                    ClassificationPriority(winner.Kind))
                    winner = feature;
            }

            if (winner == null)
                return SemanticHit.Unknown(position);
            float distance = winner.NearestBoundary(point, out Vector2 nearest);
            return new SemanticHit(winner.Kind, winner.FeatureId, winner.Confidence, true,
                distance, new Vector3(nearest.x, position.y, nearest.y), winner.SourceClass);
        }

        public SemanticHit ClassifyGeo(double latitude, double longitude)
        {
            Vector3 world = GeoToWorld(latitude, longitude);
            return ClassifyWorld(world);
        }

        public IReadOnlyList<SemanticHit> QueryNearbyWorld(Vector3 position, float radiusMeters)
        {
            EnsureGrid();
            queryBuffer.Clear();
            Vector2 point = new Vector2(position.x, position.z);
            float radius = Mathf.Max(0f, radiusMeters);
            CollectCandidates(point, radius);
            foreach (int index in candidateBuffer)
            {
                SemanticFeature feature = features[index];
                bool contains = feature.Contains(point);
                float distance = feature.NearestBoundary(point, out Vector2 nearest);
                if (!contains && distance > radius)
                    continue;
                queryBuffer.Add(new SemanticHit(feature.Kind, feature.FeatureId,
                    feature.Confidence, contains, distance,
                    new Vector3(nearest.x, position.y, nearest.y), feature.SourceClass));
            }
            queryBuffer.Sort((a, b) => a.DistanceMeters.CompareTo(b.DistanceMeters));
            return queryBuffer;
        }

        public Vector3 GeoToWorld(double latitude, double longitude)
        {
            double latitudeRadians = originLatitude * Math.PI / 180.0;
            double metersLatitude = 111132.92 - 559.82 * Math.Cos(2 * latitudeRadians) +
                                    1.175 * Math.Cos(4 * latitudeRadians) -
                                    0.0023 * Math.Cos(6 * latitudeRadians);
            double metersLongitude = 111412.84 * Math.Cos(latitudeRadians) -
                                     93.5 * Math.Cos(3 * latitudeRadians) +
                                     0.118 * Math.Cos(5 * latitudeRadians);
            return new Vector3(worldOriginXZ.x + (float)((longitude - originLongitude) * metersLongitude),
                0f, worldOriginXZ.y + (float)((latitude - originLatitude) * metersLatitude));
        }

        public void WorldToGeo(Vector3 world, out double latitude, out double longitude)
        {
            double latitudeRadians = originLatitude * Math.PI / 180.0;
            double metersLatitude = 111132.92 - 559.82 * Math.Cos(2 * latitudeRadians) +
                                    1.175 * Math.Cos(4 * latitudeRadians) -
                                    0.0023 * Math.Cos(6 * latitudeRadians);
            double metersLongitude = 111412.84 * Math.Cos(latitudeRadians) -
                                     93.5 * Math.Cos(3 * latitudeRadians) +
                                     0.118 * Math.Cos(5 * latitudeRadians);
            latitude = originLatitude + (world.z - worldOriginXZ.y) / metersLatitude;
            longitude = originLongitude + (world.x - worldOriginXZ.x) / metersLongitude;
        }

        public void CollectBoundaryCrossings(Vector2 origin, Vector2 direction,
            float maxDistanceMeters, List<float> distances)
        {
            distances.Clear();
            if (distances == null || maxDistanceMeters <= 0f)
                return;
            Vector2 ray = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
            if (ray.sqrMagnitude < 0.5f)
                return;

            EnsureGrid();
            Vector2 far = origin + ray * maxDistanceMeters;
            rayCandidates.Clear();
            Vector2Int min = Cell(Vector2.Min(origin, far));
            Vector2Int max = Cell(Vector2.Max(origin, far));
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                if (!grid.TryGetValue(new Vector2Int(x, y), out List<int> indices))
                    continue;
                foreach (int index in indices)
                {
                    if (!rayCandidates.Contains(index))
                        rayCandidates.Add(index);
                }
            }

            for (int c = 0; c < rayCandidates.Count; c++)
            {
                SemanticFeature feature = features[rayCandidates[c]];
                IReadOnlyList<Vector2> vertices = feature.Vertices;
                IReadOnlyList<int> edges = feature.BoundaryEdges;
                for (int e = 0; e + 1 < edges.Count; e += 2)
                {
                    Vector2 a = vertices[edges[e]];
                    Vector2 b = vertices[edges[e + 1]];
                    if (TryIntersect(origin, ray, maxDistanceMeters, a, b, out float hit))
                        distances.Add(hit);
                }
            }

            distances.Sort();
        }

        private static bool TryIntersect(Vector2 origin, Vector2 ray, float maxDistance,
            Vector2 a, Vector2 b, out float distance)
        {
            distance = 0f;
            Vector2 edge = b - a;
            float denominator = ray.x * edge.y - ray.y * edge.x;
            if (Mathf.Abs(denominator) < 1e-7f)
                return false;

            Vector2 offset = a - origin;
            float along = (offset.x * edge.y - offset.y * edge.x) / denominator;
            float across = (offset.x * ray.y - offset.y * ray.x) / denominator;
            if (along <= 1e-4f || along > maxDistance || across < 0f || across > 1f)
                return false;

            distance = along;
            return true;
        }

        private void EnsureGrid()
        {
            if (gridReady)
                return;
            grid.Clear();
            for (int i = 0; i < features.Count; i++)
            {
                Rect bounds = features[i].Bounds;
                Vector2Int min = Cell(new Vector2(bounds.xMin, bounds.yMin));
                Vector2Int max = Cell(new Vector2(bounds.xMax, bounds.yMax));
                for (int y = min.y; y <= max.y; y++)
                for (int x = min.x; x <= max.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!grid.TryGetValue(cell, out List<int> indices))
                    {
                        indices = new List<int>();
                        grid.Add(cell, indices);
                    }
                    indices.Add(i);
                }
            }
            gridReady = true;
        }

        private void CollectCandidates(Vector2 point, float radius)
        {
            candidateBuffer.Clear();
            Vector2Int min = Cell(point - Vector2.one * radius);
            Vector2Int max = Cell(point + Vector2.one * radius);
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                if (!grid.TryGetValue(new Vector2Int(x, y), out List<int> indices))
                    continue;
                foreach (int index in indices)
                    candidateBuffer.Add(index);
            }
        }

        private Vector2Int Cell(Vector2 point) => new Vector2Int(
            Mathf.FloorToInt((point.x - studyCenterXZ.x) / gridCellMeters),
            Mathf.FloorToInt((point.y - studyCenterXZ.y) / gridCellMeters));

        private static int ClassificationPriority(SemanticSpaceKind kind)
        {
            switch (kind)
            {
                case SemanticSpaceKind.Crosswalk: return 4;
                case SemanticSpaceKind.Curb: return 3;
                case SemanticSpaceKind.Sidewalk: return 2;
                case SemanticSpaceKind.Road: return 1;
                default: return 0;
            }
        }
    }
}
