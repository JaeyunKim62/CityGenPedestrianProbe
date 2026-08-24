using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class FeedbackCoordinatorTests
    {
        private GameObject runtimeObject;
        private GameObject playerObject;
        private SemanticSpaceMap map;
        private FeedbackCoordinator coordinator;
        private SemanticPedestrianController player;
        private SemanticSessionLogger logger;

        [SetUp]
        public void SetUp()
        {
            runtimeObject = new GameObject("Feedback Coordinator Test Runtime");
            playerObject = new GameObject("Feedback Coordinator Test Player");
            map = ScriptableObject.CreateInstance<SemanticSpaceMap>();
            player = playerObject.AddComponent<SemanticPedestrianController>();
            logger = runtimeObject.AddComponent<SemanticSessionLogger>();
            coordinator = runtimeObject.AddComponent<FeedbackCoordinator>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(runtimeObject);
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(map);
        }

        [Test]
        public void Configure_RegistersModulesByStableId()
        {
            var counter = runtimeObject.AddComponent<CountingFeedbackModule>();

            Configure();

            Assert.That(coordinator.ModuleCount, Is.EqualTo(2));
            Assert.That(counter.ConfigureCount, Is.EqualTo(1));
            Assert.That(coordinator.TryGetModuleState(CountingFeedbackModule.StableId,
                out bool counterEnabled), Is.True);
            Assert.That(counterEnabled, Is.True);
            Assert.That(coordinator.TryGetModuleState(SemanticAudioGuide.StableModuleId,
                out bool semanticEnabled), Is.True);
            Assert.That(semanticEnabled, Is.True);
        }

        [Test]
        public void SetModuleEnabled_ChangesStateAndIgnoresRepeatedValue()
        {
            var counter = runtimeObject.AddComponent<CountingFeedbackModule>();
            Configure();

            Assert.That(coordinator.SetModuleEnabled(CountingFeedbackModule.StableId,
                false, "test|disable"), Is.True);
            Assert.That(counter.IsFeedbackEnabled, Is.False);
            Assert.That(counter.StateChangeCount, Is.EqualTo(1));

            Assert.That(coordinator.SetModuleEnabled(CountingFeedbackModule.StableId,
                false, "repeated"), Is.True);
            Assert.That(counter.StateChangeCount, Is.EqualTo(1));

            Assert.That(coordinator.SetModuleEnabled(CountingFeedbackModule.StableId,
                true, "test-enable"), Is.True);
            Assert.That(counter.IsFeedbackEnabled, Is.True);
            Assert.That(counter.StateChangeCount, Is.EqualTo(2));
        }

        [Test]
        public void SetModuleEnabled_ReturnsFalseForUnknownId()
        {
            Configure();

            Assert.That(coordinator.SetModuleEnabled("missing.module", false), Is.False);
            Assert.That(coordinator.TryGetModuleState("missing.module", out _), Is.False);
        }

        [Test]
        public void SemanticAudioModule_CanBeDisabledAndReenabled()
        {
            Configure();

            Assert.That(coordinator.SetModuleEnabled(SemanticAudioGuide.StableModuleId,
                false, "test"), Is.True);
            Assert.That(coordinator.TryGetModuleState(SemanticAudioGuide.StableModuleId,
                out bool disabledState), Is.True);
            Assert.That(disabledState, Is.False);

            Assert.That(coordinator.SetModuleEnabled(SemanticAudioGuide.StableModuleId,
                true, "test"), Is.True);
            Assert.That(coordinator.TryGetModuleState(SemanticAudioGuide.StableModuleId,
                out bool enabledState), Is.True);
            Assert.That(enabledState, Is.True);
        }

        [Test]
        public void BoundaryContact_IsSentOnlyToEnabledModules()
        {
            var counter = runtimeObject.AddComponent<CountingFeedbackModule>();
            Configure();
            MethodInfo handle = typeof(FeedbackCoordinator).GetMethod("HandleBoundaryContact",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handle, Is.Not.Null);

            handle.Invoke(coordinator, new object[] { Vector3.forward });
            Assert.That(counter.BoundaryCount, Is.EqualTo(1));

            coordinator.SetModuleEnabled(CountingFeedbackModule.StableId, false, "test");
            handle.Invoke(coordinator, new object[] { Vector3.forward });
            Assert.That(counter.BoundaryCount, Is.EqualTo(1));
        }

        [Test]
        public void Configure_HandsEveryModuleTheSharedSignalBus()
        {
            var counter = runtimeObject.AddComponent<CountingFeedbackModule>();

            Configure();

            Assert.That(counter.LastContext.SignalBus, Is.SameAs(coordinator.SignalBus));
            Assert.That(counter.LastContext.Map, Is.SameAs(map));
            Assert.That(counter.LastContext.Logger, Is.SameAs(logger));
            Assert.That(counter.LastContext.Listener, Is.SameAs(runtimeObject.transform));
        }

        [Test]
        public void Configure_RegistersSignalRenderersFoundOnTheRuntimeObject()
        {
            runtimeObject.AddComponent<AudioSignalRenderer>();

            Configure();

            Assert.That(coordinator.SignalBus.RendererCount, Is.EqualTo(1));
        }

        [Test]
        public void Configure_LeavesTheBusWithoutRenderersWhenNoneArePresent()
        {
            Configure();

            Assert.That(coordinator.SignalBus.RendererCount, Is.EqualTo(0));
        }

        [Test]
        public void ProbeObservation_IsSentOnlyToEnabledSinkModules()
        {
            var counter = runtimeObject.AddComponent<CountingFeedbackModule>();
            Configure();
            MethodInfo handle = typeof(FeedbackCoordinator).GetMethod("HandleProbeObservation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handle, Is.Not.Null);
            var hit = SpatialProbeHit.NoHit(new Vector2(0f, 5f), 5f,
                SpatialProbeSample.Unknown);
            var observation = new SpatialProbeObservation(Vector2.zero, Vector2.up, 0f, hit);

            handle.Invoke(coordinator, new object[] { observation });
            Assert.That(counter.ObservationCount, Is.EqualTo(1));
            Assert.That(counter.LastObservation.RequestedRangeMeters, Is.EqualTo(5f));

            coordinator.SetModuleEnabled(CountingFeedbackModule.StableId, false, "test");
            handle.Invoke(coordinator, new object[] { observation });
            Assert.That(counter.ObservationCount, Is.EqualTo(1));
        }

        private void Configure() => coordinator.Configure(map, runtimeObject.transform,
            logger, player);
    }

    public sealed class CountingFeedbackModule : FeedbackModule, ISpatialProbeObservationSink
    {
        public const string StableId = "test.counter";
        public override string ModuleId => StableId;
        public int ConfigureCount { get; private set; }
        public int StateChangeCount { get; private set; }
        public int BoundaryCount { get; private set; }
        public int ObservationCount { get; private set; }
        public FeedbackContext LastContext { get; private set; }
        public SpatialProbeObservation LastObservation { get; private set; }

        public override void Configure(in FeedbackContext context)
        {
            ConfigureCount++;
            LastContext = context;
        }

        protected override void OnFeedbackEnabledChanged(bool value)
        {
            StateChangeCount++;
        }

        public override void OnBoundaryContact(Vector3 attemptedDirection)
        {
            BoundaryCount++;
        }

        public void OnSpatialProbeObservation(SpatialProbeObservation observation)
        {
            ObservationCount++;
            LastObservation = observation;
        }
    }

}
