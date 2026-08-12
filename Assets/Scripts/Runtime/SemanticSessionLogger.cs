using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    public sealed class SemanticSessionLogger : MonoBehaviour
    {
        [SerializeField] private SemanticSpaceMap semanticMap;
        [SerializeField] private Transform player;
        private StreamWriter writer;
        private float nextSample;
        private float startedAt;

        public void Configure(SemanticSpaceMap map, Transform playerTransform)
        {
            semanticMap = map;
            player = playerTransform;
        }

        private void Start()
        {
            startedAt = Time.unscaledTime;
            string directory = Path.Combine(Application.persistentDataPath, "PedestrianProbe");
            Directory.CreateDirectory(directory);
            string filename = $"session_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            writer = new StreamWriter(Path.Combine(directory, filename), false, new UTF8Encoding(true));
            writer.AutoFlush = true;
            writer.WriteLine("record_type,time_s,utc_iso,x,y,z,latitude,longitude,heading_deg,kind,feature_id,confidence,event,detail,map_version,database_id");
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
            float heading = player != null ? player.eulerAngles.y : 0f;
            var c = CultureInfo.InvariantCulture;
            writer.WriteLine(string.Join(",",
                Csv(recordType), (Time.unscaledTime - startedAt).ToString("F3", c),
                Csv(DateTime.UtcNow.ToString("O", c)), position.x.ToString("F3", c),
                position.y.ToString("F3", c), position.z.ToString("F3", c),
                latitude.ToString("F8", c), longitude.ToString("F8", c),
                heading.ToString("F2", c), hit.Kind, hit.FeatureId,
                hit.Confidence.ToString("F2", c), Csv(eventName), Csv(detail),
                Csv(semanticMap != null ? semanticMap.BakeVersion : string.Empty),
                Csv(semanticMap != null ? semanticMap.SourceDatabaseId : string.Empty)));
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
