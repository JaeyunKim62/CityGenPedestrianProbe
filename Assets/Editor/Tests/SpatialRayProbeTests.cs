using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class SpatialRayProbeTests
    {
        [Test]
        public void Probe_ReturnsFirstClassificationAndDistance()
        {
            var field = new DelegateField(point => point.x < 3f
                ? Sample("Sidewalk", "Highway", 10)
                : Sample("Road", "MapRoad", 20));
            var request = new SpatialProbeRequest(Vector2.zero, Vector2.right,
                5f, 0.5f, 6);

            SpatialProbeHit hit = new RayBoundaryProbe().Probe(field, request);

            Assert.That(hit.HasHit, Is.True);
            Assert.That(hit.DistanceMeters, Is.EqualTo(3f).Within(0.01f));
            Assert.That(hit.RequestedRangeMeters, Is.EqualTo(5f));
            Assert.That(hit.From.DisplayLabel, Is.EqualTo("Sidewalk / Highway"));
            Assert.That(hit.To.DisplayLabel, Is.EqualTo("Road / MapRoad"));
            Assert.That(hit.To.FeatureId, Is.EqualTo(20));
        }

        [Test]
        public void Probe_StopsAtRequestedRangeWhenThereIsNoHit()
        {
            var field = new DelegateField(_ => Sample("Sidewalk", "Highway", 10));
            var request = new SpatialProbeRequest(new Vector2(1f, 2f), Vector2.right,
                5f, 0.5f, 4);

            SpatialProbeHit hit = new RayBoundaryProbe().Probe(field, request);

            Assert.That(hit.HasHit, Is.False);
            Assert.That(hit.DistanceMeters, Is.EqualTo(float.PositiveInfinity));
            Assert.That(hit.RequestedRangeMeters, Is.EqualTo(5f));
            Assert.That(hit.Point, Is.EqualTo(new Vector2(6f, 2f)));
        }

        [Test]
        public void Probe_IgnoresFeatureChangesWithinSameClassification()
        {
            var field = new DelegateField(point =>
            {
                if (point.x < 2f)
                    return Sample("Sidewalk", "Highway", 10);
                return point.x < 4f
                    ? Sample("Sidewalk", "Highway", 11)
                    : Sample("Road", "MapRoad", 20);
            });
            var request = new SpatialProbeRequest(Vector2.zero, Vector2.right,
                5f, 0.25f, 6);

            SpatialProbeHit hit = new RayBoundaryProbe().Probe(field, request);

            Assert.That(hit.HasHit, Is.True);
            Assert.That(hit.DistanceMeters, Is.EqualTo(4f).Within(0.01f));
            Assert.That(hit.To.DisplayLabel, Is.EqualTo("Road / MapRoad"));
        }

        [Test]
        public void Probe_UsesCallerSuppliedHorizontalDirection()
        {
            var field = new DelegateField(point => point.y < 2f
                ? Sample("A", string.Empty, 1)
                : Sample("B", string.Empty, 2));
            var request = new SpatialProbeRequest(Vector2.zero, Vector2.up,
                5f, 0.5f, 6);

            SpatialProbeHit hit = new RayBoundaryProbe().Probe(field, request);

            Assert.That(hit.HasHit, Is.True);
            Assert.That(hit.DistanceMeters, Is.EqualTo(2f).Within(0.01f));
            Assert.That(hit.Point.x, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Probe_IgnoresSourceChangesWithinSameSemanticCategory()
        {
            var field = new DelegateField(point =>
            {
                if (point.x < 2f)
                    return Sample("Road", "MapRoad", 10);
                return point.x < 4f
                    ? Sample("Road", "Highway", 11)
                    : Sample("Sidewalk", "Highway", 20);
            });
            var request = new SpatialProbeRequest(Vector2.zero, Vector2.right,
                5f, 0.25f, 6);

            SpatialProbeHit hit = new RayBoundaryProbe().Probe(field, request);

            Assert.That(hit.HasHit, Is.True);
            Assert.That(hit.DistanceMeters, Is.EqualTo(4f).Within(0.01f));
            Assert.That(hit.From.DisplayLabel, Is.EqualTo("Road / Highway"));
            Assert.That(hit.To.DisplayLabel, Is.EqualTo("Sidewalk / Highway"));
        }

        [Test]
        public void SemanticMapAdapter_ReportsWhatWasHitWithoutCityDependencyInProbe()
        {
            SemanticSpaceMap map = ScriptableObject.CreateInstance<SemanticSpaceMap>();
            try
            {
                map.SetData("test", "db", 37.5, 127.0, Vector2.zero, Vector2.zero,
                    20f, 1f, new List<SemanticFeature>
                    {
                        Quad(SemanticSpaceKind.Sidewalk, 7, -1f, -1f, 1f, 1f,
                            "CityGen3D.Highway")
                    });
                var adapter = new SemanticSpaceMapProbeField(map);

                SpatialProbeSample sample = adapter.Sample(Vector2.zero);

                Assert.That(sample.IsKnown, Is.True);
                Assert.That(sample.DisplayLabel, Is.EqualTo("Sidewalk / Highway"));
                Assert.That(sample.FeatureId, Is.EqualTo(7));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
            }
        }

        [Test]
        public void RuntimeProbe_DefaultRangeIsFiveMeters()
        {
            var gameObject = new GameObject("Egocentric Sweep Probe Test");
            try
            {
                var runtimeProbe = gameObject.AddComponent<EgocentricSweepProbe>();
                Assert.That(runtimeProbe.RangeMeters, Is.EqualTo(5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Observation_PreservesNormalizedDirectionAngleRangeAndHit()
        {
            var hit = new SpatialProbeHit(true, new Vector2(3f, 4f), 4.5f, 5f,
                Sample("Sidewalk", "Highway", 10), Sample("Road", "MapRoad", 20));

            var observation = new SpatialProbeObservation(new Vector2(1f, 2f),
                new Vector2(3f, 4f), -30f, hit);

            Assert.That(observation.Origin, Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(observation.Direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(observation.RelativeAngleDegrees, Is.EqualTo(-30f));
            Assert.That(observation.RequestedRangeMeters, Is.EqualTo(5f));
            Assert.That(observation.HasHit, Is.True);
            Assert.That(observation.Hit.To.DisplayLabel, Is.EqualTo("Road / MapRoad"));
        }

        private static SpatialProbeSample Sample(string category, string source, long id) =>
            new SpatialProbeSample(true, category, source, id, 1f);

        private static SemanticFeature Quad(SemanticSpaceKind kind, long id,
            float minX, float minY, float maxX, float maxY, string sourceClass)
        {
            return new SemanticFeature(kind, id, 1f,
                new[]
                {
                    new Vector2(minX, minY), new Vector2(maxX, minY),
                    new Vector2(maxX, maxY), new Vector2(minX, maxY)
                },
                new[] { 0, 1, 2, 0, 2, 3 }, sourceClass);
        }

        private sealed class DelegateField : ISpatialProbeField
        {
            private readonly Func<Vector2, SpatialProbeSample> sample;

            public DelegateField(Func<Vector2, SpatialProbeSample> sampleFunction)
            {
                sample = sampleFunction;
            }

            public SpatialProbeSample Sample(Vector2 worldPosition) => sample(worldPosition);
        }
    }
}
