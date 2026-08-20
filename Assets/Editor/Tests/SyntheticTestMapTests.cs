using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class SyntheticTestMapTests
    {
        private const float Range = 5f;
        private readonly List<SemanticSpaceMap> created = new List<SemanticSpaceMap>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
                UnityEngine.Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void EveryLayout_PlacesTheSpawnOnWalkableGround()
        {
            foreach (SyntheticLayout layout in SyntheticTestMaps.Layouts)
            {
                SemanticHit hit = Map(layout.Id).ClassifyWorld(Vector3.zero);
                Assert.That(hit.Kind, Is.EqualTo(SemanticSpaceKind.Sidewalk), layout.Id);
            }
        }

        [Test]
        public void Corridor4m_ReportsBothWallsAtTwoMetres()
        {
            SemanticSpaceMap map = Map("SYN01_Corridor4m");

            AssertHit(map, Vector2.zero, -90f, 2f);
            AssertHit(map, Vector2.zero, 90f, 2f);
        }

        [Test]
        public void Corridor4m_ReportsNothingStraightAhead()
        {
            AssertMiss(Map("SYN01_Corridor4m"), Vector2.zero, 0f);
        }

        [Test]
        public void Corridor4m_ReportsAFartherWallOnTheDiagonal()
        {
            AssertHit(Map("SYN01_Corridor4m"), Vector2.zero, 45f, 2.83f);
        }

        [Test]
        public void Corridor8m_ReportsBothWallsAtFourMetres()
        {
            SemanticSpaceMap map = Map("SYN02_Corridor8m");

            AssertHit(map, Vector2.zero, -90f, 4f);
            AssertHit(map, Vector2.zero, 90f, 4f);
        }

        [Test]
        public void Corridor8m_PutsItsDiagonalOutOfRange()
        {
            AssertMiss(Map("SYN02_Corridor8m"), Vector2.zero, 45f);
        }

        [Test]
        public void OpeningLeft_IsOpenOnTheLeftAndClosedOnTheRight()
        {
            SemanticSpaceMap map = Map("SYN03_OpeningLeft");

            AssertMiss(map, Vector2.zero, -90f);
            AssertHit(map, Vector2.zero, 90f, 2f);
        }

        [Test]
        public void OpeningRight_MirrorsOpeningLeft()
        {
            SemanticSpaceMap map = Map("SYN04_OpeningRight");

            AssertHit(map, Vector2.zero, -90f, 2f);
            AssertMiss(map, Vector2.zero, 90f);
        }

        [Test]
        public void Tee_LooksLikeACorridorBeforeTheJunction()
        {
            SemanticSpaceMap map = Map("SYN05_Tee");

            AssertHit(map, Vector2.zero, -90f, 2f);
            AssertHit(map, Vector2.zero, 90f, 2f);
            AssertMiss(map, Vector2.zero, 0f);
        }

        [Test]
        public void Tee_InvertsThatPatternInsideTheArm()
        {
            SemanticSpaceMap map = Map("SYN05_Tee");
            var insideArm = new Vector2(0f, 6f);

            AssertMiss(map, insideArm, -90f);
            AssertMiss(map, insideArm, 90f);
            AssertHit(map, insideArm, 0f, 2f);
        }

        [Test]
        public void CrossingBetweenTwoWalkableFeatures_IsNotABoundary()
        {
            SemanticSpaceMap map = Map("SYN05_Tee");

            SpatialProbeHit hit = Probe(map, new Vector2(0f, 2f), 0f);

            Assert.That(hit.HasHit, Is.False,
                "the stem and the arm are both walkable, so their shared edge is not a " +
                "spatial boundary even though the feature id changes");
        }

        private SemanticSpaceMap Map(string layoutId)
        {
            SemanticSpaceMap map = SyntheticTestMaps.Create(layoutId);
            created.Add(map);
            return map;
        }

        private static SpatialProbeHit Probe(SemanticSpaceMap map, Vector2 origin,
            float angleDegrees)
        {
            Vector3 direction = Quaternion.Euler(0f, angleDegrees, 0f) * Vector3.forward;
            var request = new SpatialProbeRequest(origin,
                new Vector2(direction.x, direction.z), Range);
            return new RayBoundaryProbe().Probe(new SemanticSpaceMapProbeField(map), request);
        }

        private static void AssertHit(SemanticSpaceMap map, Vector2 origin,
            float angleDegrees, float expectedMetres)
        {
            SpatialProbeHit hit = Probe(map, origin, angleDegrees);

            Assert.That(hit.HasHit, Is.True, $"expected a boundary at {angleDegrees} deg");
            Assert.That(hit.DistanceMeters, Is.EqualTo(expectedMetres).Within(0.1f),
                $"boundary distance at {angleDegrees} deg");
        }

        private static void AssertMiss(SemanticSpaceMap map, Vector2 origin,
            float angleDegrees)
        {
            SpatialProbeHit hit = Probe(map, origin, angleDegrees);

            Assert.That(hit.HasHit, Is.False,
                $"expected open space at {angleDegrees} deg");
        }
    }
}
