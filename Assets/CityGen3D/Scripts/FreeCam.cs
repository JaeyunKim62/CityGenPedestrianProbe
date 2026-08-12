using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace CityGen3D
{
    public class FreeCam : MonoBehaviour
    {
        public enum RotationAxes { MouseXAndY = 0, MouseX = 1, MouseY = 2 }
        public RotationAxes axes = RotationAxes.MouseXAndY;
        public float speed = 20.0f;
        public float fastSpeed = 100.0f;
        public float sensitivity = 10.0f;
        public float minimumX = -360.0f;
        public float maximumX = 360.0f;
        public float minimumY = -60.0f;
        public float maximumY = 60.0f;
        public float rotationY = 0.0f;
        public float movement = 0.0f;
        public float strafe = 0.0f;
        public bool mouseLook = true;

        void Update()
        {
            // toggle mouse look
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Keyboard.current != null && Keyboard.current.leftCtrlKey.wasReleasedThisFrame)
#else
            if ( Input.GetKeyUp( KeyCode.LeftControl ) )
#endif
            {
                mouseLook = !mouseLook;
            }

            // read mouse delta (new input system gives raw pixel delta)
            Vector2 mouseDelta = Vector2.zero;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Mouse.current != null)
            {
                mouseDelta = Mouse.current.delta.ReadValue();
            }
#else
            mouseDelta.x = Input.GetAxis( "Mouse X" );
            mouseDelta.y = Input.GetAxis( "Mouse Y" );
#endif

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // scale factor to make new system pixel delta feel similar to old GetAxis
            // tune this if you want a different feel
            const float newInputMouseScale = 0.02f;
#endif

            // mouse look
            if ( mouseLook )
            {
                if ( axes == RotationAxes.MouseXAndY )
                {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                    float rotationX = transform.localEulerAngles.y + mouseDelta.x * sensitivity * newInputMouseScale;
                    rotationY += mouseDelta.y * sensitivity * newInputMouseScale;
#else
                    float rotationX = transform.localEulerAngles.y + mouseDelta.x * sensitivity;
                    rotationY += mouseDelta.y * sensitivity;
#endif
                    rotationY = Mathf.Clamp( rotationY, minimumY, maximumY );
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                    rotationX = Mathf.Clamp( rotationX, minimumX, maximumX );
#else
                    rotationX = Mathf.Clamp( rotationX, minimumX, maximumX );
#endif
                    transform.localEulerAngles = new Vector3( -rotationY, rotationX, 0 );
                }
                else if ( axes == RotationAxes.MouseX )
                {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                    transform.Rotate( 0, mouseDelta.x * sensitivity * newInputMouseScale, 0 );
#else
                    transform.Rotate( 0, mouseDelta.x * sensitivity, 0 );
#endif
                }
                else
                {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                    rotationY += mouseDelta.y * sensitivity * newInputMouseScale;
#else
                    rotationY += mouseDelta.y * sensitivity;
#endif
                    rotationY = Mathf.Clamp( rotationY, minimumY, maximumY );
                    transform.localEulerAngles = new Vector3( -rotationY, transform.localEulerAngles.y, 0 );
                }
            }

            // speed
            float velocity = speed;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
#else
            if ( Input.GetKey( KeyCode.LeftShift ) )
#endif
            {
                // use fast speed while user is pressing Shift
                velocity = fastSpeed;
            }

            // movement
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // build axis-like values from keyboard for the new input system
            float move = 0.0f;
            float str = 0.0f;
            if ( Keyboard.current != null )
            {
                if ( Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed ) move += 1.0f;
                if ( Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed ) move -= 1.0f;
                if ( Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed ) str += 1.0f;
                if ( Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed ) str -= 1.0f;
            }
            movement = move;
            strafe = str;
#else
            movement = Input.GetAxis( "Vertical" );
            strafe = Input.GetAxis( "Horizontal" );
#endif

            if ( movement != 0 )
            {
                transform.position = transform.position + transform.forward * movement * velocity * Time.deltaTime;
            }
            if ( strafe != 0 )
            {
                transform.position = transform.position + transform.right * strafe * velocity * Time.deltaTime;
            }

        }

    }
}