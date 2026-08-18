using UnityEngine;

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class PedestrianProbeRuntime : MonoBehaviour
    {
        [SerializeField] private SemanticSpaceMap semanticMap;
        [SerializeField] private SemanticPedestrianController player;
        [SerializeField] private Terrain terrain;

        public void Configure(SemanticSpaceMap map, SemanticPedestrianController controller,
            Terrain ground)
        {
            semanticMap = map;
            player = controller;
            terrain = ground;
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

            if (GetComponent<EgocentricSweepProbe>() == null)
                gameObject.AddComponent<EgocentricSweepProbe>();

            FeedbackCoordinator feedback = GetComponent<FeedbackCoordinator>();
            if (feedback == null)
                feedback = gameObject.AddComponent<FeedbackCoordinator>();
            feedback.Configure(semanticMap, camera.transform, logger, player);
        }
    }
}
