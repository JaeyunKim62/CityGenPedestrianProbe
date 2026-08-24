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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
            // A blind session paints the screen black and the participant never
            // touches it, so Android would dim and lock the screen mid-trial.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // The probe queries ten times a second, so thirty frames buys it
            // everything it can use and leaves the phone cool enough to last.
            Application.targetFrameRate = 30;

            // Two thumbs, held the same way every session, so that left and
            // right mean the same thing to every participant.
            if (SystemInfo.deviceType == DeviceType.Handheld)
                Screen.orientation = ScreenOrientation.LandscapeLeft;

            var host = Object.FindFirstObjectByType<PedestrianProbeRuntime>();
            if (host == null)
                return;

            if (host.GetComponent<ProbeSessionAnnotator>() == null)
                host.gameObject.AddComponent<ProbeSessionAnnotator>();

            AttachTouchControls(host);

#if PROBE_DEMO
            if (host.GetComponent<AutoSweepDemo>() == null)
                host.gameObject.AddComponent<AutoSweepDemo>();
#endif
        }

        private static void AttachTouchControls(PedestrianProbeRuntime host)
        {
            if (host.GetComponent<ProbeInputRouter>() != null)
                return;

            var router = host.gameObject.AddComponent<ProbeInputRouter>();
            router.Configure(Object.FindFirstObjectByType<SemanticPedestrianController>(),
                host.GetComponent<EgocentricSweepProbe>(),
                host.GetComponent<FeedbackCoordinator>(),
                host.GetComponent<BlindModeOverlay>());
            router.AddSource(host.gameObject.AddComponent<TouchProbeInputSource>());
        }
    }
}
