using Jolly.Contracts;
using Jolly.Core.Diagnostics;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Diagnostics
{
    public sealed class RedactingLogTests
    {
        [TestCase("evt=x key=LLM_123_abcDEF-xyz rest", "evt=x key=*** rest")]
        [TestCase("Authorization: Bearer abc.def-ghi", "Authorization: Bearer ***")]
        [TestCase("evt=plain ms=12", "evt=plain ms=12")]
        public void Write_MasksKeyPatterns(string message, string expected)
        {
            var inner = new RecordingLog();
            var log = new RedactingLog(inner, new string[0]);

            log.Write(LogLevel.Info, "net", message);

            Assert.That(inner.Entries[0].Message, Is.EqualTo(expected));
        }

        [Test]
        public void Write_MasksConfiguredSecretValues()
        {
            var inner = new RecordingLog();
            var log = new RedactingLog(inner, new[] { "witTokenValue123" });

            log.Write(LogLevel.Warning, "wit", "evt=tts.fail token=witTokenValue123");

            Assert.That(inner.Entries[0].Message, Is.EqualTo("evt=tts.fail token=***"));
        }

        [Test]
        public void Write_WithException_RedactsItsTextAndPassesNoExceptionOn()
        {
            var inner = new RecordingLog();
            var log = new RedactingLog(inner, new[] { "witTokenValue123" });

            log.Write(LogLevel.Error, "llm", "evt=llm.failed",
                new ProviderException("llm", ProviderErrorKind.Unauthorized, "bad key LLM_42_secretpart token witTokenValue123"));

            Assert.That(inner.Entries[0].Exception, Is.Null);
            Assert.That(inner.Entries[0].Message, Does.StartWith("evt=llm.failed\n").And.Contain("bad key *** token ***"));
            Assert.That(inner.Entries[0].Message, Does.Not.Contain("secretpart").And.Not.Contain("witTokenValue123"));
        }

        [Test]
        public void Write_IgnoresShortOrEmptySecretValues()
        {
            var inner = new RecordingLog();
            var log = new RedactingLog(inner, new[] { "", null, "abc" });

            log.Write(LogLevel.Info, "x", "evt=abc");

            Assert.That(inner.Entries[0].Message, Is.EqualTo("evt=abc"));
        }

        [Test]
        public void Write_BelowInnerLevel_IsDropped()
        {
            var inner = new RecordingLog { MinimumLevel = LogLevel.Warning };
            var log = new RedactingLog(inner, new string[0]);

            log.Write(LogLevel.Info, "x", "evt=dropped");

            Assert.That(inner.Entries, Is.Empty);
            Assert.That(log.IsEnabled(LogLevel.Info), Is.False);
        }
    }
}
