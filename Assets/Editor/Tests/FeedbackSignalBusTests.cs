using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class FeedbackSignalBusTests
    {
        private FeedbackSignalBus bus;
        private RecordingSignalRenderer first;
        private RecordingSignalRenderer second;

        [SetUp]
        public void SetUp()
        {
            bus = new FeedbackSignalBus();
            first = new RecordingSignalRenderer();
            second = new RecordingSignalRenderer();
        }

        [Test]
        public void Emit_ReachesEveryRegisteredRenderer()
        {
            bus.SetRenderers(new ISignalRenderer[] { first, second });

            bus.Emit(Intent("probe.hit"));

            Assert.That(first.Rendered, Is.EqualTo(new[] { "probe.hit" }));
            Assert.That(second.Rendered, Is.EqualTo(new[] { "probe.hit" }));
        }

        [Test]
        public void Emit_IgnoresIntentWithoutSignalId()
        {
            bus.SetRenderers(new ISignalRenderer[] { first });

            bus.Emit(SignalIntent.NonPositional("  ", SignalChannel.Answer,
                SignalForm.OneShot));

            Assert.That(first.Rendered, Is.Empty);
        }

        [Test]
        public void SetRenderers_ReplacesThePreviousSet()
        {
            bus.SetRenderers(new ISignalRenderer[] { first });
            bus.SetRenderers(new ISignalRenderer[] { second });

            bus.Emit(Intent("probe.hit"));

            Assert.That(first.Rendered, Is.Empty);
            Assert.That(second.Rendered, Is.EqualTo(new[] { "probe.hit" }));
            Assert.That(bus.RendererCount, Is.EqualTo(1));
        }

        [Test]
        public void SetRenderers_SkipsDuplicatesAndNulls()
        {
            bus.SetRenderers(new ISignalRenderer[] { first, first, null });

            bus.Emit(Intent("probe.hit"));

            Assert.That(bus.RendererCount, Is.EqualTo(1));
            Assert.That(first.Rendered.Count, Is.EqualTo(1));
        }

        [Test]
        public void Release_ReachesEveryRenderer()
        {
            bus.SetRenderers(new ISignalRenderer[] { first, second });

            bus.Release(" probe.hit ");

            Assert.That(first.Released, Is.EqualTo(new[] { "probe.hit" }));
            Assert.That(second.Released, Is.EqualTo(new[] { "probe.hit" }));
        }

        [Test]
        public void StopAll_ReachesEveryRenderer()
        {
            bus.SetRenderers(new ISignalRenderer[] { first, second });

            bus.StopAll();

            Assert.That(first.StopAllCount, Is.EqualTo(1));
            Assert.That(second.StopAllCount, Is.EqualTo(1));
        }

        [Test]
        public void UnresolvedSignal_FromADroppedRendererIsNoLongerObserved()
        {
            bus.SetRenderers(new ISignalRenderer[] { first });
            bus.SetRenderers(new ISignalRenderer[] { second });

            Assert.DoesNotThrow(() => first.RaiseUnresolved("region.plaza.state"));
        }

        [Test]
        public void Emit_WithoutRenderersDoesNotThrow()
        {
            Assert.DoesNotThrow(() => bus.Emit(Intent("probe.hit")));
        }

        private static SignalIntent Intent(string signalId) =>
            SignalIntent.Positional(signalId, SignalChannel.Answer, SignalForm.Sustained,
                new Vector3(1f, 0f, 2f), 2.5f, 7L);

        private sealed class RecordingSignalRenderer : ISignalRenderer
        {
            public readonly List<string> Rendered = new List<string>();
            public readonly List<string> Released = new List<string>();
            public int StopAllCount { get; private set; }

            public event Action<string> SignalUnresolved;

            public void Render(in SignalIntent intent) => Rendered.Add(intent.SignalId);

            public void Release(string signalId) => Released.Add(signalId);

            public void StopAll() => StopAllCount++;

            public void RaiseUnresolved(string signalId) => SignalUnresolved?.Invoke(signalId);
        }
    }
}
