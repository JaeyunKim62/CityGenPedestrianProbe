using NUnit.Framework;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class StepCadenceTests
    {
        private const float Stride = 0.72f;

        private StepCadence cadence;

        [SetUp]
        public void SetUp()
        {
            cadence = new StepCadence(Stride);
        }

        [Test]
        public void StandingStill_NeverSteps()
        {
            for (int i = 0; i < 600; i++)
                Assert.That(cadence.TryAdvance(0f), Is.False);
        }

        // Walking into the study bounds clamps the position, so the distance
        // this is handed is zero however hard the person pushes. No step is
        // the honest answer: they are not getting anywhere.
        [Test]
        public void PushingAgainstSomethingImmovable_NeverSteps()
        {
            for (int i = 0; i < 600; i++)
                Assert.That(cadence.TryAdvance(0f), Is.False);
            Assert.That(cadence.TravelledMeters, Is.EqualTo(0f));
        }

        [Test]
        public void OneStrideOfTravel_IsOneStep()
        {
            int steps = 0;
            // 1.4 m/s at 60 fps, for one second.
            for (int i = 0; i < 60; i++)
            {
                if (cadence.TryAdvance(1.4f / 60f))
                    steps++;
            }

            Assert.That(steps, Is.EqualTo(1), "1.4 m in one second is a step and a bit");
            Assert.That(cadence.TravelledMeters, Is.EqualTo(1.4f - Stride).Within(0.01f));
        }

        [Test]
        public void CadenceMatchesDistanceOverALongWalk()
        {
            int steps = 0;
            for (int i = 0; i < 6000; i++)
            {
                if (cadence.TryAdvance(0.01f))
                    steps++;
            }

            // 60 m at 0.72 m a step.
            Assert.That(steps, Is.EqualTo(83));
        }

        [Test]
        public void WalkingFaster_StepsMoreOften()
        {
            int slow = CountSteps(new StepCadence(Stride), 1.4f / 60f, 60);
            int fast = CountSteps(new StepCadence(Stride), 2.5f / 60f, 60);

            Assert.That(fast, Is.GreaterThan(slow));
        }

        [Test]
        public void ASingleLongFrame_SoundsOneStepRatherThanQueueingSeveral()
        {
            Assert.That(cadence.TryAdvance(Stride * 2.5f), Is.True);
            Assert.That(cadence.TravelledMeters, Is.EqualTo(0f),
                "a hitch should be one step late, not a burst of them");
            Assert.That(cadence.TryAdvance(0f), Is.False);
        }

        [Test]
        public void ALeapNoWalkerCouldTake_IsNotWalking()
        {
            cadence.TryAdvance(Stride * 0.9f);

            Assert.That(cadence.TryAdvance(Stride * 10f), Is.False);
            Assert.That(cadence.TravelledMeters, Is.EqualTo(0f),
                "whatever moved the body that far, it was not legs");
        }

        [Test]
        public void Reset_DropsWhatWasBanked()
        {
            cadence.TryAdvance(Stride * 0.9f);
            cadence.Reset();

            Assert.That(cadence.TravelledMeters, Is.EqualTo(0f));
            Assert.That(cadence.TryAdvance(Stride * 0.5f), Is.False);
        }

        [Test]
        public void AShorterStride_StepsMoreOftenOverTheSameGround()
        {
            int longStride = CountSteps(new StepCadence(1.0f), 0.05f, 100);
            int shortStride = CountSteps(new StepCadence(0.5f), 0.05f, 100);

            Assert.That(longStride, Is.EqualTo(5));
            Assert.That(shortStride, Is.EqualTo(10));
        }

        private static int CountSteps(StepCadence subject, float perFrame, int frames)
        {
            int steps = 0;
            for (int i = 0; i < frames; i++)
            {
                if (subject.TryAdvance(perFrame))
                    steps++;
            }
            return steps;
        }
    }
}
