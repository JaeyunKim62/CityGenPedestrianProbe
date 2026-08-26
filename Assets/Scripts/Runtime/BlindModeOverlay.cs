using System;
using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] private EgocentricSweepProbe sweepProbe;
        [SerializeField] private AudioSignalRenderer signalRenderer;
        [SerializeField] private bool logStateToConsole = true;
        [SerializeField] private bool showMap = true;
        [SerializeField] private bool sourceClassView = true;
        private ProbeHeading heading;
        private Texture2D blackTexture;
        private GUIStyle debugStyle;
        private GUIStyle mapLabelStyle;
        private SemanticDebugMapRenderer mapRenderer;
        private bool localMapView;
        private float nextConsoleLog;

        public event Action<bool> BlindModeChanged;
        public bool IsBlind => blind;

        // The debug map's own view state. Exposed because the settings menu
        // offers the same three toggles the keyboard shortcuts below do, and
        // the map belongs to this component rather than to the menu.
        public bool ShowMap
        {
            get => showMap;
            set => showMap = value;
        }

        public bool LocalMapView
        {
            get => localMapView;
            set => localMapView = value;
        }

        public bool SourceClassView
        {
            get => sourceClassView;
            set => sourceClassView = value;
        }

        public void Configure(SemanticSpaceMap map, Transform playerTransform)
        {
            semanticMap = map;
            player = playerTransform;

            // A map with no source classes, which is every synthetic testbed
            // layout, would draw an empty minimap in the source-class view. Ask
            // the map which view it can actually fill rather than making the
            // person press C to find out.
            sourceClassView = map != null && map.SourceClasses.Count > 0;
        }

        /// <summary>
        /// Sets the blind condition from outside, so a trial script or an input
        /// router can drive it. Entering blind is always allowed; whether a
        /// person may leave it is a policy their caller has to decide.
        /// </summary>
        public void SetBlind(bool value)
        {
            if (blind == value)
                return;
            blind = value;
            BlindModeChanged?.Invoke(blind);
        }

        public void SetSweepProbe(EgocentricSweepProbe probe)
        {
            sweepProbe = probe;
        }

        /// <summary>
        /// Gives the status text the heading model, so it can show where the
        /// body points and where attention points as two separate readings.
        /// </summary>
        public void SetHeading(ProbeHeading probeHeading)
        {
            heading = probeHeading;
        }

        public void SetSignalRenderer(AudioSignalRenderer renderer)
        {
            signalRenderer = renderer;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PROBE_EXPERIMENTER
            bool pressed;
#if ENABLE_INPUT_SYSTEM
            pressed = Keyboard.current != null &&
                      (Keyboard.current.f1Key.wasPressedThisFrame ||
                       Keyboard.current.bKey.wasPressedThisFrame);
#else
            pressed = Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.B);
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD || PROBE_EXPERIMENTER
            bool mapPressed;
#if ENABLE_INPUT_SYSTEM
            mapPressed = Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
            mapPressed = Input.GetKeyDown(KeyCode.M);
#endif
            if (mapPressed)
                localMapView = !localMapView;

            bool mapVisibilityPressed;
#if ENABLE_INPUT_SYSTEM
            mapVisibilityPressed = Keyboard.current != null &&
                                   Keyboard.current.nKey.wasPressedThisFrame;
#else
            mapVisibilityPressed = Input.GetKeyDown(KeyCode.N);
#endif
            if (mapVisibilityPressed)
                showMap = !showMap;

            bool classViewPressed;
#if ENABLE_INPUT_SYSTEM
            classViewPressed = Keyboard.current != null &&
                               Keyboard.current.cKey.wasPressedThisFrame;
#else
            classViewPressed = Input.GetKeyDown(KeyCode.C);
