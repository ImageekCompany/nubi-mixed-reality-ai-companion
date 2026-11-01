using System.IO;
using Jolly.Core.Diagnostics;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Diagnostics
{
    public sealed class AtomicFileTests
    {
        string _dir;

        [SetUp]
        public void SetUp() => _dir = TempDirectory.Create();

        [TearDown]
        public void TearDown() => TempDirectory.Delete(_dir);

        [Test]
        public void WriteAllText_FirstSave_CreatesTargetAndLeavesNoTemp()
        {
            var path = Path.Combine(_dir, "memory", "a.json");

            AtomicFile.WriteAllText(path, "one");

            Assert.That(File.ReadAllText(path), Is.EqualTo("one"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void WriteAllText_ExistingTarget_ReplacesContent()
        {
            var path = Path.Combine(_dir, "a.json");
            AtomicFile.WriteAllText(path, "one");

            AtomicFile.WriteAllText(path, "two");

            Assert.That(File.ReadAllText(path), Is.EqualTo("two"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void WriteAllText_StaleTempFromCrashedWrite_IsOverwritten()
        {
            var path = Path.Combine(_dir, "a.json");
            File.WriteAllText(path + ".tmp", "garbage");

            AtomicFile.WriteAllText(path, "fresh");

            Assert.That(File.ReadAllText(path), Is.EqualTo("fresh"));
        }

        [Test]
        public void TryReadAllText_MissingFile_ReturnsFalse()
        {
            Assert.That(AtomicFile.TryReadAllText(Path.Combine(_dir, "none.json"), out var text), Is.False);
            Assert.That(text, Is.Null);
        }

        [Test]
        public void Delete_RemovesTargetAndTemp()
        {
            var path = Path.Combine(_dir, "a.json");
            AtomicFile.WriteAllText(path, "x");
            File.WriteAllText(path + ".tmp", "y");

            AtomicFile.Delete(path);

            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
    }
}
