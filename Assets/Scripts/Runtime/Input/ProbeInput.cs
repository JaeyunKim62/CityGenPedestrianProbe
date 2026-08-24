using UnityEngine;

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// One frame of intent, already resolved out of whatever device produced
    /// it. Nothing downstream of this struct knows whether a thumb, a key or a
    /// stick asked for it.
    /// </summary>
    public readonly struct ProbeInputFrame
    {
        /// <summary>Forward in y, unused x, each in -1..1.</summary>
        public readonly Vector2 Move;

        /// <summary>Degrees to turn this frame: yaw in x, pitch in y.</summary>
        public readonly Vector2 LookDegrees;

        public readonly bool Fast;

        /// <summary>Where to point the sweep, or NaN for "no instruction".</summary>
        public readonly float SweepAngleAbsolute;

        /// <summary>Degrees per second to swing the sweep by.</summary>
        public readonly float SweepAngleRate;

        /// <summary>Range to hold, or NaN for "no instruction".</summary>
        public readonly float RangeAbsolute;

        public readonly bool CenterSweep;
        public readonly bool ToggleSemanticAudio;
        public readonly bool ToggleBlind;

        public ProbeInputFrame(Vector2 move, Vector2 lookDegrees, bool fast,
            float sweepAngleAbsolute, float sweepAngleRate, float rangeAbsolute,
            bool centerSweep, bool toggleSemanticAudio, bool toggleBlind)
        {
            Move = move;
            LookDegrees = lookDegrees;
            Fast = fast;
            SweepAngleAbsolute = sweepAngleAbsolute;
            SweepAngleRate = sweepAngleRate;
            RangeAbsolute = rangeAbsolute;
            CenterSweep = centerSweep;
            ToggleSemanticAudio = toggleSemanticAudio;
            ToggleBlind = toggleBlind;
        }

        public static ProbeInputFrame Empty => new ProbeInputFrame(Vector2.zero,
            Vector2.zero, false, float.NaN, 0f, float.NaN, false, false, false);

        public bool HasSweepAngle => !float.IsNaN(SweepAngleAbsolute);
        public bool HasRange => !float.IsNaN(RangeAbsolute);

        /// <summary>
        /// Combines two sources for a frame. Buttons latch on, axes add, and an
        /// absolute instruction wins over a later one so that a thumb already
        /// holding an angle is not fought by a second device.
        /// </summary>
        public ProbeInputFrame MergedWith(in ProbeInputFrame other)
        {
            Vector2 move = Move + other.Move;
            if (move.sqrMagnitude > 1f)
                move.Normalize();
            return new ProbeInputFrame(move, LookDegrees + other.LookDegrees,
                Fast || other.Fast,
                HasSweepAngle ? SweepAngleAbsolute : other.SweepAngleAbsolute,
                SweepAngleRate + other.SweepAngleRate,
                HasRange ? RangeAbsolute : other.RangeAbsolute,
                CenterSweep || other.CenterSweep,
                ToggleSemanticAudio || other.ToggleSemanticAudio,
                ToggleBlind || other.ToggleBlind);
        }
    }

    /// <summary>A device that can be asked what the person wants this frame.</summary>
    public interface IProbeInputSource
    {
        /// <summary>Stable name, recorded so a session says how it was driven.</summary>
        string SourceId { get; }

        /// <summary>False when the device is absent, so it can be skipped.</summary>
        bool IsPresent { get; }

        ProbeInputFrame Read(float deltaSeconds, float currentSweepAngle,
            float currentRangeMeters);
    }

    /// <summary>
    /// Lets something outside the controller drive walking and turning. With no
    /// source set the controller keeps reading the keyboard and mouse itself,
    /// so a desktop session behaves exactly as it did before this existed.
    /// </summary>
    public interface IPedestrianMoveSource
    {
        void ReadMovement(out Vector2 movement, out bool fast, out Vector2 lookDegrees);
    }
}
