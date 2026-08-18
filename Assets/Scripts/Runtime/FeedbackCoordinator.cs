using UnityEngine;

namespace CityGenPedestrianProbe
{
    [DisallowMultipleComponent]
    public sealed class FeedbackCoordinator : MonoBehaviour
    {
        private FeedbackModule[] modules;
        private SemanticPedestrianController player;

        public void Configure(SemanticSpaceMap map, Transform listener,
            SemanticSessionLogger logger, SemanticPedestrianController controller)
        {
            player = controller;

            if (GetComponent<SemanticAudioGuide>() == null)
                gameObject.AddComponent<SemanticAudioGuide>();

            modules = GetComponents<FeedbackModule>();
            for (int i = 0; i < modules.Length; i++)
                modules[i].Configure(map, listener, logger);

            player.BoundaryContact += HandleBoundaryContact;
        }

        private void OnDestroy()
        {
            if (player != null)
                player.BoundaryContact -= HandleBoundaryContact;
        }

        private void HandleBoundaryContact(Vector3 attemptedDirection)
        {
            for (int i = 0; i < modules.Length; i++)
                modules[i].OnBoundaryContact(attemptedDirection);
        }
    }
}
