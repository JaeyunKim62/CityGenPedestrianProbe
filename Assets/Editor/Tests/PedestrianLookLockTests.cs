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
        public void Pitch_StartsEnabledSoTheEditorCanBeDriven()
        {
            Assert.That(player.PitchEnabled, Is.True);
        }

        [Test]
        public void LockingPitch_LevelsAnAlreadyTiltedHead()
        {
            playerObject.transform.localEulerAngles = new Vector3(40f, 120f, 0f);

            player.SetPitchEnabled(false);

            Assert.That(player.PitchEnabled, Is.False);
            Assert.That(Mathf.DeltaAngle(0f, playerObject.transform.localEulerAngles.x),
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void LockingPitch_LeavesTheHeadingAlone()
        {
            playerObject.transform.localEulerAngles = new Vector3(40f, 120f, 0f);

            player.SetPitchEnabled(false);

            Assert.That(Mathf.DeltaAngle(120f, playerObject.transform.localEulerAngles.y),
                Is.EqualTo(0f).Within(0.001f),
                "the body still has to be able to face down a street while blind");
        }

        [Test]
        public void LockingPitch_LevelsHeadsTiltedDownwardToo()
        {
            playerObject.transform.localEulerAngles = new Vector3(-55f, 0f, 0f);

            player.SetPitchEnabled(false);

            Assert.That(Mathf.DeltaAngle(0f, playerObject.transform.localEulerAngles.x),
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void UnlockingPitch_DoesNotDisturbTheTransform()
        {
            player.SetPitchEnabled(false);
            playerObject.transform.localEulerAngles = new Vector3(0f, 75f, 0f);

            player.SetPitchEnabled(true);

            Assert.That(player.PitchEnabled, Is.True);
            Assert.That(Mathf.DeltaAngle(75f, playerObject.transform.localEulerAngles.y),
                Is.EqualTo(0f).Within(0.001f));
        }
    }
}
