using System.Globalization;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// Writes one row naming the device and the audio settings the session
    /// actually got.
    /// </summary>
    /// <remarks>
    /// Without this row, a click or a latency complaint from a phone session
    /// cannot be told apart from the same complaint on a desktop. In
    /// particular <c>audio_hz</c> against <c>clip_hz</c> answers on its own
    /// whether the generated clips match the rate the device is mixing at.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ProbeSessionAnnotator : MonoBehaviour
    {
        // Logged from the first Update because SemanticSessionLogger opens its
        // file in Start, and Start ordering between components is undefined.
        private void Update()
        {
            enabled = false;

            SemanticSessionLogger logger = GetComponent<SemanticSessionLogger>();
            if (logger == null)
                return;

            AudioConfiguration audio = AudioSettings.GetConfiguration();
            var c = CultureInfo.InvariantCulture;
            logger.LogEvent("device_info", SemanticHit.Unknown(transform.position),
                $"device={Safe(SystemInfo.deviceModel)}|" +
                $"os={Safe(SystemInfo.operatingSystem)}|" +
                $"graphics={SystemInfo.graphicsDeviceType}|" +
                $"audio_hz={audio.sampleRate.ToString(c)}|" +
                $"dsp={audio.dspBufferSize.ToString(c)}|" +
                $"speakers={audio.speakerMode}|" +
                $"clip_hz={SignalClipFactory.SampleRate.ToString(c)}|" +
                $"screen={Screen.width}x{Screen.height}|" +
                $"dpi={Screen.dpi.ToString("F0", c)}|" +
                // Whether the footfalls were felt as well as heard. A session
                // on a handset with no motor, or one where reaching it failed,
                // ran a quieter condition than one where it worked, and the
                // difference is invisible from anywhere else in the file.
                $"haptics={HasHaptics()}");
        }

        private bool HasHaptics()
        {
            HapticSignalRenderer haptics = GetComponent<HapticSignalRenderer>();
            return haptics != null && haptics.HasVibrator;
        }

        private static string Safe(string value) =>
            string.IsNullOrWhiteSpace(value) ? "unknown" : value.Replace('|', '/');
    }
}
