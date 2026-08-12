using UnityEngine;
using UnityEditor;

namespace CityGen3D.EditorExtension
{
    // custom inspector FreeCam to show controls 
    [CustomEditor( typeof( FreeCam ) )]
    public class EditorFreeCam : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox( "[W] = forwards\n" +
                                     "[S] = backwards\n" +
                                     "[A] = left\n" +
                                     "[D] = right\n" +
                                     "[LeftShift] = move faster\n" +
                                     "[Mouse] = look around\n" +
                                     "[LeftCtrl] = mouse look on/off",
                                     MessageType.Info, true );

            DrawDefaultInspector();        
        }
    }
}
