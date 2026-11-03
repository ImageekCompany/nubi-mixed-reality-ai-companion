using System.Collections;
using Cysharp.Threading.Tasks;
using Jolly.Tests.PlayMode.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.E2E
{
    /// <summary>
    /// Main.unity overrides every view's root size, and the override silently wins over the prefab, so a size changed
    /// only in the prefab passes the prefab-level tests and never reaches the device.
    /// </summary>
    public sealed class SceneViewSizeTests
    {
        [UnityTest]
        public IEnumerator Boot_ViewRootSizesMatchTheirPrefabs() =>
            UniTask.ToCoroutine(async () =>
            {
                var app = await AppHarness.Start(online: false);
                try
                {
                    var scene = app.Graph.Scene;

                    AssertMatchesPrefab(scene.toolBadges, "ToolBadges");
                    AssertMatchesPrefab(scene.settings, "SettingsPanel");
                    AssertMatchesPrefab(scene.captions, "Captions");
                    AssertMatchesPrefab(scene.latencyHud, "LatencyHud");
                    AssertMatchesPrefab(scene.status, "StatusIndicator");
                }
                finally
                {
                    await app.Unload();
                }
            });

        static void AssertMatchesPrefab(Component view, string prefab)
        {
            var asset = PresentationAssets.Load<GameObject>(PresentationAssets.PrefabFolder + prefab + ".prefab");
            var inScene = ((RectTransform)view.transform.root).sizeDelta;
            Assert.That(inScene, Is.EqualTo(((RectTransform)asset.transform).sizeDelta), prefab);
        }
    }
}
