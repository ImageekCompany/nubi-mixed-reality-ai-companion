#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Spatial;
using Jolly.Testing;
using Jolly.XR.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>
    /// An unscanned room (ADR-0024) end to end: the real RoomScanMonitor, FloorNavMeshBuilder and Nubi, with no floor but
    /// the fallback area. When the user walks away, the rebuilt NavMesh must still hold Nubi, so come_here and follow
    /// keep working.
    /// </summary>
    public sealed class FallbackFloorWalkTests
    {
        /// <summary>Faster than this over a frame is a teleport, not a walk (catch-up speed is 1.2 m/s).</summary>
        const float MaxWalkSpeed = 2f;

        CharacterTestRig _rig;
        GameObject _room;
        FloorNavMeshBuilder _floor;
        RoomScanMonitor _monitor;
        int _jumps;

        /// <summary>ISceneData over the builder with the floor at y = 0 and no scan ever arriving.</summary>
        sealed class UnscannedRoom : ISceneData
        {
            readonly FloorNavMeshBuilder _floor;
            public UnscannedRoom(FloorNavMeshBuilder floor) => _floor = floor;
            public bool PermissionGranted => true;
            public bool HasScannedFloor => false;
            public bool CanRequestCapture => false;
            public bool TryRequestCapture() => false;
            public void Requery() { }
            public bool SetFallbackFloor(Vector3 center, IReadOnlyList<Vector2> outline) =>
                _floor.SetFallbackFloor(center, outline, 0f);
            public void ClearFallbackFloor() => _floor.ClearFallbackFloor();
        }

        [SetUp]
        public void SetUp()
        {
            _rig = CharacterTestRig.Create(floor: false);
            _rig.Nubi.transform.position = Vector3.zero;
            _room = new GameObject("room");
            _floor = _room.AddComponent<FloorNavMeshBuilder>();
            _floor.Construct(_rig.Clock);
            var settings = new RoomSettings { ScanTimeout = TimeSpan.Zero, OfferScan = false };
            _monitor = new RoomScanMonitor(new UnscannedRoom(_floor), _rig.User, _rig.Nubi, _rig.Clock, new RecordingLog(),
                settings, () => true);
            _jumps = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_room);
            _rig.Dispose();
        }

        void PutUser(Vector3 floor) => _rig.User.Head = new Pose(floor + Vector3.up * 1.6f, Quaternion.identity);

        /// <summary>Runs the app's loop for scaledSeconds or until stopWhen holds: the clock follows game time, the
        /// monitor polls every frame, and frames where Nubi moved faster than it can walk are counted as jumps.</summary>
        async UniTask Run(float scaledSeconds, Func<bool> stopWhen = null, Action<float> eachFrame = null)
        {
            var start = Time.time;
            var last = _rig.Nubi.Position;
            while (Time.time - start < scaledSeconds && (stopWhen == null || !stopWhen()))
            {
                eachFrame?.Invoke(Time.time - start);
                // Not TimeSpan.FromSeconds: Mono rounds it to whole milliseconds, which freezes the clock at a high frame rate.
                _rig.Clock.Advance(TimeSpan.FromTicks((long)(Time.deltaTime * TimeSpan.TicksPerSecond)));
                _monitor.Tick();
                await UniTask.Yield(PlayerLoopTiming.Update);
                var position = _rig.Nubi.Position;
                if (Vector3.Distance(last, position) > MaxWalkSpeed * Time.deltaTime + 0.02f)
                    _jumps++;
                last = position;
            }
        }

        /// <summary>Long enough for a moved area to pass NavMeshBaker's 2 s rebuild throttle.</summary>
        UniTask Settle() => Run(2.5f);

        static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [UnityTest]
        public IEnumerator UserWalksAwayFromAStandingNubi_ComeHereStillArrives(
            [Values(1.5f, 3f)] float distance) => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            PutUser(new Vector3(0f, 0f, 1f));
            await Settle();
            Assert.That(_floor.WalkableArea, Is.EqualTo(WalkableArea.Fallback));

            // Act: the area moves with the user, away from Nubi, in two re-centres.
            PutUser(new Vector3(0f, 0f, 1f + distance * 0.5f));
            await Settle();
            var user = new Vector3(0f, 0f, 1f + distance);
            PutUser(user);
            await Settle();
            var target = user - Vector3.forward;
            var started = _rig.Nubi.TryStartMoveTo(target, 0.1f, out _);
            var outcome = await _rig.Nubi.WhenArrived(CancellationToken.None).Timeout(TimeSpan.FromSeconds(20));

            // Assert
            Assert.That(started, Is.True, "Nubi is still on the NavMesh, and the user's side is reachable");
            Assert.That(outcome, Is.EqualTo(MoveOutcome.Arrived));
            Assert.That(FlatDistance(_rig.Nubi.Position, target), Is.LessThan(0.3f));
        });

        [UnityTest]
        public IEnumerator Follow_KeepsUpAcrossSeveralRecentres() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            PutUser(new Vector3(0f, 0f, 1f));
            await Settle();
            _rig.Nubi.Follow(0.8f, 1.5f);
            var legs = new[] { new Vector3(0f, 0f, 3f), new Vector3(2f, 0f, 3f), new Vector3(2f, 0f, 0.5f), new Vector3(-1f, 0f, 0.5f) };

            foreach (var corner in legs)
            {
                // Act: the user walks to the corner at 0.7 m/s, then waits for Nubi.
                var from = _rig.User.Head.position;
                from.y = 0f;
                var walk = Vector3.Distance(from, corner) / 0.7f;
                await Run(walk, eachFrame: elapsed => PutUser(Vector3.Lerp(from, corner, elapsed / walk)));
                PutUser(corner);
                await Run(8f, () => FlatDistance(_rig.Nubi.Position, corner) <= 1.6f);

                // Assert
                Assert.That(FlatDistance(_rig.Nubi.Position, corner), Is.LessThanOrEqualTo(1.6f), $"caught up at {corner}");
                Assert.That(_rig.Nubi.Mode, Is.EqualTo(LocomotionMode.Following));
            }
            Assert.That(_jumps, Is.EqualTo(0), "Nubi walked; it never jumped");
        });

        [UnityTest]
        public IEnumerator UserFartherThanTheMaximumSpan_NubiPopsBackAndCanWalkAgain() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            PutUser(new Vector3(0f, 0f, 1f));
            await Settle();
            var scale = _rig.Nubi.transform.localScale;

            // Act
            var user = new Vector3(0f, 0f, 5.5f);
            PutUser(user);
            var shrank = false;
            await Run(1f, eachFrame: _ => shrank |= _rig.Nubi.transform.localScale.x < scale.x * 0.5f);
            var landed = _rig.Nubi.Position;
            await Settle();
            var started = _rig.Nubi.TryStartMoveTo(user + Vector3.right * 0.8f, 0.1f, out _);
            var outcome = await _rig.Nubi.WhenArrived(CancellationToken.None).Timeout(TimeSpan.FromSeconds(20));

            // Assert
            Assert.That(FlatDistance(landed, user), Is.EqualTo(1f).Within(0.05f), "back beside the user");
            Assert.That(shrank, Is.True, "the move is shown as a pop, not hidden");
            Assert.That(_rig.Nubi.transform.localScale, Is.EqualTo(scale));
            Assert.That(started, Is.True);
            Assert.That(outcome, Is.EqualTo(MoveOutcome.Arrived));
        });
    }
}
#endif
