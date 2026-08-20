using NUnit.Framework;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class PedestrianLookLockTests
    {
        private GameObject playerObject;
        private SemanticPedestrianController player;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("Look Lock Test Player");
            player = playerObject.AddComponent<SemanticPedestrianController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(playerObject);
        }

        [Test]
        public void Look_StartsEnabledSoTheEditorCanBeDriven()
        {
            Assert.That(player.LookEnabled, Is.True);
        }

        [Test]
        public void LockingLook_LevelsAnAlreadyTiltedHead()
        {
            playerObject.transform.localEulerAngles = new Vector3(40f, 120f, 0f);

            player.SetLookEnabled(false);

            Assert.That(player.LookEnabled, Is.False);
            Assert.That(Mathf.DeltaAngle(0f, playerObject.transform.localEulerAngles.x),
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void LockingLook_KeepsTheHeadingItWasFacing()
        {
            playerObject.transform.localEulerAngles = new Vector3(40f, 120f, 0f);

            player.SetLookEnabled(false);

            Assert.That(Mathf.DeltaAngle(120f, playerObject.transform.localEulerAngles.y),
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void LockingLook_LevelsHeadsTiltedDownwardToo()
        {
            playerObject.transform.localEulerAngles = new Vector3(-55f, 0f, 0f);

            player.SetLookEnabled(false);

            Assert.That(Mathf.DeltaAngle(0f, playerObject.transform.localEulerAngles.x),
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void UnlockingLook_DoesNotDisturbTheTransform()
        {
            player.SetLookEnabled(false);
            playerObject.transform.localEulerAngles = new Vector3(0f, 75f, 0f);

            player.SetLookEnabled(true);

            Assert.That(player.LookEnabled, Is.True);
            Assert.That(Mathf.DeltaAngle(75f, playerObject.transform.localEulerAngles.y),
                Is.EqualTo(0f).Within(0.001f));
        }
    }
}
