using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class SweepKeyboardControls : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float angleRateDegreesPerSecond = 90f;
        [SerializeField, Min(0.1f)] private float rangeStepMeters = 1f;
        [SerializeField] private bool allowModuleToggle = true;

        private EgocentricSweepProbe sweep;
        private FeedbackCoordinator coordinator;

        public void Configure(EgocentricSweepProbe sweepProbe, FeedbackCoordinator feedback)
        {
            sweep = sweepProbe;
            coordinator = feedback;
        }

        public void StepAngle(float direction, float deltaSeconds)
        {
            if (sweep == null || Mathf.Approximately(direction, 0f))
                return;
            sweep.SetRelativeAngle(sweep.RelativeAngleDegrees +
                direction * angleRateDegreesPerSecond * deltaSeconds);
        }

        public void StepRange(float direction)
        {
            if (sweep == null || Mathf.Approximately(direction, 0f))
                return;
            sweep.SetRange(sweep.RangeMeters + direction * rangeStepMeters);
        }

        public bool ToggleSemanticAudio()
        {
            if (!allowModuleToggle || coordinator == null)
                return false;
            if (!coordinator.TryGetModuleState(SemanticAudioGuide.StableModuleId,
                    out bool isEnabled))
                return false;
            return coordinator.SetModuleEnabled(SemanticAudioGuide.StableModuleId,
                !isEnabled, "manual-key");
        }

        private void Update()
        {
            if (sweep == null)
                return;

            float direction = 0f;
            if (Held(KeyCode.Q))
                direction -= 1f;
            if (Held(KeyCode.E))
                direction += 1f;
            StepAngle(direction, Time.unscaledDeltaTime);

            if (Pressed(KeyCode.R))
                sweep.SetRelativeAngle(0f);
            if (Pressed(KeyCode.LeftBracket))
                StepRange(-1f);
            if (Pressed(KeyCode.RightBracket))
                StepRange(1f);
            if (Pressed(KeyCode.G))
                ToggleSemanticAudio();
        }

        private static bool Held(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            KeyControl control = Resolve(key);
            return control != null && control.isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        private static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            KeyControl control = Resolve(key);
            return control != null && control.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static KeyControl Resolve(KeyCode key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return null;
            switch (key)
            {
                case KeyCode.Q: return keyboard.qKey;
                case KeyCode.E: return keyboard.eKey;
                case KeyCode.R: return keyboard.rKey;
                case KeyCode.G: return keyboard.gKey;
                case KeyCode.LeftBracket: return keyboard.leftBracketKey;
                case KeyCode.RightBracket: return keyboard.rightBracketKey;
                default: return null;
            }
        }
#endif
    }
}
