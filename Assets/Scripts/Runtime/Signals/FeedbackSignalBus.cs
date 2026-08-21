using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    public sealed class FeedbackSignalBus
    {
        private readonly List<ISignalRenderer> renderers = new List<ISignalRenderer>();
        private SemanticSessionLogger sessionLogger;
        private SemanticSpaceMap semanticMap;
        private Transform listener;

        public int RendererCount => renderers.Count;

        public void Configure(SemanticSessionLogger logger, SemanticSpaceMap map,
            Transform listenerTransform)
        {
            sessionLogger = logger;
            semanticMap = map;
            listener = listenerTransform;
        }

        public void SetRenderers(IEnumerable<ISignalRenderer> signalRenderers)
        {
            for (int i = 0; i < renderers.Count; i++)
                renderers[i].SignalUnresolved -= HandleSignalUnresolved;
            renderers.Clear();

            if (signalRenderers == null)
                return;
            foreach (ISignalRenderer renderer in signalRenderers)
            {
                if (renderer == null || renderers.Contains(renderer))
                    continue;
                renderers.Add(renderer);
                renderer.SignalUnresolved += HandleSignalUnresolved;
            }
        }

        public void Emit(in SignalIntent intent)
        {
            if (!intent.IsValid)
                return;

            for (int i = 0; i < renderers.Count; i++)
                renderers[i].Render(intent);
            LogEvent("signal_emit", Describe(intent));
        }

        public void Release(string signalId)
        {
            if (string.IsNullOrWhiteSpace(signalId))
                return;

            string trimmed = signalId.Trim();
            for (int i = 0; i < renderers.Count; i++)
                renderers[i].Release(trimmed);
            LogEvent("signal_release", $"signal={trimmed}");
        }

        public void StopAll()
        {
            for (int i = 0; i < renderers.Count; i++)
                renderers[i].StopAll();
        }

        public void LogConfigurationChange(string detail) =>
            LogEvent("signal_catalog", Sanitize(detail));

        private void HandleSignalUnresolved(string signalId) =>
            LogEvent("signal_unresolved", $"signal={Sanitize(signalId)}");

        private static string Describe(in SignalIntent intent)
        {
            var c = CultureInfo.InvariantCulture;
            string distance = float.IsInfinity(intent.DistanceMeters) ||
                              float.IsNaN(intent.DistanceMeters)
                ? "N/A"
                : intent.DistanceMeters.ToString("F2", c);
            string point = intent.HasWorldPosition
                ? $"{intent.WorldPosition.x.ToString("F2", c)}:" +
                  $"{intent.WorldPosition.z.ToString("F2", c)}"
                : "N/A";
            return $"signal={Sanitize(intent.SignalId)}|channel={intent.Channel}|" +
                   $"form={intent.Form}|distance={distance}|" +
                   $"feature={intent.ReferentFeatureId}|point={point}";
        }

        private void LogEvent(string eventName, string detail)
        {
            if (sessionLogger == null)
                return;
            Vector3 position = listener != null ? listener.position : Vector3.zero;
            SemanticHit hit = semanticMap != null
                ? semanticMap.ClassifyWorld(position)
                : SemanticHit.Unknown(position);
            sessionLogger.LogEvent(eventName, hit, detail);
        }

        private static string Sanitize(string value) =>
            string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().Replace('|', '/');
    }
}
