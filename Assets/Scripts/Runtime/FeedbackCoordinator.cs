using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class FeedbackCoordinator : MonoBehaviour
    {
        private FeedbackModule[] modules = Array.Empty<FeedbackModule>();
        private readonly Dictionary<string, FeedbackModule> modulesById =
            new Dictionary<string, FeedbackModule>(StringComparer.Ordinal);
        private readonly FeedbackSignalBus signalBus = new FeedbackSignalBus();
        private SemanticSpaceMap semanticMap;
        private SemanticSessionLogger sessionLogger;
        private SemanticPedestrianController player;

        public int ModuleCount => modulesById.Count;
        public FeedbackSignalBus SignalBus => signalBus;

        public void Configure(SemanticSpaceMap map, Transform listener,
            SemanticSessionLogger logger, SemanticPedestrianController controller)
        {
            if (player != null)
                player.BoundaryContact -= HandleBoundaryContact;

            semanticMap = map;
            sessionLogger = logger;
            player = controller;

            if (GetComponent<SemanticAudioGuide>() == null)
                gameObject.AddComponent<SemanticAudioGuide>();

            signalBus.StopAll();
            signalBus.Configure(logger, map, listener);
            signalBus.SetRenderers(GetComponents<ISignalRenderer>());

            RegisterModules(new FeedbackContext(map, listener, logger, signalBus));

            if (player != null)
                player.BoundaryContact += HandleBoundaryContact;
        }

        public bool TryGetModuleState(string moduleId, out bool isEnabled)
        {
            if (!TryGetModule(moduleId, out FeedbackModule module))
            {
                isEnabled = false;
                return false;
            }

            isEnabled = module.IsFeedbackEnabled;
            return true;
        }

        public bool SetModuleEnabled(string moduleId, bool isEnabled,
            string reason = "runtime")
        {
            if (!TryGetModule(moduleId, out FeedbackModule module))
                return false;

            bool previous = module.IsFeedbackEnabled;
            if (!module.SetFeedbackEnabled(isEnabled))
                return true;

            string safeReason = SanitizeDetail(reason);
            LogToggle(module.ModuleId.Trim(), previous, isEnabled, safeReason);
            return true;
        }

        private void OnDestroy()
        {
            if (player != null)
                player.BoundaryContact -= HandleBoundaryContact;
            signalBus.StopAll();
            signalBus.SetRenderers(null);
        }

        private void HandleBoundaryContact(Vector3 attemptedDirection)
        {
            for (int i = 0; i < modules.Length; i++)
            {
                FeedbackModule module = modules[i];
                if (module != null && module.IsFeedbackEnabled && module.isActiveAndEnabled)
                    module.OnBoundaryContact(attemptedDirection);
            }
        }

        private void RegisterModules(in FeedbackContext context)
        {
            modulesById.Clear();
            var registered = new List<FeedbackModule>();
            FeedbackModule[] discovered = GetComponents<FeedbackModule>();
            for (int i = 0; i < discovered.Length; i++)
            {
                FeedbackModule module = discovered[i];
                string moduleId = module.ModuleId == null ? string.Empty : module.ModuleId.Trim();
                if (string.IsNullOrEmpty(moduleId))
                {
                    Debug.LogError($"Feedback module {module.GetType().Name} has no module ID.",
                        module);
                    module.enabled = false;
                    continue;
                }
                if (modulesById.ContainsKey(moduleId))
                {
                    Debug.LogError($"Duplicate feedback module ID: {moduleId}", module);
                    module.enabled = false;
                    continue;
                }

                modulesById.Add(moduleId, module);
                registered.Add(module);
                module.Configure(context);
            }
            modules = registered.ToArray();
        }

        private bool TryGetModule(string moduleId, out FeedbackModule module)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                module = null;
                return false;
            }
            return modulesById.TryGetValue(moduleId.Trim(), out module);
        }

        private void LogToggle(string moduleId, bool previous, bool current, string reason)
        {
            Vector3 position = player != null ? player.transform.position : Vector3.zero;
            SemanticHit hit = semanticMap != null
                ? semanticMap.ClassifyWorld(position)
                : SemanticHit.Unknown(position);
            sessionLogger?.LogEvent("feedback_toggle", hit,
                $"module={moduleId}|old={previous}|enabled={current}|reason={reason}");
        }

        private static string SanitizeDetail(string value) =>
            string.IsNullOrWhiteSpace(value) ? "runtime" : value.Replace('|', '/');
    }
}
