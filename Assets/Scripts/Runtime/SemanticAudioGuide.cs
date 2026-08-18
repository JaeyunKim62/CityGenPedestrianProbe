using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class SemanticAudioGuide : FeedbackModule
    {
        public const string StableModuleId = "semantic.audio";
        private const float QueryInterval = 0.1f;
        private const float StableSeconds = 0.2f;
        private const float EventCooldown = 1f;
        private const float ApproachRadius = 6f;

        [SerializeField] private SemanticSpaceMap semanticMap;
        [SerializeField] private Transform listener;
        [SerializeField] private SemanticSessionLogger sessionLogger;

        private readonly Dictionary<SemanticSpaceKind, AudioClip> clips =
            new Dictionary<SemanticSpaceKind, AudioClip>();
        private AudioClip boundaryClip;
        private AudioSource approachSource;
        private AudioSource entrySource;
        private AudioSource boundarySource;
        private float nextQuery;
        private float nextApproachTone;
        private float nextTransitionAllowed;
        private SemanticSpaceKind stableKind = SemanticSpaceKind.Unknown;
        private SemanticSpaceKind candidateKind = SemanticSpaceKind.Unknown;
        private float candidateSince;
        private long activeApproachId;

        public override string ModuleId => StableModuleId;

        public override void Configure(SemanticSpaceMap map, Transform listenerTransform,
            SemanticSessionLogger logger)
        {
            semanticMap = map;
            listener = listenerTransform;
            sessionLogger = logger;
        }

        private void Awake()
        {
            approachSource = CreateSource("Semantic Approach Audio");
            entrySource = CreateSource("Semantic Entry Audio");
            boundarySource = CreateSource("Study Boundary Audio");
            clips[SemanticSpaceKind.Sidewalk] = CreateTone("Sidewalk 440Hz", 440f, 0.18f, false);
            clips[SemanticSpaceKind.Curb] = CreateTone("Curb 1200Hz", 1200f, 0.07f, true);
            clips[SemanticSpaceKind.Crosswalk] = CreateAlternatingTone("Crosswalk 660-880Hz", 660f, 880f);
            clips[SemanticSpaceKind.Road] = CreateTone("Road 110Hz", 110f, 0.28f, true);
            boundaryClip = CreateTripleTone();
        }

        private void Update()
        {
            if (semanticMap == null || listener == null || Time.unscaledTime < nextQuery)
                return;
            nextQuery = Time.unscaledTime + QueryInterval;
            SemanticHit current = semanticMap.ClassifyWorld(listener.position);
            UpdateStableKind(current);
            UpdateApproach(current);
        }

        protected override void OnFeedbackEnabledChanged(bool value)
        {
            if (value)
            {
                nextQuery = 0f;
                candidateKind = SemanticSpaceKind.Unknown;
                stableKind = SemanticSpaceKind.Unknown;
                candidateSince = Time.unscaledTime;
                return;
            }

            CancelInvoke(nameof(RepeatEntryTone));
            approachSource?.Stop();
            entrySource?.Stop();
            boundarySource?.Stop();
            activeApproachId = 0;
        }

        public override void OnBoundaryContact(Vector3 attemptedDirection)
        {
            if (listener == null || boundarySource == null)
                return;
            Vector3 horizontal = Vector3.ProjectOnPlane(attemptedDirection, Vector3.up).normalized;
            boundarySource.transform.position = listener.position + horizontal * 2f;
            boundarySource.PlayOneShot(boundaryClip, 0.85f);
            SemanticHit current = semanticMap != null
                ? semanticMap.ClassifyWorld(listener.position)
                : SemanticHit.Unknown(listener.position);
            sessionLogger?.LogEvent("boundary_contact", current,
                $"direction={horizontal.x:F3}|{horizontal.z:F3}");
        }

        private void UpdateStableKind(SemanticHit current)
        {
            if (current.Kind != candidateKind)
            {
                candidateKind = current.Kind;
                candidateSince = Time.unscaledTime;
                return;
            }
            if (candidateKind == stableKind || Time.unscaledTime - candidateSince < StableSeconds ||
                Time.unscaledTime < nextTransitionAllowed)
                return;

            SemanticSpaceKind previous = stableKind;
            stableKind = candidateKind;
            nextTransitionAllowed = Time.unscaledTime + EventCooldown;
            if (previous != SemanticSpaceKind.Unknown)
                sessionLogger?.LogEvent("exit", current, previous.ToString());
            if (stableKind == SemanticSpaceKind.Unknown)
                return;
            PlayEntry(current);
            sessionLogger?.LogEvent("enter", current);
        }

        private void UpdateApproach(SemanticHit current)
        {
            IReadOnlyList<SemanticHit> nearby = semanticMap.QueryNearbyWorld(listener.position, ApproachRadius);
            bool found = false;
            SemanticHit selected = default;
            int selectedPriority = -1;
            for (int i = 0; i < nearby.Count; i++)
            {
                SemanticHit hit = nearby[i];
                if (hit.Contains || hit.Kind == current.Kind || hit.Kind == SemanticSpaceKind.Unknown)
                    continue;
                int priority = HazardPriority(hit.Kind);
                if (!found || priority > selectedPriority ||
                    (priority == selectedPriority && hit.DistanceMeters < selected.DistanceMeters))
                {
                    selected = hit;
                    selectedPriority = priority;
                    found = true;
                }
            }

            if (!found)
            {
                activeApproachId = 0;
                return;
            }

            if (activeApproachId != selected.FeatureId)
            {
                activeApproachId = selected.FeatureId;
                sessionLogger?.LogEvent("approach_start", selected);
                nextApproachTone = 0f;
            }
            if (Time.unscaledTime < nextApproachTone)
                return;

            float normalized = 1f - Mathf.Clamp01(selected.DistanceMeters / ApproachRadius);
            nextApproachTone = Time.unscaledTime + Mathf.Lerp(1.25f, 0.25f, normalized);
            approachSource.transform.position = selected.NearestPointWorld;
            approachSource.PlayOneShot(clips[selected.Kind], Mathf.Lerp(0.18f, 0.8f, normalized));
        }

        private void PlayEntry(SemanticHit hit)
        {
            if (!clips.TryGetValue(hit.Kind, out AudioClip clip))
                return;
            entrySource.transform.position = listener.position;
            entrySource.PlayOneShot(clip, 0.9f);
            Invoke(nameof(RepeatEntryTone), Mathf.Max(0.12f, clip.length + 0.06f));
        }

        private void RepeatEntryTone()
        {
            if (stableKind == SemanticSpaceKind.Unknown || listener == null ||
                !clips.TryGetValue(stableKind, out AudioClip clip))
                return;
            entrySource.transform.position = listener.position;
            entrySource.PlayOneShot(clip, 0.75f);
        }

        private AudioSource CreateSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.5f;
            source.maxDistance = 15f;
            return source;
        }

        private static AudioClip CreateTone(string clipName, float frequency, float duration, bool pulse)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * i / Mathf.Max(1, sampleCount - 1));
                if (pulse)
                    envelope *= Mathf.Clamp01(1f - t / duration);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.45f;
            }
            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateAlternatingTone(string clipName, float first, float second)
        {
            const int sampleRate = 44100;
            const float duration = 0.32f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float frequency = t < duration * 0.5f ? first : second;
                float local = t < duration * 0.5f ? t : t - duration * 0.5f;
                float envelope = Mathf.Sin(Mathf.PI * local / (duration * 0.5f));
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.42f;
            }
            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTripleTone()
        {
            const int sampleRate = 44100;
            const float duration = 0.42f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float phase = t % 0.14f;
                float envelope = phase < 0.08f ? Mathf.Sin(Mathf.PI * phase / 0.08f) : 0f;
                samples[i] = Mathf.Sin(2f * Mathf.PI * 330f * t) * envelope * 0.45f;
            }
            AudioClip clip = AudioClip.Create("Study Boundary Triple Warning", sampleCount, 1,
                sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static int HazardPriority(SemanticSpaceKind kind)
        {
            switch (kind)
            {
                case SemanticSpaceKind.Road: return 4;
                case SemanticSpaceKind.Curb: return 3;
                case SemanticSpaceKind.Crosswalk: return 2;
                case SemanticSpaceKind.Sidewalk: return 1;
                default: return 0;
            }
        }
    }
}
