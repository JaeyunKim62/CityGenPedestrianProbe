using UnityEngine;

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// Applies the handheld settings a blind session needs, and attaches the
    /// annotator that records which device the session actually ran on.
    /// </summary>
    /// <remarks>
    /// This runs from a runtime hook rather than from a component so that no
    /// scene has to be rebuilt to gain it. The synthetic testbed scenes are
    /// committed as the stimulus a result was measured against, so they are
    /// deliberately left byte-identical.
    /// </remarks>
    public static class ProbeSessionPlatform
    {
        /// <summary>
        /// Settings the device forces on us once, for as long as the app runs.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
            // The only two things a handheld genuinely forces on us. Asked of
            // the device rather than of the compiler, so one build behaves
            // correctly wherever it runs and the desktop keeps running exactly
            // the code the phone does.
            if (SystemInfo.deviceType != DeviceType.Handheld)
                return;

            // A blind session paints the screen black and the participant never
            // touches it, so Android would dim and lock the screen partway
            // through and end the trial with it.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Held the same way every session, so that left and right mean the
            // same thing to every participant.
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        /// <summary>
        /// Gives one probe runtime the parts a session needs around it.
        /// </summary>
        /// <remarks>
        /// Called from the runtime's own Awake, once per scene. It used to hang
        /// off the same runtime-initialize hook as the screen settings above,
        /// which runs once for the whole app: walking to the next layout left
        /// the new scene with no router and no input sources at all, so the
        /// controls simply stopped. Screen settings are an app-lifetime
        /// concern and this is a per-scene one, and they cannot share a hook.
        /// </remarks>
        internal static void AttachSessionComponents(PedestrianProbeRuntime host)
        {
            if (host == null)
                return;

            if (host.GetComponent<ProbeSessionAnnotator>() == null)
                host.gameObject.AddComponent<ProbeSessionAnnotator>();

            AttachInputSources(host);

#if PROBE_DEMO
            if (host.GetComponent<AutoSweepDemo>() == null)
                host.gameObject.AddComponent<AutoSweepDemo>();
#endif
        }

        /// <summary>
        /// Registers every source, on every platform, and lets each one report
        /// whether its device is actually there.
        /// </summary>
        /// <remarks>
        /// Both are always added because presence is a runtime question, not a
        /// build-time one: a desktop has no touchscreen, a phone usually has no
        /// keyboard, and a phone with one paired has both. The router skips
        /// whichever is absent and merges the rest.
        ///
        /// Registering only touch would strand the desktop. The router becomes
        /// the controller's move source as soon as it is configured, so the
        /// keyboard path inside the controller stops being reachable and has to
        /// come back in as a source rather than as a fallback.
        /// </remarks>
        private static void AttachInputSources(PedestrianProbeRuntime host)
        {
            if (host.GetComponent<ProbeInputRouter>() != null)
                return;

            var router = host.gameObject.AddComponent<ProbeInputRouter>();
            router.Configure(Object.FindFirstObjectByType<SemanticPedestrianController>(),
                host.GetComponent<EgocentricSweepProbe>(),
                host.GetComponent<FeedbackCoordinator>(),
                host.GetComponent<BlindModeOverlay>());
            router.AddSource(host.gameObject.AddComponent<TouchProbeInputSource>());
            router.AddSource(host.gameObject.AddComponent<KeyboardProbeInputSource>());
        }
    }
}
