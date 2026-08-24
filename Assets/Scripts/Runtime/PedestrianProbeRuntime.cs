using System;
using System.Collections.Generic;
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
        [SerializeField] private SignalCatalog[] comparisonCatalogs = Array.Empty<SignalCatalog>();

        public void Configure(SemanticSpaceMap map, SemanticPedestrianController controller,
            Terrain ground, SignalCatalog catalog = null,
            SignalCatalog[] comparisons = null)
        {
            semanticMap = map;
            player = controller;
            terrain = ground;
            if (catalog != null)
                signalCatalog = catalog;
            if (comparisons != null)
                comparisonCatalogs = comparisons;
        }

        private List<SignalCatalog> BuildCatalogCycle()
        {
            var cycle = new List<SignalCatalog>();
            if (signalCatalog != null)
                cycle.Add(signalCatalog);
            for (int i = 0; i < comparisonCatalogs.Length; i++)
            {
                SignalCatalog comparison = comparisonCatalogs[i];
                if (comparison != null && !cycle.Contains(comparison))
                    cycle.Add(comparison);
            }
            return cycle;
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
            overlay.BlindModeChanged += blind => player.SetPitchEnabled(!blind);
            player.SetPitchEnabled(!overlay.IsBlind);

            EgocentricSweepProbe sweep = GetComponent<EgocentricSweepProbe>();
            if (sweep == null)
                sweep = gameObject.AddComponent<EgocentricSweepProbe>();
            overlay.SetSweepProbe(sweep);

            AudioSignalRenderer signalRenderer = GetComponent<AudioSignalRenderer>();
            if (signalRenderer == null)
                signalRenderer = gameObject.AddComponent<AudioSignalRenderer>();
            signalRenderer.Configure(signalCatalog, camera.transform);

            // Named in every row, so a finished session says which of the rival
            // catalogs it was heard through rather than only where it went.
            logger.SetSignalRenderer(signalRenderer);
            overlay.SetSignalRenderer(signalRenderer);
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
            controls.SetCatalogs(signalRenderer, BuildCatalogCycle());

            WarmUpSpatialIndex();
        }

        /// <summary>
        /// Pays for the map's spatial index here, while the scene is still
        /// loading, rather than on whichever query happens to ask for it first.
        /// </summary>
        /// <remarks>
        /// The index is built lazily on first use. On the NYC map, with two
        /// thousand features in it, that cost measured 0.72 s and landed three
        /// seconds into a session — a freeze while someone was already walking.
        /// A single throwaway query moves it into the loading screen, where a
        /// pause is what everyone already expects.
        /// </remarks>
        private void WarmUpSpatialIndex()
        {
            if (semanticMap == null)
                return;
            semanticMap.ClassifyWorld(player != null
                ? player.transform.position
                : Vector3.zero);
        }
    }
}
