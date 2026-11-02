#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Character;
using Jolly.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>
    /// The character faces the user through a conversation turn (ADR-0041). It stands at the origin facing +z; the
    /// user's head is placed around it at standing height. Limits are the device self-test's: 25° body, 15° head.
    /// </summary>
    public sealed class AttentionTests
    {
        const float BodyLimit = 25f;
        const float HeadLimit = 15f;
        const float SettleSeconds = 3f;
        /// <summary>Git-ignored (ADR-0026): the Jolly case is skipped on a checkout without it.</summary>
        const string JollyDefinition = "Assets/Jolly/Local/Characters/Jolly/JollyCharacter.asset";

        CharacterTestRig _rig;

        [TearDown]
        public void TearDown() => _rig?.Dispose();

        void Create(CharacterDefinition character = null) => _rig = CharacterTestRig.Create(character: character);

        void PlaceUser(float yawDegrees, float distance = 1.5f)
        {
            var flat = Quaternion.AngleAxis(yawDegrees, Vector3.up) * Vector3.forward * distance;
            _rig.User.Head = new Pose(_rig.Nubi.Position + flat + Vector3.up * 1.6f, Quaternion.LookRotation(-flat));
        }

        (float Body, float Head) Offsets()
        {
            Assert.That(_rig.Nubi.TryMeasureUser(out var body, out var head), Is.True);
            return (body, head);
        }

        bool Facing() => Offsets() is var o && o.Body < 5f && o.Head < 10f;

        /// <summary>
        /// Unsigned yaw from where the rendered face points to the user's head: the skinned eye vertices relative to the
        /// head bone. Unlike <see cref="NubiCharacterController.TryMeasureUser"/> it assumes nothing about which way the
        /// model faces, so a model turned away from the root's +z fails here (ADR-0046).
        /// </summary>
        float FaceYawOff()
        {
            var bones = _rig.Nubi.Definition.bones;
            var head = CharacterRig.Find(_rig.Nubi.transform, bones.head);
            var eyes = new[] { CharacterRig.Find(_rig.Nubi.transform, bones.eyeLeft), CharacterRig.Find(_rig.Nubi.transform, bones.eyeRight) };
            var face = _rig.Nubi.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.bones.Contains(eyes[0]));
            var baked = new Mesh();
            try
            {
                face.BakeMesh(baked, true);
                var vertices = baked.vertices;
                var weights = face.sharedMesh.boneWeights;
                var sum = Vector3.zero;
                var count = 0;
                for (var i = 0; i < weights.Length; i++)
                {
                    if (weights[i].weight0 < 0.5f || !eyes.Contains(face.bones[weights[i].boneIndex0]))
                        continue;
                    sum += face.transform.TransformPoint(vertices[i]);
                    count++;
                }
                Assert.That(count, Is.GreaterThan(0), "no vertex is skinned to the eyes");
                return Mathf.Abs(LookAtSolver.Yaw(sum / count - head.position, _rig.User.Head.position - head.position, Vector3.up));
            }
            finally
            {
                Object.Destroy(baked);
            }
        }

        async UniTask AssertFacesTheUser()
        {
            await CharacterTestRig.Until(Facing, SettleSeconds);
            var (body, head) = Offsets();
            Assert.That(body, Is.LessThan(BodyLimit), "body yaw off the user");
            Assert.That(head, Is.LessThan(HeadLimit), "head yaw off the user");
            Assert.That(FaceYawOff(), Is.LessThan(BodyLimit), "the rendered face points away from the user");
            Assert.That(_rig.Nubi.Attending, Is.EqualTo(AttentionTarget.User));
        }

        [UnityTest]
        public IEnumerator Speaking_AfterAnIdleGlanceLeftNoTarget_FacesTheUser() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: IdleScheduler.Glance ends with LookAt(None).
            Create();
            _rig.Nubi.LookAt(LookTarget.None);
            PlaceUser(70f);

            // Act
            _rig.Nubi.SetConversationState(ConversationState.Speaking);

            // Assert
            await AssertFacesTheUser();
        });

        [UnityTest]
        public IEnumerator Speaking_UserInsideTheIdleTurnThreshold_StillTurnsTheBody() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: 35° is inside bodyTurnBeyond (45°), where only the head used to turn.
            Create();
            _rig.Nubi.LookAt(LookTarget.User);
            PlaceUser(-35f);

            // Act
            _rig.Nubi.SetConversationState(ConversationState.Speaking);
            await CharacterTestRig.Until(Facing, SettleSeconds);

            // Assert
            Assert.That(Offsets().Body, Is.LessThan(new LookSettings().faceUserBeyond));
        });

        [UnityTest]
        public IEnumerator Thinking_TurnsTheBodyWhileTheHeadPlaysThePose() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            Create();
            PlaceUser(90f);

            // Act
            _rig.Nubi.SetConversationState(ConversationState.Thinking);
            await CharacterTestRig.Until(() => Offsets().Body < 5f, SettleSeconds);

            // Assert
            Assert.That(Offsets().Body, Is.LessThan(BodyLimit));
        });

        [UnityTest]
        public IEnumerator LookAtPoint_HoldExpires_ReturnsToTheUser() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: the user ahead, the tool's point to the left.
            Create();
            PlaceUser(0f);
            _rig.Nubi.SetConversationState(ConversationState.Speaking);
            _rig.Nubi.LookAt(LookTarget.At(new Vector3(-1.5f, 0.8f, 0.5f)));
            await CharacterTestRig.Until(() => Offsets().Head > HeadLimit, SettleSeconds);
            Assert.That(_rig.Nubi.Attending, Is.EqualTo(AttentionTarget.Point));
            Assert.That(Offsets().Head, Is.GreaterThan(HeadLimit), "the head turns to the point first");

            // Act
            _rig.Clock.Advance(TimeSpan.FromSeconds(new LookSettings().pointHoldSeconds + 0.1));

            // Assert
            await AssertFacesTheUser();
        });

        [UnityTest]
        public IEnumerator Listening_DropsAHeldPoint() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            Create();
            PlaceUser(30f);
            _rig.Nubi.LookAt(LookTarget.At(new Vector3(-1.5f, 0.8f, 0.5f)));
            await UniTask.DelayFrame(3);

            // Act
            _rig.Nubi.SetConversationState(ConversationState.Listening);

            // Assert
            await AssertFacesTheUser();
        });

        [UnityTest]
        public IEnumerator LookAround_Finishes_ReturnsToTheUser() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            Create();
            PlaceUser(20f);
            var sweep = _rig.Nubi.PlayGesture(GestureId.LookAround, CancellationToken.None);

            // Act
            for (var step = 0; step < 3; step++)
            {
                await UniTask.DelayFrame(3);
                _rig.Clock.Advance(TimeSpan.FromSeconds(0.8));
            }
            await sweep;

            // Assert
            await AssertFacesTheUser();
        });

        [UnityTest]
        public IEnumerator WalkToTheUser_Arrives_EndsFacingThem() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: come_here walks to a point beside the user's line, so the walk ends facing past them.
            Create();
            PlaceUser(0f, 3f);
            _rig.Nubi.LookAt(LookTarget.User);

            // Act
            _rig.Nubi.TryStartMoveTo(new Vector3(1.2f, 0f, 1.5f), 0.15f, out _);
            await _rig.Nubi.WhenArrived(CancellationToken.None);

            // Assert
            await AssertFacesTheUser();
        });

        [UnityTest]
        public IEnumerator IdleAndFar_NoTarget_LeavesTheBodyAlone() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            Create();
            _rig.Nubi.LookAt(LookTarget.None);
            PlaceUser(90f, new LookSettings().attendWithin + 1f);

            // Act
            await UniTask.DelayFrame(30);

            // Assert
            Assert.That(_rig.Nubi.Attending, Is.EqualTo(AttentionTarget.None));
            Assert.That(Offsets().Body, Is.EqualTo(90f).Within(1f));
        });

        [UnityTest]
        public IEnumerator Speaking_LocalJollyCharacter_FacesTheUser() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var jolly = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(JollyDefinition);
            if (jolly == null)
                Assert.Ignore("no local Jolly character on this checkout");
            Create(jolly);
            PlaceUser(-70f);

            // Act
            _rig.Nubi.SetConversationState(ConversationState.Speaking);

            // Assert
            await AssertFacesTheUser();
        });
    }
}
#endif
