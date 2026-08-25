using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class ProbeHeadingTests
    {
        private ProbeHeading heading;

        [SetUp]
        public void SetUp()
        {
            heading = new ProbeHeading();
        }

        [Test]
        public void Bearing_StartsAtNorth()
        {
            Assert.That(heading.BearingDegrees, Is.EqualTo(0f).Within(0.001f));
            Assert.That(ProbeHeading.CompassName(heading.BearingDegrees), Is.EqualTo("N"));
        }

        [Test]
        public void TurnBy_AccumulatesFromWhereItAlreadyIs()
        {
            heading.TurnBy(30f);
            heading.TurnBy(15f);

            Assert.That(heading.BearingDegrees, Is.EqualTo(45f).Within(0.001f));
        }

        [Test]
        public void TurnBy_WrapsPastNorthInBothDirections()
        {
            heading.SetBearing(350f);
            heading.TurnBy(20f);
            Assert.That(heading.BearingDegrees, Is.EqualTo(10f).Within(0.001f));

            heading.SetBearing(10f);
            heading.TurnBy(-20f);
            Assert.That(heading.BearingDegrees, Is.EqualTo(350f).Within(0.001f));
        }

        [Test]
        public void SetBearing_ReplacesRatherThanAccumulates()
        {
            heading.SetBearing(120f);
            heading.SetBearing(120f);

            Assert.That(heading.BearingDegrees, Is.EqualTo(120f).Within(0.001f),
                "an absolute instruction repeated must not turn the body twice");
        }

        // The mapping the device mode is built on: the phone reports an
        // anticlockwise quarter turn, that reaches the model as a negative
        // bearing, and it has to land on west rather than on -90 or on east.
        // A wrong sign here is invisible on a blind screen — it shows up only
        // as a participant who cannot find their way — so it is pinned before
        // any sensor exists to produce it.
        [Test]
        public void SetBearing_FoldsNegativeDegreesOntoTheCompass()
        {
            heading.SetBearing(-90f);

            Assert.That(heading.BearingDegrees, Is.EqualTo(270f).Within(0.001f));
            Assert.That(ProbeHeading.CompassName(heading.BearingDegrees), Is.EqualTo("W"));
        }

        // Mathf.Repeat rounds up to exactly 360 for inputs a hair below zero,
        // which would put a bearing outside the range this promises and write
        // the same direction into a session file as both 0.00 and 360.00.
        [TestCase(-1e-7f)]
        [TestCase(-1e-5f)]
        [TestCase(360f)]
        [TestCase(-360f)]
        [TestCase(719.9999f)]
        public void Bearing_NeverLandsOnAFullTurn(float degrees)
        {
            heading.SetBearing(degrees);

            Assert.That(heading.BearingDegrees, Is.GreaterThanOrEqualTo(0f));
            Assert.That(heading.BearingDegrees, Is.LessThan(360f));
        }

        [Test]
        public void SetBearing_IgnoresAMeasurementThatIsNotThere()
        {
            heading.SetBearing(120f);

            heading.SetBearing(float.NaN);
            heading.SetBearing(float.PositiveInfinity);

            Assert.That(heading.BearingDegrees, Is.EqualTo(120f).Within(0.001f),
                "a phone laid flat stops producing an angle, and the last good " +
                "bearing is the right answer to that rather than NaN");
        }

        [Test]
        public void GazeBearing_IsTheBodyPlusWhereAttentionSits()
        {
            heading.SetBearing(100f);
            heading.SetGazeOffset(45f);

            Assert.That(heading.GazeOffsetDegrees, Is.EqualTo(45f).Within(0.001f));
            Assert.That(heading.GazeBearingDegrees, Is.EqualTo(145f).Within(0.001f));
        }

        [Test]
        public void GazeBearing_WrapsWhenAttentionCrossesNorth()
        {
            heading.SetBearing(350f);
            heading.SetGazeOffset(30f);

            Assert.That(heading.GazeBearingDegrees, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void GazeOffset_IsSignedRatherThanFoldedInto0To360()
        {
            heading.SetGazeOffset(270f);

            Assert.That(heading.GazeOffsetDegrees, Is.EqualTo(-90f).Within(0.001f),
                "offset is read as 'how far off the body', so it has to keep its sign");
        }

        [Test]
        public void TurningTheBody_CarriesAttentionWithIt()
        {
            heading.SetBearing(0f);
            heading.SetGazeOffset(60f);

            heading.TurnBy(90f);

            Assert.That(heading.BearingDegrees, Is.EqualTo(90f).Within(0.001f));
            Assert.That(heading.GazeOffsetDegrees, Is.EqualTo(60f).Within(0.001f),
                "the sweep holds an angle relative to the body, not to the world");
            Assert.That(heading.GazeBearingDegrees, Is.EqualTo(150f).Within(0.001f));
        }

        [Test]
        public void MovementFrame_StartsOnTheBodySoNothingChangesUntilItIsAsked()
        {
            heading.SetBearing(120f);

            Assert.That(heading.MovementFrame, Is.EqualTo(ProbeMovementFrame.Body));
            Assert.That(heading.MovementBearingDegrees, Is.EqualTo(120f).Within(0.001f));
        }

        [Test]
        public void BodyFrame_WalksWhereverTheBodyIsPointed()
        {
            heading.MovementFrame = ProbeMovementFrame.Body;

            heading.SetBearing(90f);
            Assert.That(heading.MovementBearingDegrees, Is.EqualTo(90f).Within(0.001f));
            heading.SetBearing(200f);
            Assert.That(heading.MovementBearingDegrees, Is.EqualTo(200f).Within(0.001f));
        }

        [Test]
        public void CompassFrame_WalksNorthWhicheverWayTheBodyIsPointed()
        {
            heading.MovementFrame = ProbeMovementFrame.World;

            heading.SetBearing(90f);
            Assert.That(heading.MovementBearingDegrees, Is.EqualTo(0f).Within(0.001f));
            heading.SetBearing(270f);
            Assert.That(heading.MovementBearingDegrees, Is.EqualTo(0f).Within(0.001f));
        }

        // The point of the compass frame: the body keeps pointing where it was
        // pointed, so the sweep can hold something to one side while the person
        // walks past it. Travel direction and attention direction come apart.
        [Test]
        public void CompassFrame_LeavesTheBodyAndAttentionWhereTheyWere()
        {
            heading.SetBearing(90f);
            heading.SetGazeOffset(30f);

            heading.MovementFrame = ProbeMovementFrame.World;

            Assert.That(heading.BearingDegrees, Is.EqualTo(90f).Within(0.001f));
            Assert.That(heading.GazeBearingDegrees, Is.EqualTo(120f).Within(0.001f));
            Assert.That(heading.MovementBearingDegrees, Is.EqualTo(0f).Within(0.001f));
        }

        [TestCase(0f, "N")]
        [TestCase(22.4f, "N")]
        [TestCase(22.6f, "NE")]
        [TestCase(90f, "E")]
        [TestCase(180f, "S")]
        [TestCase(270f, "W")]
        [TestCase(359f, "N")]
        public void CompassName_NamesEachSector(float bearing, string expected)
        {
            Assert.That(ProbeHeading.CompassName(bearing), Is.EqualTo(expected));
        }
    }
}
