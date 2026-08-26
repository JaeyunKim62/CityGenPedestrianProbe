using UnityEngine;

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// Reads a world bearing off the way a phone is held.
    /// </summary>
    /// <remarks>
    /// <para>Held in landscape the body faces north; turned a quarter turn
    /// anticlockwise it faces west. The hand becomes the compass dial.</para>
    ///
    /// <para><b>There is no state to drift.</b> Gravity's direction within the
    /// screen plane is a fresh absolute measurement every frame, so the angle
    /// is the bearing — no calibration, no neutral to capture, no accumulated
    /// error, and nothing to unwrap. Laying the phone down and picking it up
    /// again lands on the right bearing because the phone's attitude is the
    /// only thing that was ever consulted. Only the smoothing carries state,
    /// and it is applied to the gravity vector rather than to the angle so
    /// that it cannot be confused by the wrap at north.</para>
    ///
    /// <para>The one thing gravity cannot answer is a phone lying flat: with
    /// the screen facing the sky there is no gravity left in the screen plane
    /// to take a direction from, and the same physical twist has become a
    /// rotation about the world's vertical, which only a gyro or a compass
    /// could see. Rather than invent a bearing there, this reports the posture
    /// invalid and lets the caller hold the last one.</para>
    ///
    /// <para>No Unity input here on purpose. A sign error in the mapping is
    /// invisible on a blind screen — it shows up only as a participant who
    /// cannot find their way — so the arithmetic has to be provable from an
    /// edit-mode test.</para>
    /// </remarks>
    public sealed class RollSteering
    {
        /// <summary>
        /// How much of gravity has to lie in the screen plane. About 17 degrees
        /// off flat, below which the direction is more noise than signal.
        /// </summary>
        public const float MinPlanarShare = 0.3f;

        private const float SmoothingPerSecond = 14f;

        private Vector3 smoothedGravity;
        private bool hasSmoothed;

        /// <summary>Whether the phone was held steeply enough to read.</summary>
        public bool IsPostureValid { get; private set; }

        /// <summary>How much of gravity lay in the screen plane, 0 to 1.</summary>
        public float PlanarShare { get; private set; }

        /// <summary>
        /// The bearing the phone is pointing at, or NaN if it is too flat.
        /// </summary>
        public bool TryGetBearing(Vector3 gravity, float deltaSeconds,
            out float bearingDegrees)
        {
            Vector3 filtered = Smooth(gravity, deltaSeconds);

            float planar = new Vector2(filtered.x, filtered.y).magnitude;
            float total = filtered.magnitude;
            PlanarShare = total > 1e-4f ? planar / total : 0f;
            IsPostureValid = PlanarShare >= MinPlanarShare;
            if (!IsPostureValid)
            {
                bearingDegrees = float.NaN;
                return false;
            }

            // Gravity points down the screen when the phone is upright, so the
            // negated components put zero at that resting attitude. Anticlock-
            // wise from the holder's side is positive, and a bearing runs the
            // other way, hence the sign.
            float roll = Mathf.Atan2(-filtered.x, -filtered.y) * Mathf.Rad2Deg;
            bearingDegrees = ProbeHeading.Wrap360(-roll);
            return true;
        }

        /// <summary>Forgets the filter, for a fresh session or a mode change.</summary>
        public void Reset()
        {
            hasSmoothed = false;
            IsPostureValid = false;
            PlanarShare = 0f;
        }

        private Vector3 Smooth(Vector3 gravity, float deltaSeconds)
        {
            if (!hasSmoothed || deltaSeconds <= 0f)
            {
                hasSmoothed = true;
                smoothedGravity = gravity;
                return gravity;
            }

            float blend = 1f - Mathf.Exp(-SmoothingPerSecond * deltaSeconds);
            smoothedGravity = Vector3.Lerp(smoothedGravity, gravity, blend);
            return smoothedGravity;
        }
    }
}
