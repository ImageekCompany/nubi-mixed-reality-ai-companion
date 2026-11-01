using System.Collections;
using System.Collections.Generic;
using Jolly.XR.Tracking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Hands;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class XrUserPoseProviderTests
    {
        GameObject _root;
        Transform _head;
        FakeHandSource _hands;
        XrUserPoseProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("user");
            _head = new GameObject("head").transform;
            _head.SetParent(_root.transform);
            _head.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.Euler(10f, 20f, 0f));
            _hands = new FakeHandSource();
            _provider = _root.AddComponent<XrUserPoseProvider>();
            _provider.Construct(_head, _hands);
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        [Test]
        public void Head_IsTheCameraTransformPose()
        {
            Assert.That(_provider.Head.position, Is.EqualTo(_head.position));
            Assert.That(_provider.Head.rotation, Is.EqualTo(_head.rotation));
        }

        [Test]
        public void TryGetPointingRay_NoPointingHand_False()
        {
            _hands.Set(Handedness.Right, HandPoses.Open(Pose.identity));

            Assert.That(_provider.TryGetPointingRay(out _), Is.False);
        }

        [Test]
        public void TryGetPointingRay_LeftHandPointing_ReturnsItsIndexRay()
        {
            _hands.Set(Handedness.Right, HandPoses.Fist(Pose.identity));
            _hands.Set(Handedness.Left, HandPoses.Pointing(new Pose(Vector3.one, Quaternion.Euler(0f, -90f, 0f))));

            Assert.That(_provider.TryGetPointingRay(out var ray), Is.True);
            Assert.That(Vector3.Angle(ray.direction, Vector3.left), Is.LessThan(1f));
        }

        [Test]
        public void GetHandKeyPoints_FivePerTrackedHand_ClearsTheList()
        {
            var points = new List<Vector3> { Vector3.one * 99f };
            _hands.Set(Handedness.Right, HandPoses.Open(Pose.identity));

            _provider.GetHandKeyPoints(points);

            Assert.That(points, Has.Count.EqualTo(5));
            Assert.That(points, Has.No.Member(Vector3.one * 99f));
        }

        [UnityTest]
        public IEnumerator TryGetHeadAt_InterpolatesRecordedFrames()
        {
            var start = Time.realtimeSinceStartupAsDouble;
            yield return null;
            _head.position = new Vector3(1f, 1.6f, 0f);
            yield return null;

            Assert.That(_provider.TryGetHeadAt(Time.realtimeSinceStartupAsDouble + 1.0, out var latest), Is.True);
            Assert.That(latest.position.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(_provider.TryGetHeadAt(start - 10.0, out _), Is.False);
        }
    }
}
