using Jolly.XR.Tracking;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class PoseHistoryTests
    {
        [Test]
        public void TrySample_BetweenTwoSamples_Interpolates()
        {
            var history = new PoseHistory(8);
            history.Add(1.0, new Pose(Vector3.zero, Quaternion.identity));
            history.Add(1.1, new Pose(new Vector3(1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f)));

            Assert.That(history.TrySample(1.05, out var pose), Is.True);

            Assert.That(Vector3.Distance(pose.position, new Vector3(0.5f, 0f, 0f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(pose.rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.1f));
        }

        [Test]
        public void TrySample_NewerThanTheLatest_ReturnsTheLatest()
        {
            var history = new PoseHistory(8);
            history.Add(1.0, new Pose(Vector3.one, Quaternion.identity));

            Assert.That(history.TrySample(1.5, out var pose), Is.True);
            Assert.That(pose.position, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void TrySample_OlderThanTheOldest_ReturnsFalse()
        {
            var history = new PoseHistory(8);
            history.Add(1.0, Pose.identity);

            Assert.That(history.TrySample(0.9, out _), Is.False);
        }

        [Test]
        public void TrySample_Empty_ReturnsFalse()
        {
            Assert.That(new PoseHistory(4).TrySample(0, out _), Is.False);
        }

        [Test]
        public void Add_BeyondCapacity_DropsTheOldest()
        {
            var history = new PoseHistory(3);
            for (var i = 0; i < 5; i++)
                history.Add(i, new Pose(new Vector3(i, 0f, 0f), Quaternion.identity));

            Assert.That(history.TrySample(1.5, out _), Is.False, "samples 0 and 1 were overwritten");
            Assert.That(history.TrySample(2.5, out var pose), Is.True);
            Assert.That(pose.position.x, Is.EqualTo(2.5f).Within(1e-5f));
        }

        [Test]
        public void Add_OutOfOrderTimestamp_IsIgnored()
        {
            var history = new PoseHistory(4);
            history.Add(2.0, new Pose(Vector3.one, Quaternion.identity));
            history.Add(1.0, Pose.identity);

            Assert.That(history.TrySample(1.5, out _), Is.False);
        }
    }
}
