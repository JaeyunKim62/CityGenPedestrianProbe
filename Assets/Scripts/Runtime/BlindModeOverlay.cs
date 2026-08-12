using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class BlindModeOverlay : MonoBehaviour
    {
        [SerializeField] private bool blind = true;
        [SerializeField] private SemanticSpaceMap semanticMap;
        [SerializeField] private Transform player;
        [SerializeField] private bool logStateToConsole = true;
        private Texture2D blackTexture;
        private GUIStyle debugStyle;
        private GUIStyle mapLabelStyle;
        private SemanticDebugMapRenderer mapRenderer;
        private bool localMapView;
        private float nextConsoleLog;

        public event Action<bool> BlindModeChanged;
        public bool IsBlind => blind;

        public void Configure(SemanticSpaceMap map, Transform playerTransform)
        {
            semanticMap = map;
            player = playerTransform;
        }

        private void Awake()
        {
            mapRenderer = new SemanticDebugMapRenderer();
            blackTexture = new Texture2D(1, 1, TextureFormat.RGB24, false);
            blackTexture.SetPixel(0, 0, Color.black);
            blackTexture.Apply();
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool pressed;
#if ENABLE_INPUT_SYSTEM
            pressed = Keyboard.current != null &&
                      (Keyboard.current.f1Key.wasPressedThisFrame ||
                       Keyboard.current.bKey.wasPressedThisFrame);
#else
            pressed = Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.B);
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool mapPressed;
#if ENABLE_INPUT_SYSTEM
            mapPressed = Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
            mapPressed = Input.GetKeyDown(KeyCode.M);
#endif
            if (mapPressed)
                localMapView = !localMapView;
#endif
            if (pressed)
            {
                blind = !blind;
                BlindModeChanged?.Invoke(blind);
            }
#endif

            if (logStateToConsole && semanticMap != null && player != null &&
                Time.unscaledTime >= nextConsoleLog)
            {
                nextConsoleLog = Time.unscaledTime + 1f;
                Debug.Log(BuildStatusText(false), player);
            }
        }

        private void OnGUI()
        {
            if (blind)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), blackTexture,
                    ScaleMode.StretchToFill);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (semanticMap == null || player == null)
                return;
            if (debugStyle == null)
            {
                debugStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 16,
                    normal = { textColor = Color.white }
                };
            }
            GUI.Box(new Rect(12f, 12f, 650f, 230f), BuildStatusText(true), debugStyle);
            DrawSemanticMap();
#endif
        }

        private void DrawSemanticMap()
        {
            float size = Mathf.Min(520f, Screen.height - 50f, Screen.width * 0.44f);
            if (size < 180f)
                return;
            Rect mapRect = new Rect(Screen.width - size - 18f, 18f, size, size);
            SemanticHit current = semanticMap.ClassifyWorld(player.position);
            SemanticHit nearest = FindNearestOther(
                semanticMap.QueryNearbyWorld(player.position, 30f), current);
            mapRenderer.Draw(semanticMap, player, mapRect, localMapView, 40f, nearest);

            if (mapLabelStyle == null)
            {
                mapLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
            }
            GUI.Label(new Rect(mapRect.x + 8f, mapRect.y + 6f, mapRect.width - 16f, 24f),
                localMapView ? "LOCAL 40m (M: full map)" : "FULL 256m (M: local map)",
                mapLabelStyle);
            GUI.Label(new Rect(mapRect.x + 8f, mapRect.yMax - 27f, mapRect.width - 16f, 22f),
                "Road  ■ red   Sidewalk  ■ blue   Curb  ■ orange   Crosswalk  ■ yellow   Player  ▲ green",
                mapLabelStyle);
        }

        private string BuildStatusText(bool includeHeader)
        {
            Vector3 position = player.position;
            SemanticHit current = semanticMap.ClassifyWorld(position);
            IReadOnlyList<SemanticHit> nearby = semanticMap.QueryNearbyWorld(position, 20f);
            SemanticHit nearest = FindNearestOther(nearby, current);
            semanticMap.WorldToGeo(position, out double latitude, out double longitude);

            string nearestText = "None";
            if (nearest.Kind != SemanticSpaceKind.Unknown)
            {
                Vector3 toTarget = nearest.NearestPointWorld - position;
                toTarget.y = 0f;
                float worldBearing = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                if (worldBearing < 0f) worldBearing += 360f;
                float relativeBearing = Mathf.DeltaAngle(player.eulerAngles.y, worldBearing);
                nearestText = $"{nearest.Kind}  {nearest.DistanceMeters:F2}m  " +
                              $"relative={relativeBearing:+0.0;-0.0;0.0}deg ({DirectionName(relativeBearing)})";
            }

            string header = includeHeader ? "PEDESTRIAN PROBE STATUS (B/F1: blind)\n" :
                "[PedestrianProbe] ";
            return header +
                   $"Position XYZ: {position.x:F2}, {position.y:F2}, {position.z:F2}\n" +
                   $"Geo: {latitude:F8}, {longitude:F8}\n" +
                   $"Heading: {player.eulerAngles.y:F1}deg\n" +
                   $"Current: {current.Kind}  id={current.FeatureId}  " +
                   $"confidence={current.Confidence:F2}  edge={FormatDistance(current.DistanceMeters)}\n" +
                   $"Nearest other: {nearestText}\n" +
                   $"Study bounds X: {semanticMap.StudyBounds.xMin:F1}..{semanticMap.StudyBounds.xMax:F1}, " +
                   $"Z: {semanticMap.StudyBounds.yMin:F1}..{semanticMap.StudyBounds.yMax:F1}";
        }

        private static SemanticHit FindNearestOther(IReadOnlyList<SemanticHit> nearby,
            SemanticHit current)
        {
            for (int i = 0; i < nearby.Count; i++)
            {
                SemanticHit hit = nearby[i];
                if (!hit.Contains && hit.Kind != current.Kind)
                    return hit;
            }
            return SemanticHit.Unknown(Vector3.zero);
        }

        private static string FormatDistance(float distance) =>
            float.IsInfinity(distance) ? "N/A" : $"{distance:F2}m";

        private static string DirectionName(float relativeBearing)
        {
            float angle = Mathf.Abs(relativeBearing);
            if (angle <= 22.5f) return "front";
            if (angle >= 157.5f) return "back";
            if (relativeBearing > 0f)
                return angle <= 67.5f ? "front-right" : angle <= 112.5f ? "right" : "back-right";
            return angle <= 67.5f ? "front-left" : angle <= 112.5f ? "left" : "back-left";
        }

        private void OnDestroy()
        {
            if (blackTexture != null)
                Destroy(blackTexture);
            mapRenderer?.Dispose();
        }
    }
}
