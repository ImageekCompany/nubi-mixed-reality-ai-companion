using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Presentation;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Presentation
{
    public sealed class MaskOverlayViewTests
    {
        const float Tolerance = 1e-4f;
        static readonly CameraIntrinsics Intrinsics = new(new Vector2(500f, 500f), new Vector2(320f, 240f), new Vector2Int(640, 480));

        FakeClock _clock;
        FakeConversationEvents _events;
        MaskOverlayView _view;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _events = new FakeConversationEvents();
            _view = PresentationAssets.Instantiate<MaskOverlayView>("MaskOverlay");
            _root = _view.transform.root.gameObject;
            _view.Construct(_clock, _events);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        [Test]
        public void CentredBox_IsPlacedOnTheOpticalAxis_FacingTheCamera_SizedFromIntrinsics()
        {
            var box = new RectInt(270, 190, 100, 100);   // centre (320, 240) = principal point

            _view.Show(new[] { Segment(box) }, Image(Pose.identity), 2f);

            var quad = _view.QuadAt(0);
            AssertNear(quad.position, new Vector3(0f, 0f, 2f));
            Assert.That(Quaternion.Angle(quad.rotation, Quaternion.identity), Is.LessThan(0.01f));
            AssertNear(quad.lossyScale, new Vector3(0.4f, 0.4f, 1f));   // 100 px / 500 px × 2 m
            Assert.That(_view.VisibleCount, Is.EqualTo(1));
        }

        [Test]
        public void BoxAboveAndRightOfCentre_IsUpAndRight_AtTheGivenDistanceAlongItsRay()
        {
            var box = new RectInt(370, 90, 100, 100);   // centre (420, 140): +100 px right, 100 px up

            _view.Show(new[] { Segment(box) }, Image(Pose.identity), 3f);

            var position = _view.QuadAt(0).position;
            Assert.That(position.x, Is.GreaterThan(0f));
            Assert.That(position.y, Is.GreaterThan(0f), "image y grows downwards, world y upwards");
            Assert.That(position.magnitude, Is.EqualTo(3f).Within(Tolerance));
            Assert.That(position.x / position.z, Is.EqualTo(0.2f).Within(Tolerance));
        }

        [Test]
        public void CameraPose_TransformsThePlacement()
        {
            var camera = new Pose(new Vector3(1f, 1.5f, 0f), Quaternion.Euler(0f, 90f, 0f));   // looking along world +X

            _view.Show(new[] { Segment(new RectInt(270, 190, 100, 100)) }, Image(camera), 2f);

            var quad = _view.QuadAt(0);
            AssertNear(quad.position, new Vector3(3f, 1.5f, 0f));
            Assert.That(Quaternion.Angle(quad.rotation, camera.rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void MaskTexture_KeepsTheTopRowOnTop()
        {
            var bits = new ulong[1];
            bits[0] = 1UL;   // only pixel (0, 0): top-left in image space
            var obj = new SegmentedObject(1, new RectInt(0, 0, 4, 2), 0.9f, new BitMask(4, 2, bits));

            _view.Show(new[] { obj }, Image(Pose.identity), 1f);

            var texture = _view.TextureAt(0);
            Assert.That(texture.GetPixel(0, 1).r, Is.EqualTo(1f).Within(0.01f), "texture row 1 is the image's top row");
            Assert.That(texture.GetPixel(0, 0).r, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void SeveralObjects_GetOneQuadEach()
        {
            _view.Show(new[] { Segment(new RectInt(0, 0, 10, 10)), Segment(new RectInt(100, 100, 20, 20)) }, Image(Pose.identity), 1f);

            Assert.That(_view.VisibleCount, Is.EqualTo(2));
            Assert.That(_view.QuadAt(0).gameObject.activeSelf && _view.QuadAt(1).gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator Overlay_FadesOutAfterSixSeconds() => UniTask.ToCoroutine(async () =>
        {
            _view.Show(new[] { Segment(new RectInt(0, 0, 10, 10)) }, Image(Pose.identity), 1f);

            _clock.Advance(TimeSpan.FromSeconds(5.9));
            await PresentationAssets.AfterUpdate();
            Assert.That(_view.VisibleCount, Is.EqualTo(1));

            _clock.Advance(TimeSpan.FromSeconds(1));
            await PresentationAssets.AfterUpdate();
            Assert.That(_view.VisibleCount, Is.Zero);
            Assert.That(_view.QuadAt(0).gameObject.activeSelf, Is.False);
        });

        [Test]
        public void NextTurn_ClearsTheOverlay()
        {
            _view.Show(new[] { Segment(new RectInt(0, 0, 10, 10)) }, Image(Pose.identity), 1f);

            _events.RaiseState(ConversationState.Idle, ConversationState.Listening);

            Assert.That(_view.VisibleCount, Is.Zero);
        }

        static SegmentedObject Segment(RectInt box) =>
            new(1, box, 0.9f, new BitMask(box.width, box.height, new ulong[(box.width * box.height + 63) / 64]));

        static EncodedImage Image(Pose camera) => new(Array.Empty<byte>(), "image/jpeg", 640, 480, Intrinsics, camera);

        static void AssertNear(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), $"{actual:F4} vs {expected:F4}");
    }
}
