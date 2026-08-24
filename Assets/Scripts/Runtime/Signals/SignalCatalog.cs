using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    public enum SignalWaveform
    {
        Sine,
        Triangle,
        Harmonic,
        Noise
    }

    [Serializable]
    public sealed class SignalDefinition
    {
        [SerializeField] private string signalId = string.Empty;
        [SerializeField] private SignalWaveform waveform = SignalWaveform.Sine;
        [SerializeField, Min(20f)] private float baseFrequencyHz = 440f;
        [SerializeField, Range(1, 12)] private int harmonicCount = 1;
        [SerializeField, Min(0f)] private float roughnessHz;
        [SerializeField, Min(0.02f)] private float durationSeconds = 0.18f;
        [SerializeField] private bool attackClick = true;
        [SerializeField, Range(0f, 1f)] private float level = 0.6f;
        [SerializeField, Min(20f)] private float nearCutoffHz = 8000f;
        [SerializeField, Min(20f)] private float farCutoffHz = 700f;
        [SerializeField, Min(0.1f)] private float farDistanceMeters = 5f;
        [SerializeField, Min(0f)] private float nearPulseHz;
        [SerializeField, Min(0f)] private float farPulseHz;
        [SerializeField, Min(0f)] private float nearPitchHz;
        [SerializeField, Min(0f)] private float farPitchHz;

        public string SignalId => signalId ?? string.Empty;
        public SignalWaveform Waveform => waveform;
        public float BaseFrequencyHz => baseFrequencyHz;
        public int HarmonicCount => Mathf.Max(1, harmonicCount);
        public float RoughnessHz => Mathf.Max(0f, roughnessHz);
        public float DurationSeconds => Mathf.Max(0.02f, durationSeconds);
        public bool AttackClick => attackClick;
        public float Level => Mathf.Clamp01(level);
        public float NearCutoffHz => nearCutoffHz;
        public float FarCutoffHz => farCutoffHz;
        public float FarDistanceMeters => Mathf.Max(0.1f, farDistanceMeters);

        public SignalDefinition()
        {
        }

        public SignalDefinition(string id, SignalWaveform form, float frequencyHz,
            float duration, float signalLevel)
        {
            signalId = id ?? string.Empty;
            waveform = form;
            baseFrequencyHz = frequencyHz;
            durationSeconds = duration;
            level = signalLevel;
        }

        public float NearPulseHz => Mathf.Max(0f, nearPulseHz);
        public float FarPulseHz => Mathf.Max(0f, farPulseHz);
        public float NearPitchHz => Mathf.Max(0f, nearPitchHz);
        public float FarPitchHz => Mathf.Max(0f, farPitchHz);
        public bool MapsDistanceToPulse => NearPulseHz > 0f && FarPulseHz > 0f;
        public bool MapsDistanceToPitch => NearPitchHz > 0f && FarPitchHz > 0f;

        public float CutoffHzAt(float distanceMeters)
        {
            float near = Mathf.Max(20f, NearCutoffHz);
            float far = Mathf.Max(20f, FarCutoffHz);
            return near * Mathf.Pow(far / near, Normalized(distanceMeters));
        }

        public float PulseHzAt(float distanceMeters)
        {
            if (!MapsDistanceToPulse)
                return 0f;

            float nearPeriod = 1f / NearPulseHz;
            float farPeriod = 1f / FarPulseHz;
            float period = Mathf.Lerp(nearPeriod, farPeriod, Normalized(distanceMeters));
            return period > 0f ? 1f / period : 0f;
        }

        public float PitchScaleAt(float distanceMeters)
        {
            if (!MapsDistanceToPitch)
                return 1f;

            float hz = NearPitchHz * Mathf.Pow(FarPitchHz / NearPitchHz,
                Normalized(distanceMeters));
            return Mathf.Clamp(hz / BaseFrequencyHz, 0.05f, 3f);
        }

        private float Normalized(float distanceMeters)
        {
            if (float.IsNaN(distanceMeters) || float.IsInfinity(distanceMeters))
                return 1f;
            return Mathf.Clamp01(Mathf.Max(0f, distanceMeters) / FarDistanceMeters);
        }
    }

    [CreateAssetMenu(menuName = "Pedestrian Probe/Signal Catalog", fileName = "SignalCatalog")]
    public sealed class SignalCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId = "default";
        [SerializeField, Min(1)] private int revision = 1;
        [SerializeField] private List<SignalDefinition> definitions = new List<SignalDefinition>();

        private Dictionary<string, SignalDefinition> index;

        public string CatalogId => catalogId ?? string.Empty;
        public int Revision => Mathf.Max(1, revision);
        public int DefinitionCount => definitions?.Count ?? 0;

        public void SetDefinitions(string id, int catalogRevision,
            IEnumerable<SignalDefinition> signalDefinitions)
        {
            catalogId = id ?? string.Empty;
            revision = Mathf.Max(1, catalogRevision);
            definitions = signalDefinitions == null
                ? new List<SignalDefinition>()
                : new List<SignalDefinition>(signalDefinitions);
            index = null;
        }

        public bool TryResolve(string signalId, out SignalDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(signalId))
                return false;

            BuildIndex();
            string candidate = signalId.Trim();
            while (candidate.Length > 0)
            {
                if (index.TryGetValue(candidate, out definition))
                    return true;
                int lastSeparator = candidate.LastIndexOf('.');
                if (lastSeparator <= 0)
                    break;
                candidate = candidate.Substring(0, lastSeparator);
            }

            definition = null;
            return false;
        }

        private void BuildIndex()
        {
            if (index != null)
                return;

            index = new Dictionary<string, SignalDefinition>(StringComparer.Ordinal);
            if (definitions == null)
                return;
            for (int i = 0; i < definitions.Count; i++)
            {
                SignalDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.SignalId))
                    continue;
                string key = definition.SignalId.Trim();
                if (index.ContainsKey(key))
                {
                    Debug.LogError($"Duplicate signal id in catalog {CatalogId}: {key}", this);
                    continue;
                }
                index.Add(key, definition);
            }
        }

        private void OnValidate()
        {
            index = null;
        }
    }
}
