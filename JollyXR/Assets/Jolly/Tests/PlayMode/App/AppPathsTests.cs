using System.IO;
using Jolly.App.Platform;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.App
{
    public sealed class AppPathsTests
    {
        [Test]
        public void RedirectMemory_MovesBothMemoryStoresButNotUsageOrIdentity()
        {
            var paths = new AppPaths("root");

            paths.RedirectMemory(Path.Combine("root", "selftest", "memory"));

            Assert.That(paths.MemoryDirectory, Is.EqualTo(Path.Combine("root", "selftest", "memory")));
            Assert.That(paths.SceneMemory, Is.EqualTo(Path.Combine("root", "selftest", "memory", "scene.json")));
            Assert.That(paths.Usage, Is.EqualTo(Path.Combine("root", "usage.json")));
            Assert.That(paths.InstallId, Is.EqualTo(Path.Combine("root", "install_id")));
        }
    }
}
