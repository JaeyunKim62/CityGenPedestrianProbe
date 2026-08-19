using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class SignalArbiterTests
    {
        private SignalArbiter arbiter;

        [SetUp]
        public void SetUp()
        {
            arbiter = new SignalArbiter(new SignalArbitrationSettings(0.12f, 0.4f, 2f));
        }

        [Test]
        public void Admit_RejectsIntentWithoutSignalId()
        {
            var intent = SignalIntent.NonPositional("   ", SignalChannel.Transition);

            Assert.That(arbiter.Admit(intent, 0f),
                Is.EqualTo(SignalAdmission.RejectedInvalid));
        }

        [Test]
        public void Admit_AcceptsFirstSignal()
        {
            Assert.That(arbiter.Admit(Intent(SignalChannel.Proximity), 0f),
                Is.EqualTo(SignalAdmission.Admitted));
        }

        [Test]
        public void Admit_RejectsSameChannelInsideMinimumGap()
        {
            arbiter.Admit(Intent(SignalChannel.Proximity), 0f);

            Assert.That(arbiter.Admit(Intent(SignalChannel.Proximity), 0.05f),
                Is.EqualTo(SignalAdmission.RejectedTooSoon));
        }

        [Test]
        public void Admit_RejectsLowerChannelInsideMinimumGap()
        {
            arbiter.Admit(Intent(SignalChannel.Transition), 0f);

            Assert.That(arbiter.Admit(Intent(SignalChannel.Proximity), 0.05f),
                Is.EqualTo(SignalAdmission.RejectedTooSoon));
        }

        [Test]
        public void Admit_LetsHigherChannelPreemptInsideMinimumGap()
        {
            arbiter.Admit(Intent(SignalChannel.Proximity), 0f);

            Assert.That(arbiter.Admit(Intent(SignalChannel.Alarm), 0.05f),
                Is.EqualTo(SignalAdmission.Admitted));
        }

        [Test]
        public void Admit_RejectedSignalDoesNotExtendMinimumGap()
        {
            arbiter.Admit(Intent(SignalChannel.Proximity), 0f);
            arbiter.Admit(Intent(SignalChannel.Proximity), 0.05f);

            Assert.That(arbiter.Admit(Intent(SignalChannel.Proximity), 0.13f),
                Is.EqualTo(SignalAdmission.Admitted));
        }

        [Test]
        public void Admit_SuppressesLowerChannelsWhileAlarmWindowIsOpen()
        {
            arbiter.Admit(Intent(SignalChannel.Alarm), 0f);

            Assert.That(arbiter.Admit(Intent(SignalChannel.Answer), 0.2f),
                Is.EqualTo(SignalAdmission.RejectedByAlarm));
        }

        [Test]
        public void Admit_AcceptsLowerChannelAfterAlarmWindowCloses()
        {
            arbiter.Admit(Intent(SignalChannel.Alarm), 0f);

            Assert.That(arbiter.Admit(Intent(SignalChannel.Answer), 0.5f),
                Is.EqualTo(SignalAdmission.Admitted));
        }

        [Test]
        public void Admit_AllowsAlarmInsideItsOwnSuppressionWindow()
        {
            arbiter.Admit(Intent(SignalChannel.Alarm), 0f);

            Assert.That(arbiter.Admit(Intent(SignalChannel.Alarm), 0.2f),
                Is.EqualTo(SignalAdmission.Admitted));
        }

        [Test]
        public void Admit_HoldsStateChannelUntilTheSceneIsQuiet()
        {
            arbiter.Admit(Intent(SignalChannel.Transition), 0f);

            Assert.That(arbiter.Admit(Intent(SignalChannel.State), 1f),
                Is.EqualTo(SignalAdmission.RejectedStateNotQuiet));
            Assert.That(arbiter.Admit(Intent(SignalChannel.State), 2f),
                Is.EqualTo(SignalAdmission.Admitted));
        }

        [Test]
        public void Reset_ClearsGapAndSuppressionHistory()
        {
            arbiter.Admit(Intent(SignalChannel.Alarm), 0f);

            arbiter.Reset();

            Assert.That(arbiter.Admit(Intent(SignalChannel.State), 0.05f),
                Is.EqualTo(SignalAdmission.Admitted));
        }

        [Test]
        public void Intent_KeepsWorldPositionAndDistance()
        {
            var intent = new SignalIntent("region.curb.answer", SignalChannel.Answer,
                new Vector3(1f, 0f, 2f), 3.5f);

            Assert.That(intent.IsValid, Is.True);
            Assert.That(intent.HasWorldPosition, Is.True);
            Assert.That(intent.WorldPosition, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(intent.DistanceMeters, Is.EqualTo(3.5f));
        }

        [Test]
        public void NonPositionalIntent_ReportsNoWorldPosition()
        {
            var intent = SignalIntent.NonPositional("probe.miss", SignalChannel.Answer);

            Assert.That(intent.HasWorldPosition, Is.False);
            Assert.That(float.IsPositiveInfinity(intent.DistanceMeters), Is.True);
        }

        private static SignalIntent Intent(SignalChannel channel) =>
            SignalIntent.NonPositional($"test.{channel}", channel);
    }
}
