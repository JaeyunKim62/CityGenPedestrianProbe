using UnityEngine;

namespace CityGenPedestrianProbe
{
    public abstract class FeedbackModule : MonoBehaviour
    {
        public abstract void Configure(SemanticSpaceMap map, Transform listener,
            SemanticSessionLogger logger);

        public virtual void OnBoundaryContact(Vector3 attemptedDirection)
        {
        }
    }
}
