using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class AudioSignalRenderer : MonoBehaviour, ISignalRenderer
    {
        private const int OneShotVoiceCount = 4;
        private const float MaxAudibleMeters = 15f;

        [SerializeField] private SignalCatalog catalog;

        private Transform listener;
        private readonly Dictionary<string, AudioSource> sustainedVoices =
            new Dictionary<string, AudioSource>(StringComparer.Ordinal);
        private readonly Stack<AudioSource> freeSustainedVoices = new Stack<AudioSource>();
        private readonly Dictionary<string, AudioClip> oneShotClips =
            new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> loopClips =
            new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private AudioSource[] oneShotVoices = Array.Empty<AudioSource>();
        private AudioSource flatVoice;
        private AudioClip attackClip;
        private int nextOneShotVoice;

        public event Action<string> SignalUnresolved;

        public SignalCatalog Catalog => catalog;
        public int ActiveSustainedCount => sustainedVoices.Count;

        public void Configure(SignalCatalog signalCatalog, Transform listenerTransform)
        {
            StopAll();
            catalog = signalCatalog;
            listener = listenerTransform;
            oneShotClips.Clear();
            loopClips.Clear();
        }

        public void Render(in SignalIntent intent)
        {
            if (!intent.IsValid || catalog == null)
                return;
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
            if (!sustainedVoices.TryGetValue(key, out AudioSource voice))
                return;

            sustainedVoices.Remove(key);
            if (voice == null)
                return;
            voice.Stop();
            voice.clip = null;
            freeSustainedVoices.Push(voice);
        }

        public void StopAll()
        {
            foreach (KeyValuePair<string, AudioSource> pair in sustainedVoices)
            {
                AudioSource voice = pair.Value;
                if (voice == null)
                    continue;
                voice.Stop();
                voice.clip = null;
                freeSustainedVoices.Push(voice);
            }
            sustainedVoices.Clear();

            for (int i = 0; i < oneShotVoices.Length; i++)
                oneShotVoices[i]?.Stop();
            flatVoice?.Stop();
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
            if (!sustainedVoices.TryGetValue(intent.SignalId, out AudioSource voice) ||
                voice == null)
            {
                voice = TakeSustainedVoice();
                if (voice == null)
                    return;
                sustainedVoices[intent.SignalId] = voice;
                voice.clip = clip;
                voice.loop = true;
                voice.volume = definition.Level;
                voice.Play();
                started = true;
            }

            voice.transform.position = ResolvePosition(intent);
            AudioLowPassFilter filter = voice.GetComponent<AudioLowPassFilter>();
            if (filter != null)
                filter.cutoffFrequency = definition.CutoffHzAt(intent.DistanceMeters);
            if (started && definition.AttackClick && attackClip != null)
                voice.PlayOneShot(attackClip, definition.Level);
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

        private AudioSource TakeSustainedVoice()
        {
            while (freeSustainedVoices.Count > 0)
            {
                AudioSource pooled = freeSustainedVoices.Pop();
                if (pooled != null)
                    return pooled;
            }
            return CreateVoice($"Sustained Signal Voice {sustainedVoices.Count}", true, true);
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
            source.minDistance = 0.5f;
            source.maxDistance = MaxAudibleMeters;
            if (lowPass)
                child.AddComponent<AudioLowPassFilter>();
            return source;
        }

        private void OnDisable()
        {
            StopAll();
        }
    }
}
