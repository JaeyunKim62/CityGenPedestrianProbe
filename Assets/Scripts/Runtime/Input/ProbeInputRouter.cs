using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// Collects every attached input source into one frame of intent and spends
    /// it on the controller, the sweep and the feedback modules.
    /// </summary>
    /// <remarks>
    /// Sources are merged rather than switched between, so a Bluetooth keyboard
    /// can be used alongside a thumb during bring-up without a mode to toggle.
    ///
    /// The frame is built on demand and cached per frame, so the controller
    /// pulling movement out of this router does not depend on which of the two
    /// components Unity happens to update first.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ProbeInputRouter : MonoBehaviour, IPedestrianMoveSource
    {
        private readonly List<IProbeInputSource> sources = new List<IProbeInputSource>();

        private SemanticPedestrianController player;
        private EgocentricSweepProbe sweep;
        private FeedbackCoordinator coordinator;
        private BlindModeOverlay overlay;

        private ProbeInputFrame frame = ProbeInputFrame.Empty;
        private int frameStamp = -1;

        /// <summary>True once a person has actually asked for something.</summary>
        public bool HasReceivedInput { get; private set; }

        public int SourceCount => sources.Count;

        public void Configure(SemanticPedestrianController controller,
            EgocentricSweepProbe sweepProbe, FeedbackCoordinator feedback,
            BlindModeOverlay blindOverlay)
        {
            player = controller;
            sweep = sweepProbe;
            coordinator = feedback;
            overlay = blindOverlay;
            if (player != null)
                player.SetMoveSource(this);
        }

        public void AddSource(IProbeInputSource source)
        {
            if (source != null && !sources.Contains(source))
                sources.Add(source);
        }

        public void ReadMovement(out Vector2 movement, out bool fast,
            out Vector2 lookDegrees)
        {
            ProbeInputFrame current = Current();
            movement = current.Move;
            fast = current.Fast;
            lookDegrees = current.LookDegrees;
        }

        private void Update()
        {
            ProbeInputFrame current = Current();

            if (sweep != null)
            {
                if (current.CenterSweep)
                    sweep.SetRelativeAngle(0f);
                else if (current.HasSweepAngle)
                    sweep.SetRelativeAngle(current.SweepAngleAbsolute);
                else if (!Mathf.Approximately(current.SweepAngleRate, 0f))
                    sweep.SetRelativeAngle(sweep.RelativeAngleDegrees +
                        current.SweepAngleRate * Time.unscaledDeltaTime);

                if (current.HasRange)
                    sweep.SetRange(current.RangeAbsolute);
            }

            if (current.ToggleSemanticAudio)
                ToggleSemanticAudio();
            if (current.ToggleBlind)
                RequestBlindToggle();
        }

        private ProbeInputFrame Current()
        {
            if (frameStamp == Time.frameCount)
                return frame;

            frameStamp = Time.frameCount;
            frame = Merge();
            return frame;
        }

        private ProbeInputFrame Merge()
        {
            ProbeInputFrame merged = ProbeInputFrame.Empty;
            float deltaSeconds = Time.unscaledDeltaTime;
            float angle = sweep != null ? sweep.RelativeAngleDegrees : 0f;
            float range = sweep != null ? sweep.RangeMeters : 1f;

            for (int i = 0; i < sources.Count; i++)
            {
                IProbeInputSource source = sources[i];
                if (source == null || !source.IsPresent)
                    continue;

                ProbeInputFrame read = source.Read(deltaSeconds, angle, range);
                merged = merged.MergedWith(read);

                // Named the first time anything is actually asked for, because
                // HasReceivedInput retires the demo sweep and a wrong latch
                // leaves the probe frozen with nothing saying which device
                // claimed the person had moved.
                if (!HasReceivedInput && Requests(read))
                {
                    HasReceivedInput = true;
                    Debug.Log($"Input taken over by '{source.SourceId}'.", this);
                }
            }

            return merged;
        }

        private void ToggleSemanticAudio()
        {
            if (coordinator == null)
                return;
            if (!coordinator.TryGetModuleState(SemanticAudioGuide.StableModuleId,
                    out bool isEnabled))
                return;
            coordinator.SetModuleEnabled(SemanticAudioGuide.StableModuleId,
                !isEnabled, "touch");
        }

        private void RequestBlindToggle()
        {
            if (overlay == null)
                return;

            // Leaving blind is a policy decision, not a mechanism one: a release
            // build handed to a participant must not offer a way out of the
            // condition, so only a build that already carries the debug HUD
            // honours the request.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            overlay.SetBlind(!overlay.IsBlind);
#endif
        }

        // A stick pushed to a hundredth of its travel, or a tenth of a degree of
        // turn, is noise rather than a request. Reading a bare zero let a phone
        // that reports a mouse it does not have retire the demo sweep before
        // anybody had touched the screen.
        private const float RequestEpsilon = 0.01f;

        private static bool Requests(in ProbeInputFrame candidate) =>
            candidate.Move.sqrMagnitude > RequestEpsilon * RequestEpsilon ||
            candidate.LookDegrees.sqrMagnitude > RequestEpsilon * RequestEpsilon ||
            candidate.HasSweepAngle || candidate.HasRange ||
            candidate.CenterSweep || candidate.ToggleSemanticAudio ||
            candidate.ToggleBlind;
    }
}
