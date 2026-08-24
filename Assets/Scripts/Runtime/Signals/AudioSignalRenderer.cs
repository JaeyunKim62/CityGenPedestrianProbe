using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class AudioSignalRenderer : MonoBehaviour, ISignalRenderer
    {
        private const int OneShotVoiceCount = 4;
        private const float GateEdge = 0.06f;

        [SerializeField] private SignalCatalog catalog;
        [SerializeField, Min(0.5f)] private float fullLevelRadiusMeters = 2f;

        private Transform listener;
        private readonly Dictionary<string, SustainedVoice> sustainedVoices =
            new Dictionary<string, SustainedVoice>(StringComparer.Ordinal);
        private readonly Stack<SustainedVoice> freeSustainedVoices =
            new Stack<SustainedVoice>();
        private readonly Dictionary<string, AudioClip> oneShotClips =
            new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> loopClips =
            new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private AudioSource[] oneShotVoices = Array.Empty<AudioSource>();
        private AudioSource flatVoice;
        private AudioClip attackClip;
        private int nextOneShotVoice;
        private bool warnedAboutMissingCatalog;

        public event Action<string> SignalUnresolved;

        public SignalCatalog Catalog => catalog;
        public int ActiveSustainedCount => sustainedVoices.Count;

        public void Configure(SignalCatalog signalCatalog, Transform listenerTransform)
        {
            listener = listenerTransform;
            SetCatalog(signalCatalog);
        }

        public void SetCatalog(SignalCatalog signalCatalog)
        {
            StopAll();
            catalog = signalCatalog;
            warnedAboutMissingCatalog = false;
            oneShotClips.Clear();
            loopClips.Clear();
        }

        public void Render(in SignalIntent intent)
        {
            if (!intent.IsValid)
                return;
            if (catalog == null)
            {
                if (!warnedAboutMissingCatalog)
                {
                    warnedAboutMissingCatalog = true;
                    Debug.LogWarning($"Signal '{intent.SignalId}' was emitted but this " +
                                     "renderer has no catalog, so nothing can sound.", this);
                }
                return;
            }
            if (!catalog.TryResolve(intent.SignalId, out SignalDefinition definition))
            {
                SignalUnresolved?.Invoke(intent.SignalId);
                return;
            }

            EnsureVoices();
            if (intent.Form == SignalForm.Sustained)
                RenderSustained(intent, definition);
            else
                RenderOneShot(intent, definition);
        }

        public void Release(string signalId)
        {
            if (string.IsNullOrWhiteSpace(signalId))
                return;
            string key = signalId.Trim();
            if (!sustainedVoices.TryGetValue(key, out SustainedVoice voice))
                return;

            sustainedVoices.Remove(key);
            Recycle(voice);
        }

        public void StopAll()
        {
            foreach (KeyValuePair<string, SustainedVoice> pair in sustainedVoices)
                Recycle(pair.Value);
            sustainedVoices.Clear();

            for (int i = 0; i < oneShotVoices.Length; i++)
                oneShotVoices[i]?.Stop();
            flatVoice?.Stop();
        }

        private void Update()
        {
            foreach (KeyValuePair<string, SustainedVoice> pair in sustainedVoices)
            {
                SustainedVoice voice = pair.Value;
                if (voice.Source == null)
                    continue;
                if (voice.PulseHz <= 0f)
                {
                    voice.Source.volume = voice.Level;
                    continue;
                }

                voice.Phase = Mathf.Repeat(
                    voice.Phase + voice.PulseHz * Time.unscaledDeltaTime, 1f);
                voice.Source.volume = voice.Level * Gate(voice.Phase);
            }
        }

        private static float Gate(float phase)
        {
            const float duty = 0.5f;
            if (phase >= duty)
                return 0f;
            if (phase < GateEdge)
                return phase / GateEdge;
            if (phase > duty - GateEdge)
                return (duty - phase) / GateEdge;
            return 1f;
        }

        private void RenderOneShot(in SignalIntent intent, SignalDefinition definition)
        {
            AudioClip clip = ResolveClip(oneShotClips, definition, true);
            if (clip == null)
                return;

            AudioSource voice = intent.HasWorldPosition ? NextOneShotVoice() : flatVoice;
            if (voice == null)
                return;
            voice.transform.position = ResolvePosition(intent);
            voice.pitch = definition.PitchScaleAt(intent.DistanceMeters);
            if (definition.AttackClick && attackClip != null)
                voice.PlayOneShot(attackClip, definition.Level);
            voice.PlayOneShot(clip, definition.Level);
        }

        private void RenderSustained(in SignalIntent intent, SignalDefinition definition)
        {
            AudioClip clip = ResolveClip(loopClips, definition, false);
            if (clip == null)
                return;

            bool started = false;
            if (!sustainedVoices.TryGetValue(intent.SignalId, out SustainedVoice voice) ||
                voice.Source == null)
            {
                voice = TakeSustainedVoice();
                if (voice == null)
                    return;
                sustainedVoices[intent.SignalId] = voice;
                voice.Phase = 0f;
                voice.Source.clip = clip;
                voice.Source.loop = true;
                voice.Source.Play();
                started = true;
            }

            voice.Definition = definition;
            voice.Level = definition.Level;
            voice.PulseHz = definition.PulseHzAt(intent.DistanceMeters);
            voice.Source.transform.position = ResolvePosition(intent);
            voice.Source.pitch = definition.PitchScaleAt(intent.DistanceMeters);
            if (voice.PulseHz <= 0f)
                voice.Source.volume = voice.Level;
            if (voice.Filter != null)
                voice.Filter.cutoffFrequency = definition.CutoffHzAt(intent.DistanceMeters);
            if (started && definition.AttackClick && attackClip != null)
                voice.Source.PlayOneShot(attackClip, definition.Level);
        }

        private Vector3 ResolvePosition(in SignalIntent intent)
        {
            if (intent.HasWorldPosition)
                return intent.WorldPosition;
            return listener != null ? listener.position : transform.position;
        }

        private AudioClip ResolveClip(Dictionary<string, AudioClip> cache,
            SignalDefinition definition, bool oneShot)
        {
            string key = definition.SignalId;
            if (cache.TryGetValue(key, out AudioClip clip) && clip != null)
                return clip;

            clip = oneShot
                ? SignalClipFactory.CreateOneShot(definition)
                : SignalClipFactory.CreateSustainedLoop(definition);
            cache[key] = clip;
            return clip;
        }

        private AudioSource NextOneShotVoice()
        {
            if (oneShotVoices.Length == 0)
                return null;
            AudioSource voice = oneShotVoices[nextOneShotVoice];
            nextOneShotVoice = (nextOneShotVoice + 1) % oneShotVoices.Length;
            return voice;
        }

        private SustainedVoice TakeSustainedVoice()
        {
            while (freeSustainedVoices.Count > 0)
            {
                SustainedVoice pooled = freeSustainedVoices.Pop();
                if (pooled.Source != null)
                    return pooled;
            }

            AudioSource source = CreateVoice(
                $"Sustained Signal Voice {sustainedVoices.Count}", true, true);
            return new SustainedVoice
            {
                Source = source,
                Filter = source.GetComponent<AudioLowPassFilter>()
            };
        }

        private void Recycle(SustainedVoice voice)
        {
            if (voice?.Source == null)
                return;
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.Source.pitch = 1f;
            voice.PulseHz = 0f;
            freeSustainedVoices.Push(voice);
        }

        private void EnsureVoices()
        {
            if (attackClip == null)
                attackClip = SignalClipFactory.CreateAttack();
            if (oneShotVoices.Length == 0)
            {
                var created = new AudioSource[OneShotVoiceCount];
                for (int i = 0; i < created.Length; i++)
                    created[i] = CreateVoice($"One Shot Signal Voice {i}", true, false);
                oneShotVoices = created;
            }
            if (flatVoice == null)
                flatVoice = CreateVoice("Flat Signal Voice", false, false);
        }

        private AudioSource CreateVoice(string voiceName, bool spatial, bool lowPass)
        {
            var child = new GameObject(voiceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = Mathf.Max(0.5f, fullLevelRadiusMeters);
            source.maxDistance = 500f;
            if (lowPass)
                child.AddComponent<AudioLowPassFilter>();
            return source;
        }

        private void OnDisable()
        {
            StopAll();
        }

        private sealed class SustainedVoice
        {
            public AudioSource Source;
            public AudioLowPassFilter Filter;
            public SignalDefinition Definition;
            public float Level;
            public float PulseHz;
            public float Phase;
        }
    }
}
