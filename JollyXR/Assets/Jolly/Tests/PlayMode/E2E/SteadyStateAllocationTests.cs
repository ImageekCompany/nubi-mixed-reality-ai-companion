using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.E2E
{
    /// <summary>Architecture §11 Memory and GC: the booted app allocates nothing per frame while idle.</summary>
    public sealed class SteadyStateAllocationTests
    {
        const int WarmUpFrames = 60;
        const int SampleFrames = 120;

        [UnityTest]
        public IEnumerator NoGcAllocInSteadyStateIdle() => UniTask.ToCoroutine(async () =>
        {
            var app = await AppHarness.Start();
            try
            {
                Assert.That(await app.WaitForIdle(5), Is.True, app.StatePath());
                for (var i = 0; i < WarmUpFrames; i++)
                    await UniTask.NextFrame();

                var recorder = Recorder.Get("GC.Alloc");
                recorder.FilterToCurrentThread();
                recorder.enabled = true;
                var allocations = 0;
                for (var i = 0; i < SampleFrames; i++)
                {
                    await UniTask.NextFrame();
                    allocations += recorder.sampleBlockCount;
                }
                recorder.enabled = false;
                recorder.CollectFromAllThreads();

                Assert.That(allocations, Is.Zero, $"GC.Alloc samples over {SampleFrames} idle frames");
            }
            finally
            {
                await app.Unload();
            }
        });
    }
}
