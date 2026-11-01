using System;
using Jolly.Contracts;
using Jolly.Core.Spatial;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Spatial
{
    public sealed class RoomScanDetectorTests
    {
        RoomScanDetector _detector;

        [SetUp]
        public void SetUp() => _detector = new RoomScanDetector(TimeSpan.FromSeconds(8));

        [Test]
        public void WithoutPermission_IsNoPermissionWhateverTheFloor()
        {
            Assert.That(_detector.Update(0, permissionGranted: false, hasFloor: false), Is.EqualTo(RoomScanStatus.NoPermission));
            Assert.That(_detector.Update(100, permissionGranted: false, hasFloor: true), Is.EqualTo(RoomScanStatus.NoPermission));
        }

        [Test]
        public void NoFloor_IsPendingUntilTheTimeoutFromTheGrant_ThenUnscanned()
        {
            _detector.Update(0, false, false);
            Assert.That(_detector.Update(10, true, false), Is.EqualTo(RoomScanStatus.Pending), "the clock starts at the grant");
            Assert.That(_detector.Update(17.9, true, false), Is.EqualTo(RoomScanStatus.Pending));
            Assert.That(_detector.Update(18, true, false), Is.EqualTo(RoomScanStatus.Unscanned));
        }

        [Test]
        public void FloorAppears_IsScannedAtOnce_EvenAfterUnscanned()
        {
            _detector.Update(0, true, false);
            Assert.That(_detector.Update(9, true, false), Is.EqualTo(RoomScanStatus.Unscanned));

            Assert.That(_detector.Update(9.5, true, true), Is.EqualTo(RoomScanStatus.Scanned));
        }

        [Test]
        public void FloorRemoved_WaitsAFullTimeoutAgain()
        {
            _detector.Update(0, true, true);
            Assert.That(_detector.Update(20, true, false), Is.EqualTo(RoomScanStatus.Pending));
            Assert.That(_detector.Update(28, true, false), Is.EqualTo(RoomScanStatus.Unscanned));
        }

        [Test]
        public void Restart_ReArmsTheTimeout()
        {
            _detector.Update(0, true, false);
            Assert.That(_detector.Update(8, true, false), Is.EqualTo(RoomScanStatus.Unscanned));

            _detector.Restart(30);

            Assert.That(_detector.Update(31, true, false), Is.EqualTo(RoomScanStatus.Pending));
            Assert.That(_detector.Update(38, true, false), Is.EqualTo(RoomScanStatus.Unscanned));
        }

        [Test]
        public void PermissionRevoked_ThenRegranted_StartsTheTimeoutOver()
        {
            _detector.Update(0, true, false);
            _detector.Update(5, false, false);

            Assert.That(_detector.Update(9, true, false), Is.EqualTo(RoomScanStatus.Pending));
            Assert.That(_detector.Update(17, true, false), Is.EqualTo(RoomScanStatus.Unscanned));
        }
    }
}
