using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class RollSteeringTests
    {
        // Long enough that the filter has settled, so each case reads the
        // attitude it was handed rather than a blend with the one before.
        private const float Settled = 1f;

        private RollSteering steering;

        [SetUp]
        public void SetUp()
        {
            steering = new RollSteering();
        }

        /// <summary>Gravity as a phone rolled this far anticlockwise reports it.</summary>
        private static Vector3 RolledAnticlockwise(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Sin(radians), -Mathf.Cos(radians), 0f);
        }

        private float BearingAt(float rollDegrees)
        {
            steering.Reset();
            Assert.That(steering.TryGetBearing(RolledAnticlockwise(rollDegrees), Settled,
                out float bearing), Is.True, $"roll {rollDegrees} should be readable");
            return bearing;
        }

        // The whole convention, pinned. A sign error here is invisible on a
        // blind screen: it surfaces only as a participant who cannot find their
        // way, by which point it is indistinguishable from the task being hard.
        [TestCase(0f, 0f, "N")]
        [TestCase(90f, 270f, "W")]
        [TestCase(180f, 180f, "S")]
        [TestCase(270f, 90f, "E")]
        public void LandscapeIsNorthAndAnticlockwiseGoesWest(float roll,
            float expectedBearing, string expectedCompass)
        {
            float bearing = BearingAt(roll);

            Assert.That(Mathf.DeltaAngle(bearing, expectedBearing),
                Is.EqualTo(0f).Within(0.01f));
            Assert.That(ProbeHeading.CompassName(bearing), Is.EqualTo(expectedCompass));
        }

        [Test]
        public void BearingAlwaysComesBackOnTheCompass()
        {
            for (float roll = -720f; roll <= 720f; roll += 7.5f)
            {
                float bearing = BearingAt(roll);
                Assert.That(bearing, Is.GreaterThanOrEqualTo(0f).And.LessThan(360f), $"{roll}");
            }
        }

        // No calibration, no neutral to capture, no accumulated error: the
        // reading is a fresh absolute measurement, so the same attitude has to
        // give the same bearing however it was reached.
        [Test]
        public void TheSameAttitudeGivesTheSameBearingHoweverItWasReached()
        {
            float direct = BearingAt(120f);

            steering.Reset();
            foreach (float roll in new[] { 0f, 45f, -80f, 300f, 120f })
                steering.TryGetBearing(RolledAnticlockwise(roll), Settled, out _);
            steering.TryGetBearing(RolledAnticlockwise(120f), Settled, out float wandered);

            Assert.That(Mathf.DeltaAngle(direct, wandered), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void SeveralFullTurnsLandWhereOneWould()
        {
            Assert.That(Mathf.DeltaAngle(BearingAt(90f), BearingAt(90f + 360f * 3f)),
                Is.EqualTo(0f).Within(0.01f));
        }

        // Screen flat to the sky: the twist has become a rotation about the
        // world's vertical, which gravity cannot see at all.
        [Test]
        public void APhoneLaidFlatHasNoBearingToGive()
        {
            Assert.That(steering.TryGetBearing(new Vector3(0f, 0f, -1f), Settled,
                out float bearing), Is.False);
            Assert.That(float.IsNaN(bearing), Is.True);
            Assert.That(steering.IsPostureValid, Is.False);
        }

        [Test]
        public void PostureIsCalledInvalidBeforeTheReadingGoesBad()
        {
            // Tipped back until only a sliver of gravity is left in the screen.
            float share = Mathf.Sin(10f * Mathf.Deg2Rad);
            var nearlyFlat = new Vector3(0f, -share, -Mathf.Cos(10f * Mathf.Deg2Rad));

            Assert.That(steering.TryGetBearing(nearlyFlat, Settled, out _), Is.False);
            Assert.That(steering.PlanarShare, Is.LessThan(RollSteering.MinPlanarShare));
        }

        [Test]
        public void HeldUprightThePostureIsValidAndFullyPlanar()
        {
            steering.TryGetBearing(RolledAnticlockwise(35f), Settled, out _);

            Assert.That(steering.IsPostureValid, Is.True);
            Assert.That(steering.PlanarShare, Is.EqualTo(1f).Within(0.001f));
        }

        // Coming back from flat must not need a re-calibration: there is no
        // state to have gone stale while the reading was unavailable.
        [Test]
        public void PickingThePhoneBackUpResumesOnTheRightBearing()
        {
            steering.TryGetBearing(RolledAnticlockwise(90f), Settled, out float before);
            steering.TryGetBearing(new Vector3(0f, 0f, -1f), Settled, out _);
            steering.TryGetBearing(RolledAnticlockwise(90f), Settled, out float after);

            Assert.That(Mathf.DeltaAngle(before, after), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void ReadingIsUnaffectedByHowStronglyGravityIsReported()
        {
            steering.TryGetBearing(RolledAnticlockwise(60f), Settled, out float unit);
            steering.Reset();
            steering.TryGetBearing(RolledAnticlockwise(60f) * 9.81f, Settled,
                out float metric);

            Assert.That(Mathf.DeltaAngle(unit, metric), Is.EqualTo(0f).Within(0.01f),
                "some devices report g and some report metres per second squared");
        }

        [Test]
        public void SmoothingHoldsBackASingleJoltedSample()
        {
            steering.TryGetBearing(RolledAnticlockwise(0f), Settled, out _);
            steering.TryGetBearing(RolledAnticlockwise(90f), 1f / 60f, out float jolted);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(jolted, 0f)), Is.LessThan(45f),
                "one frame of hand tremor must not swing the body a quarter turn");
        }
    }
}
