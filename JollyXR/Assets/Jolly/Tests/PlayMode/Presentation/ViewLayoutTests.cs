using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Jolly.Tests.PlayMode.Presentation
{
    /// <summary>
    /// The head-following views stay inside a Quest system recording and a comfortable glance (ADR-0042). A 1920×1080
    /// recording of 2026-11-01 cut off the HUD (outer edge 33° off centre) and the badges (29°); measured on its frames,
    /// the recording shows about ±30° across and ±18° up and down.
    /// </summary>
    public sealed class ViewLayoutTests
    {
        const float MaxSideDegrees = 25f;
        const float MaxUpDownDegrees = 18f;

        [TestCase("Captions")]
        [TestCase("ToolBadges")]
        [TestCase("LatencyHud")]
        [TestCase("StatusIndicator")]
        public void HeadFollowingView_FitsInsideTheComfortableFieldOfView(string prefab)
        {
            var root = PresentationAssets.Load<GameObject>(PresentationAssets.PrefabFolder + prefab + ".prefab");
            var offset = root.GetComponent<LazyFollow>().targetOffset;
            var rect = (RectTransform)root.transform;
            var half = rect.sizeDelta * rect.localScale.x / 2f;

            var horizontal = Mathf.Atan2(Mathf.Abs(offset.x) + half.x, offset.z) * Mathf.Rad2Deg;
            var vertical = Mathf.Atan2(Mathf.Abs(offset.y) + half.y, offset.z) * Mathf.Rad2Deg;

            Assert.That(horizontal, Is.LessThanOrEqualTo(MaxSideDegrees), "outer edge, degrees off straight ahead");
            Assert.That(vertical, Is.LessThanOrEqualTo(MaxUpDownDegrees), "top or bottom edge, degrees off straight ahead");
        }
    }
}
