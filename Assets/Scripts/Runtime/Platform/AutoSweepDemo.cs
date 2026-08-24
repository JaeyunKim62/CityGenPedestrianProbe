using UnityEngine;

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// Swings the sweep from side to side on its own, so a build with no input
    /// device attached still sounds out the whole probe → signal → audio path.
    /// </summary>
    /// <remarks>
    /// Bring-up aid only. It is attached solely in <c>PROBE_DEMO</c> builds and
    /// must never be present in a session that produces experimental data: it
    /// drives the same control a participant would, so its rows are
    /// indistinguishable from theirs once written.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AutoSweepDemo : MonoBehaviour
    {
        [SerializeField, Range(5f, 90f)] private float amplitudeDegrees = 90f;
        [SerializeField, Min(0.01f)] private float sweepsPerSecond = 0.1f;

        // Matched to EgocentricSweepProbe's own query interval. Aiming faster
        // than the probe samples would only inflate the session log, because
        // SetRelativeAngle forces a query on every call.
        [SerializeField, Min(0.02f)] private float aimIntervalSeconds = 0.1f;

        private EgocentricSweepProbe sweep;
        private ProbeInputRouter router;
        private float nextAim;

        private void Start()
        {
            sweep = GetComponent<EgocentricSweepProbe>();
            router = GetComponent<ProbeInputRouter>();
            if (sweep == null)
                Debug.LogWarning("AutoSweepDemo found no sweep probe to drive.", this);
            else
                Debug.Log($"AutoSweepDemo is driving the sweep across " +
                          $"+/-{amplitudeDegrees:F0} deg at {sweepsPerSecond:F2} Hz. " +
                          "This build is a demo, not an experiment.", this);
        }

        private void Update()
        {
            // Hand over the moment a person touches anything, so the demo
            // introduces the signal without then fighting the thumb aiming it.
            if (router != null && router.HasReceivedInput)
            {
                Debug.Log("AutoSweepDemo is standing down: touch input has taken over.",
                    this);
                enabled = false;
                return;
            }

            if (sweep == null || Time.unscaledTime < nextAim)
                return;

            nextAim = Time.unscaledTime + aimIntervalSeconds;
            float phase = Time.unscaledTime * sweepsPerSecond * 2f * Mathf.PI;
            sweep.SetRelativeAngle(Mathf.Sin(phase) * amplitudeDegrees);
        }
    }
}
