using System;
using System.Collections.Generic;
using System.Linq;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Spatial;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Spatial
{
    public sealed class RoomScanMonitorTests
    {
        FakeSceneData _scene;
        FakeUserPoseProvider _user;
        RecordingCharacterController _nubi;
        FakeClock _clock;
        RecordingLog _log;
        RoomSettings _settings;
        bool _canSpeak;
        RoomScanMonitor _monitor;
        int _offers;

        [SetUp]
        public void SetUp()
        {
            _scene = new FakeSceneData();
            _user = new FakeUserPoseProvider { Head = new Pose(new Vector3(1f, 1.6f, 2f), Quaternion.identity) };
            _nubi = new RecordingCharacterController { Position = new Vector3(1f, 0f, 2.8f) };
            _clock = new FakeClock();
            _log = new RecordingLog();
            _settings = new RoomSettings();
            _canSpeak = true;
            _offers = 0;
            Build();
        }

        void Build()
        {
            _monitor = new RoomScanMonitor(_scene, _user, _nubi, _clock, _log, _settings, () => _canSpeak);
            _monitor.OfferScan += () => _offers++;
        }

        void TickAt(double seconds)
        {
            _clock.Advance(TimeSpan.FromSeconds(seconds) - TimeSpan.FromSeconds(_clock.Now));
            _monitor.Tick();
        }

        [Test]
        public void ScannedRoom_NeverGetsAFallbackFloorOrAnOffer()
        {
            _scene.HasScannedFloor = true;

            TickAt(0);
            TickAt(30);

            Assert.That(_monitor.Status, Is.EqualTo(RoomScanStatus.Scanned));
            Assert.That(_scene.FallbackSets, Is.EqualTo(0));
            Assert.That(_offers, Is.EqualTo(0));
        }

        [Test]
        public void NoFloorAfterTheTimeout_PlacesTheFallbackAroundTheUserAndLogsDegraded()
        {
            TickAt(0);
            TickAt(7.9);
            Assert.That(_monitor.Status, Is.EqualTo(RoomScanStatus.Pending));
            Assert.That(_scene.FallbackCenter, Is.Null);

            TickAt(8);

            Assert.That(_monitor.Status, Is.EqualTo(RoomScanStatus.Unscanned));
            Assert.That(_log.Contains(LogLevel.Warning, "evt=room.status status=Unscanned"), Is.True);
            Assert.That(_scene.FallbackCenter, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(_scene.FallbackOutline.Count, Is.EqualTo(_settings.FallbackSegments));
            Assert.That(_scene.FallbackOutline[0].magnitude, Is.EqualTo(_settings.FallbackRadius).Within(1e-5f));
            Assert.That(_log.Contains(LogLevel.Warning, "evt=room.degraded walkable=fallback radius_m=1.5"), Is.True);
        }

        [Test]
        public void Fallback_FollowsTheUserOnlyPastTheRecentreDistance()
        {
            TickAt(0);
            TickAt(8);

            _user.Head = new Pose(new Vector3(1.4f, 1.6f, 2f), Quaternion.identity);
            TickAt(8.5);
            Assert.That(_scene.FallbackSets, Is.EqualTo(1));

            _user.Head = new Pose(new Vector3(1.6f, 1.6f, 2f), Quaternion.identity);
            TickAt(9);
            Assert.That(_scene.FallbackSets, Is.EqualTo(2));
            Assert.That(_scene.FallbackCenter, Is.EqualTo(new Vector3(1.6f, 0f, 2f)));
        }

        [TestCase(1.5f)]
        [TestCase(3f)]
        public void UserWalksAwayFromAStandingNubi_TheAreaStillHoldsNubi(float distance)
        {
            TickAt(0);
            TickAt(8);

            for (var step = 1; step <= 6; step++)
            {
                _user.Head = new Pose(new Vector3(1f + distance * step / 6f, 1.6f, 2.8f), Quaternion.identity);
                TickAt(8 + step * 0.5);
            }

            Assert.That(Inside(_nubi.Position, FallbackFloorArea.CharacterMargin), Is.True, "Nubi is not stranded");
            Assert.That(Inside(_user.Head.position, 0.9f), Is.True, "the user is within the recentre distance");
            Assert.That(_nubi.Relocations, Is.Empty);
        }

        [Test]
        public void NubiLeavesTheArea_RebuildsItThoughTheUserStayed()
        {
            TickAt(0);
            TickAt(8);

            _nubi.Position = new Vector3(3f, 0f, 2f);
            TickAt(8.5);

            Assert.That(_scene.FallbackSets, Is.EqualTo(2));
            Assert.That(Inside(_nubi.Position, FallbackFloorArea.CharacterMargin), Is.True);
        }

        [Test]
        public void UserFartherThanTheMaximumSpan_PopsNubiBackBesideThem()
        {
            TickAt(0);
            TickAt(8);

            _user.Head = new Pose(new Vector3(1f, 1.6f, 2.8f + 4.5f), Quaternion.identity);
            TickAt(8.5);

            Assert.That(_nubi.Relocations.Count, Is.EqualTo(1));
            var back = _nubi.Relocations[0];
            Assert.That(Vector3.Distance(back, new Vector3(1f, 0f, 2.8f + 3.5f)), Is.LessThan(1e-4f),
                "1 m from the user, towards where Nubi was");
            Assert.That(Inside(back, FallbackFloorArea.CharacterMargin), Is.True);
            Assert.That(_log.Contains(LogLevel.Info, "evt=room.character_returned from_m=4.5"), Is.True);

            TickAt(9);
            Assert.That(_scene.FallbackSets, Is.EqualTo(2), "back inside: no rebuild");
        }

        [Test]
        public void WhileNubiPopsBack_ItsOldPositionDoesNotTriggerAnotherReturn()
        {
            TickAt(0);
            TickAt(8);
            var stranded = new Vector3(1f, 0f, 2.8f);
            _user.Head = new Pose(new Vector3(1f, 1.6f, 7.3f), Quaternion.identity);
            TickAt(8.5);

            _nubi.Position = stranded;
            TickAt(9);
            Assert.That(_nubi.Relocations.Count, Is.EqualTo(1), "the pop has not moved Nubi yet");
            Assert.That(_scene.FallbackSets, Is.EqualTo(2));

            TickAt(10);
            Assert.That(_nubi.Relocations.Count, Is.EqualTo(2), "still stranded after the pop would have finished");
        }

        [Test]
        public void FloorArrivesLater_ClearsTheFallback()
        {
            TickAt(0);
            TickAt(8);

            _scene.HasScannedFloor = true;
            TickAt(8.5);

            Assert.That(_monitor.Status, Is.EqualTo(RoomScanStatus.Scanned));
            Assert.That(_scene.FallbackCenter, Is.Null);
        }

        [Test]
        public void FallbackDisabled_StaysUnscannedWithoutAFloor()
        {
            _settings.FallbackFloor = false;
            Build();

            TickAt(0);
            TickAt(8);

            Assert.That(_monitor.Status, Is.EqualTo(RoomScanStatus.Unscanned));
            Assert.That(_scene.FallbackSets, Is.EqualTo(0));
        }

        [Test]
        public void FallbackRefused_IsLoggedOnceAndNotRetried()
        {
            _scene.FallbackSupported = false;

            TickAt(0);
            TickAt(8);
            TickAt(9);

            Assert.That(_log.Entries.Count(e => e.Message.Contains("evt=room.fallback_floor_unavailable")), Is.EqualTo(1));
            Assert.That(_scene.FallbackCenter, Is.Null);
        }

        [Test]
        public void NoPermission_IsNotUnscanned_SoThereIsNoFallbackOrOffer()
        {
            _scene.PermissionGranted = false;

            TickAt(0);
            TickAt(60);

            Assert.That(_monitor.Status, Is.EqualTo(RoomScanStatus.NoPermission));
            Assert.That(_scene.FallbackSets, Is.EqualTo(0));
            Assert.That(_offers, Is.EqualTo(0));
        }

        [Test]
        public void Offer_WaitsUntilNubiCanSpeak_AndComesOncePerSession()
        {
            _canSpeak = false;
            TickAt(0);
            TickAt(8);
            Assert.That(_offers, Is.EqualTo(0));

            _canSpeak = true;
            TickAt(8.5);
            TickAt(9);
            _monitor.OnResumed();
            TickAt(30);

            Assert.That(_offers, Is.EqualTo(1));
        }

        [Test]
        public void Offer_WithoutSpaceSetupToOpen_NeverRaised()
        {
            _scene.CanRequestCapture = false;

            TickAt(0);
            TickAt(8);
            TickAt(30);

            Assert.That(_monitor.Status, Is.EqualTo(RoomScanStatus.Unscanned));
            Assert.That(_offers, Is.EqualTo(0));
        }

        [Test]
        public void Offer_Disabled_NeverRaised()
        {
            _settings.OfferScan = false;
            Build();

            TickAt(0);
            TickAt(8);

            Assert.That(_offers, Is.EqualTo(0));
        }

        [Test]
        public void RequestScan_OpensSpaceSetupOnlyOnceNubiIsSilent()
        {
            _canSpeak = false;
            TickAt(0);

            Assert.That(_monitor.RequestScan(), Is.True);
            TickAt(1);
            Assert.That(_scene.CaptureRequests, Is.EqualTo(0), "the reply is still being spoken");

            _canSpeak = true;
            TickAt(1.5);
            TickAt(2);

            Assert.That(_scene.CaptureRequests, Is.EqualTo(1));
        }

        [Test]
        public void RequestScan_OpensAnywayAfterTheMaximumWait()
        {
            _canSpeak = false;
            TickAt(0);
            _monitor.RequestScan();

            TickAt(9.9);
            Assert.That(_scene.CaptureRequests, Is.EqualTo(0));
            TickAt(10);

            Assert.That(_scene.CaptureRequests, Is.EqualTo(1));
        }

        [Test]
        public void RequestScan_WithoutSpaceSetup_IsRefused()
        {
            _scene.CanRequestCapture = false;

            Assert.That(_monitor.CanRequestScan, Is.False);
            Assert.That(_monitor.RequestScan(), Is.False);
            TickAt(0);
            Assert.That(_scene.CaptureRequests, Is.EqualTo(0));
        }

        [Test]
        public void Resume_RequeriesAndWaitsAFullTimeout_KeepingTheFallbackMeanwhile()
        {
            TickAt(0);
            TickAt(8);

            TickAt(20);
            _monitor.OnResumed();
            TickAt(21);

            Assert.That(_scene.Requeries, Is.EqualTo(1));
            Assert.That(_monitor.Status, Is.EqualTo(RoomScanStatus.Pending));
            Assert.That(_scene.FallbackCenter, Is.Not.Null, "walking must not vanish while the new scan is read");

            _scene.HasScannedFloor = true;
            TickAt(22);

            Assert.That(_monitor.Status, Is.EqualTo(RoomScanStatus.Scanned));
            Assert.That(_scene.FallbackCenter, Is.Null);
        }

        /// <summary>Whether point's floor position is at least margin inside the fallback outline last set.</summary>
        bool Inside(Vector3 point, float margin)
        {
            var center = _scene.FallbackCenter ?? throw new AssertionException("no fallback floor");
            var outline = _scene.FallbackOutline;
            var p = new Vector2(point.x - center.x, point.z - center.z);
            for (var i = 0; i < outline.Count; i++)
            {
                var edge = outline[(i + 1) % outline.Count] - outline[i];
                var toPoint = p - outline[i];
                if ((edge.x * toPoint.y - edge.y * toPoint.x) / edge.magnitude < margin)
                    return false;
            }
            return true;
        }

        [Test]
        public void Resume_WhenAlreadyScanned_DoesNothing()
        {
            _scene.HasScannedFloor = true;
            TickAt(0);

            _monitor.OnResumed();

            Assert.That(_scene.Requeries, Is.EqualTo(0));
        }
    }
}
