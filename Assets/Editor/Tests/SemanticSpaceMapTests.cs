using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class SemanticSpaceMapTests
    {
        private SemanticSpaceMap map;

        [SetUp]
        public void SetUp()
        {
            map = ScriptableObject.CreateInstance<SemanticSpaceMap>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(map);
        }

        [Test]
        public void ClassifyWorld_UsesRequiredOverlapPriority()
        {
            var features = new List<SemanticFeature>
            {
                Quad(SemanticSpaceKind.Road, 1, -5f, -5f, 5f, 5f),
                Quad(SemanticSpaceKind.Sidewalk, 2, -4f, -4f, 4f, 4f),
                Quad(SemanticSpaceKind.Curb, 3, -3f, -3f, 3f, 3f),
                Quad(SemanticSpaceKind.Crosswalk, 4, -2f, -2f, 2f, 2f)
            };
            map.SetData("test", "db", 37.5, 127.0, Vector2.zero, Vector2.zero,
                256f, 5f, features);

            Assert.That(map.ClassifyWorld(Vector3.zero).Kind,
                Is.EqualTo(SemanticSpaceKind.Crosswalk));
        }

        [Test]
        public void QueryNearby_ReportsOuterBoundaryNotTriangleDiagonal()
        {
            map.SetData("test", "db", 37.5, 127.0, Vector2.zero, Vector2.zero,
                256f, 5f, new List<SemanticFeature>
                {
                    Quad(SemanticSpaceKind.Sidewalk, 1, 0f, 0f, 10f, 10f)
                });
            SemanticHit hit = map.ClassifyWorld(new Vector3(5f, 0f, 5f));
            Assert.That(hit.Contains, Is.True);
            Assert.That(hit.DistanceMeters, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void GeoWorldRoundTrip_IsWithinQuarterMeter()
        {
            map.SetData("test", "db", 37.5665, 126.978, new Vector2(128f, 128f),
                new Vector2(128f, 128f), 256f, 5f, new List<SemanticFeature>());
            Vector3 original = new Vector3(151.25f, 0f, 93.5f);
            map.WorldToGeo(original, out double latitude, out double longitude);
            Vector3 roundTrip = map.GeoToWorld(latitude, longitude);
            Assert.That(Vector2.Distance(new Vector2(original.x, original.z),
                new Vector2(roundTrip.x, roundTrip.z)), Is.LessThan(0.25f));
        }

        private static SemanticFeature Quad(SemanticSpaceKind kind, long id,
            float minX, float minY, float maxX, float maxY)
        {
            return new SemanticFeature(kind, id, 1f,
                new[]
                {
                    new Vector2(minX, minY), new Vector2(maxX, minY),
                    new Vector2(maxX, maxY), new Vector2(minX, maxY)
                },
                new[] { 0, 1, 2, 0, 2, 3 });
        }
    }
}
