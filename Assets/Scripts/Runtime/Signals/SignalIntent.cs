using UnityEngine;

namespace CityGenPedestrianProbe
{
    public enum SignalForm
    {
        OneShot,
        Sustained
    }

    public readonly struct SignalIntent
    {
        public readonly string SignalId;
        public readonly SignalChannel Channel;
        public readonly SignalForm Form;
        public readonly Vector3 WorldPosition;
        public readonly bool HasWorldPosition;
        public readonly float DistanceMeters;
        public readonly long ReferentFeatureId;

        private SignalIntent(string signalId, SignalChannel channel, SignalForm form,
            Vector3 worldPosition, bool hasWorldPosition, float distanceMeters,
            long referentFeatureId)
        {
            SignalId = Normalize(signalId);
            Channel = channel;
            Form = form;
            WorldPosition = worldPosition;
            HasWorldPosition = hasWorldPosition;
            DistanceMeters = distanceMeters;
            ReferentFeatureId = referentFeatureId;
        }

        public bool IsValid => SignalId.Length > 0;

        public static SignalIntent Positional(string signalId, SignalChannel channel,
            SignalForm form, Vector3 worldPosition, float distanceMeters,
            long referentFeatureId = 0L) =>
            new SignalIntent(signalId, channel, form, worldPosition, true, distanceMeters,
                referentFeatureId);

        public static SignalIntent NonPositional(string signalId, SignalChannel channel,
            SignalForm form) =>
            new SignalIntent(signalId, channel, form, Vector3.zero, false,
                float.PositiveInfinity, 0L);

        private static string Normalize(string signalId) =>
            string.IsNullOrWhiteSpace(signalId) ? string.Empty : signalId.Trim();
    }
}
