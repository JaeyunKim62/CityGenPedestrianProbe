using System;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// Renders self-motion as a tap through the handset's vibration motor.
    /// </summary>
    /// <remarks>
    /// <para>A second renderer on the same signal bus as the audio one, picked
    /// up automatically because the coordinator asks the game object for every
    /// <see cref="ISignalRenderer"/> on it. Nothing had to be told this exists.
    /// </para>
    ///
    /// <para>Only footfalls, on purpose. A phone has one motor and it can say
    /// one thing at a time, so spending it on the channel about one's own body
    /// keeps it out of the way of the sweep, which is what a session measures
    /// and which lives entirely in the ears. It also answers the one real
    /// weakness of the footfall sound: at 95 Hz a phone's own speaker barely
    /// reproduces it, and a participant without headphones would otherwise get
    /// no self-motion cue at all.</para>
    ///
    /// <para><c>Handheld.Vibrate</c> is not usable here. It buzzes for half a
    /// second, and at nearly two steps a second that is not a footfall, it is a
    /// continuous rumble. The platform's own one-shot effect gives a tap short
    /// enough to have a rhythm, and it needs API 26, which is already the
    /// minimum this app builds against.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HapticSignalRenderer : MonoBehaviour, ISignalRenderer
    {
        // Short enough to read as a tap rather than a buzz, firm enough to feel
        // through a hand that is also gripping the phone.
        private const long StepMilliseconds = 18L;
        private const int StepAmplitude = 160;

        /// <summary>Never raised: the pulse is a constant, not a lookup.</summary>
        public event Action<string> SignalUnresolved
        {
            add { }
            remove { }
        }

        /// <summary>Whether this device turned out to have a motor to drive.</summary>
        public bool HasVibrator { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private AndroidJavaObject stepEffect;
        private bool resolved;
#endif

        // Resolved up front rather than on the first step, so that the session
        // annotator can record whether this run had haptics at all. It writes
        // its row from the first Update, by which time every Start has run.
        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureVibrator();
#endif
        }

        public void Render(in SignalIntent intent)
        {
            if (intent.Channel != SignalChannel.Locomotion ||
                intent.Form != SignalForm.OneShot)
                return;

            Pulse();
        }

        // A tap is over before anything could ask for it to stop.
        public void Release(string signalId)
        {
        }

        public void StopAll()
        {
        }

        private void Pulse()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureVibrator();
            if (vibrator == null || stepEffect == null)
                return;

            // The effect is built once and replayed. Making a Java object per
            // step would put two allocations a second through JNI for the whole
            // of a session.
            vibrator.Call("vibrate", stepEffect);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void EnsureVibrator()
        {
            if (resolved)
                return;
            resolved = true;

            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    // Android 12 moved the motor behind a manager that knows
                    // about devices with more than one.
                    if (version.GetStatic<int>("SDK_INT") >= 31)
                    {
                        using (AndroidJavaObject manager = activity.Call<AndroidJavaObject>(
                                   "getSystemService", "vibrator_manager"))
                        {
                            vibrator = manager?.Call<AndroidJavaObject>("getDefaultVibrator");
                        }
                    }
                    else
                    {
                        vibrator = activity.Call<AndroidJavaObject>(
                            "getSystemService", "vibrator");
                    }
                }

                if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                {
                    vibrator = null;
                    Debug.LogWarning("This device has no vibration motor, so steps " +
                                     "are heard and not felt.", this);
                    return;
                }

                using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    stepEffect = effects.CallStatic<AndroidJavaObject>("createOneShot",
                        StepMilliseconds, StepAmplitude);
                }

                HasVibrator = stepEffect != null;
            }
            catch (Exception exception)
            {
                vibrator = null;
                stepEffect = null;
                Debug.LogWarning($"Could not reach the vibration motor, so steps are " +
                                 $"heard and not felt: {exception.Message}", this);
            }
        }

        private void OnDestroy()
        {
            stepEffect?.Dispose();
            vibrator?.Dispose();
            stepEffect = null;
            vibrator = null;
        }
#endif
    }
}
