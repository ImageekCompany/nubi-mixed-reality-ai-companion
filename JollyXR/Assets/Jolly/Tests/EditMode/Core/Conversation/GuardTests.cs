using System.Collections.Generic;
using System.Threading;
using Jolly.Core.Conversation;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class GuardTests
    {
        [TestCase("Hi newbie, come here.", "Hi Nubi, come here.")]
        [TestCase("Nobi, come here.", "Nubi, come here.")]
        [TestCase("NOOBIE stop", "Nubi stop")]
        [TestCase("newbies are welcome", "newbies are welcome")]
        [TestCase("hey nubie! and newby?", "hey Nubi! and Nubi?")]
        [TestCase("", "")]
        public void TranscriptAliases_Default_RewritesWholeWordsCaseInsensitively(string heard, string expected)
        {
            Assert.That(TranscriptAliases.Default.Apply(heard), Is.EqualTo(expected));
        }

        [Test]
        public void TranscriptAliases_NoMatch_ReturnsSameInstance()
        {
            const string text = "what is this remote";

            Assert.That(TranscriptAliases.Default.Apply(text), Is.SameAs(text));
        }

        [Test]
        public void TranscriptAliases_Null_ReturnsEmpty()
        {
            Assert.That(new TranscriptAliases(new Dictionary<string, string>()).Apply(null), Is.Empty);
        }

        [TestCase("Stop.", true)]
        [TestCase("stop!", true)]
        [TestCase("Nubi, stop!", true)]
        [TestCase("Wait please", true)]
        [TestCase("Stay there.", true)]
        [TestCase("hold on", true)]
        [TestCase("Stop following me and tell me a joke", false)]
        [TestCase("Where is the bus stop?", false)]
        [TestCase("please", false)]
        [TestCase("", false)]
        public void StopGuard_MatchesOnlyShortStopUtterances(string transcript, bool expected)
        {
            var guard = new StopGuard(new FakeLocalizer());

            Assert.That(guard.Matches(transcript), Is.EqualTo(expected));
        }

        [Test]
        public void StopGuard_UsesLocalizedTableForCurrentLanguage()
        {
            var localizer = new FakeLocalizer()
                .With("pl", GuardTables.Table, GuardTables.StopTriggers, "stój|czekaj")
                .With("pl", GuardTables.Table, GuardTables.StopFillers, "nubi|proszę");
            var guard = new StopGuard(localizer);

            localizer.SetLanguage("pl", CancellationToken.None);

            Assert.That(guard.Matches("Nubi, stój!"), Is.True);
            Assert.That(guard.Matches("stop"), Is.False);
        }

        [TestCase("I want to kill myself", true)]
        [TestCase("Sometimes I don't want to live anymore.", true)]
        [TestCase("I feel SUICIDAL today", true)]
        [TestCase("This show is killing me, so funny", false)]
        [TestCase("What is this remote?", false)]
        public void CrisisGuard_MatchesDefaultEnglishKeywords(string transcript, bool expected)
        {
            Assert.That(new CrisisGuard(new FakeLocalizer()).Matches(transcript), Is.EqualTo(expected));
        }

        [Test]
        public void CrisisGuard_TableOverridesDefaults()
        {
            var localizer = new FakeLocalizer().With(GuardTables.Table, GuardTables.CrisisKeywords, "custom phrase");

            var guard = new CrisisGuard(localizer);

            Assert.That(guard.Matches("this has a Custom Phrase in it"), Is.True);
            Assert.That(guard.Matches("I want to kill myself"), Is.False);
        }

        [Test]
        public void CrisisGuard_Polish_UsesBuiltInFallback()
        {
            var localizer = new FakeLocalizer("pl");

            Assert.That(new CrisisGuard(localizer).Matches("Nie chcę żyć."), Is.True);
        }
    }
}
