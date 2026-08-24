using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    /// <summary>
    /// The touch scheme drives a screen the participant cannot see, so it has
    /// to be proved here rather than by feel on a device.
    /// </summary>
    public sealed class BlindTouchGestureTests
    {
        // Ten pixels to the millimetre over a thousand-pixel screen, so the
        // halfway line sits at 500 and every distance below is easy to read.
        private const float PixelsPerMm = 10f;
        private const float ScreenWidth = 1000f;
        private const float StickFullScalePixels = 120f;   // 12 mm
        private const float AimFullScalePixels = 300f;     // 30 mm

        private BlindTouchGestures gestures;

        [SetUp]
        public void SetUp()
        {
            gestures = new BlindTouchGestures(PixelsPerMm, ScreenWidth);
        }

        private static List<ProbeTouch> One(float x, float y) =>
            new List<ProbeTouch> { new ProbeTouch(1, new Vector2(x, y)) };

        private static List<ProbeTouch> Many(int count)
        {
            var touches = new List<ProbeTouch>();
            for (int i = 0; i < count; i++)
                touches.Add(new ProbeTouch(i, new Vector2(200f + i * 30f, 500f)));
            return touches;
        }

        private static readonly List<ProbeTouch> None = new List<ProbeTouch>();

        [Test]
        public void AimingFullyRight_PointsTheSweepAtNinetyDegrees()
        {
            gestures.Resolve(One(600f, 500f), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(
                One(600f + AimFullScalePixels, 500f), 0.1f, 0.1f, 5f);

            Assert.That(frame.HasSweepAngle, Is.True);
            Assert.That(frame.SweepAngleAbsolute, Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void AimingFullyLeft_PointsTheSweepTheOtherWay()
        {
            gestures.Resolve(One(600f, 500f), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(
                One(600f - AimFullScalePixels, 500f), 0.1f, 0.1f, 5f);

            Assert.That(frame.SweepAngleAbsolute, Is.EqualTo(-90f).Within(0.01f));
        }

        [Test]
        public void AimingAcrossTheMiddleOfTheScreen_KeepsAiming()
        {
            // The thumb starts just right of centre and swings a long way left,
            // which carries it into the walking half. It must still be aiming.
            gestures.Resolve(One(520f, 500f), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(One(220f, 500f), 0.1f, 0.1f, 5f);

            Assert.That(frame.HasSweepAngle, Is.True);
            Assert.That(frame.SweepAngleAbsolute, Is.EqualTo(-90f).Within(0.01f));
            Assert.That(frame.Move.sqrMagnitude, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void AimingBeyondFullScale_StopsAtNinetyDegrees()
        {
            gestures.Resolve(One(600f, 500f), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(
                One(600f + AimFullScalePixels * 3f, 500f), 0.1f, 0.1f, 5f);

            Assert.That(frame.SweepAngleAbsolute, Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void TheAimIsRelativeToWhereTheThumbLanded()
        {
            // A thumb landing far to the right must not snap the sweep there.
            ProbeInputFrame frame = gestures.Resolve(One(950f, 500f), 0f, 0.1f, 5f);

            Assert.That(frame.SweepAngleAbsolute, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void DraggingUpOnTheRight_LengthensTheRange()
        {
            gestures.Resolve(One(600f, 500f), 0f, 0.1f, 5f);

            // Forty millimetres up, at four millimetres per metre, is ten metres.
            ProbeInputFrame frame = gestures.Resolve(One(600f, 900f), 0.1f, 0.1f, 5f);

            Assert.That(frame.HasRange, Is.True);
            Assert.That(frame.RangeAbsolute, Is.EqualTo(15f).Within(0.01f));
        }

        [Test]
        public void WalkingForward_ComesFromTheLeftHalf()
        {
            gestures.Resolve(One(200f, 500f), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(
                One(200f, 500f + StickFullScalePixels), 0.1f, 0.1f, 5f);

            Assert.That(frame.Move.y, Is.EqualTo(1f).Within(0.01f));
            Assert.That(frame.HasSweepAngle, Is.False);
        }

        [Test]
        public void DraggingSidewaysOnTheLeft_TurnsTheBody()
        {
            gestures.Resolve(One(200f, 500f), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(
                One(200f + StickFullScalePixels, 500f), 0.1f, 0.1f, 5f);

            // Ninety degrees a second, asked for across a tenth of a second.
            Assert.That(frame.LookDegrees.x, Is.EqualTo(9f).Within(0.01f));
            Assert.That(frame.Move.x, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void AThumbThatBarelyMoves_IsHeldStill()
        {
            gestures.Resolve(One(200f, 500f), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(One(210f, 505f), 0.1f, 0.1f, 5f);

            Assert.That(frame.Move.sqrMagnitude, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(frame.LookDegrees.sqrMagnitude, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void TwoFingerTap_RecentresTheSweep()
        {
            gestures.Resolve(Many(2), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(None, 0.1f, 0.1f, 5f);

            Assert.That(frame.CenterSweep, Is.True);
            Assert.That(frame.ToggleSemanticAudio, Is.False);
        }

        [Test]
        public void ThreeFingerTap_TogglesSemanticAudio()
        {
            gestures.Resolve(Many(3), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(None, 0.1f, 0.1f, 5f);

            Assert.That(frame.ToggleSemanticAudio, Is.True);
            Assert.That(frame.CenterSweep, Is.False);
        }

        [Test]
        public void FourFingerTap_TogglesBlind()
        {
            gestures.Resolve(Many(4), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(None, 0.1f, 0.1f, 5f);

            Assert.That(frame.ToggleBlind, Is.True);
        }

        [Test]
        public void MultipleFingers_DoNotAlsoWalkOrAim()
        {
            ProbeInputFrame frame = gestures.Resolve(Many(2), 0f, 0.1f, 5f);

            Assert.That(frame.Move.sqrMagnitude, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(frame.HasSweepAngle, Is.False);
        }

        [Test]
        public void ReleasingOneFinger_IsNotATap()
        {
            // Otherwise every time a thumb left the walking stick the sweep
            // would jump back to centre.
            gestures.Resolve(One(200f, 500f), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(None, 0.1f, 0.1f, 5f);

            Assert.That(frame.CenterSweep, Is.False);
            Assert.That(frame.ToggleSemanticAudio, Is.False);
            Assert.That(frame.ToggleBlind, Is.False);
        }

        [Test]
        public void FingersThatRestedTooLong_AreNotATap()
        {
            gestures.Resolve(Many(2), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(None, 2f, 0.1f, 5f);

            Assert.That(frame.CenterSweep, Is.False);
        }

        [Test]
        public void FingersThatTravelled_AreNotATap()
        {
            gestures.Resolve(Many(2), 0f, 0.1f, 5f);
            gestures.Resolve(new List<ProbeTouch>
            {
                new ProbeTouch(0, new Vector2(600f, 500f)),
                new ProbeTouch(1, new Vector2(630f, 500f))
            }, 0.1f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(None, 0.2f, 0.1f, 5f);

            Assert.That(frame.CenterSweep, Is.False);
        }
    }
}
