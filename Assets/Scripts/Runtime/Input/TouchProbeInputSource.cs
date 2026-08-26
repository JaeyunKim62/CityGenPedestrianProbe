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
        private readonly HashSet<int> menuTouches = new HashSet<int>();
        private readonly List<int> liftedTouches = new List<int>();
        private BlindTouchGestures gestures;
        private ProbeSettingsMenu menu;
        private DeviceTiltInputSource tilt;
        private int builtForWidth = -1;

        /// <summary>
        /// Whether the handset's own attitude is steering, in which case this
        /// half of the screen only has to say go.
        /// </summary>
        /// <remarks>
        /// Read off the tilt source rather than set from the settings menu, so
        /// there is one answer to the question rather than two that have to be
        /// kept in step.
        /// </remarks>
        private bool TiltSteers
        {
            get
            {
                if (tilt == null)
                    tilt = GetComponent<DeviceTiltInputSource>();
                return tilt != null && tilt.SteeringEnabled;
            }
        }

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
            gestures.WalkStyle = TiltSteers ? TouchWalkStyle.Hold : TouchWalkStyle.Stick;
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
            liftedTouches.Clear();
#if ENABLE_INPUT_SYSTEM
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
                return;

            if (menu == null)
                menu = GetComponent<ProbeSettingsMenu>();

            var all = screen.touches;
            for (int i = 0; i < all.Count; i++)
            {
                TouchControl touch = all[i];
                int id = touch.touchId.ReadValue();
                if (!touch.press.isPressed)
                {
                    liftedTouches.Add(id);
                    continue;
                }

                Vector2 position = touch.position.ReadValue();

                // Judged where the finger landed and then held for as long as
                // it stays down, the same way a stick keeps the half of the
                // screen it started in. Deciding afresh every frame would hand
                // a thumb already working the sweep over to the menu the moment
                // it strayed under the panel, resetting the angle it was
                // holding.
                if (touch.press.wasPressedThisFrame &&
                    menu != null && menu.OccupiesScreenPoint(position))
                    menuTouches.Add(id);
                if (menuTouches.Contains(id))
                    continue;

                touches.Add(new ProbeTouch(id, position));
            }

            for (int i = 0; i < liftedTouches.Count; i++)
                menuTouches.Remove(liftedTouches[i]);
#endif
        }
    }
}
