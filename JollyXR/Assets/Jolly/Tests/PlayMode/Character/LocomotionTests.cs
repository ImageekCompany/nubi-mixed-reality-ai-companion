#if UNITY_EDITOR
using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.Character
{
    public sealed class LocomotionTests
    {
        static readonly Vector3 Target = new(2f, 0f, 0f);

        CharacterTestRig _rig;

        [SetUp]
        public void SetUp() => _rig = CharacterTestRig.Create();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        [UnityTest]
        public IEnumerator TryStartMoveTo_ReachableTarget_ArrivesNearTarget() => UniTask.ToCoroutine(async () =>
        {
            // Act
            var started = _rig.Nubi.TryStartMoveTo(Target, 0.15f, out var eta);
            var outcome = await _rig.Nubi.WhenArrived(CancellationToken.None);

            // Assert
            Assert.That(started, Is.True);
            Assert.That(eta, Is.EqualTo(2f / 0.6f).Within(0.5f));
            Assert.That(outcome, Is.EqualTo(MoveOutcome.Arrived));
            Assert.That(Vector3.Distance(_rig.Nubi.Position, Target), Is.LessThan(0.3f));
            Assert.That(_rig.Nubi.Mode, Is.EqualTo(LocomotionMode.None));
            Assert.That(_rig.Nubi.IsMoving, Is.False);
        });

        [UnityTest]
        public IEnumerator TryStartMoveTo_DisconnectedIsland_ReturnsFalseAndUnreachable() => UniTask.ToCoroutine(async () =>
        {
            // Act
            var started = _rig.Nubi.TryStartMoveTo(CharacterTestRig.Island, 0.15f, out var eta);
            var outcome = await _rig.Nubi.WhenArrived(CancellationToken.None);

            // Assert
            Assert.That(started, Is.False);
            Assert.That(eta, Is.Zero);
            Assert.That(outcome, Is.EqualTo(MoveOutcome.Unreachable));
            Assert.That(_rig.Nubi.IsMoving, Is.False);
        });

        [UnityTest]
        public IEnumerator TryStartMoveTo_WhileMoving_SupersedesFirstWithCancelled() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            _rig.Nubi.TryStartMoveTo(Target, 0.15f, out _);
            var first = _rig.Nubi.WhenArrived(CancellationToken.None);
            await UniTask.DelayFrame(5);

            // Act
            var second = new Vector3(-1.5f, 0f, 1f);
            _rig.Nubi.TryStartMoveTo(second, 0.15f, out _);

            // Assert
            Assert.That(await first, Is.EqualTo(MoveOutcome.Cancelled));
            Assert.That(await _rig.Nubi.WhenArrived(CancellationToken.None), Is.EqualTo(MoveOutcome.Arrived));
            Assert.That(Vector3.Distance(_rig.Nubi.Position, second), Is.LessThan(0.3f));
        });

        [UnityTest]
        public IEnumerator TryStartMoveTo_AwaiterStopsOnCancelled_StopWins() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            _rig.Nubi.TryStartMoveTo(Target, 0.15f, out _);
            var reaction = StopWhenCancelled(_rig.Nubi.WhenArrived(CancellationToken.None));
            await UniTask.DelayFrame(5);

            // Act
            _rig.Nubi.TryStartMoveTo(new Vector3(-1.5f, 0f, 1f), 0.15f, out _);
            var second = _rig.Nubi.WhenArrived(CancellationToken.None);

            // Assert
            await reaction;
            Assert.That(_rig.Nubi.Mode, Is.EqualTo(LocomotionMode.None));
            Assert.That(_rig.Nubi.IsMoving, Is.False);
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(await second, Is.EqualTo(MoveOutcome.Cancelled));
        });

        [UnityTest]
        public IEnumerator WhenArrived_TokenCancelled_DetachesAwaiterButKeepsWalking() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            _rig.Nubi.TryStartMoveTo(Target, 0.15f, out _);
            using var barge = new CancellationTokenSource();
            var detached = _rig.Nubi.WhenArrived(barge.Token);
            await UniTask.DelayFrame(5);

            // Act
            barge.Cancel();

            // Assert
            var detachedThrew = false;
            try { await detached; }
            catch (OperationCanceledException) { detachedThrew = true; }
            Assert.That(detachedThrew, Is.True);
            Assert.That(_rig.Nubi.IsMoving, Is.True);
            Assert.That(_rig.Nubi.Mode, Is.EqualTo(LocomotionMode.MovingTo));
            Assert.That(await _rig.Nubi.WhenArrived(CancellationToken.None), Is.EqualTo(MoveOutcome.Arrived));
        });

        [UnityTest]
        public IEnumerator Stop_WhileMoving_HaltsAndReportsCancelled() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            _rig.Nubi.TryStartMoveTo(Target, 0.15f, out _);
            var awaiter = _rig.Nubi.WhenArrived(CancellationToken.None);
            await UniTask.DelayFrame(10);

            // Act
            _rig.Nubi.Stop();
            var stoppedAt = _rig.Nubi.Position;
            await UniTask.DelayFrame(10);

            // Assert
            Assert.That(await awaiter, Is.EqualTo(MoveOutcome.Cancelled));
            Assert.That(_rig.Nubi.IsMoving, Is.False);
            Assert.That(Vector3.Distance(_rig.Nubi.Position, stoppedAt), Is.LessThan(0.05f));
        });

        [UnityTest]
        public IEnumerator TryStartMoveTo_DeadlinePasses_ReportsTimedOut() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            _rig.Nubi.TryStartMoveTo(Target, 0.15f, out var eta);
            var awaiter = _rig.Nubi.WhenArrived(CancellationToken.None);
            await UniTask.DelayFrame(2);

            // Act
            _rig.Clock.Advance(TimeSpan.FromSeconds(2 * eta + _rig.Nubi.Settings.locomotion.timeoutSlack + 0.1));
            await UniTask.DelayFrame(2);

            // Assert
            Assert.That(await awaiter, Is.EqualTo(MoveOutcome.TimedOut));
            Assert.That(_rig.Nubi.Mode, Is.EqualTo(LocomotionMode.None));
        });

        [UnityTest]
        public IEnumerator Follow_UserFarAway_WalksIntoComfortBand() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            _rig.User.Head = new Pose(new Vector3(0f, 1.6f, 4f), Quaternion.identity);

            // Act
            _rig.Nubi.Follow(1.0f, 1.5f);
            var inBand = await CharacterTestRig.Until(() =>
            {
                var d = Vector3.ProjectOnPlane(_rig.Nubi.Position - _rig.User.Head.position, Vector3.up).magnitude;
                return d >= 0.9f && d <= 1.6f && !_rig.Nubi.IsMoving;
            }, 15f);

            // Assert
            Assert.That(inBand, Is.True, "never settled 1.0–1.5 m from the user");
            Assert.That(_rig.Nubi.Mode, Is.EqualTo(LocomotionMode.Following));
            Assert.That(Vector3.Angle(_rig.Nubi.Forward, Vector3.forward), Is.LessThan(30f), "faces the user");
        });

        [UnityTest]
        public IEnumerator Follow_UserTwoMetresAwayStanding_WalksInsteadOfCatchingUp() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var agent = _rig.Nubi.GetComponent<NavMeshAgent>();
            _rig.User.Head = new Pose(new Vector3(0f, 1.6f, 2f), Quaternion.identity);

            // Act
            _rig.Nubi.Follow(1.0f, 1.5f);
            var moving = await CharacterTestRig.Until(() => _rig.Nubi.IsMoving, 1f);

            // Assert
            Assert.That(moving, Is.True);
            Assert.That(agent.speed, Is.EqualTo(_rig.Nubi.Settings.locomotion.walkSpeed));
        });

        async UniTask StopWhenCancelled(UniTask<MoveOutcome> arrival)
        {
            if (await arrival == MoveOutcome.Cancelled)
                _rig.Nubi.Stop();
        }
    }
}
#endif
