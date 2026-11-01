using System.Collections.Generic;
using System.Threading;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Tools;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class GuardTests
    {
        static readonly TranscriptAliases Nubi =
            TranscriptAliases.ForName("Nubi", new[] { "newbie", "noobie", "nubie", "newby", "nobi" });

        [TestCase("Hi newbie, come here.", "Hi Nubi, come here.")]
        [TestCase("Nobi, come here.", "Nubi, come here.")]
        [TestCase("NOOBIE stop", "Nubi stop")]
        [TestCase("newbies are welcome", "newbies are welcome")]
        [TestCase("hey nubie! and newby?", "hey Nubi! and Nubi?")]
        [TestCase("", "")]
        public void TranscriptAliases_ForName_RewritesWholeWordsCaseInsensitively(string heard, string expected)
        {
            Assert.That(Nubi.Apply(heard), Is.EqualTo(expected));
        }

        [Test]
        public void TranscriptAliases_NoMatch_ReturnsSameInstance()
        {
            const string text = "what is this remote";

            Assert.That(Nubi.Apply(text), Is.SameAs(text));
        }

        [Test]
        public void TranscriptAliases_ForName_RewritesOnlyThatCharactersAliases()
        {
            // Arrange
            var jolly = TranscriptAliases.ForName("Jolly", new[] { "Jolie", " jollie ", "", null, "JOLLY" });

            // Act
            var text = jolly.Apply("Jolie, come here. jollie? jolly! newbie");

            // Assert
            Assert.That(text, Is.EqualTo("Jolly, come here. Jolly? jolly! newbie"));
        }

        [Test]
        public void TranscriptAliases_ForName_NoAliases_KeepsTheTranscript()
        {
            const string text = "Hi newbie";

            Assert.That(TranscriptAliases.ForName("Kiwi", null).Apply(text), Is.SameAs(text));
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

        [TestCase("come here", true)]
        [TestCase("Nubi, come here.", true)]
        [TestCase("Hi Nubi, come here please", true)]
        [TestCase("Hey, come to me!", true)]
        [TestCase("Nubi come closer", true)]
        [TestCase("Hello Nubi, come over here.", true)]
        [TestCase("hi Nubi", false)]
        [TestCase("don't come here", false)]
        [TestCase("come here and tell me about this remote", false)]
        [TestCase("what does come here mean", false)]
        [TestCase("please please please please Nubi come here", false)]
        [TestCase("", false)]
        public void ComeGuard_MatchesOnlyShortComeRequests(string transcript, bool expected)
        {
            var guard = new ComeGuard(new FakeLocalizer());

            Assert.That(guard.Matches(transcript), Is.EqualTo(expected));
        }

        [Test]
        public void ComeGuard_UsesLocalizedTableForCurrentLanguage()
        {
            var localizer = new FakeLocalizer()
                .With("pl", GuardTables.Table, GuardTables.ComeTriggers, "chodź tu|podejdź")
                .With("pl", GuardTables.Table, GuardTables.ComeFillers, "nubi|cześć");
            var guard = new ComeGuard(localizer);

            localizer.SetLanguage("pl", CancellationToken.None);

            Assert.That(guard.Matches("Cześć Nubi, chodź tu!"), Is.True);
            Assert.That(guard.Matches("come here"), Is.False);
        }

        [Test]
        public void ComeGuard_Polish_UsesBuiltInFallback()
        {
            Assert.That(new ComeGuard(new FakeLocalizer("pl")).Matches("Hej Nubi, chodź do mnie."), Is.True);
        }

        [Test]
        public void ApplyComeHere_PersonaWithComeHere_StartsTheWalkThroughTheTool()
        {
            // Arrange
            var c = new GuardContext();

            // Act
            var started = Sync.Run(c.Guards.ApplyComeHere("Hi Nubi, come here", CancellationToken.None));

            // Assert
            Assert.That(started, Is.True);
            Assert.That(c.Character.Mode, Is.EqualTo(LocomotionMode.MovingTo));
            Assert.That(c.Character.MoveStopDistance, Is.EqualTo(new LocomotionSettings().ComeHereStopDistance));
            Assert.That(c.Character.LastLook?.Kind, Is.EqualTo(LookTargetKind.User));
            Assert.That(c.Tools, Is.EqualTo(new[] { "start:" + TurnGuards.ComeHereCallId, "done:" + TurnGuards.ComeHereCallId }));
            Assert.That(c.Log.Contains(LogLevel.Info, "evt=guard.come status=walking"), Is.True);
        }

        [Test]
        public void ApplyComeHere_PersonaWithoutComeHere_DoesNotMove()
        {
            // Arrange
            var c = new GuardContext();
            c.Personas.Current.AllowedTools = new[] { ToolNames.Stop };

            // Act
            var started = Sync.Run(c.Guards.ApplyComeHere("Nubi, come here", CancellationToken.None));

            // Assert
            Assert.That(started, Is.False);
            Assert.That(c.Character.MoveTarget, Is.Null);
            Assert.That(c.Tools, Is.Empty);
        }

        [Test]
        public void ApplyComeHere_GreetingAlone_DoesNotMove()
        {
            var c = new GuardContext();

            Assert.That(Sync.Run(c.Guards.ApplyComeHere("Hi Nubi, who are you?", CancellationToken.None)), Is.False);
            Assert.That(c.Character.MoveTarget, Is.Null);
        }

        [Test]
        public void ApplyComeHere_Unreachable_LeavesTheRequestToTheModel()
        {
            // Arrange
            var c = new GuardContext();
            c.World.Walkable = false;

            // Act
            var started = Sync.Run(c.Guards.ApplyComeHere("come here", CancellationToken.None));

            // Assert
            Assert.That(started, Is.False, "the turn goes to the model, which calls come_here itself and explains 'unreachable'");
            Assert.That(c.Character.Mode, Is.EqualTo(LocomotionMode.None));
            Assert.That(c.Log.Contains(LogLevel.Info, "evt=guard.come status=unreachable"), Is.True);
        }

        sealed class GuardContext
        {
            public readonly RecordingCharacterController Character = new();
            public readonly FakeWorldQuery World = new();
            public readonly FakePersonaProvider Personas = new();
            public readonly RecordingLog Log = new();
            public readonly List<string> Tools = new();
            public readonly TurnGuards Guards;

            public GuardContext()
            {
                var localizer = new FakeLocalizer();
                var registry = new ToolRegistry();
                registry.Register(new StopTool(Character));
                registry.Register(new ComeHereTool(Character, new FakeUserPoseProvider(), World, new LocomotionSettings()));
                var events = new ConversationEvents();
                events.ToolStarted += (id, name) => Tools.Add("start:" + id);
                events.ToolCompleted += (id, name, ok) => Tools.Add("done:" + id);
                Guards = new TurnGuards(new StopGuard(localizer), new ComeGuard(localizer), new CrisisGuard(localizer),
                    new QuestionCue(localizer), Character, registry, Personas, new FakeUserPreferences(), events, Log);
            }
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

        [TestCase("What is this?", true)]
        [TestCase("Nubi, what is this", true)]
        [TestCase("Hey Nubi, where are my keys", true)]
        [TestCase("Is it going to rain today", true)]
        [TestCase("I wonder if it's going to rain?", true)]
        [TestCase("Nubi, come here.", false)]
        [TestCase("Hi Nubi, come here.", false)]
        [TestCase("Find my keys.", false)]
        [TestCase("Tell me a joke.", false)]
        [TestCase("Can you find my keys?", false)]
        [TestCase("Nubi, could you come here?", false)]
        [TestCase("Stop, Nubi!", false)]
        [TestCase("This is what I made.", false)]
        [TestCase("Don't touch that.", false)]
        [TestCase("", false)]
        public void QuestionCue_MatchesQuestionsNotCommandsOrPoliteRequests(string transcript, bool expected)
        {
            Assert.That(new QuestionCue(new FakeLocalizer()).Matches(transcript), Is.EqualTo(expected));
        }

        [Test]
        public void QuestionCue_Polish_UsesBuiltInFallback()
        {
            var cue = new QuestionCue(new FakeLocalizer("pl"));

            Assert.That(cue.Matches("Nubi, co to jest"), Is.True);
            Assert.That(cue.Matches("Czy będzie padać"), Is.True);
            Assert.That(cue.Matches("Czy możesz tu podejść?"), Is.False);
            Assert.That(cue.Matches("Chodź tu."), Is.False);
        }

        [Test]
        public void QuestionCue_UsesLocalizedTableForCurrentLanguage()
        {
            var localizer = new FakeLocalizer().With(GuardTables.Table, GuardTables.QuestionOpeners, "pray tell");

            var cue = new QuestionCue(localizer);

            Assert.That(cue.Matches("Pray tell, Nubi"), Is.True);
            Assert.That(cue.Matches("what is this"), Is.False);
        }
    }
}
