using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class AudioSignalRendererTests
    {
        private GameObject rendererObject;
        private AudioSignalRenderer renderer;
        private SignalCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            rendererObject = new GameObject("Audio Signal Renderer Test");
            renderer = rendererObject.AddComponent<AudioSignalRenderer>();
            catalog = ScriptableObject.CreateInstance<SignalCatalog>();
            catalog.SetDefinitions("test", 1, new List<SignalDefinition>
            {
                new SignalDefinition("probe.hit", SignalWaveform.Sine, 440f, 0.18f, 0.6f),
                new SignalDefinition("probe.miss", SignalWaveform.Noise, 300f, 0.12f, 0.4f)
            });
            renderer.Configure(catalog, rendererObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(rendererObject);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Render_ReportsUnresolvedSignalInsteadOfFallingSilent()
        {
            string unresolved = null;
            renderer.SignalUnresolved += id => unresolved = id;

            renderer.Render(SignalIntent.NonPositional("region.plaza.state",
                SignalChannel.State, SignalForm.OneShot));

            Assert.That(unresolved, Is.EqualTo("region.plaza.state"));
        }

        [Test]
        public void Render_DoesNotReportResolvedSignal()
        {
            string unresolved = null;
            renderer.SignalUnresolved += id => unresolved = id;

            renderer.Render(SignalIntent.NonPositional("probe.miss", SignalChannel.Answer,
                SignalForm.OneShot));

            Assert.That(unresolved, Is.Null);
        }

        [Test]
        public void Render_KeepsOneVoicePerSustainedSignal()
        {
            for (int i = 0; i < 5; i++)
                renderer.Render(Sustained("probe.hit", i));

            Assert.That(renderer.ActiveSustainedCount, Is.EqualTo(1));
        }

        [Test]
        public void Render_KeepsSeparateVoicesForDifferentSustainedSignals()
        {
            renderer.Render(Sustained("probe.hit", 1f));
            renderer.Render(Sustained("probe.miss", 2f));

            Assert.That(renderer.ActiveSustainedCount, Is.EqualTo(2));
        }

        [Test]
        public void OneShotSignals_DoNotHoldVoices()
        {
            renderer.Render(SignalIntent.Positional("probe.hit", SignalChannel.Answer,
                SignalForm.OneShot, Vector3.zero, 2f));

            Assert.That(renderer.ActiveSustainedCount, Is.EqualTo(0));
        }

        [Test]
        public void Release_FreesOnlyTheNamedSustainedSignal()
        {
            renderer.Render(Sustained("probe.hit", 1f));
            renderer.Render(Sustained("probe.miss", 2f));

            renderer.Release("probe.hit");

            Assert.That(renderer.ActiveSustainedCount, Is.EqualTo(1));
        }

        [Test]
        public void Release_IgnoresUnknownSignalId()
        {
            renderer.Render(Sustained("probe.hit", 1f));

            renderer.Release("probe.nothing");

            Assert.That(renderer.ActiveSustainedCount, Is.EqualTo(1));
        }

        [Test]
        public void StopAll_ReleasesEverySustainedSignal()
        {
            renderer.Render(Sustained("probe.hit", 1f));
            renderer.Render(Sustained("probe.miss", 2f));

            renderer.StopAll();

            Assert.That(renderer.ActiveSustainedCount, Is.EqualTo(0));
        }

        [Test]
        public void Render_WithoutCatalogDoesNothing()
        {
            renderer.Configure(null, rendererObject.transform);
            string unresolved = null;
            renderer.SignalUnresolved += id => unresolved = id;

            renderer.Render(Sustained("probe.hit", 1f));

            Assert.That(renderer.ActiveSustainedCount, Is.EqualTo(0));
            Assert.That(unresolved, Is.Null);
        }

        [Test]
        public void Configure_StopsSignalsFromThePreviousSession()
        {
            renderer.Render(Sustained("probe.hit", 1f));

            renderer.Configure(catalog, rendererObject.transform);

            Assert.That(renderer.ActiveSustainedCount, Is.EqualTo(0));
        }

        private static SignalIntent Sustained(string signalId, float distanceMeters) =>
            SignalIntent.Positional(signalId, SignalChannel.Answer, SignalForm.Sustained,
                new Vector3(0f, 0f, distanceMeters), distanceMeters);
    }
}
