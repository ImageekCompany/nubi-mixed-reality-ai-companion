using Jolly.XR.Capture;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class Camera2LensPoseTests
    {
        // Camera id 50 (left) on a Quest 3, Horizon OS v209 (device-spike.md §1).
        static readonly Vector3 LeftTranslation = new(-0.03229f, -0.01753f, -0.06316f);
        static readonly Quaternion LeftRotation = new(-0.99527f, 0.00269f, -0.00406f, 0.09704f);

        [Test]
        public void ToUnity_CameraFacingAwayFromTheViewer_IsIdentityRotation()
        {
            // Camera2 cameras look down the sensor frame's -Z with image-down = sensor -Y: 180° about X.
            var pose = Camera2LensPose.ToUnity(Vector3.zero, new Quaternion(1f, 0f, 0f, 0f));

            Assert.That(Quaternion.Angle(pose.rotation, Quaternion.identity), Is.LessThan(0.01f));
        }

        [Test]
        public void ToUnity_FlipsTheSensorZAxis()
        {
            var pose = Camera2LensPose.ToUnity(LeftTranslation, LeftRotation);

            Assert.That(Vector3.Distance(pose.position, new Vector3(-0.03229f, -0.01753f, 0.06316f)), Is.LessThan(1e-6f));
        }

        [Test]
        public void ToUnity_QuestLeftCamera_LooksForwardAndSlightlyDown()
        {
            var pose = Camera2LensPose.ToUnity(LeftTranslation, LeftRotation);
            var forward = pose.rotation * Vector3.forward;
            var up = pose.rotation * Vector3.up;

            Assert.That(forward.z, Is.GreaterThan(0.97f));
            Assert.That(forward.y, Is.LessThan(-0.15f).And.GreaterThan(-0.25f), "about 11° below the horizon");
            Assert.That(up.y, Is.GreaterThan(0.97f), "image up stays world up");
        }

        [Test]
        public void ToUnity_UnnormalisedQuaternion_IsNormalised()
        {
            var pose = Camera2LensPose.ToUnity(Vector3.zero, new Quaternion(2f, 0f, 0f, 0f));

            Assert.That(Quaternion.Angle(pose.rotation, Quaternion.identity), Is.LessThan(0.01f));
        }
    }
}
