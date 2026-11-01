using System;
using System.IO;
using Jolly.Core.Secrets;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Secrets
{
    public sealed class InstallIdentityTests
    {
        string _dir;

        [SetUp]
        public void SetUp() => _dir = TempDirectory.Create();

        [TearDown]
        public void TearDown() => TempDirectory.Delete(_dir);

        [Test]
        public void SafetyIdentifier_IsStableAcrossLoadsAnd32Hex()
        {
            var path = Path.Combine(_dir, "install_id");
            var calls = 0;
            Guid NewGuid() { calls++; return new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"); }

            var first = InstallIdentity.LoadOrCreateSafetyIdentifier(path, NewGuid);
            var second = InstallIdentity.LoadOrCreateSafetyIdentifier(path, NewGuid);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.Match("^[0-9a-f]{32}$"));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(File.ReadAllText(path), Does.Not.Contain(first), "the file stores the GUID, not the hash");
        }

        [Test]
        public void SafetyIdentifier_CorruptFile_IsRegenerated()
        {
            var path = Path.Combine(_dir, "install_id");
            File.WriteAllText(path, "not-a-guid");

            var id = InstallIdentity.LoadOrCreateSafetyIdentifier(path, () => new Guid("11111111-2222-3333-4444-555555555555"));

            Assert.That(id, Does.Match("^[0-9a-f]{32}$"));
            Assert.That(File.ReadAllText(path), Is.EqualTo("11111111-2222-3333-4444-555555555555"));
        }
    }
}
