using UnityEngine;

namespace CityGenPedestrianProbe
{
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

        /// <summary>Bearing folded into 0..360.</summary>
        public static float Wrap360(float degrees) => Mathf.Repeat(degrees, 360f);

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
