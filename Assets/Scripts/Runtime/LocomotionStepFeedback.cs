using UnityEngine;

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// Counts distance walked into footfalls.
    /// </summary>
    /// <remarks>
    /// Driven by distance rather than by time, so a step means a step: someone
    /// standing still makes none, and someone walked into a wall makes none
    /// either, because the study bounds stop the position from changing. A
    /// timer would have kept marching in both cases and told the person they
    /// were making progress they were not.
    ///
    /// Kept free of Unity so the cadence can be exercised without a scene.
    /// </remarks>
    public sealed class StepCadence
    {
        // Beyond this in one frame is not walking. Nothing in the runtime
        // teleports today, but a step burst is the loudest possible way to
        // find out that something started to.
        private const float JumpStrides = 4f;

        private readonly float strideMeters;
        private float travelled;

        public StepCadence(float stride)
        {
            strideMeters = Mathf.Max(0.1f, stride);
        }

        public float StrideMeters => strideMeters;

        /// <summary>Distance banked toward the next footfall.</summary>
        public float TravelledMeters => travelled;

        /// <summary>
        /// Adds one frame of travel and says whether a foot came down.
        /// </summary>
        /// <remarks>
        /// At most one per call. A frame that hitches long enough to cover two
        /// strides should sound like one step late, not like two steps at once,
        /// so the surplus is dropped rather than queued.
        /// </remarks>
        public bool TryAdvance(float distanceMeters)
        {
            if (!(distanceMeters > 0f))
                return false;
            if (distanceMeters > strideMeters * JumpStrides)
            {
                travelled = 0f;
                return false;
            }

            travelled += distanceMeters;
            if (travelled < strideMeters)
                return false;

            travelled -= strideMeters;
            if (travelled > strideMeters)
                travelled = 0f;
            return true;
        }

        public void Reset()
        {
            travelled = 0f;
        }
    }

    /// <summary>
    /// Sounds a footfall every stride the person actually covers.
    /// </summary>
    /// <remarks>
    /// <para>Walking blind through a simulation gives none of what walking
    /// normally gives: no jolt through the legs, no sound of one's own feet.
    /// Without them there is nothing to tell someone they are moving except
    /// the environment changing, which is exactly the thing the study is
    /// asking them to work out. This puts self-motion back on its own
    /// channel.</para>
    ///
    /// <para>Position is read off the listener rather than asked of the
    /// controller, which keeps the controller unaware this module exists and
    /// makes the boundary case come out right for free: walking into the study
    /// bounds clamps the position, the position does not change, and no step
    /// sounds.</para>
    ///
    /// <para>One signal, no left and right. Laterality would need panning at
    /// arm's length from the listener, where the spatialiser swings hard to
    /// one side, and the phone has one vibration motor and could not carry it
    /// anyway. Cadence is the part that says "you are moving".</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LocomotionStepFeedback : FeedbackModule
    {
        public const string StableModuleId = "locomotion.step";
        public const string StepSignalId = "locomotion.step";

        // An adult walking at 1.4 m/s takes a step roughly every 0.72 m, which
        // puts the cadence just under two a second.
        [SerializeField, Min(0.1f)] private float strideMeters = 0.72f;

        private Transform listener;
        private FeedbackSignalBus signalBus;
        private StepCadence cadence;
        private Vector3 lastPosition;
        private bool hasLastPosition;

        public override string ModuleId => StableModuleId;

        public override void Configure(in FeedbackContext context)
        {
            listener = context.Listener;
            signalBus = context.SignalBus;
            cadence = new StepCadence(strideMeters);
            hasLastPosition = false;
        }

        private void Update()
        {
            if (listener == null || signalBus == null || cadence == null)
                return;

            Vector3 position = listener.position;
            if (!hasLastPosition)
            {
                lastPosition = position;
                hasLastPosition = true;
                return;
            }

            // Height is dropped because the ground is sampled every frame, and
            // a slope would otherwise add distance the legs never travelled.
            Vector3 delta = position - lastPosition;
            delta.y = 0f;
            lastPosition = position;

            if (cadence.TryAdvance(delta.magnitude))
                signalBus.Emit(SignalIntent.NonPositional(StepSignalId,
                    SignalChannel.Locomotion, SignalForm.OneShot));
        }

        protected override void OnFeedbackEnabledChanged(bool value)
        {
            if (!value)
                return;

            // Turned back on mid-session, the person has moved since. Starting
            // from where they are now rather than from where they were avoids
            // a footfall that reports the distance they covered while silent.
            hasLastPosition = false;
            cadence?.Reset();
        }
    }
}
