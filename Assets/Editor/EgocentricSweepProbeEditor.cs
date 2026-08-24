using UnityEditor;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor
{
    [CustomEditor(typeof(EgocentricSweepProbe))]
    internal sealed class EgocentricSweepProbeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var targetProbe = (EgocentricSweepProbe)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime result", EditorStyles.boldLabel);
            if (!Application.isPlaying || !targetProbe.HasCurrentResult)
            {
                EditorGUILayout.LabelField("Status", "Not queried");
                return;
            }

            SpatialProbeHit hit = targetProbe.CurrentHit;
            EditorGUILayout.LabelField("Hit", hit.HasHit ? "Yes" : "No");
            EditorGUILayout.LabelField("Target",
                hit.HasHit ? hit.To.DisplayLabel : "None within range");
            EditorGUILayout.LabelField("Distance",
                hit.HasHit ? $"{hit.DistanceMeters:F2} m" : "N/A");
            EditorGUILayout.LabelField("Requested range", $"{hit.RequestedRangeMeters:F2} m");
            if (hit.HasHit)
            {
                EditorGUILayout.LabelField("From", hit.From.DisplayLabel);
                EditorGUILayout.LabelField("Feature ID", hit.To.FeatureId.ToString());
            }

            if (GUI.changed)
                SceneView.RepaintAll();
            Repaint();
        }
    }
}
