using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class SemanticPedestrianController : MonoBehaviour
    {
        [SerializeField] private SemanticSpaceMap semanticMap;
        [SerializeField] private Terrain terrain;
        [SerializeField] private float eyeHeight = 1.7f;
        [SerializeField] private float walkSpeed = 1.4f;
        [SerializeField] private float fastWalkSpeed = 2.5f;
        [SerializeField] private float mouseSensitivity = 0.08f;
        [SerializeField] private float boundaryInset = 0.25f;

        [SerializeField] private bool pitchEnabled = true;

        private float pitch;
        private float nextBoundaryEventTime;
        private IPedestrianMoveSource moveSource;

        public event Action<Vector3> BoundaryContact;
        public SemanticSpaceMap Map => semanticMap;
        public Terrain GroundTerrain => terrain;
        public bool PitchEnabled => pitchEnabled;
        public IPedestrianMoveSource MoveSource => moveSource;

        /// <summary>
        /// Hands walking and turning to something outside this controller, such
        /// as the touch router on a handheld build. Passing null restores the
        /// keyboard and mouse path, which is what a desktop session keeps using.
        /// </summary>
        public void SetMoveSource(IPedestrianMoveSource source)
        {
            moveSource = source;
        }

        public void SetPitchEnabled(bool value)
        {
            pitchEnabled = value;
            if (!value)
                LevelPitch();
        }

        public void Configure(SemanticSpaceMap map, Terrain ground)
        {
            semanticMap = map;
            terrain = ground;
        }

        private void Start()
        {
            if (terrain == null)
                terrain = Terrain.activeTerrain;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SnapToGround();
        }

        private void Update()
        {
            ReadInput(out Vector2 input, out bool fast, out Vector2 look);
            transform.Rotate(0f, look.x, 0f, Space.World);
            if (pitchEnabled)
                pitch = Mathf.Clamp(pitch - look.y, -75f, 75f);
            Vector3 euler = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(pitchEnabled ? pitch : 0f, euler.y, 0f);

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 delta = (forward * input.y + right * input.x);
            if (delta.sqrMagnitude > 1f)
                delta.Normalize();
            Vector3 requested = transform.position + delta *
                (fast ? fastWalkSpeed : walkSpeed) * Time.deltaTime;

            bool touchedBoundary = ClampToStudyArea(ref requested);
            transform.position = requested;
            SnapToGround();

            if (touchedBoundary && Time.unscaledTime >= nextBoundaryEventTime)
            {
                nextBoundaryEventTime = Time.unscaledTime + 1f;
                BoundaryContact?.Invoke(delta.sqrMagnitude > 0f ? delta.normalized : transform.forward);
            }
        }

        public bool ClampToStudyArea(ref Vector3 position)
        {
            if (semanticMap == null)
                return false;
            Rect bounds = semanticMap.StudyBounds;
            float x = Mathf.Clamp(position.x, bounds.xMin + boundaryInset, bounds.xMax - boundaryInset);
            float z = Mathf.Clamp(position.z, bounds.yMin + boundaryInset, bounds.yMax - boundaryInset);
            bool clamped = !Mathf.Approximately(x, position.x) || !Mathf.Approximately(z, position.z);
            position.x = x;
            position.z = z;
            return clamped;
        }

        private void LevelPitch()
        {
            pitch = 0f;
            Vector3 euler = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(0f, euler.y, 0f);
        }

        public void SnapToGround()
        {
            if (terrain == null || terrain.terrainData == null)
                return;
            Vector3 position = transform.position;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + eyeHeight;
            transform.position = position;
        }

        /// <summary>
        /// Asks the attached source, or falls back to reading the devices
        /// directly. Look is returned already in degrees for this frame, so a
        /// touch source can turn at its own rate without inheriting the mouse
        /// sensitivity that only a mouse delta needs.
        /// </summary>
        private void ReadInput(out Vector2 movement, out bool fast,
            out Vector2 lookDegrees)
        {
            if (moveSource != null)
            {
                moveSource.ReadMovement(out movement, out fast, out lookDegrees);
                return;
            }

            ReadMove(out movement, out fast);
            ReadLook(out Vector2 look);
            lookDegrees = look * mouseSensitivity;
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
