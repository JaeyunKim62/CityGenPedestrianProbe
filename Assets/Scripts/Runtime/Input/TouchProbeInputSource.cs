using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// Reads the touchscreen and hands the frames to <see cref="BlindTouchGestures"/>.
    /// </summary>
    /// <remarks>
    /// Everything this type does is device access. The decisions all live in the
    /// gesture reader, which has no Unity input dependency and can therefore be
    /// tested without a phone in hand.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TouchProbeInputSource : MonoBehaviour, IProbeInputSource
    {
        // Some devices report no density at all; a mid-range phone is a better
        // guess than dividing by zero.
        private const float FallbackDpi = 400f;

        private readonly List<ProbeTouch> touches = new List<ProbeTouch>();
        private BlindTouchGestures gestures;
        private int builtForWidth = -1;

        public string SourceId => "touch";

        public bool IsPresent
        {
#if ENABLE_INPUT_SYSTEM
            get { return Touchscreen.current != null; }
#else
            get { return Input.touchSupported; }
#endif
        }

        public ProbeInputFrame Read(float deltaSeconds, float currentSweepAngle,
            float currentRangeMeters)
        {
            EnsureGestures();
            CollectTouches();
            return gestures.Resolve(touches, Time.unscaledTime, deltaSeconds,
                currentRangeMeters);
        }

        private void EnsureGestures()
        {
            if (gestures != null && builtForWidth == Screen.width)
                return;

            float dpi = Screen.dpi > 1f ? Screen.dpi : FallbackDpi;
            builtForWidth = Screen.width;
            gestures = new BlindTouchGestures(dpi / 25.4f, builtForWidth);
            Debug.Log($"Touch controls ready: {builtForWidth}x{Screen.height} at " +
                      $"{dpi:F0} dpi. Left half walks and turns, right half aims " +
                      "the sweep and sets its range.", this);
        }

        private void CollectTouches()
        {
            touches.Clear();
#if ENABLE_INPUT_SYSTEM
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
                return;

            var all = screen.touches;
            for (int i = 0; i < all.Count; i++)
            {
                TouchControl touch = all[i];
                if (!touch.press.isPressed)
                    continue;
                touches.Add(new ProbeTouch(touch.touchId.ReadValue(),
                    touch.position.ReadValue()));
            }
#endif
        }
    }
}
