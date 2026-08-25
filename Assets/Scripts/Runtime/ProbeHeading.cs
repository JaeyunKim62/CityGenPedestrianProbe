using UnityEngine;

namespace CityGenPedestrianProbe
{
    /// <summary>Which direction a movement request is measured from.</summary>
    /// <remarks>
    /// The reference frame axis of the design space, applied to walking. Aiming
    /// is deliberately not on this list: the sweep is one feedback module among
    /// several, and letting movement resolve against it would leave walking
    /// undefined in any condition that module is switched off in.
    /// </remarks>
    public enum ProbeMovementFrame
    {
        /// <summary>Forward is wherever the body points.</summary>
        Body = 0,

        /// <summary>Forward is north, whichever way the body points.</summary>
        World = 1
    }

    /// <summary>
    /// Where a person is pointed, held as two quantities rather than one.
    /// </summary>
    /// <remarks>
    /// <para><b>Bearing</b> is the body: an absolute world direction, north at
    /// zero, and the direction walking moves in. <b>Gaze bearing</b> is where
    /// attention is pointed, which is the bearing plus whatever offset the
    /// sweep is holding.</para>
    ///
    /// <para>Both already existed before this type did, but only as side
    /// effects — the body as the player transform's yaw, attention as a sum
    /// computed inside the sweep and spent on one log line. Neither could be
    /// set, and a finished session recorded the first and not the second, so
    /// the question of whether a person navigates by the way their body faces
    /// or by the way their attention points could not be asked of the data at
    /// all. Naming them here is what makes that comparison possible later.</para>
    ///
    /// <para>Bearing is settable two ways on purpose. <see cref="TurnBy"/> is
    /// what a key or a thumb asks for — turn me a bit from wherever I am — and
    /// <see cref="SetBearing"/> is what a device that measures its own angle
    /// asks for. A phone rolled in the hand reports an absolute attitude, and
    /// funnelling that through repeated small turns would both accumulate
    /// error and throw away the one interesting thing about it, which is that
    /// it is absolute.</para>
    ///
    /// <para>Deliberately not a MonoBehaviour. The same reasoning as
    /// <see cref="BlindTouchGestures"/>: the arithmetic here is what a wrong
    /// sign or a bad wrap would corrupt, and that has to be provable from an
    /// edit-mode test rather than by walking a black screen on a phone.</para>
    /// </remarks>
    public sealed class ProbeHeading
    {
        private static readonly string[] CompassPoints =
            { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        private float bearingDegrees;
        private float gazeOffsetDegrees;

        /// <summary>Body direction in world degrees, north at 0, east at 90.</summary>
        public float BearingDegrees => bearingDegrees;

        /// <summary>Where attention sits relative to the body, in -180..180.</summary>
        public float GazeOffsetDegrees => gazeOffsetDegrees;

        /// <summary>Attention as an absolute world direction.</summary>
        public float GazeBearingDegrees => Wrap360(bearingDegrees + gazeOffsetDegrees);

        /// <summary>Which direction walking is measured from. Body by default.</summary>
        public ProbeMovementFrame MovementFrame { get; set; } = ProbeMovementFrame.Body;

        /// <summary>
        /// The direction a forward request travels in.
        /// </summary>
        /// <remarks>
        /// The whole of the frame choice is this one angle, which is the reason
        /// for choosing from the bearings already held here rather than storing
        /// a third. Walking in world coordinates does not turn the body, so a
        /// person can hold the sweep on something to the east while walking
        /// north — travel direction and attention direction come apart, which
        /// is the condition this frame exists to create.
        /// </remarks>
        public float MovementBearingDegrees =>
            MovementFrame == ProbeMovementFrame.World ? 0f : bearingDegrees;

        /// <summary>Points the body at an absolute world direction.</summary>
        /// <remarks>
        /// Non-finite input is dropped rather than stored. A device deriving an
        /// angle from gravity produces NaN the moment gravity has no component
        /// left to measure — a phone laid flat — and one NaN written here would
        /// spread to the transform and end the session with a body pointed
        /// nowhere. Holding the last good bearing is the correct answer to a
        /// missing measurement.
        /// </remarks>
        public void SetBearing(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return;
            bearingDegrees = Wrap360(degrees);
        }

        /// <summary>Turns the body by a signed amount from where it now is.</summary>
        public void TurnBy(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return;
            bearingDegrees = Wrap360(bearingDegrees + degrees);
        }

        /// <summary>Moves attention relative to the body.</summary>
        public void SetGazeOffset(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return;
            gazeOffsetDegrees = Mathf.DeltaAngle(0f, degrees);
        }

        /// <summary>Bearing folded into 0 up to but not including 360.</summary>
        /// <remarks>
        /// Mathf.Repeat lands on exactly 360 for an input a hair below zero,
        /// because 360 minus a millionth of a degree is 360 once it is a float.
        /// Left alone, a bearing would occasionally leave here outside the
        /// range everything downstream was promised, and a session file would
        /// carry the same direction as 0.00 on one row and 360.00 on the next.
        /// </remarks>
        public static float Wrap360(float degrees)
        {
            float wrapped = Mathf.Repeat(degrees, 360f);
            return wrapped >= 360f ? 0f : wrapped;
        }

        /// <summary>
        /// One of eight compass points, so a heading can be read at a glance.
        /// </summary>
        /// <remarks>
        /// Cheap, and it is what makes a device that claims to map its own
        /// angle onto a world direction checkable by eye: turn the thing a
        /// quarter turn anticlockwise and the readout has to say W.
        /// </remarks>
        public static string CompassName(float bearingDegrees)
        {
            float bearing = Wrap360(bearingDegrees);
            return CompassPoints[Mathf.RoundToInt(bearing / 45f) % CompassPoints.Length];
        }
    }
}
