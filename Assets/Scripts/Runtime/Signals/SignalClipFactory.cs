using UnityEngine;

namespace CityGenPedestrianProbe
{
    public static class SignalClipFactory
    {
        public const int SampleRate = 44100;
        private const float AttackSeconds = 0.008f;
        private const float SustainedLoopTargetSeconds = 0.5f;
        private const float RoughnessDepth = 0.6f;

        public static AudioClip CreateOneShot(SignalDefinition definition)
        {
            if (definition == null)
                return null;

            int sampleCount = Mathf.CeilToInt(definition.DurationSeconds * SampleRate);
            var samples = new float[sampleCount];
            var random = new System.Random(StableHash(definition.SignalId));
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float envelope = Mathf.Sin(Mathf.PI * i / Mathf.Max(1, sampleCount - 1));
                samples[i] = Oscillate(definition, time, random) * envelope;
            }

            return Build($"{definition.SignalId}.oneshot", samples);
        }

        public static AudioClip CreateSustainedLoop(SignalDefinition definition)
        {
            if (definition == null)
                return null;

            int sampleCount = LoopSampleCount(definition.BaseFrequencyHz);
            var samples = new float[sampleCount];
            var random = new System.Random(StableHash(definition.SignalId));
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                samples[i] = Oscillate(definition, time, random);
            }

            return Build($"{definition.SignalId}.loop", samples);
        }

        public static AudioClip CreateAttack()
        {
            int sampleCount = Mathf.CeilToInt(AttackSeconds * SampleRate);
            var samples = new float[sampleCount];
            var random = new System.Random(StableHash("signal.attack"));
            float previous = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                previous = Mathf.Lerp(previous, white, 0.65f);
                float decay = 1f - i / (float)sampleCount;
                samples[i] = previous * decay * decay;
            }

            return Build("signal.attack", samples);
        }

        public static int LoopSampleCount(float baseFrequencyHz)
        {
            float frequency = Mathf.Max(20f, baseFrequencyHz);
            int cycles = Mathf.Max(1, Mathf.RoundToInt(SustainedLoopTargetSeconds * frequency));
            return Mathf.Max(2, Mathf.RoundToInt(cycles * SampleRate / frequency));
        }

        private static float Oscillate(SignalDefinition definition, float time,
            System.Random random)
        {
            float phase = Mathf.Repeat(time * definition.BaseFrequencyHz, 1f);
            float value = definition.Waveform switch
            {
                SignalWaveform.Sine => Mathf.Sin(2f * Mathf.PI * phase),
                SignalWaveform.Triangle => 4f * Mathf.Abs(phase - 0.5f) - 1f,
                SignalWaveform.Harmonic => Harmonic(phase, definition.HarmonicCount),
                SignalWaveform.Noise => (float)(random.NextDouble() * 2.0 - 1.0),
                _ => 0f
            };

            if (definition.RoughnessHz > 0f)
            {
                float modulation = 0.5f + 0.5f *
                    Mathf.Sin(2f * Mathf.PI * definition.RoughnessHz * time);
                value *= 1f - RoughnessDepth + RoughnessDepth * modulation;
            }

            return value;
        }

        private static float Harmonic(float phase, int harmonicCount)
        {
            float sum = 0f;
            float normalizer = 0f;
            for (int n = 1; n <= harmonicCount; n += 2)
            {
                sum += Mathf.Sin(2f * Mathf.PI * n * phase) / n;
                normalizer += 1f / n;
            }
            return normalizer > 0f ? sum / normalizer : 0f;
        }

        private static AudioClip Build(string clipName, float[] samples)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                const int offset = (int)2166136261;
                const int prime = 16777619;
                int hash = offset;
                if (string.IsNullOrEmpty(value))
                    return hash;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash ^ value[i]) * prime;
                return hash;
            }
        }
    }
}
