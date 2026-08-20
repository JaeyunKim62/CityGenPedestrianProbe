using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class SignalIntentTests
    {
        [Test]
        public void Positional_KeepsWorldPositionDistanceAndReferent()
        {
            var intent = SignalIntent.Positional("region.curb.answer", SignalChannel.Answer,
                SignalForm.OneShot, new Vector3(1f, 0f, 2f), 3.5f, 42L);

            Assert.That(intent.IsValid, Is.True);
            Assert.That(intent.HasWorldPosition, Is.True);
            Assert.That(intent.WorldPosition, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(intent.DistanceMeters, Is.EqualTo(3.5f));
            Assert.That(intent.ReferentFeatureId, Is.EqualTo(42L));
            Assert.That(intent.Form, Is.EqualTo(SignalForm.OneShot));
        }

        [Test]
        public void NonPositional_ReportsNoWorldPositionAndNoDistance()
        {
            var intent = SignalIntent.NonPositional("probe.miss", SignalChannel.Answer,
                SignalForm.OneShot);

            Assert.That(intent.HasWorldPosition, Is.False);
            Assert.That(float.IsPositiveInfinity(intent.DistanceMeters), Is.True);
            Assert.That(intent.ReferentFeatureId, Is.EqualTo(0L));
        }

        [Test]
        public void SustainedIntent_KeepsItsForm()
        {
            var intent = SignalIntent.Positional("probe.hit", SignalChannel.Answer,
                SignalForm.Sustained, Vector3.zero, 2f);

            Assert.That(intent.Form, Is.EqualTo(SignalForm.Sustained));
        }

        [Test]
        public void BlankSignalId_IsNotValid()
        {
            var intent = SignalIntent.NonPositional("   ", SignalChannel.State,
                SignalForm.OneShot);

            Assert.That(intent.IsValid, Is.False);
        }

        [Test]
        public void SignalId_IsTrimmed()
        {
            var intent = SignalIntent.NonPositional("  probe.miss  ", SignalChannel.Answer,
                SignalForm.OneShot);

            Assert.That(intent.SignalId, Is.EqualTo("probe.miss"));
        }
    }
}
