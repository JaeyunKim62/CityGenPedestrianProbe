using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    /// <summary>One touch as the gesture reader sees it.</summary>
    public readonly struct ProbeTouch
    {
        public readonly int Id;
        public readonly Vector2 Position;

        public ProbeTouch(int id, Vector2 position)
        {
            Id = id;
            Position = position;
        }
    }

    /// <summary>
    /// Turns touches into intent for a screen nobody can see.
    /// </summary>
    /// <remarks>
    /// Every control is relative to where the finger landed, because a
    /// participant working blind cannot aim at a fixed button. Distances are
    /// held in millimetres and converted with the screen's own pixel density,
    /// so the same thumb travel means the same thing on a different phone: a
    /// gesture measured in pixels would silently change the stimulus when the
    /// hardware changed.
    ///
    /// This type deliberately knows nothing about Touchscreen or MonoBehaviour.
    /// A gesture on a black screen cannot be debugged on the device, so the
    /// whole state machine has to be exercisable from an edit-mode test.
    /// </remarks>
    public sealed class BlindTouchGestures
    {
        // Full walking speed once the thumb has travelled this far.
        private const float StickDeflectionMm = 12f;

        // Full +/-90 degrees of sweep across this much thumb travel.
        private const float AimDeflectionMm = 30f;

        // Vertical thumb travel that buys one metre of range.
        private const float RangeMmPerMetre = 4f;

        // Turning at full deflection.
        private const float YawDegreesPerSecond = 90f;

        private const float DeadZoneMm = 1.5f;
        private const float TapSeconds = 0.35f;
        private const float TapSlackMm = 8f;

        private readonly float pixelsPerMm;
        private readonly float halfWidthPixels;

        private int leftId = -1;
        private int rightId = -1;
        private Vector2 leftOrigin;
        private Vector2 rightOrigin;
        private float rightBaseRange;

        private bool episodeActive;
        private float episodeStart;
        private int episodeMaxTouches;
        private Vector2 episodeOrigin;
        private float episodeDriftPixels;

        public BlindTouchGestures(float pixelsPerMillimetre, float screenWidthPixels)
        {
            pixelsPerMm = Mathf.Max(1f, pixelsPerMillimetre);
            halfWidthPixels = Mathf.Max(1f, screenWidthPixels) * 0.5f;
        }

        /// <summary>Highest finger count seen in the gesture now in progress.</summary>
        public int ActiveFingerCount => episodeMaxTouches;

        public ProbeInputFrame Resolve(IReadOnlyList<ProbeTouch> touches,
            float unscaledTime, float deltaSeconds, float currentRangeMeters)
        {
            int count = touches == null ? 0 : touches.Count;
            if (count == 0)
                return EndEpisode(unscaledTime);

            Vector2 centroid = Centroid(touches);
            if (!episodeActive)
            {
                episodeActive = true;
                episodeStart = unscaledTime;
                episodeMaxTouches = 0;
                episodeOrigin = centroid;
                episodeDriftPixels = 0f;
            }

            episodeMaxTouches = Mathf.Max(episodeMaxTouches, count);
            episodeDriftPixels = Mathf.Max(episodeDriftPixels,
                Vector2.Distance(centroid, episodeOrigin));

            // Two or more fingers is always a tap being formed, never a stick.
            if (count > 1)
            {
                leftId = -1;
                rightId = -1;
                return ProbeInputFrame.Empty;
            }

            // Which half a finger belongs to is settled where it lands and then
            // held for as long as it stays down. Deciding it afresh every frame
            // would hand the gesture to the other control the moment a thumb
            // aiming hard to one side dragged past the middle of the screen.
            ProbeTouch touch = touches[0];
            if (touch.Id == leftId)
                return ResolveWalk(touch, deltaSeconds);
            if (touch.Id == rightId)
                return ResolveAim(touch, currentRangeMeters);

            return touch.Position.x < halfWidthPixels
                ? ResolveWalk(touch, deltaSeconds)
                : ResolveAim(touch, currentRangeMeters);
        }

        private ProbeInputFrame ResolveWalk(ProbeTouch touch, float deltaSeconds)
        {
            if (leftId != touch.Id)
            {
                leftId = touch.Id;
                rightId = -1;
                leftOrigin = touch.Position;
            }

            Vector2 offsetMm = (touch.Position - leftOrigin) / pixelsPerMm;
            float forward = Deflection(offsetMm.y, StickDeflectionMm);
            float turn = Deflection(offsetMm.x, StickDeflectionMm);
            return new ProbeInputFrame(new Vector2(0f, forward),
                new Vector2(turn * YawDegreesPerSecond * deltaSeconds, 0f), false,
                float.NaN, 0f, float.NaN, false, false, false);
        }

        private ProbeInputFrame ResolveAim(ProbeTouch touch, float currentRangeMeters)
        {
            if (rightId != touch.Id)
            {
                rightId = touch.Id;
                leftId = -1;
                rightOrigin = touch.Position;
                rightBaseRange = currentRangeMeters;
            }

            Vector2 offsetMm = (touch.Position - rightOrigin) / pixelsPerMm;
            float angle = Deflection(offsetMm.x, AimDeflectionMm) * 90f;
            float range = rightBaseRange + offsetMm.y / RangeMmPerMetre;
            return new ProbeInputFrame(Vector2.zero, Vector2.zero, false,
                angle, 0f, range, false, false, false);
        }

        private ProbeInputFrame EndEpisode(float unscaledTime)
        {
            leftId = -1;
            rightId = -1;
            if (!episodeActive)
                return ProbeInputFrame.Empty;

            episodeActive = false;
            int fingers = episodeMaxTouches;
            episodeMaxTouches = 0;

            // A stick being released is not a tap: it either lingered or moved.
            bool quick = unscaledTime - episodeStart <= TapSeconds &&
                         episodeDriftPixels <= TapSlackMm * pixelsPerMm;
            if (!quick || fingers < 2)
                return ProbeInputFrame.Empty;

            return new ProbeInputFrame(Vector2.zero, Vector2.zero, false,
                float.NaN, 0f, float.NaN,
                centerSweep: fingers == 2,
                toggleSemanticAudio: fingers == 3,
                toggleBlind: fingers >= 4);
        }

        private static float Deflection(float offsetMm, float fullScaleMm)
        {
            float magnitude = Mathf.Abs(offsetMm);
            if (magnitude <= DeadZoneMm)
                return 0f;
            float scaled = (magnitude - DeadZoneMm) / Mathf.Max(0.01f,
                fullScaleMm - DeadZoneMm);
            return Mathf.Sign(offsetMm) * Mathf.Clamp01(scaled);
        }

        private static Vector2 Centroid(IReadOnlyList<ProbeTouch> touches)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < touches.Count; i++)
                sum += touches[i].Position;
            return sum / touches.Count;
        }
    }
}
