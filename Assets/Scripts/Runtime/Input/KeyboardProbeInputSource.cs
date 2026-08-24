using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CityGenPedestrianProbe
{
    /// <summary>
    /// Walking and turning from a keyboard and mouse, offered to the router as
    /// one more source.
    /// </summary>
    /// <remarks>
    /// Only movement and look. The sweep keys are already served by
    /// <see cref="SweepKeyboardControls"/>, which polls them directly, and
    /// answering them here as well would apply each press twice.
    ///
    /// The router takes over as the controller's move source the moment it is
    /// attached, so without this the desktop path inside the controller becomes
    /// unreachable and WASD stops working. Being a source rather than a special
    /// case also means a phone with a keyboard paired gets both at once: the
    /// experimenter drives the condition while the participant aims.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class KeyboardProbeInputSource : MonoBehaviour, IProbeInputSource
    {
        // Mirrors SemanticPedestrianController's own default, which is what a
        // mouse delta needed before the router existed. Kept a field so the two
        // can be tuned apart if a session ever wants that.
        [SerializeField] private float mouseSensitivity = 0.08f;

        public string SourceId => "keyboard";

        public bool IsPresent
        {
#if ENABLE_INPUT_SYSTEM
            get { return Keyboard.current != null || Mouse.current != null; }
#else
            get { return true; }
#endif
        }

        public ProbeInputFrame Read(float deltaSeconds, float currentSweepAngle,
            float currentRangeMeters)
        {
            ReadMove(out Vector2 move, out bool fast);
            ReadLook(out Vector2 look);
            return new ProbeInputFrame(move, look * mouseSensitivity, fast,
                float.NaN, 0f, float.NaN, false, false, false);
        }

        private static void ReadMove(out Vector2 movement, out bool fast)
        {
            movement = Vector2.zero;
            fast = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) movement.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) movement.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) movement.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) movement.x -= 1f;
            fast = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
#else
            movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        }

        private static void ReadLook(out Vector2 look)
        {
#if ENABLE_INPUT_SYSTEM
            look = Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue();
#else
            look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
        }
    }
}
