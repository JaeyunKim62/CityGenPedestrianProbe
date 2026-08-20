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
            Assert.That(catalog.Revision, Is.EqualTo(1));
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
            Assert.That(definition.BaseFrequencyHz, Is.EqualTo(320f));
            Assert.That(definition.HarmonicCount, Is.EqualTo(5));
            Assert.That(definition.AttackClick, Is.True);
            Assert.That(definition.Level, Is.EqualTo(0.55f).Within(0.001f));
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
        public void MissSignal_StaysQuietAndDullAtEveryDistance()
        {
            catalog.TryResolve(EgocentricSweepProbe.MissSignalId,
                out SignalDefinition definition);

            Assert.That(definition.Level, Is.LessThan(0.25f));
            Assert.That(definition.AttackClick, Is.False);
            Assert.That(definition.CutoffHzAt(0f),
                Is.EqualTo(definition.CutoffHzAt(5f)).Within(0.01f));
        }
    }
}
