using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class SignalCatalogTests
    {
        private SignalCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<SignalCatalog>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void TryResolve_FindsExactSignalId()
        {
            Fill("C1", 3, Definition("probe.hit.curb"), Definition("probe.hit"));

            Assert.That(catalog.TryResolve("probe.hit.curb", out SignalDefinition definition),
                Is.True);
            Assert.That(definition.SignalId, Is.EqualTo("probe.hit.curb"));
            Assert.That(catalog.CatalogId, Is.EqualTo("C1"));
            Assert.That(catalog.Revision, Is.EqualTo(3));
        }

        [Test]
        public void TryResolve_FallsBackToShorterSignalId()
        {
            Fill("C2", 1, Definition("probe.hit"));

            Assert.That(catalog.TryResolve("probe.hit.curb", out SignalDefinition definition),
                Is.True);
            Assert.That(definition.SignalId, Is.EqualTo("probe.hit"));
        }

        [Test]
        public void TryResolve_PrefersTheMostSpecificDefinition()
        {
            Fill("C1", 1, Definition("probe.hit"), Definition("probe.hit.curb"));

            catalog.TryResolve("probe.hit.curb", out SignalDefinition definition);

            Assert.That(definition.SignalId, Is.EqualTo("probe.hit.curb"));
        }

        [Test]
        public void TryResolve_ReportsFailureWhenNoSegmentMatches()
        {
            Fill("C2", 1, Definition("probe.hit"));

            Assert.That(catalog.TryResolve("region.road.proximity",
                out SignalDefinition definition), Is.False);
            Assert.That(definition, Is.Null);
        }

        [Test]
        public void TryResolve_DoesNotFallBackPastTheFirstSegment()
        {
            Fill("C2", 1, Definition("probe"));

            Assert.That(catalog.TryResolve("probe.hit", out SignalDefinition definition),
                Is.True);
            Assert.That(definition.SignalId, Is.EqualTo("probe"));
        }

        [Test]
        public void TryResolve_RejectsBlankSignalId()
        {
            Fill("C2", 1, Definition("probe.hit"));

            Assert.That(catalog.TryResolve("  ", out _), Is.False);
        }

        [Test]
        public void CutoffHz_MovesFromNearToFarAcrossTheRange()
        {
            var definition = Definition("probe.hit");

            float near = definition.CutoffHzAt(0f);
            float mid = definition.CutoffHzAt(definition.FarDistanceMeters * 0.5f);
            float far = definition.CutoffHzAt(definition.FarDistanceMeters);

            Assert.That(near, Is.EqualTo(definition.NearCutoffHz).Within(0.01f));
            Assert.That(far, Is.EqualTo(definition.FarCutoffHz).Within(0.01f));
            Assert.That(mid, Is.LessThan(near));
            Assert.That(mid, Is.GreaterThan(far));
        }

        [Test]
        public void CutoffHz_FallsByEqualOctavesPerMetre()
        {
            var definition = new SignalDefinition("probe.hit", SignalWaveform.Harmonic,
                440f, 0.18f, 0.55f);
            float far = definition.FarDistanceMeters;

            float first = definition.CutoffHzAt(far * 0.25f) / definition.CutoffHzAt(0f);
            float second = definition.CutoffHzAt(far * 0.5f) / definition.CutoffHzAt(far * 0.25f);
            float third = definition.CutoffHzAt(far * 0.75f) / definition.CutoffHzAt(far * 0.5f);

            Assert.That(second, Is.EqualTo(first).Within(0.001f));
            Assert.That(third, Is.EqualTo(first).Within(0.001f));
            Assert.That(first, Is.LessThan(1f));
        }

        [Test]
        public void CutoffHz_ClampsBeyondTheFarDistance()
        {
            var definition = Definition("probe.hit");

            Assert.That(definition.CutoffHzAt(definition.FarDistanceMeters * 10f),
                Is.EqualTo(definition.FarCutoffHz).Within(0.01f));
        }

        [Test]
        public void CutoffHz_TreatsMissDistanceAsFarthest()
        {
            var definition = Definition("probe.miss");

            Assert.That(definition.CutoffHzAt(float.PositiveInfinity),
                Is.EqualTo(definition.FarCutoffHz).Within(0.01f));
        }

        [Test]
        public void SustainedLoop_HoldsWholeCyclesSoItCanRepeatSeamlessly()
        {
            int samples = SignalClipFactory.LoopSampleCount(440f);
            float cycles = samples * 440f / SignalClipFactory.SampleRate;

            Assert.That(cycles, Is.EqualTo(Mathf.Round(cycles)).Within(0.01f));
        }

        private static SignalDefinition Definition(string signalId) =>
            new SignalDefinition(signalId, SignalWaveform.Sine, 440f, 0.18f, 0.6f);

        private void Fill(string id, int revision, params SignalDefinition[] definitions) =>
            catalog.SetDefinitions(id, revision, new List<SignalDefinition>(definitions));
    }
}
