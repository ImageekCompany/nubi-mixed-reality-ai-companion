using Jolly.XR.Occlusion;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class OcclusionPolicyTests
    {
        [TestCase(true, true, OcclusionMode.EnvironmentDepth)]
        [TestCase(true, false, OcclusionMode.EnvironmentDepth)]
        [TestCase(false, true, OcclusionMode.SceneMeshes)]
        [TestCase(false, false, OcclusionMode.None)]
        public void Select_DepthPreferred_UsesDepthWhileLiveElseTheFallback(bool depthLive, bool fallback, OcclusionMode expected)
        {
            Assert.That(OcclusionPolicy.Select(OcclusionPreference.EnvironmentDepth, depthLive, fallback), Is.EqualTo(expected));
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void Select_SceneMeshesPreferred_NeverUsesDepth(bool depthLive, bool fallback)
        {
            Assert.That(OcclusionPolicy.Select(OcclusionPreference.SceneMeshes, depthLive, fallback),
                Is.EqualTo(OcclusionMode.SceneMeshes));
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        public void Select_Off_IsNone(bool depthLive, bool fallback)
        {
            Assert.That(OcclusionPolicy.Select(OcclusionPreference.Off, depthLive, fallback), Is.EqualTo(OcclusionMode.None));
        }

        [TestCase(10.0, 9.6, true)]
        [TestCase(10.0, 9.5, true)]
        [TestCase(10.0, 9.4, false)]
        public void IsDepthLive_WithinTheStaleWindow(double now, double lastFrame, bool expected)
        {
            Assert.That(OcclusionPolicy.IsDepthLive(now, lastFrame, 0.5), Is.EqualTo(expected));
        }

        [Test]
        public void IsDepthLive_BeforeTheFirstFrame_IsFalse()
        {
            Assert.That(OcclusionPolicy.IsDepthLive(0.0, double.NegativeInfinity, 0.5), Is.False);
        }
    }
}
