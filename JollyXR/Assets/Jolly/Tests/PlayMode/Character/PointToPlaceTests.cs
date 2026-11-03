#if UNITY_EDITOR
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using CharacterDefinition = Jolly.Character.CharacterDefinition;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Tools;
using Jolly.Testing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>The real character and its real Point clip (ADR-0051): does the arm end up on the place?</summary>
    public sealed class PointToPlaceTests
    {
        const float MaxArmError = 8f;

        CharacterTestRig _rig;
        LocomotionSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _rig = CharacterTestRig.Create();
            // No camera in a batch run: a culled animator would leave the bones in their last pose.
            _rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _settings = new LocomotionSettings();
        }

        [TearDown]
        public void TearDown() => _rig.Dispose();

        PointToPlace Pointer() => new(_rig.Nubi, new RigWorld(_rig.NavMesh), _settings, new RecordingLog());

        Transform Bone(string name)
        {
            foreach (var t in _rig.Nubi.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            Assert.Fail("no bone " + name);
            return null;
        }

        /// <summary>Largest reach of the arm during the clip: yaw off the target when the arm is most raised.</summary>
        async UniTask<float> ArmErrorWhilePointing(Vector3 target)
        {
            var upper = Bone("UpperArm_R");
            var hand = Bone("Hand_R");
            var best = float.MaxValue;
            await CharacterTestRig.Until(() =>
            {
                var arm = Vector3.ProjectOnPlane(hand.position - upper.position, Vector3.up);
                var to = Vector3.ProjectOnPlane(target - upper.position, Vector3.up);
                if (arm.magnitude > 0.05f)
                    best = Mathf.Min(best, Vector3.Angle(arm, to));
                return false;
            }, 1.5f);
            return best;
        }

        [UnityTest]
        public IEnumerator FrontTarget_TheArmAimsAtIt() => UniTask.ToCoroutine(async () =>
        {
            var target = _rig.Nubi.Position + Vector3.forward * 1.1f + Vector3.up;
            Assert.That(await Pointer().Run(target, CancellationToken.None), Is.True);
            var error = await ArmErrorWhilePointing(target);
            Debug.Log($"evt=test.point_arm_error character=nubi deg={error:F1}");
            Assert.That(error, Is.LessThan(MaxArmError));
        });

        /// <summary>The demo's character: its own proportions under the shared clips. Git-ignored (ADR-0026).</summary>
        [UnityTest]
        public IEnumerator LocalJolly_TheArmAimsAtIt([Values(1.1f, 3f)] float distance) => UniTask.ToCoroutine(async () =>
        {
            var jolly = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/Jolly/Local/Characters/Jolly/JollyCharacter.asset");
            if (jolly == null)
                Assert.Ignore("no local Jolly character on this checkout");
            _rig.Dispose();
            _rig = CharacterTestRig.Create(character: jolly);
            _rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var target = _rig.Nubi.Position + Vector3.right * distance + Vector3.up;

            Assert.That(await Pointer().Run(target, CancellationToken.None), Is.True);
            var error = await ArmErrorWhilePointing(target);
            Debug.Log($"evt=test.point_arm_error character=jolly distance={distance} deg={error:F1}");

            Assert.That(error, Is.LessThan(MaxArmError));
        });

        [UnityTest]
        public IEnumerator TargetBehind_TurnsFirst_AndStaysTurnedForTheClip() => UniTask.ToCoroutine(async () =>
        {
            var forward = _rig.Nubi.Forward;
            var target = _rig.Nubi.Position - forward * 1.1f + Vector3.up;

            Assert.That(await Pointer().Run(target, CancellationToken.None), Is.True);
            var error = await ArmErrorWhilePointing(target);

            Assert.That(error, Is.LessThan(MaxArmError));
            Assert.That(Vector3.Dot(_rig.Nubi.Forward, forward), Is.LessThan(-0.8f), "not turned back to the user during the clip");
        });

        [UnityTest]
        public IEnumerator PlaceOnAnUnreachableIsland_WalksToTheFloorsEdge_AndPoints() => UniTask.ToCoroutine(async () =>
        {
            var start = _rig.Nubi.Position;
            var target = CharacterTestRig.Island + Vector3.up;

            Assert.That(await Pointer().Run(target, CancellationToken.None), Is.True);
            Assert.That(await ArmErrorWhilePointing(target), Is.LessThan(MaxArmError));

            Assert.That(_rig.Nubi.Position.x, Is.GreaterThan(start.x + 1.5f), "it set off along the way");
            Assert.That(_rig.Nubi.Position.x, Is.LessThan(5.1f), "on the floor it stands on");
        });
    }
}
#endif
