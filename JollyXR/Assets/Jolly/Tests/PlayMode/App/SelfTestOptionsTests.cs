using Jolly.App.SelfTest;
using NUnit.Framework;

namespace Jolly.Tests.PlayMode.App
{
    public sealed class SelfTestOptionsTests
    {
        [Test]
        public void Parse_Turns_SelectsTheLatencyMode()
        {
            var options = SelfTestOptions.Parse("{\"turns\":20}\n");

            Assert.That(options.LatencyTurns, Is.EqualTo(20));
        }

        [TestCase("{}")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("not json")]
        [TestCase("{\"turns\":\"twenty\"}")]
        [TestCase("{\"turns\":-3}")]
        [TestCase("[20]")]
        public void Parse_NoUsableTurns_IsTheDefaultRun(string json)
        {
            var options = SelfTestOptions.Parse(json);

            Assert.That(options.LatencyTurns, Is.Zero);
        }

        [Test]
        public void Parse_TooManyTurns_IsCapped()
        {
            var options = SelfTestOptions.Parse("{\"turns\":1000}");

            Assert.That(options.LatencyTurns, Is.EqualTo(SelfTestOptions.MaxLatencyTurns));
        }
    }
}
