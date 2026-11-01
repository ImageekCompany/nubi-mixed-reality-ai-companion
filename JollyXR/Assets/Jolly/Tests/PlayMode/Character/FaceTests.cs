#if UNITY_EDITOR
using System.Collections;
using Cysharp.Threading.Tasks;
using Jolly.Character;
using Jolly.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.Character
{
    public sealed class FaceTests
    {
        CharacterTestRig _rig;
        ToneLipSync _tone;
        SkinnedMeshRenderer _mesh;

        [SetUp]
        public void SetUp()
        {
            _tone = new ToneLipSync(frequency: 220f, amplitude: 0.8f);
            _rig = CharacterTestRig.Create(_tone);
            _mesh = _rig.Nubi.FaceMesh;
        }

        [TearDown]
        public void TearDown() => _rig.Dispose();

        float Weight(string shape) => _mesh.GetBlendShapeWeight(_mesh.sharedMesh.GetBlendShapeIndex(shape));

        [UnityTest]
        public IEnumerator MouthWeights_FollowATestTone() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            await UniTask.DelayFrame(3);
            Assert.That(Weight("A"), Is.LessThan(1f), "closed before the tone");

            // Act + Assert: opens within the attack time
            _tone.ToneOn = true;
            Assert.That(await CharacterTestRig.Until(() => Weight("A") > 60f, 0.4f), Is.True, "opens on the tone");

            // Act + Assert: closes within ~100 ms of silence (90 ms release)
            _tone.ToneOn = false;
            Assert.That(await CharacterTestRig.Until(() => Weight("A") < 5f, 0.4f), Is.True, "closes in silence");
        });

        [UnityTest]
        public IEnumerator SetMood_Happy_SmilesAfterTheFade() => UniTask.ToCoroutine(async () =>
        {
            // Act
            _rig.Nubi.SetMood(Mood.Happy);

            // Assert
            Assert.That(await CharacterTestRig.Until(() => Weight("Smile") > 45f, 0.5f), Is.True);
        });

        [UnityTest]
        public IEnumerator Blink_HappensWithinTheMaximumInterval() => UniTask.ToCoroutine(async () =>
        {
            var blinked = await CharacterTestRig.Until(() => Weight("Blink_L") > 50f && Weight("Blink_R") > 50f,
                                                       _rig.Nubi.Settings.face.blinkIntervalMax + 0.5f);

            Assert.That(blinked, Is.True);
        });

        [UnityTest]
        public IEnumerator LookAt_TargetFarToTheSide_TurnsTheBody() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var side = new Vector3(3f, 0.6f, 0f);

            // Act
            _rig.Nubi.LookAt(LookTarget.At(side));
            var turned = await CharacterTestRig.Until(() => Vector3.Angle(_rig.Nubi.Forward, Vector3.right) < 10f, 2f);

            // Assert
            Assert.That(turned, Is.True, "a 90° target is beyond the head and eye range");
        });
    }
}
#endif
