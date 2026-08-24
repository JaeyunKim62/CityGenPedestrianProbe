using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class ExactBoundaryProbeTests
    {
        private const float Range = 5f;
        private SemanticSpaceMap map;

        [SetUp]
        public void SetUp()
        {
            map = ScriptableObject.CreateInstance<SemanticSpaceMap>();
            map.SetData("synthetic-kerb", "TEST_THIN_KERB", 0d, 0d, Vector2.zero,
                Vector2.zero, 60f, 5f, new List<SemanticFeature>
                {
                    Rect(SemanticSpaceKind.Sidewalk, 1, -2f, 2.6f),
                    Rect(SemanticSpaceKind.Curb, 2, 2.6f, 2.75f),
                    Rect(SemanticSpaceKind.Sidewalk, 3, 2.75f, 9f)
                });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(map);
        }

        [Test]
        public void ExactProbe_FindsAKerbNarrowerThanTheSamplingStep()
        {
            SpatialProbeHit hit = Probe(new SemanticSpaceMapProbeField(map));

            Assert.That(hit.HasHit, Is.True);
            Assert.That(hit.To.Category, Is.EqualTo("Curb"));
        }

        [Test]
        public void ExactProbe_PutsTheBoundaryWhereTheGeometryIs()
        {
            SpatialProbeHit hit = Probe(new SemanticSpaceMapProbeField(map));

            Assert.That(hit.DistanceMeters, Is.EqualTo(2.6f).Within(0.02f));
        }

        [Test]
        public void MarchingProbe_MissesItEntirely()
        {
            SpatialProbeHit hit = Probe(
                new SampleOnlyField(new SemanticSpaceMapProbeField(map)));

            Assert.That(hit.HasHit, Is.False,
                "sidewalk either side of a 15 cm kerb means both bracketing samples agree " +
                "with the origin, so half-metre marching never learns the kerb is there");
        }

        [Test]
        public void ExactProbe_ReportsOpenSpaceWhenNothingIsCrossed()
        {
            var field = new SemanticSpaceMapProbeField(map);
            var request = new SpatialProbeRequest(Vector2.zero, Vector2.up, Range);

            SpatialProbeHit hit = new RayBoundaryProbe().Probe(field, request);

            Assert.That(hit.HasHit, Is.False,
                "the sidewalk runs the length of the ray, so nothing is crossed");
        }

        [Test]
        public void ExactProbe_IgnoresBoundariesBeyondTheRequestedRange()
        {
            var field = new SemanticSpaceMapProbeField(map);
            var request = new SpatialProbeRequest(new Vector2(-1f, 0f), Vector2.left, 0.5f);

            SpatialProbeHit hit = new RayBoundaryProbe().Probe(field, request);

            Assert.That(hit.HasHit, Is.False,
                "walking away from the kerb leaves nothing inside 5 m");
        }

        private static SpatialProbeHit Probe(ISpatialProbeField field)
        {
            var request = new SpatialProbeRequest(Vector2.zero, Vector2.right, Range);
            return new RayBoundaryProbe().Probe(field, request);
        }

        private static SemanticFeature Rect(SemanticSpaceKind kind, long id,
            float minX, float maxX)
        {
            var vertices = new[]
            {
                new Vector2(minX, -20f), new Vector2(maxX, -20f),
                new Vector2(maxX, 20f), new Vector2(minX, 20f)
            };
            return new SemanticFeature(kind, id, 1f, vertices, new[] { 0, 1, 2, 0, 2, 3 });
        }

        private sealed class SampleOnlyField : ISpatialProbeField
        {
            private readonly ISpatialProbeField inner;

            public SampleOnlyField(ISpatialProbeField field) => inner = field;

            public SpatialProbeSample Sample(Vector2 worldPosition) =>
                inner.Sample(worldPosition);
        }
    }
}
