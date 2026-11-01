using System.Collections;
using Cysharp.Threading.Tasks;
using Jolly.XR.Occlusion;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.E2E
{
    /// <summary>Main.unity's composition root wires real-world occlusion and the contact shadow (ADR-0023).</summary>
    public sealed class MixedRealityWiringTests
    {
        [UnityTest]
        public IEnumerator Boot_ConstructsOcclusion_FallsBackToSceneMeshesWithoutDepth_AndGroundsNubi() =>
            UniTask.ToCoroutine(async () =>
            {
                var app = await AppHarness.Start(online: false);
                try
                {
                    var scene = app.Graph.Scene;

                    Assert.That(scene.occlusion.IsConstructed, Is.True);
                    // The Editor has no occlusion subsystem, so no depth frame arrives: the shipped config falls back.
                    Assert.That(scene.occlusion.Mode, Is.EqualTo(OcclusionMode.SceneMeshes));
                    Assert.That(scene.occlusion.DepthManager, Is.Not.Null, "depth is the shipped preference");
                    Assert.That(scene.nubi.ShadowRig.IsComplete, Is.True);
                    Assert.That(scene.nubi.ShadowRig.shadow.enabled, Is.True);
                }
                finally
                {
                    await app.Unload();
                }
            });
    }
}
