using UnityEngine;

namespace CityGenPedestrianProbe
{
    public readonly struct FeedbackContext
    {
        public readonly SemanticSpaceMap Map;
        public readonly Transform Listener;
        public readonly SemanticSessionLogger Logger;
        public readonly FeedbackSignalBus SignalBus;

        public FeedbackContext(SemanticSpaceMap map, Transform listener,
            SemanticSessionLogger logger, FeedbackSignalBus signalBus)
        {
            Map = map;
            Listener = listener;
            Logger = logger;
            SignalBus = signalBus;
        }
    }

    public abstract class FeedbackModule : MonoBehaviour
    {
        public abstract string ModuleId { get; }
        public bool IsFeedbackEnabled => enabled;

        public abstract void Configure(in FeedbackContext context);

        internal bool SetFeedbackEnabled(bool value)
        {
            if (enabled == value)
                return false;

            enabled = value;
            OnFeedbackEnabledChanged(value);
            return true;
        }

        protected virtual void OnFeedbackEnabledChanged(bool value)
        {
        }

        public virtual void OnBoundaryContact(Vector3 attemptedDirection)
        {
        }
    }
}
