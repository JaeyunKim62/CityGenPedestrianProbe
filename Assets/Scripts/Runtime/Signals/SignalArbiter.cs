using UnityEngine;

namespace CityGenPedestrianProbe
{
    public enum SignalAdmission
    {
        Admitted,
        RejectedInvalid,
        RejectedTooSoon,
        RejectedByAlarm,
        RejectedStateNotQuiet
    }

    public readonly struct SignalArbitrationSettings
    {
        public readonly float MinimumGapSeconds;
        public readonly float AlarmSuppressionSeconds;
        public readonly float StateQuietSeconds;

        public SignalArbitrationSettings(float minimumGapSeconds,
            float alarmSuppressionSeconds, float stateQuietSeconds)
        {
            MinimumGapSeconds = Mathf.Max(0f, minimumGapSeconds);
            AlarmSuppressionSeconds = Mathf.Max(0f, alarmSuppressionSeconds);
            StateQuietSeconds = Mathf.Max(0f, stateQuietSeconds);
        }

        public static SignalArbitrationSettings Default =>
            new SignalArbitrationSettings(0.12f, 0.4f, 2f);
    }

    public sealed class SignalArbiter
    {
        private readonly SignalArbitrationSettings settings;
        private bool hasAdmitted;
        private float lastAdmittedTime;
        private SignalChannel lastAdmittedChannel;
        private float alarmSuppressedUntil;

        public SignalArbiter() : this(SignalArbitrationSettings.Default)
        {
        }

        public SignalArbiter(SignalArbitrationSettings arbitrationSettings)
        {
            settings = arbitrationSettings;
            Reset();
        }

        public SignalArbitrationSettings Settings => settings;

        public void Reset()
        {
            hasAdmitted = false;
            lastAdmittedTime = 0f;
            lastAdmittedChannel = SignalChannel.State;
            alarmSuppressedUntil = float.NegativeInfinity;
        }

        public SignalAdmission Admit(in SignalIntent intent, float timeSeconds)
        {
            if (!intent.IsValid)
                return SignalAdmission.RejectedInvalid;
            if (intent.Channel != SignalChannel.Alarm && timeSeconds < alarmSuppressedUntil)
                return SignalAdmission.RejectedByAlarm;

            if (hasAdmitted)
            {
                float elapsed = timeSeconds - lastAdmittedTime;
                if (intent.Channel == SignalChannel.State &&
                    elapsed < settings.StateQuietSeconds)
                    return SignalAdmission.RejectedStateNotQuiet;
                if (elapsed < settings.MinimumGapSeconds &&
                    intent.Channel <= lastAdmittedChannel)
                    return SignalAdmission.RejectedTooSoon;
            }

            hasAdmitted = true;
            lastAdmittedTime = timeSeconds;
            lastAdmittedChannel = intent.Channel;
            if (intent.Channel == SignalChannel.Alarm)
                alarmSuppressedUntil = timeSeconds + settings.AlarmSuppressionSeconds;
            return SignalAdmission.Admitted;
        }
    }
}
