using System;
using System.Text;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class EgocentricSweepProbe : FeedbackModule
    {
        public const string StableModuleId = "probe.egocentric-sweep";
        public const string HitSignalId = "probe.hit";
        public const string MissSignalId = "probe.miss";

        [Header("Probe control")]
        [SerializeField, Range(-180f, 0f)] private float minRelativeAngle = -90f;
        [SerializeField, Range(0f, 180f)] private float maxRelativeAngle = 90f;
        [SerializeField] private float relativeAngleDegrees;
        [SerializeField, Min(0.1f)] private float minRangeMeters = 1f;
        [SerializeField, Min(0.1f)] private float maxRangeMeters = 40f;
        [SerializeField, Min(0.1f)] private float rangeMeters = 5f;

        [Header("Detection")]
        [SerializeField, Min(0.01f)] private float sampleStepMeters = 0.5f;
        [SerializeField, Range(0, 16)] private int refinementIterations = 4;
        [SerializeField, Min(0.02f)] private float queryIntervalSeconds = 0.1f;

        [Header("Development diagnostics")]
        [SerializeField] private bool drawDebugRay = true;
        [SerializeField] private bool logResultChanges = true;

        private SemanticSpaceMap semanticMap;
        private Transform listener;
        private SemanticSessionLogger sessionLogger;
        private FeedbackSignalBus signalBus;
        private string activeSignalId = string.Empty;
        private SemanticSpaceMapProbeField field;
        private readonly RayBoundaryProbe probe = new RayBoundaryProbe();
        private SpatialProbeHit currentHit;
        private bool hasCurrentResult;
        private SpatialProbeObservation currentObservation;
        private float nextQuery;
        private bool hasLoggedResult;
        private bool lastLoggedHasHit;
        private string lastLoggedLabel = string.Empty;
        private long lastLoggedFeatureId;
        private float lastLoggedDistance;
        private float lastLoggedAngle;
        private float lastLoggedRange;

        public override string ModuleId => StableModuleId;
        public float RelativeAngleDegrees => relativeAngleDegrees;
        public float RangeMeters => rangeMeters;
        public bool HasCurrentResult => hasCurrentResult;
        public SpatialProbeHit CurrentHit => currentHit;
        public SpatialProbeObservation CurrentObservation => currentObservation;
        public event Action<SpatialProbeObservation> ObservationSampled;

        public override void Configure(in FeedbackContext context)
        {
            semanticMap = context.Map;
            listener = context.Listener;
            sessionLogger = context.Logger;
            signalBus = context.SignalBus;
            field = new SemanticSpaceMapProbeField(context.Map);
            ClampSettings();
            ResetRuntimeState();
        }

        public void SetRelativeAngle(float angleDegrees)
        {
            relativeAngleDegrees = Mathf.Clamp(angleDegrees, minRelativeAngle,
                maxRelativeAngle);
            nextQuery = 0f;
        }

        public void SetRange(float distanceMeters)
        {
            rangeMeters = Mathf.Clamp(distanceMeters, minRangeMeters, maxRangeMeters);
            nextQuery = 0f;
        }

        private void OnValidate()
        {
            ClampSettings();
        }

        private void Update()
        {
            if (field == null || listener == null || Time.unscaledTime < nextQuery)
                return;

            nextQuery = Time.unscaledTime + queryIntervalSeconds;
            Vector3 horizontalForward = Vector3.ProjectOnPlane(listener.forward,
                Vector3.up).normalized;
            if (horizontalForward.sqrMagnitude < 0.5f)
                return;
            Vector3 worldDirection = Quaternion.Euler(0f, relativeAngleDegrees, 0f) *
                                     horizontalForward;
            var request = new SpatialProbeRequest(
                new Vector2(listener.position.x, listener.position.z),
                new Vector2(worldDirection.x, worldDirection.z), rangeMeters,
                sampleStepMeters, refinementIterations);
            currentHit = probe.Probe(field, request);
            currentObservation = new SpatialProbeObservation(request.Origin,
                request.Direction, relativeAngleDegrees, currentHit);
            hasCurrentResult = true;
            ObservationSampled?.Invoke(currentObservation);
            EmitSignalFor(currentObservation);

            if (drawDebugRay)
                DrawProbe(request, currentHit, listener.position.y);
            if (logResultChanges && ResultChanged(currentHit))
                LogResult(currentHit);
        }

        public void EmitSignalFor(in SpatialProbeObservation observation)
        {
            if (signalBus == null)
                return;

            SpatialProbeHit hit = observation.Hit;
            string signalId = hit.HasHit ? HitSignalIdFor(hit.To) : MissSignalId;
            if (!string.Equals(activeSignalId, signalId, StringComparison.Ordinal))
            {
                ReleaseSignal();
                activeSignalId = signalId;
            }

            float height = listener != null ? listener.position.y : 0f;
            var point = new Vector3(hit.Point.x, height, hit.Point.y);
            float distance = hit.HasHit ? hit.DistanceMeters : hit.RequestedRangeMeters;
            signalBus.Emit(SignalIntent.Positional(signalId, SignalChannel.Answer,
                SignalForm.Sustained, point, distance, hit.HasHit ? hit.To.FeatureId : 0L));
        }

        public static string HitSignalIdFor(SpatialProbeSample sample)
        {
            string category = sample.Category;
            if (string.IsNullOrWhiteSpace(category))
                return HitSignalId;

            var builder = new StringBuilder(HitSignalId.Length + 1 + category.Length);
            builder.Append(HitSignalId).Append('.');
            for (int i = 0; i < category.Length; i++)
            {
                char character = category[i];
                builder.Append(char.IsLetterOrDigit(character)
                    ? char.ToLowerInvariant(character)
                    : '-');
            }
            return builder.ToString();
        }

        protected override void OnFeedbackEnabledChanged(bool value)
        {
            if (value)
            {
                ResetRuntimeState();
                return;
            }

            ReleaseSignal();
            hasCurrentResult = false;
        }

        private void ReleaseSignal()
        {
            if (activeSignalId.Length == 0)
                return;
            signalBus?.Release(activeSignalId);
            activeSignalId = string.Empty;
        }

        private void ClampSettings()
        {
            minRelativeAngle = Mathf.Clamp(minRelativeAngle, -180f, 0f);
            maxRelativeAngle = Mathf.Clamp(maxRelativeAngle, 0f, 180f);
            relativeAngleDegrees = Mathf.Clamp(relativeAngleDegrees, minRelativeAngle,
                maxRelativeAngle);
            minRangeMeters = Mathf.Max(0.1f, minRangeMeters);
            maxRangeMeters = Mathf.Max(minRangeMeters, maxRangeMeters);
            rangeMeters = Mathf.Clamp(rangeMeters, minRangeMeters, maxRangeMeters);
            sampleStepMeters = Mathf.Max(0.01f, sampleStepMeters);
            refinementIterations = Mathf.Clamp(refinementIterations, 0, 16);
            queryIntervalSeconds = Mathf.Max(0.02f, queryIntervalSeconds);
        }

        private void ResetRuntimeState()
        {
            nextQuery = 0f;
            hasCurrentResult = false;
            hasLoggedResult = false;
            activeSignalId = string.Empty;
        }

        private bool ResultChanged(SpatialProbeHit hit)
        {
            string label = hit.HasHit ? hit.To.DisplayLabel : "No hit";
            long featureId = hit.HasHit ? hit.To.FeatureId : 0;
            return !hasLoggedResult || lastLoggedHasHit != hit.HasHit ||
                   !string.Equals(lastLoggedLabel, label, StringComparison.Ordinal) ||
                   lastLoggedFeatureId != featureId ||
                   DistanceChanged(lastLoggedDistance, hit.DistanceMeters) ||
                   Mathf.Abs(lastLoggedAngle - relativeAngleDegrees) >= 0.1f ||
                   Mathf.Abs(lastLoggedRange - rangeMeters) >= 0.1f;
        }

        private void LogResult(SpatialProbeHit hit)
        {
            hasLoggedResult = true;
            lastLoggedHasHit = hit.HasHit;
            lastLoggedLabel = hit.HasHit ? hit.To.DisplayLabel : "No hit";
            lastLoggedFeatureId = hit.HasHit ? hit.To.FeatureId : 0;
            lastLoggedDistance = hit.DistanceMeters;
            lastLoggedAngle = relativeAngleDegrees;
            lastLoggedRange = rangeMeters;

            Vector3 point = new Vector3(hit.Point.x,
                listener != null ? listener.position.y : 0f, hit.Point.y);
            SemanticHit semanticHit = semanticMap != null
                ? semanticMap.ClassifyWorld(point)
                : SemanticHit.Unknown(point);
            string detail = $"angle={relativeAngleDegrees:F2}|range={rangeMeters:F2}|" +
                            $"hit={hit.HasHit}|distance=" +
                            $"{(hit.HasHit ? hit.DistanceMeters.ToString("F2") : "N/A")}|" +
                            $"from={Safe(hit.From.DisplayLabel)}|" +
                            $"to={Safe(hit.HasHit ? hit.To.DisplayLabel : "None")}|" +
                            $"feature={(hit.HasHit ? hit.To.FeatureId : 0)}|" +
                            $"point={hit.Point.x:F2}:{hit.Point.y:F2}";
            sessionLogger?.LogEvent("egocentric_probe_result", semanticHit, detail);
        }

        private static void DrawProbe(SpatialProbeRequest request, SpatialProbeHit hit,
            float height)
        {
            Vector3 start = new Vector3(request.Origin.x, height, request.Origin.y);
            Vector3 end = new Vector3(hit.Point.x, height, hit.Point.y);
            Color color = hit.HasHit ? Color.cyan : Color.gray;
            Debug.DrawLine(start, end, color, 0.12f, false);
            if (!hit.HasHit)
                return;

            const float markerSize = 0.2f;
            Debug.DrawLine(end - Vector3.right * markerSize,
                end + Vector3.right * markerSize, color, 0.12f, false);
            Debug.DrawLine(end - Vector3.forward * markerSize,
                end + Vector3.forward * markerSize, color, 0.12f, false);
        }

        private static string Safe(string value) =>
            string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Replace('|', '/');

        private static bool DistanceChanged(float previous, float current)
        {
            bool previousFinite = !float.IsInfinity(previous) && !float.IsNaN(previous);
            bool currentFinite = !float.IsInfinity(current) && !float.IsNaN(current);
            return previousFinite != currentFinite ||
                   previousFinite && Mathf.Abs(previous - current) >= 0.1f;
        }
    }
}
