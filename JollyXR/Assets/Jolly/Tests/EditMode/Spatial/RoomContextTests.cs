using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Spatial;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Spatial
{
    public sealed class RoomContextTests
    {
        FakeRoomScan _scan;
        FakeWorldQuery _world;
        FakeUserPoseProvider _user;
        RoomSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _scan = new FakeRoomScan();
            _world = new FakeWorldQuery();
            _user = new FakeUserPoseProvider { Head = new Pose(new Vector3(0f, 1.6f, 0f), Quaternion.identity) };
            _settings = new RoomSettings();
        }

        RoomContext Context() => new(_scan, _world, _user, _settings);

        [Test]
        public void Scanned_ListsOneEntryPerLabel_NearestFirst_InHalfMetres()
        {
            _world.AddBox("couch", new Vector3(0f, 0f, 2.6f), Vector3.one);
            _world.AddBox("table", new Vector3(1f, 0f, 0f), Vector3.one);
            _world.AddBox("table", new Vector3(4f, 0f, 0f), Vector3.one);
            _world.AddBox("bed", new Vector3(0.1f, 0f, 0.1f), Vector3.one);

            var text = Context().Build(300);

            Assert.That(text, Is.EqualTo("Room: scanned. Furniture: bed 0.5 m, table 1 m, couch 2.5 m."));
        }

        [Test]
        public void Scanned_LabelsAreSanitisedLikePlaceNames()
        {
            _world.Surfaces.Add(new SceneSurface("Ta\"ble<x>\n", new Pose(new Vector3(1f, 0.4f, 0f), Quaternion.identity), Vector3.one));

            Assert.That(Context().Build(300), Is.EqualTo("Room: scanned. Furniture: tablex 1 m."));
        }

        [Test]
        public void Scanned_EntriesThatDoNotFitAreLeftOutWhole()
        {
            _world.AddBox("table", new Vector3(1f, 0f, 0f), Vector3.one);
            _world.AddBox("couch", new Vector3(2f, 0f, 0f), Vector3.one);

            var text = Context().Build("Room: scanned. Furniture: table 1 m.".Length);

            Assert.That(text, Is.EqualTo("Room: scanned. Furniture: table 1 m."));
        }

        [Test]
        public void Scanned_WithoutFurniture_SaysSo()
        {
            Assert.That(Context().Build(300), Is.EqualTo("Room: scanned, no furniture labelled."));
        }

        [Test]
        public void Unscanned_TellsTheModelWalkingIsLimitedToTheFallbackDisc()
        {
            _scan.Status = RoomScanStatus.Unscanned;

            Assert.That(Context().Build(300),
                Is.EqualTo("Room: not scanned (no Space Setup); walking is limited to about 1.5 m around the user."));
        }

        [Test]
        public void Unscanned_WithoutFallback_WalkingIsUnavailable()
        {
            _scan.Status = RoomScanStatus.Unscanned;
            _settings.FallbackFloor = false;

            Assert.That(Context().Build(300), Is.EqualTo("Room: not scanned (no Space Setup); walking is unavailable."));
        }

        [Test]
        public void NoPermission_SaysWalkingIsUnavailable()
        {
            _scan.Status = RoomScanStatus.NoPermission;

            Assert.That(Context().Build(300), Is.EqualTo("Room: scene permission denied; walking is unavailable."));
        }

        [Test]
        public void Pending_IsEmpty()
        {
            _scan.Status = RoomScanStatus.Pending;
            _world.AddBox("table", new Vector3(1f, 0f, 0f), Vector3.one);

            Assert.That(Context().Build(300), Is.Empty);
        }
    }
}
