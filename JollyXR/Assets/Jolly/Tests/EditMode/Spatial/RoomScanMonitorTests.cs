using System;
using System.Collections.Generic;
using System.Linq;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Spatial;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Spatial
{
    public sealed class RoomScanMonitorTests
    {
        FakeSceneData _scene;
        FakeUserPoseProvider _user;
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
            _clock = new FakeClock();
            _log = new RecordingLog();
            _settings = new RoomSettings();
            _canSpeak = true;
            _offers = 0;
            Build();
        }

        void Build()
        {
            _monitor = new RoomScanMonitor(_scene, _user, _clock, _log, _settings, () => _canSpeak);
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