#endif
            if (classViewPressed)
                sourceClassView = !sourceClassView;
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
            }

            // Diagnostics stay behind the guard: the status text and the map
            // say where the participant is, which is the one thing a blind
            // session must not show them. PROBE_EXPERIMENTER opens them to a
            // signed build as well, because the person running a session over
            // internal testing needs to see the map on the same handset the
            // trial will run on, and cannot install a development APK there.
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PROBE_EXPERIMENTER
            if (!blind && semanticMap != null && player != null)
            {
                if (debugStyle == null)
                {
                    debugStyle = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.UpperLeft,
                        fontSize = 16,
                        normal = { textColor = Color.white }
                    };
                }
                GUI.Box(new Rect(12f, 12f, 760f, 300f), BuildStatusText(true), debugStyle);
                if (showMap)
                    DrawSemanticMap();
            }
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
            mapRenderer.Draw(semanticMap, player, mapRect, localMapView, 40f, nearest,
                sourceClassView);

            if (mapLabelStyle == null)
            {
                mapLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
            }
            GUI.Label(new Rect(mapRect.x + 8f, mapRect.y + 6f, mapRect.width - 16f, 24f),
                (sourceClassView ? "SOURCE CLASSES  |  " : "PEDESTRIAN SEMANTICS  |  ") +
                (localMapView ? "LOCAL 40m (M: full map)" : "FULL 256m (M: local map)"),
                mapLabelStyle);
            string legend = sourceClassView
                ? "Surface green | Plot purple | Building gray | MapRoad red\n" +
                  "Highway blue | Landscape brown | OSM_Node yellow | C: semantics"
                : "Road red | Sidewalk blue | Curb orange | Crosswalk yellow | C: sources";
            GUI.Label(new Rect(mapRect.x + 8f, mapRect.yMax - 48f,
                mapRect.width - 16f, 44f), legend, mapLabelStyle);
        }

        private string BuildStatusText(bool includeHeader)
        {
            Vector3 position = player.position;
            SemanticHit current = semanticMap.ClassifyWorld(position);
            IReadOnlyList<SemanticHit> nearby = semanticMap.QueryNearbyWorld(position, 20f);
            SemanticHit nearest = FindNearestOther(nearby, current);
            string currentLayers = string.Join(", ", nearby
                .Where(hit => hit.Contains && hit.HasClassification)
                .Select(hit => hit.DisplayClass)
                .Distinct()
                .OrderBy(className => className));
            if (string.IsNullOrEmpty(currentLayers))
                currentLayers = "None";
            semanticMap.WorldToGeo(position, out double latitude, out double longitude);

            string nearestText = "None";
            if (nearest.HasClassification)
            {
                Vector3 toTarget = nearest.NearestPointWorld - position;
                toTarget.y = 0f;
                float worldBearing = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                if (worldBearing < 0f) worldBearing += 360f;
                float relativeBearing = Mathf.DeltaAngle(player.eulerAngles.y, worldBearing);
                nearestText = $"{nearest.DisplayClass}  {nearest.DistanceMeters:F2}m  " +
                              $"relative={relativeBearing:+0.0;-0.0;0.0}deg ({DirectionName(relativeBearing)})";
            }

            string header = includeHeader
                ? "PEDESTRIAN PROBE STATUS (B/F1: blind, N: map, M: scale, C: classes, " +
                  "Q/E: sweep, R: centre, [/]: range, G: semantic audio)\n"
                :
                "[PedestrianProbe] ";
            string semanticText = current.Kind == SemanticSpaceKind.Unknown
                ? string.Empty
                : $"  semantic={current.Kind}";
            return header +
                   $"Position XYZ: {position.x:F2}, {position.y:F2}, {position.z:F2}\n" +
                   $"Geo: {latitude:F8}, {longitude:F8}\n" +
                   $"Heading: {BuildHeadingText()}\n" +
                   $"Current: {current.DisplayClass}{semanticText}  " +
                   $"id={current.FeatureId}  " +
                   $"confidence={current.Confidence:F2}  edge={FormatDistance(current.DistanceMeters)}\n" +
                   $"Current layers: {currentLayers}\n" +
                   $"Nearest other: {nearestText}\n" +
                   $"Sweep: {BuildSweepText()}\n" +
                   $"Signal: {BuildSignalText()}\n" +
                   $"Study bounds X: {semanticMap.StudyBounds.xMin:F1}..{semanticMap.StudyBounds.xMax:F1}, " +
                   $"Z: {semanticMap.StudyBounds.yMin:F1}..{semanticMap.StudyBounds.yMax:F1}";
        }

        /// <summary>
        /// Body and attention on one line, each named by compass point.
        /// </summary>
        /// <remarks>
        /// The compass points are what make a device that claims to map its own
        /// angle onto a world bearing checkable without instruments: hold the
        /// thing in landscape and the body has to read N, turn it a quarter
        /// turn anticlockwise and it has to read W. A number alone leaves the
        /// sign of that mapping to be discovered by a confused participant.
        /// </remarks>
        private string BuildHeadingText()
        {
            if (heading == null)
                return $"{player.eulerAngles.y:F1}deg";

            float bearing = heading.BearingDegrees;
            float gaze = heading.GazeBearingDegrees;
            return $"body {bearing:F1}deg ({ProbeHeading.CompassName(bearing)})  " +
                   $"gaze {gaze:F1}deg ({ProbeHeading.CompassName(gaze)})  " +
                   $"offset {heading.GazeOffsetDegrees:+0.0;-0.0;0.0}deg";
        }

        private string BuildSignalText()
        {
            if (signalRenderer == null)
                return "no renderer in scene";
            if (signalRenderer.Catalog == null)
                return "NO CATALOG - run Tools > Pedestrian Probe > Setup Scene";
            return $"catalog={signalRenderer.Catalog.CatalogId} " +
                   $"r{signalRenderer.Catalog.Revision}  " +
                   $"sounding={signalRenderer.ActiveSustainedCount}";
        }

        private string BuildSweepText()
        {
            if (sweepProbe == null)
                return "no probe";
            if (!sweepProbe.IsFeedbackEnabled)
                return "module off";

            string aim = $"angle={sweepProbe.RelativeAngleDegrees:+0.0;-0.0;0.0}deg " +
                         $"({DirectionName(sweepProbe.RelativeAngleDegrees)})  " +
                         $"range={sweepProbe.RangeMeters:F1}m";
            if (!sweepProbe.HasCurrentResult)
                return $"{aim}  not queried";

            SpatialProbeHit hit = sweepProbe.CurrentHit;
            return hit.HasHit
                ? $"{aim}  hit {hit.To.DisplayLabel} at {hit.DistanceMeters:F2}m"
                : $"{aim}  no boundary within range";
        }

        private static SemanticHit FindNearestOther(IReadOnlyList<SemanticHit> nearby,
            SemanticHit current)
        {
            for (int i = 0; i < nearby.Count; i++)
            {
                SemanticHit hit = nearby[i];
                if (!hit.Contains &&
                    (hit.Kind != current.Kind || !string.Equals(hit.SourceClass,
                        current.SourceClass, StringComparison.Ordinal)))
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
