using NUnit.Framework;
using UnityEditor;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class ShippedSignalCatalogTests
    {
        private const string CatalogPath =
            "Assets/Generated/Signals/SignalCatalog_C002_DirectionRange.asset";

        private SignalCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<SignalCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing signal catalog at {CatalogPath}");
        }

        [Test]
        public void Catalog_CarriesItsIdAndRevisionForTheSessionLog()
        {
            Assert.That(catalog.CatalogId, Is.EqualTo("C002_DIRECTION_RANGE"));
            Assert.That(catalog.Revision, Is.GreaterThanOrEqualTo(2),
                "bump the revision whenever a definition changes, or the log cannot " +
                "say which sounds a session used");
            Assert.That(catalog.DefinitionCount, Is.EqualTo(2));
        }

        [Test]
        public void Catalog_AnswersEverySignalTheSweepCanEmit()
        {
            Assert.That(catalog.TryResolve(EgocentricSweepProbe.HitSignalId, out _), Is.True);
            Assert.That(catalog.TryResolve(EgocentricSweepProbe.MissSignalId, out _), Is.True);
        }

        [Test]
        public void Catalog_CollapsesEveryCategoryOntoTheBareHitSignal()
        {
            foreach (string category in new[] { "Sidewalk", "Curb", "Crosswalk", "Road" })
            {
                string signalId = EgocentricSweepProbe.HitSignalIdFor(
                    new SpatialProbeSample(true, category, "Highway", 1L, 1f));

                Assert.That(catalog.TryResolve(signalId, out SignalDefinition definition),
                    Is.True, signalId);
                Assert.That(definition.SignalId, Is.EqualTo(EgocentricSweepProbe.HitSignalId),
                    $"{signalId} should fall back rather than carry its own timbre in C-2");
            }
        }

        [Test]
        public void HitSignal_DeserialisedItsSynthesisFields()
        {
            catalog.TryResolve(EgocentricSweepProbe.HitSignalId,
                out SignalDefinition definition);

            Assert.That(definition.Waveform, Is.EqualTo(SignalWaveform.Harmonic));
            Assert.That(definition.BaseFrequencyHz, Is.GreaterThan(100f));
            Assert.That(definition.HarmonicCount, Is.GreaterThanOrEqualTo(3),
                "distance is read from harmonics being filtered away, so there have to " +
                "be several to lose");
            Assert.That(definition.AttackClick, Is.True);
            Assert.That(definition.Level, Is.GreaterThan(0f));
        }

        [Test]
        public void HitSignal_BrightensAsTheBoundaryComesCloser()
        {
            catalog.TryResolve(EgocentricSweepProbe.HitSignalId,
                out SignalDefinition definition);

            Assert.That(definition.CutoffHzAt(0.5f),
                Is.GreaterThan(definition.CutoffHzAt(4.5f)));
        }

        [Test]
        public void MissSignal_StaysDullAndFlatAtEveryDistance()
        {
            catalog.TryResolve(EgocentricSweepProbe.MissSignalId,
                out SignalDefinition definition);

            Assert.That(definition.AttackClick, Is.False);
            Assert.That(definition.CutoffHzAt(0f),
                Is.EqualTo(definition.CutoffHzAt(5f)).Within(0.01f),
                "a miss reports no boundary, so it must not appear to have a distance");
        }

        [Test]
        public void MissSignal_SitsUnderTheHitSignalRatherThanBesideIt()
        {
            catalog.TryResolve(EgocentricSweepProbe.HitSignalId, out SignalDefinition hit);
            catalog.TryResolve(EgocentricSweepProbe.MissSignalId, out SignalDefinition miss);

            Assert.That(miss.Level, Is.LessThan(hit.Level),
                "the boundary is the signal, the miss is the bed under it");
            Assert.That(miss.CutoffHzAt(0f), Is.LessThanOrEqualTo(hit.BaseFrequencyHz),
                "the two must not share a band, or they blur into each other");
        }
    }
}
