using System.Collections;
using Cysharp.Threading.Tasks;
using Jolly.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Smoke
{
    public sealed class PlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_SurvivesAFrameAndDestroysCleanly() => UniTask.ToCoroutine(async () =>
        {
            var go = new GameObject(nameof(JollyBootstrap));
            go.AddComponent<JollyBootstrap>();
            var frame = Time.frameCount;

            await UniTask.Yield(PlayerLoopTiming.Update);
            await UniTask.NextFrame();

            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Object.Destroy(go);
            await UniTask.NextFrame();
            Assert.That(go == null, Is.True, "destroyed GameObject compares equal to null");
        });
    }
}
