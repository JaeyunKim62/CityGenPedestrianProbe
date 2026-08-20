using UnityEngine;

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class PedestrianProbeRuntime : MonoBehaviour
    {
        [SerializeField] private SemanticSpaceMap semanticMap;
        [SerializeField] private SemanticPedestrianController player;
        [SerializeField] private Terrain terrain;
        [SerializeField] private SignalCatalog signalCatalog;

        public void Configure(SemanticSpaceMap map, SemanticPedestrianController controller,
            Terrain ground, SignalCatalog catalog = null)
        {
            semanticMap = map;
            player = controller;
            terrain = ground;
            if (catalog != null)
                signalCatalog = catalog;
        }

        private void Awake()
        {
            if (semanticMap == null || player == null)
            {
                Debug.LogError("Pedestrian Probe is not configured. Run Tools > Pedestrian Probe > Setup Scene.", this);
                enabled = false;
                return;
            }

            player.Configure(semanticMap, terrain);
            Camera camera = player.GetComponentInChildren<Camera>(true);
            if (camera == null)
            {
                Debug.LogError("Pedestrian Probe player needs a Camera.", player);
                enabled = false;
                return;
            }
            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            SemanticSessionLogger logger = GetComponent<SemanticSessionLogger>();
            if (logger == null)
                logger = gameObject.AddComponent<SemanticSessionLogger>();
            logger.Configure(semanticMap, player.transform);

            BlindModeOverlay overlay = GetComponent<BlindModeOverlay>();
            if (overlay == null)
                overlay = gameObject.AddComponent<BlindModeOverlay>();
            overlay.Configure(semanticMap, player.transform);
            overlay.BlindModeChanged += blind => logger.LogEvent("blind_mode",
                semanticMap.ClassifyWorld(player.transform.position), blind ? "on" : "off");

            EgocentricSweepProbe sweep = GetComponent<EgocentricSweepProbe>();
            if (sweep == null)
                sweep = gameObject.AddComponent<EgocentricSweepProbe>();
            overlay.SetSweepProbe(sweep);

            AudioSignalRenderer signalRenderer = GetComponent<AudioSignalRenderer>();
            if (signalRenderer == null)
                signalRenderer = gameObject.AddComponent<AudioSignalRenderer>();
            signalRenderer.Configure(signalCatalog, camera.transform);
            if (signalCatalog == null)
                Debug.LogWarning("Pedestrian Probe has no signal catalog, so feedback " +
                                 "modules on the signal bus stay silent.", this);

            FeedbackCoordinator feedback = GetComponent<FeedbackCoordinator>();
            if (feedback == null)
                feedback = gameObject.AddComponent<FeedbackCoordinator>();
            feedback.Configure(semanticMap, camera.transform, logger, player);

            SweepKeyboardControls controls = GetComponent<SweepKeyboardControls>();
            if (controls == null)
                controls = gameObject.AddComponent<SweepKeyboardControls>();
            controls.Configure(sweep, feedback);
        }
    }
}
