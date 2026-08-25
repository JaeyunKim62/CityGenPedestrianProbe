using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// Steers the body by how the phone is held: landscape is north, a quarter
    /// turn anticlockwise is west.
    /// </summary>
    /// <remarks>
    /// <para>Device access only. Every decision lives in
    /// <see cref="RollSteering"/>, which has no Unity input dependency and can
    /// therefore be checked without a phone in hand — the same split as
    /// <see cref="TouchProbeInputSource"/> and <see cref="BlindTouchGestures"/>,
    /// and for the same reason: none of this is diagnosable on a black screen.
    /// </para>
    ///
    /// <para>Off until asked for. While it reads, it owns the body's bearing
    /// outright and a thumb cannot turn against it, so it must not be something
    /// a session acquires by being held on a handset.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DeviceTiltInputSource : MonoBehaviour, IProbeInputSource
    {
        private readonly RollSteering steering = new RollSteering();
        private bool enabledByOperator;
        private bool warnedAboutMissingSensor;

        public string SourceId => "device-tilt";

        /// <summary>Whether the operator has switched tilt steering on.</summary>
        public bool SteeringEnabled
        {
            get => enabledByOperator;
            set
            {
                if (enabledByOperator == value)
                    return;
                enabledByOperator = value;
                steering.Reset();
                if (value)
                    EnableSensor();
            }
        }

        /// <summary>True when the phone is held steeply enough to read a bearing.</summary>
        public bool IsPostureValid => steering.IsPostureValid;

        /// <summary>Whether this machine can supply gravity at all.</summary>
        public bool HasSensor => ReadGravity(out _);

        public bool IsPresent => enabledByOperator;

        public ProbeInputFrame Read(float deltaSeconds, float currentSweepAngle,
            float currentRangeMeters)
        {
            if (!enabledByOperator || !ReadGravity(out Vector3 gravity))
                return ProbeInputFrame.Empty;

            if (!steering.TryGetBearing(gravity, deltaSeconds, out float bearing))
                return ProbeInputFrame.Empty;

            return new ProbeInputFrame(Vector2.zero, Vector2.zero, false,
                float.NaN, 0f, float.NaN, false, false, false,
                headingAbsolute: bearing);
        }

        private void EnableSensor()
        {
#if ENABLE_INPUT_SYSTEM
            // Sensors arrive disabled and cost battery, so Unity leaves them off
            // until something says it wants one.
            if (GravitySensor.current != null && !GravitySensor.current.enabled)
                InputSystem.EnableDevice(GravitySensor.current);
            else if (GravitySensor.current == null && Accelerometer.current != null &&
                     !Accelerometer.current.enabled)
                InputSystem.EnableDevice(Accelerometer.current);
#endif
        }

        /// <summary>
        /// Gravity in the screen's own frame: x across, y up the screen.
        /// </summary>
        /// <remarks>
        /// The gravity sensor is preferred because the accelerometer also
        /// carries whatever the hand is doing, and a walking arm swing would
        /// read as steering. The accelerometer is the fallback for a handset
        /// that reports no fused gravity, where the filter in RollSteering has
        /// to absorb the difference.
        /// </remarks>
        private bool ReadGravity(out Vector3 gravity)
        {
            gravity = Vector3.zero;

#if UNITY_EDITOR
            if (SimulatedTilt.TryRead(out gravity))
                return true;
#endif

#if ENABLE_INPUT_SYSTEM
            if (GravitySensor.current != null)
            {
                gravity = GravitySensor.current.gravity.ReadValue();
                return gravity.sqrMagnitude > 1e-6f;
            }
            if (Accelerometer.current != null)
            {
                // The accelerometer reports the force on the case, which points
                // opposite to gravity while the phone is merely being held.
                gravity = -Accelerometer.current.acceleration.ReadValue();
                return gravity.sqrMagnitude > 1e-6f;
            }
#endif

            if (!warnedAboutMissingSensor)
            {
                warnedAboutMissingSensor = true;
                Debug.LogWarning("Tilt steering is on but this device reports no " +
                                 "gravity or accelerometer, so the body keeps being " +
                                 "turned the way it was.", this);
            }
            return false;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// A pretend phone, so tilt steering can be seen without building an APK.
    /// </summary>
    /// <remarks>
    /// Hold Z to roll the imagined handset anticlockwise and X to roll it back;
    /// F lays it flat, which is how the posture warning gets exercised. Editor
    /// only, and it yields to a real sensor being present, so it can never
    /// stand in for a reading on the device it is meant to imitate.
    /// </remarks>
    internal static class SimulatedTilt
    {
        private const float DegreesPerSecond = 90f;

        private static float rollDegrees;
        private static bool flat;

        internal static bool TryRead(out Vector3 gravity)
        {
            gravity = Vector3.zero;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            // A handset actually attached wins; this exists for the desktop.
            if (GravitySensor.current != null || Accelerometer.current != null)
                return false;

            if (keyboard.fKey.wasPressedThisFrame)
                flat = !flat;
            if (keyboard.zKey.isPressed)
                rollDegrees += DegreesPerSecond * Time.unscaledDeltaTime;
            if (keyboard.xKey.isPressed)
                rollDegrees -= DegreesPerSecond * Time.unscaledDeltaTime;

            if (flat)
            {
                gravity = new Vector3(0f, 0f, 1f);
                return true;
            }

            float radians = rollDegrees * Mathf.Deg2Rad;
            gravity = new Vector3(-Mathf.Sin(radians), -Mathf.Cos(radians), 0f);
            return true;
#else
            return false;
#endif
        }
    }
#endif
}
