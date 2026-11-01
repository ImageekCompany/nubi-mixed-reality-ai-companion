using Jolly.XR.Hands;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class HandGesturesTests
    {
        static readonly Pose PalmDown = new(new Vector3(0f, 1.2f, 0.4f), Quaternion.identity);

        [Test]
        public void IsPointing_IndexOutOthersCurled_True()
        {
            Assert.That(HandGestures.IsPointing(HandPoses.Pointing(PalmDown)), Is.True);
        }

        [Test]
        public void IsPointing_OpenHandOrFist_False()
        {
            Assert.That(HandGestures.IsPointing(HandPoses.Open(PalmDown)), Is.False);
            Assert.That(HandGestures.IsPointing(HandPoses.Fist(PalmDown)), Is.False);
        }

        [Test]
        public void IsOpen_AllFingersExtended_True()
        {
            Assert.That(HandGestures.IsOpen(HandPoses.Open(PalmDown)), Is.True);
            Assert.That(HandGestures.IsOpen(HandPoses.Pointing(PalmDown)), Is.False);
        }

        [Test]
        public void PointingRay_FollowsTheIndexFinger()
        {
            var rotated = new Pose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

            var ray = HandGestures.PointingRay(HandPoses.Pointing(rotated));

            Assert.That(Vector3.Angle(ray.direction, Vector3.right), Is.LessThan(1f));
        }

        [Test]
        public void PalmFaces_PalmDownTowardsAPointBelow_True()
        {
            var hand = HandPoses.Open(PalmDown);

            Assert.That(HandGestures.PalmFaces(hand, PalmDown.position + Vector3.down, 0.6f), Is.True);
            Assert.That(HandGestures.PalmFaces(hand, PalmDown.position + Vector3.up, 0.6f), Is.False);
        }

        [Test]
        public void PinchDistance_IsTheThumbToIndexTipGap()
        {
            Assert.That(HandGestures.PinchDistance(HandPoses.Pinch(PalmDown, 0.012f)), Is.EqualTo(0.012f).Within(1e-5f));
        }
    }
}
