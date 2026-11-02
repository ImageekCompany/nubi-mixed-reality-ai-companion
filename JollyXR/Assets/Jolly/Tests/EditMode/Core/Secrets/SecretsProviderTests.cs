using System.IO;
using Jolly.Contracts;
using Jolly.Core.Secrets;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Secrets
{
    public sealed class SecretsProviderTests
    {
        string _dir;

        [SetUp]
        public void SetUp() => _dir = TempDirectory.Create();

        [TearDown]
        public void TearDown() => TempDirectory.Delete(_dir);

        string Write(string name, string content)
        {
            var path = Path.Combine(_dir, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public void ForDevice_NoPushedFile_UsesTheEmbeddedJson()
        {
            // Act
            var secrets = JsonFileSecretsProvider.ForDevice(Path.Combine(_dir, "none.json"), "{\"K\":\"embedded\"}");

            // Assert
            Assert.That(secrets.TryGet("K", out var value), Is.True);
            Assert.That(value, Is.EqualTo("embedded"));
        }

        [Test]
        public void ForDevice_PushedFile_WinsOverTheEmbeddedJson()
        {
            // Arrange
            var path = Write("secrets.json", "{\"K\":\"pushed\"}");

            // Act
            var secrets = JsonFileSecretsProvider.ForDevice(path, "{\"K\":\"embedded\"}");

            // Assert
            Assert.That(secrets.TryGet("K", out var value), Is.True);
            Assert.That(value, Is.EqualTo("pushed"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void ForDevice_NoFileAndNothingEmbedded_HasNoKeys(string embedded)
        {
            // Act
            var secrets = JsonFileSecretsProvider.ForDevice(Path.Combine(_dir, "none.json"), embedded);

            // Assert
            Assert.That(secrets.TryGet("K", out _), Is.False);
            Assert.That(secrets.LoadError, Is.Null);
        }

        [Test]
        public void DotEnv_ParsesCommentsQuotesExportAndCrlf()
        {
            var path = Write(".env",
                "# comment\r\n" +
                "META_API_KEY=plainValue\r\n" +
                "export WIT_AI_CLIENT_TOKEN=\"quoted value\"\r\n" +
                "WIT_AI_APP_ID='single' \r\n" +
                "  SPACED_KEY = spaced  # trailing comment\r\n" +
                "EMPTY=\r\n" +
                "NOEQUALS\r\n");

            var secrets = new DotEnvSecretsProvider(path);

            AssertSecret(secrets, SecretKeys.MetaApiKey, "plainValue");
            AssertSecret(secrets, SecretKeys.WitClientToken, "quoted value");
            AssertSecret(secrets, SecretKeys.WitAppId, "single");
            AssertSecret(secrets, "SPACED_KEY", "spaced");
            Assert.That(secrets.TryGet("EMPTY", out _), Is.False);
            Assert.That(secrets.TryGet("NOEQUALS", out _), Is.False);
        }

        [Test]
        public void DotEnv_QuotedValueKeepsHash()
        {
            var secrets = new DotEnvSecretsProvider(Write(".env", "A=\"x # y\"\n"));

            AssertSecret(secrets, "A", "x # y");
        }

        [Test]
        public void JsonFile_ReadsStringValues()
        {
            var path = Write("secrets.json", "{\r\n \"META_API_KEY\": \"k1\",\n \"WIT_AI_CLIENT_TOKEN\": \"k2\", \"N\": 5 }");

            var secrets = new JsonFileSecretsProvider(path);

            AssertSecret(secrets, SecretKeys.MetaApiKey, "k1");
            AssertSecret(secrets, SecretKeys.WitClientToken, "k2");
            Assert.That(secrets.TryGet("N", out _), Is.False);
            Assert.That(secrets.LoadError, Is.Null);
        }

        [Test]
        public void MissingFile_HasNoKeysAndNoError()
        {
            var json = new JsonFileSecretsProvider(Path.Combine(_dir, "none.json"));
            var env = new DotEnvSecretsProvider(Path.Combine(_dir, "none.env"));

            Assert.That(json.TryGet(SecretKeys.MetaApiKey, out var v1), Is.False);
            Assert.That(v1, Is.Null);
            Assert.That(env.TryGet(SecretKeys.MetaApiKey, out _), Is.False);
            Assert.That(json.LoadError, Is.Null);
        }

        [Test]
        public void JsonFile_Malformed_HasNoKeysAndReportsErrorWithoutValues()
        {
            var secrets = new JsonFileSecretsProvider(Write("secrets.json", "{\"META_API_KEY\": \"LLM_1_secret\""));

            Assert.That(secrets.TryGet(SecretKeys.MetaApiKey, out _), Is.False);
            Assert.That(secrets.LoadError, Is.Not.Null);
            Assert.That(secrets.LoadError, Does.Not.Contain("LLM_1_secret"));
        }

        static void AssertSecret(ISecretsProvider secrets, string key, string expected)
        {
            Assert.That(secrets.TryGet(key, out var value), Is.True, key);
            Assert.That(value, Is.EqualTo(expected), key);
        }
    }
}
