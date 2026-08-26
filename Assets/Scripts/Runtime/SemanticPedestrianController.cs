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

        private readonly ProbeHeading heading = new ProbeHeading();
        private float pitch;
        private float nextBoundaryEventTime;
        private IPedestrianMoveSource moveSource;

        public event Action<Vector3> BoundaryContact;
        public SemanticSpaceMap Map => semanticMap;
        public Terrain GroundTerrain => terrain;
        public bool PitchEnabled => pitchEnabled;
        public IPedestrianMoveSource MoveSource => moveSource;

        /// <summary>
        /// Where this body is pointed, and where its attention sits.
        /// </summary>
        /// <remarks>
        /// The transform's yaw is now a rendering of this rather than the store
        /// of it. Anything that wants to steer the body absolutely — a device
        /// that measures its own angle — asks this rather than writing the
        /// transform, because the transform is overwritten from here every
        /// frame and a write to it would last exactly one frame.
        /// </remarks>
        public ProbeHeading Heading => heading;

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

        /// <summary>
        /// Takes the bearing the scene spawned this body at.
        /// </summary>
        /// <remarks>
        /// In Awake rather than Start because the session logger writes its
        /// first row from its own Start, and ordering between two components'
        /// Start is undefined. Seeded late, that row would claim the session
        /// began facing north whichever way the scene actually pointed.
        /// </remarks>
        private void Awake()
        {
            heading.SetBearing(transform.eulerAngles.y);
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
            ReadInput(out Vector2 input, out bool fast, out Vector2 look,
                out float bearingAbsolute);

            // A device that measures its own attitude wins over accumulated
            // turning while it is reading, so a thumb and a tilted phone cannot
            // fight over the same body. When the reading lapses — a phone laid
            // flat has no bearing to give — turning takes over again from
            // wherever the body was left.
            if (!float.IsNaN(bearingAbsolute))
                heading.SetBearing(bearingAbsolute);
            else
                heading.TurnBy(look.x);
            if (pitchEnabled)
                pitch = Mathf.Clamp(pitch - look.y, -75f, 75f);
            transform.localEulerAngles = new Vector3(pitchEnabled ? pitch : 0f,
                heading.BearingDegrees, 0f);

            // One rotation, and which angle it is decides the frame. With the
            // body frame this is the same direction the transform's own forward
            // and right gave before, since both are that yaw with the pitch
            // projected out of them.
            Vector3 delta = Quaternion.Euler(0f, heading.MovementBearingDegrees, 0f) *
                            new Vector3(input.x, 0f, input.y);
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
            out Vector2 lookDegrees, out float bearingAbsolute)
        {
            if (moveSource != null)
            {
                moveSource.ReadMovement(out movement, out fast, out lookDegrees,
                    out bearingAbsolute);
                return;
            }

            ReadMove(out movement, out fast);
            ReadLook(out Vector2 look);
            lookDegrees = look * mouseSensitivity;
            bearingAbsolute = float.NaN;
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
