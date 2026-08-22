using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class CategoryAudibilityTests
    {
        private SemanticSpaceMap map;
        private SignalCatalog categoryCatalog;
        private SignalCatalog plainCatalog;

        [SetUp]
        public void SetUp()
        {
            map = SyntheticTestMaps.Create("SYN06_KerbAndRoad");
            categoryCatalog = Load("C101_PulseCategory");
            plainCatalog = Load("C003_PulseRange");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(map);
        }

        [Test]
        public void TheTwoSidesOfSyn06_ReachDifferentDefinitions()
        {
            SignalDefinition left = Resolve(categoryCatalog, -90f);
            SignalDefinition right = Resolve(categoryCatalog, 90f);

            Assert.That(left.SignalId, Is.EqualTo("probe.hit.road"));
            Assert.That(right.SignalId, Is.EqualTo("probe.hit.curb"));
            Assert.That(left.BaseFrequencyHz, Is.Not.EqualTo(right.BaseFrequencyHz));
        }

        [Test]
        public void TheRoadSideIsTheLowerOfTheTwo()
        {
            Assert.That(Resolve(categoryCatalog, -90f).BaseFrequencyHz,
                Is.LessThan(Resolve(categoryCatalog, 90f).BaseFrequencyHz),
                "road should read as heavier than kerb");
        }

        [Test]
        public void BothSidesKeepTheSameRateBecauseTheDistancesMatch()
        {
            SignalDefinition left = Resolve(categoryCatalog, -90f);
            SignalDefinition right = Resolve(categoryCatalog, 90f);

            Assert.That(left.PulseHzAt(2f), Is.EqualTo(right.PulseHzAt(2f)).Within(0.001f),
                "distance is identical on both sides, so only category may differ");
        }

        [Test]
        public void TheOtherCatalogsDeliberatelyCollapseBothSidesOntoOneSound()
        {
            SignalDefinition left = Resolve(plainCatalog, -90f);
            SignalDefinition right = Resolve(plainCatalog, 90f);

            Assert.That(left.SignalId, Is.EqualTo("probe.hit"));
            Assert.That(right.SignalId, Is.EqualTo("probe.hit"),
                "C003 carries no category, so hearing the sides as identical is correct");
        }

        private SignalDefinition Resolve(SignalCatalog catalog, float angleDegrees)
        {
            Vector3 direction = Quaternion.Euler(0f, angleDegrees, 0f) * Vector3.forward;
            var request = new SpatialProbeRequest(Vector2.zero,
                new Vector2(direction.x, direction.z), 5f);
            SpatialProbeHit hit = new RayBoundaryProbe()
                .Probe(new SemanticSpaceMapProbeField(map), request);
            Assert.That(hit.HasHit, Is.True, $"no boundary at {angleDegrees} deg");

            string signalId = EgocentricSweepProbe.HitSignalIdFor(hit.To);
            Assert.That(catalog.TryResolve(signalId, out SignalDefinition definition),
                Is.True, signalId);
            return definition;
        }

        private static SignalCatalog Load(string assetName)
        {
            string path = $"Assets/Generated/Signals/SignalCatalog_{assetName}.asset";
            var loaded = AssetDatabase.LoadAssetAtPath<SignalCatalog>(path);
            Assert.That(loaded, Is.Not.Null, path);
            return loaded;
        }
    }
}
