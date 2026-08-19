using UnityEngine;

namespace CityGenPedestrianProbe
{
    public readonly struct SignalIntent
    {
        public readonly string SignalId;
        public readonly SignalChannel Channel;
        public readonly Vector3 WorldPosition;
        public readonly bool HasWorldPosition;
        public readonly float DistanceMeters;

        public SignalIntent(string signalId, SignalChannel channel, Vector3 worldPosition,
            float distanceMeters)
        {
            SignalId = Normalize(signalId);
            Channel = channel;
            WorldPosition = worldPosition;
            HasWorldPosition = true;
            DistanceMeters = distanceMeters;
        }

        private SignalIntent(string signalId, SignalChannel channel)
        {
            SignalId = Normalize(signalId);
            Channel = channel;
            WorldPosition = Vector3.zero;
            HasWorldPosition = false;
            DistanceMeters = float.PositiveInfinity;
        }

        public bool IsValid => SignalId.Length > 0;

        public static SignalIntent NonPositional(string signalId, SignalChannel channel) =>
            new SignalIntent(signalId, channel);

        private static string Normalize(string signalId) =>
            string.IsNullOrWhiteSpace(signalId) ? string.Empty : signalId.Trim();
    }
}
