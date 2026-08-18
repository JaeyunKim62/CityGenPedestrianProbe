using UnityEngine;

namespace CityGenPedestrianProbe
{
    public abstract class FeedbackModule : MonoBehaviour
    {
        public abstract string ModuleId { get; }
        public bool IsFeedbackEnabled => enabled;

        public abstract void Configure(SemanticSpaceMap map, Transform listener,
            SemanticSessionLogger logger);

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
