using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Jolly.Tests.PlayMode.Presentation
{
    /// <summary>
    /// The head-following views stay inside a Quest system recording and a comfortable glance, and clear of each other.
    /// The stabilised recording is not centred on head-forward: measured on 2,605 frames of two recordings, it shows
    /// 13.6° above, 24.1° below, 32.6° left and 30.1° right of it. Each view's angles come from its own z, since the
    /// views sit at different depths and a shared one would misjudge their edges.
    /// </summary>
    public sealed class ViewLayoutTests
    {
        const float MaxSideDegrees = 25f;
        const float MaxAboveDegrees = 13.6f;
        const float MaxBelowDegrees = 24f;

        [TestCase("Captions")]
        [TestCase("ToolBadges")]
        [TestCase("LatencyHud")]
        [TestCase("StatusIndicator")]
        public void HeadFollowingView_FitsInsideTheRecordedWindow(string prefab)
        {
            var view = AngularRect(prefab);

            Assert.That(Mathf.Max(-view.xMin, view.xMax), Is.LessThanOrEqualTo(MaxSideDegrees), "outer edge, degrees off head-forward");
            Assert.That(view.yMax, Is.LessThanOrEqualTo(MaxAboveDegrees), "top edge, degrees above head-forward");
            Assert.That(-view.yMin, Is.LessThanOrEqualTo(MaxBelowDegrees), "bottom edge, degrees below head-forward");
        }

        [TestCase("Captions", "ToolBadges")]
        [TestCase("Captions", "StatusIndicator")]
        [TestCase("ToolBadges", "StatusIndicator")]
        [TestCase("Captions", "LatencyHud")]
        public void HeadFollowingViews_DoNotOverlap(string first, string second)
        {
            var a = AngularRect(first);
            var b = AngularRect(second);

            Assert.That(a.Overlaps(b), Is.False, $"{first} {a} and {second} {b}, degrees off head-forward");
        }

        static Rect AngularRect(string prefab)
        {
            var root = PresentationAssets.Load<GameObject>(PresentationAssets.PrefabFolder + prefab + ".prefab");
            var offset = root.GetComponent<LazyFollow>().targetOffset;
            var rect = (RectTransform)root.transform;
            var half = rect.sizeDelta * rect.localScale.x / 2f;

            return Rect.MinMaxRect(
                Degrees(offset.x - half.x, offset.z),
                Degrees(offset.y - half.y, offset.z),
                Degrees(offset.x + half.x, offset.z),
                Degrees(offset.y + half.y, offset.z));
        }

        static float Degrees(float across, float depth) => Mathf.Atan2(across, depth) * Mathf.Rad2Deg;
    }
}
