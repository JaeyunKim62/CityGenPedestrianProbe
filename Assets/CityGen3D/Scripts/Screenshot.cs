using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace CityGen3D
{
    public class Screenshot : MonoBehaviour
    {
        public string filename = "screenshot";
        public int index = 0;

        void Update()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
#else
            if (Input.GetKeyDown( KeyCode.F1 ))
#endif
            {
                string str_file = filename + index.ToString() + ".png";
                ScreenCapture.CaptureScreenshot( str_file );
                Debug.Log( "CityGen3D: screenshot taken (" + str_file + ")" );

                index++;
            }
        }
    }
}