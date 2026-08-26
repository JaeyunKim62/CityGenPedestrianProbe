using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    public sealed class SemanticSessionLogger : MonoBehaviour
    {
        [SerializeField] private SemanticSpaceMap semanticMap;
        [SerializeField] private Transform player;
        [SerializeField] private AudioSignalRenderer signalRenderer;
        private ProbeHeading heading;
        private StreamWriter writer;
        private float nextSample;
        private float startedAt;

        public void Configure(SemanticSpaceMap map, Transform playerTransform)
        {
            semanticMap = map;
            player = playerTransform;
        }

        /// <summary>
        /// Names the signal set a session is being heard through.
        /// </summary>
        /// <remarks>
        /// Four catalogs ship and they are rival conditions, not variations on
        /// one sound. Without them named, a finished session says where someone
        /// walked but not what they were listening to, which is the difference
        /// the experiment is built to measure.
        ///
        /// The renderer is held rather than the catalog it currently has,
        /// because the catalog can be cycled mid-session. A copy taken at
        /// startup would keep naming the one that was replaced.
        /// </remarks>
        public void SetSignalRenderer(AudioSignalRenderer renderer)
        {
            signalRenderer = renderer;
        }

        /// <summary>
        /// Names the heading model, so every row carries both where the body
        /// pointed and where attention pointed.
        /// </summary>
        /// <remarks>
        /// The body was already here as <c>heading_deg</c>, read off the
        /// player transform. Attention was not: it was computed inside the
        /// sweep and spent on the detail string of an event that is only
        /// written when the probe result changes. So a finished session could
        /// be asked which way someone was facing but not where they were
        /// attending, and the two could not be laid side by side over time at
        /// all. That comparison is a question this study means to ask, and a
        /// question cannot be asked of measurements that were never recorded.
        /// </remarks>
        public void SetHeading(ProbeHeading probeHeading)
        {
            heading = probeHeading;
        }

        private void Start()
        {
            startedAt = Time.unscaledTime;
            string directory = Path.Combine(Application.persistentDataPath, "PedestrianProbe");
            Directory.CreateDirectory(directory);
            string filename = $"session_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            writer = new StreamWriter(Path.Combine(directory, filename), false, new UTF8Encoding(true));
            writer.AutoFlush = true;
            // New columns are appended rather than slotted next to heading_deg.
            // Sessions already recorded end at signal_catalog_revision, and a
            // reader that takes columns by position rather than by name has to
            // keep working across both.
            writer.WriteLine("record_type,time_s,utc_iso,x,y,z,latitude,longitude,heading_deg,kind,source_class,containing_kinds,containing_source_classes,feature_id,confidence,event,detail,map_version,database_id,signal_catalog,signal_catalog_revision,gaze_bearing_deg,gaze_offset_deg");
            WriteRow("session", SemanticHit.Unknown(player != null ? player.position : Vector3.zero),
                "session_start", string.Empty);
        }

        private void Update()
        {
            if (writer == null || semanticMap == null || player == null || Time.unscaledTime < nextSample)
                return;
            nextSample = Time.unscaledTime + 0.1f;
            WriteRow("sample", semanticMap.ClassifyWorld(player.position), string.Empty, string.Empty);
        }

        public void LogEvent(string eventName, SemanticHit hit, string detail = "") =>
            WriteRow("event", hit, eventName, detail);

        private void WriteRow(string recordType, SemanticHit hit, string eventName, string detail)
        {
            if (writer == null)
                return;
            Vector3 position = player != null ? player.position : hit.NearestPointWorld;
            double latitude = 0d;
            double longitude = 0d;
            if (semanticMap != null)
                semanticMap.WorldToGeo(position, out latitude, out longitude);
            string containingKinds = string.Empty;
            string containingSourceClasses = string.Empty;
            if (semanticMap != null)
            {
                SemanticHit[] containing = semanticMap.QueryNearbyWorld(position, 0f)
                    .Where(candidate => candidate.Contains).ToArray();
                containingKinds = string.Join("|", containing
                    .Where(candidate => candidate.Kind != SemanticSpaceKind.Unknown)
                    .Select(candidate => candidate.Kind.ToString()).Distinct().OrderBy(value => value));
                containingSourceClasses = string.Join("|", containing
                    .Where(candidate => !string.IsNullOrEmpty(candidate.SourceClass))
                    .Select(candidate => candidate.SourceClass).Distinct().OrderBy(value => value));
            }
            SignalCatalog catalog = signalRenderer != null ? signalRenderer.Catalog : null;
            var c = CultureInfo.InvariantCulture;

            // The transform is the fallback, not the source. Bearing now lives
            // in the heading model and the transform's yaw is drawn from it, so
            // reading the transform here would be reading a copy.
            float bearing = heading != null
                ? heading.BearingDegrees
                : player != null ? player.eulerAngles.y : 0f;
            string gazeBearing = heading != null
                ? heading.GazeBearingDegrees.ToString("F2", c)
                : string.Empty;
            string gazeOffset = heading != null
                ? heading.GazeOffsetDegrees.ToString("F2", c)
                : string.Empty;
            writer.WriteLine(string.Join(",",
                Csv(recordType), (Time.unscaledTime - startedAt).ToString("F3", c),
                Csv(DateTime.UtcNow.ToString("O", c)), position.x.ToString("F3", c),
                position.y.ToString("F3", c), position.z.ToString("F3", c),
                latitude.ToString("F8", c), longitude.ToString("F8", c),
                bearing.ToString("F2", c), hit.Kind, Csv(hit.SourceClass),
                Csv(containingKinds), Csv(containingSourceClasses), hit.FeatureId,
                hit.Confidence.ToString("F2", c), Csv(eventName), Csv(detail),
                Csv(semanticMap != null ? semanticMap.BakeVersion : string.Empty),
                Csv(semanticMap != null ? semanticMap.SourceDatabaseId : string.Empty),
                Csv(catalog != null ? catalog.CatalogId : string.Empty),
                (catalog != null ? catalog.Revision : 0).ToString(c),
                gazeBearing, gazeOffset));
        }

        private static string Csv(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private void OnDestroy()
        {
            if (writer == null)
                return;
            WriteRow("session", SemanticHit.Unknown(player != null ? player.position : Vector3.zero),
                "session_end", string.Empty);
            writer.Dispose();
            writer = null;
        }
    }
}
