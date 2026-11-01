using Jolly.Providers.MetaApi.Asr;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Asr
{
    public sealed class TranscriptTextTests
    {
        [TestCase("Hello there。", "Hello there.")]
        [TestCase("Really？ Yes！ ok，", "Really? Yes! ok,")]
        [TestCase("what this pen is..", "what this pen is.")]
        [TestCase("wait... what", "wait... what")]
        [TestCase("  padded  ", "padded")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void Normalize_FixesPunctuationSeenInLiveOutput(string input, string expected)
        {
            Assert.That(TranscriptText.Normalize(input), Is.EqualTo(expected));
        }

        [Test]
        public void Normalize_ReturnsTheSameInstanceWhenNothingChanges()
        {
            const string text = "hey Jolly";

            Assert.That(TranscriptText.Normalize(text), Is.SameAs(text));
        }
    }
}
