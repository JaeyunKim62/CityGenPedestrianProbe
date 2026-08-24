using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class EgocentricSweepSignalTests
    {
        private GameObject runtimeObject;
        private GameObject playerObject;
        private SemanticSpaceMap map;
        private SemanticSessionLogger logger;
        private SemanticPedestrianController player;
        private FeedbackCoordinator coordinator;
        private EgocentricSweepProbe sweep;
        private RecordingSignalRenderer renderer;

        [SetUp]
        public void SetUp()
        {
            runtimeObject = new GameObject("Sweep Signal Test Runtime");
            playerObject = new GameObject("Sweep Signal Test Player");
            map = ScriptableObject.CreateInstance<SemanticSpaceMap>();
            logger = runtimeObject.AddComponent<SemanticSessionLogger>();
            player = playerObject.AddComponent<SemanticPedestrianController>();
            coordinator = runtimeObject.AddComponent<FeedbackCoordinator>();
            sweep = runtimeObject.AddComponent<EgocentricSweepProbe>();
            renderer = new RecordingSignalRenderer();

            coordinator.Configure(map, playerObject.transform, logger, player);
            coordinator.SignalBus.SetRenderers(new ISignalRenderer[] { renderer });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(runtimeObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
            UnityEngine.Object.DestroyImmediate(map);
        }

        [Test]
        public void HitSignalId_CarriesTheCategoryAsItsLastSegment()
        {
            Assert.That(EgocentricSweepProbe.HitSignalIdFor(Sample("Sidewalk")),
                Is.EqualTo("probe.hit.sidewalk"));
            Assert.That(EgocentricSweepProbe.HitSignalIdFor(Sample("Crosswalk")),
                Is.EqualTo("probe.hit.crosswalk"));
        }

        [Test]
        public void HitSignalId_FallsBackToTheBareIdWithoutACategory()
        {
            Assert.That(EgocentricSweepProbe.HitSignalIdFor(Sample(string.Empty)),
                Is.EqualTo("probe.hit"));
        }

        [Test]
        public void HitSignalId_ReplacesCharactersThatWouldSplitSegments()
        {
            Assert.That(EgocentricSweepProbe.HitSignalIdFor(Sample("Shared Use.Path")),
                Is.EqualTo("probe.hit.shared-use-path"));
        }

        [Test]
        public void Hit_EmitsASustainedAnswerAtTheBoundary()
        {
            sweep.EmitSignalFor(HitAt(new Vector2(3f, 4f), 4.5f, "Curb", 42L));

            Assert.That(renderer.Rendered.Count, Is.EqualTo(1));
            SignalIntent intent = renderer.Rendered[0];
            Assert.That(intent.SignalId, Is.EqualTo("probe.hit.curb"));
            Assert.That(intent.Channel, Is.EqualTo(SignalChannel.Answer));
            Assert.That(intent.Form, Is.EqualTo(SignalForm.Sustained));
            Assert.That(intent.DistanceMeters, Is.EqualTo(4.5f));
            Assert.That(intent.ReferentFeatureId, Is.EqualTo(42L));
            Assert.That(intent.WorldPosition.x, Is.EqualTo(3f));
            Assert.That(intent.WorldPosition.z, Is.EqualTo(4f));
        }

        [Test]
        public void Miss_EmitsAnExplicitSignalAtTheEndOfTheRay()
        {
            sweep.EmitSignalFor(MissAt(new Vector2(0f, 5f), 5f));

            Assert.That(renderer.Rendered.Count, Is.EqualTo(1));
            SignalIntent intent = renderer.Rendered[0];
            Assert.That(intent.SignalId, Is.EqualTo(EgocentricSweepProbe.MissSignalId));
            Assert.That(intent.Form, Is.EqualTo(SignalForm.Sustained));
            Assert.That(intent.DistanceMeters, Is.EqualTo(5f));
            Assert.That(intent.WorldPosition.z, Is.EqualTo(5f));
        }

        [Test]
        public void RepeatedHitOnTheSameCategory_KeepsTheSameVoice()
        {
            sweep.EmitSignalFor(HitAt(new Vector2(1f, 1f), 1.4f, "Sidewalk", 1L));
            sweep.EmitSignalFor(HitAt(new Vector2(2f, 2f), 2.8f, "Sidewalk", 1L));

            Assert.That(renderer.Released, Is.Empty);
            Assert.That(renderer.Rendered.Count, Is.EqualTo(2));
        }

        [Test]
        public void CrossingIntoAnotherCategory_ReleasesThePreviousSignal()
        {
            sweep.EmitSignalFor(HitAt(new Vector2(1f, 1f), 1.4f, "Sidewalk", 1L));
            sweep.EmitSignalFor(HitAt(new Vector2(2f, 2f), 2.8f, "Road", 2L));

            Assert.That(renderer.Released, Is.EqualTo(new[] { "probe.hit.sidewalk" }));
        }

        [Test]
        public void LosingTheBoundary_ReleasesTheHitSignal()
        {
            sweep.EmitSignalFor(HitAt(new Vector2(1f, 1f), 1.4f, "Sidewalk", 1L));
            sweep.EmitSignalFor(MissAt(new Vector2(0f, 5f), 5f));

            Assert.That(renderer.Released, Is.EqualTo(new[] { "probe.hit.sidewalk" }));
            Assert.That(renderer.Rendered[1].SignalId,
                Is.EqualTo(EgocentricSweepProbe.MissSignalId));
        }

        [Test]
        public void DisablingTheModule_ReleasesWhateverWasSounding()
        {
            sweep.EmitSignalFor(HitAt(new Vector2(1f, 1f), 1.4f, "Sidewalk", 1L));

            coordinator.SetModuleEnabled(EgocentricSweepProbe.StableModuleId, false, "test");

            Assert.That(renderer.Released, Is.EqualTo(new[] { "probe.hit.sidewalk" }));
        }

        [Test]
        public void DisablingTwice_DoesNotReleaseTwice()
        {
            sweep.EmitSignalFor(HitAt(new Vector2(1f, 1f), 1.4f, "Sidewalk", 1L));

            coordinator.SetModuleEnabled(EgocentricSweepProbe.StableModuleId, false, "test");
            coordinator.SetModuleEnabled(EgocentricSweepProbe.StableModuleId, true, "test");
            coordinator.SetModuleEnabled(EgocentricSweepProbe.StableModuleId, false, "test");

            Assert.That(renderer.Released.Count, Is.EqualTo(1));
        }

        private static SpatialProbeSample Sample(string category) =>
            new SpatialProbeSample(true, category, "Highway", 1L, 1f);

        private static SpatialProbeObservation HitAt(Vector2 point, float distance,
            string category, long featureId)
        {
            var hit = new SpatialProbeHit(true, point, distance, 5f,
                new SpatialProbeSample(true, "Sidewalk", "Highway", 0L, 1f),
                new SpatialProbeSample(true, category, "Highway", featureId, 1f));
            return new SpatialProbeObservation(Vector2.zero, Vector2.up, 0f, hit);
        }

        private static SpatialProbeObservation MissAt(Vector2 point, float range)
        {
            SpatialProbeHit hit = SpatialProbeHit.NoHit(point, range,
                new SpatialProbeSample(true, "Sidewalk", "Highway", 0L, 1f));
            return new SpatialProbeObservation(Vector2.zero, Vector2.up, 0f, hit);
        }

        private sealed class RecordingSignalRenderer : ISignalRenderer
        {
            public readonly List<SignalIntent> Rendered = new List<SignalIntent>();
            public readonly List<string> Released = new List<string>();

            public int StopAllCount { get; private set; }

#pragma warning disable CS0067
            public event Action<string> SignalUnresolved;
#pragma warning restore CS0067

            public void Render(in SignalIntent intent) => Rendered.Add(intent);

            public void Release(string signalId) => Released.Add(signalId);

            public void StopAll() => StopAllCount++;
        }
    }
}
