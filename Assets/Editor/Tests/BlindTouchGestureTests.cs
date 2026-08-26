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

        // Tilt steering leaves this half with one thing to say. Deflection stops
        // meaning anything, because a thumb cannot hold a measured travel while
        // the other hand rolls the handset like a wheel.

        [Test]
        public void Held_WalksAtOneSpeedWhereverTheThumbLanded()
        {
            gestures.WalkStyle = TouchWalkStyle.Hold;

            ProbeInputFrame justDown = gestures.Resolve(One(200f, 500f), 0f, 0.016f, 4f);
            ProbeInputFrame pushedUp = gestures.Resolve(One(200f, 500f + StickFullScalePixels),
                0.016f, 0.016f, 4f);

            Assert.That(justDown.Move.y, Is.EqualTo(1f).Within(0.001f),
                "touching at all is the whole instruction");
            Assert.That(pushedUp.Move.y, Is.EqualTo(1f).Within(0.001f),
                "and pushing further asks for nothing more");
        }

        [Test]
        public void Held_DoesNotTurnBecauseTheHandsetIsDoingThat()
        {
            gestures.WalkStyle = TouchWalkStyle.Hold;

            gestures.Resolve(One(200f, 500f), 0f, 0.016f, 4f);
            ProbeInputFrame dragged = gestures.Resolve(
                One(200f + StickFullScalePixels, 500f), 0.016f, 0.016f, 4f);

            Assert.That(dragged.LookDegrees, Is.EqualTo(Vector2.zero));
            Assert.That(dragged.Move.y, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Held_StopsTheMomentTheThumbLifts()
        {
            gestures.WalkStyle = TouchWalkStyle.Hold;
            gestures.Resolve(One(200f, 500f), 0f, 0.016f, 4f);

            ProbeInputFrame lifted = gestures.Resolve(None, 0.1f, 0.016f, 4f);

            Assert.That(lifted.Move, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Held_LeavesTheAimingHalfAlone()
        {
            gestures.WalkStyle = TouchWalkStyle.Hold;

            var both = new List<ProbeTouch>
            {
                new ProbeTouch(1, new Vector2(200f, 500f)),
                new ProbeTouch(2, new Vector2(800f, 500f))
            };
            gestures.Resolve(both, 0f, 0.016f, 4f);
            both[1] = new ProbeTouch(2, new Vector2(800f + AimFullScalePixels, 500f));
            ProbeInputFrame aimed = gestures.Resolve(both, 0.016f, 0.016f, 4f);

            Assert.That(aimed.Move.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(aimed.HasSweepAngle, Is.True);
            Assert.That(aimed.SweepAngleAbsolute, Is.EqualTo(90f).Within(0.5f),
                "walking and aiming still happen at once");
        }

        [Test]
        public void SwitchingStyleMidGestureDoesNotDropTheFingerAlreadyDown()
        {
            gestures.Resolve(One(200f, 500f), 0f, 0.016f, 4f);

            gestures.WalkStyle = TouchWalkStyle.Hold;
            ProbeInputFrame afterSwitch = gestures.Resolve(One(200f, 500f), 0.016f,
                0.016f, 4f);

            Assert.That(afterSwitch.Move.y, Is.EqualTo(1f).Within(0.001f));
        }

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
        public void ThreeFingerTap_RecentresTheSweep()
        {
            gestures.Resolve(Many(3), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(None, 0.1f, 0.1f, 5f);

            Assert.That(frame.CenterSweep, Is.True);
            Assert.That(frame.ToggleSemanticAudio, Is.False);
        }

        [Test]
        public void FourFingerTap_TogglesSemanticAudio()
        {
            gestures.Resolve(Many(4), 0f, 0.1f, 5f);

            ProbeInputFrame frame = gestures.Resolve(None, 0.1f, 0.1f, 5f);

            Assert.That(frame.ToggleSemanticAudio, Is.True);
            Assert.That(frame.CenterSweep, Is.False);
        }

        [Test]
        public void NoTapCountTogglesBlind()
        {
            // Blind lives on the settings menu. Reaching it by tap would have
            // meant a five-finger one, which nobody can perform while holding a
            // phone, and leaving the condition is a decision that should take a
            // deliberate press rather than a slip of the hand.
            for (int fingers = 2; fingers <= 5; fingers++)
            {
                gestures.Resolve(Many(fingers), 0f, 0.1f, 5f);

                ProbeInputFrame frame = gestures.Resolve(None, 0.1f, 0.1f, 5f);

                Assert.That(frame.ToggleBlind, Is.False,
                    $"a {fingers}-finger tap must not leave the blind condition");
            }
        }

        [Test]
        public void ThreeFingers_DoNotAlsoWalkOrAim()
        {
            ProbeInputFrame frame = gestures.Resolve(Many(3), 0f, 0.1f, 5f);

            Assert.That(frame.Move.sqrMagnitude, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(frame.HasSweepAngle, Is.False);
        }

        [Test]
        public void TwoThumbs_WalkAndAimAtTheSameTime()
        {
            // The movement the study is about is sweeping while walking. Making
            // the two take turns would have measured the control scheme.
            var down = new List<ProbeTouch>
            {
                new ProbeTouch(1, new Vector2(200f, 500f)),
                new ProbeTouch(2, new Vector2(700f, 500f))
            };
            gestures.Resolve(down, 0f, 0.1f, 5f);

            var moved = new List<ProbeTouch>
            {
                new ProbeTouch(1, new Vector2(200f, 500f + StickFullScalePixels)),
                new ProbeTouch(2, new Vector2(700f + AimFullScalePixels, 500f))
            };
            ProbeInputFrame frame = gestures.Resolve(moved, 0.1f, 0.1f, 5f);

            Assert.That(frame.Move.y, Is.EqualTo(1f).Within(0.01f),
                "the left thumb should be walking forward");
            Assert.That(frame.HasSweepAngle, Is.True,
                "the right thumb should be aiming at the same time");
            Assert.That(frame.SweepAngleAbsolute, Is.EqualTo(90f).Within(0.5f));
        }

        [Test]
        public void ASecondFingerInTheSameHalf_DoesNotStealTheStick()
        {
            gestures.Resolve(One(200f, 500f), 0f, 0.1f, 5f);

            var crowded = new List<ProbeTouch>
            {
                new ProbeTouch(1, new Vector2(200f, 500f + StickFullScalePixels)),
                new ProbeTouch(2, new Vector2(300f, 500f))
            };
            ProbeInputFrame frame = gestures.Resolve(crowded, 0.1f, 0.1f, 5f);

            Assert.That(frame.Move.y, Is.EqualTo(1f).Within(0.01f),
                "the thumb already walking keeps its origin");
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
