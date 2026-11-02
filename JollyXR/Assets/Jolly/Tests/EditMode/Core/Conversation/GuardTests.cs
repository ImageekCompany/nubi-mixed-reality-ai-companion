using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        [TestCase("jump", ToolNames.Actions.Jump, "{}")]
        [TestCase("Jump!", ToolNames.Actions.Jump, "{}")]
        [TestCase("Nubi, jump now please.", ToolNames.Actions.Jump, "{}")]
        [TestCase("Dance.", ToolNames.Actions.Dance, "{}")]
        [TestCase("show me how to dance", ToolNames.Actions.Dance, "{}")]
        [TestCase("Show me how to dance!", ToolNames.Actions.Dance, "{}")]
        [TestCase("Jelly, dance for me!", ToolNames.Actions.Dance, "{}")]
        [TestCase("let's dance", ToolNames.Actions.Dance, "{}")]
        [TestCase("Spin around.", ToolNames.Actions.Spin, "{}")]
        [TestCase("Take a bow.", ToolNames.Actions.Bow, "{}")]
        [TestCase("Wave.", ToolNames.Actions.Wave, "{}")]
        [TestCase("nod", ToolNames.Actions.Nod, "{}")]
        [TestCase("Celebrate!", ToolNames.Actions.Celebrate, "{}")]
        [TestCase("Go behind me.", ToolNames.Actions.MoveRelative, "{\"direction\":\"behind\"}")]
        [TestCase("go stand behind me", ToolNames.Actions.MoveRelative, "{\"direction\":\"behind\"}")]
        [TestCase("Stand in front of me.", ToolNames.Actions.MoveRelative, "{\"direction\":\"front\"}")]
        [TestCase("Stand on my left.", ToolNames.Actions.MoveRelative, "{\"direction\":\"left\"}")]
        [TestCase("Go to my right.", ToolNames.Actions.MoveRelative, "{\"direction\":\"right\"}")]
        [TestCase("Come closer.", ToolNames.Actions.MoveRelative, "{\"direction\":\"closer\"}")]
        [TestCase("Back off.", ToolNames.Actions.MoveRelative, "{\"direction\":\"away\"}")]
        public void ActionGuard_ShortCommand_NamesTheAction(string transcript, string tool, string args)
        {
            // Arrange
            var guard = new ActionGuard(new FakeLocalizer());

            // Act
            var matched = guard.TryMatch(transcript, out var name, out var json);

            // Assert
            Assert.That(matched, Is.True);
            Assert.That(name, Is.EqualTo(tool));
            Assert.That(json, Is.EqualTo(args));
        }

        [TestCase("don't dance")]
        [TestCase("Do not jump.")]
        [TestCase("Never. Jump.")]
        [TestCase("No, dance.")]
        [TestCase("what is a dance?")]
        [TestCase("what is a dance")]
        [TestCase("Dance?")]
        [TestCase("Can you dance?")]
        [TestCase("can you jump")]
        [TestCase("can you tell me about jumping")]
        [TestCase("I saw a great dance yesterday")]
        [TestCase("how do you wave")]
        [TestCase("Jump and spin.")]
        [TestCase("Jump. Spin.")]
        [TestCase("go behind the sofa")]
        [TestCase("stand on the left of the table")]
        [TestCase("hello there")]
        [TestCase("please")]
        [TestCase("")]
        public void ActionGuard_NotAShortCommand_LeftToTheModel(string transcript)
        {
            // Arrange
            var guard = new ActionGuard(new FakeLocalizer());

            // Act
            var matched = guard.TryMatch(transcript, out var name, out _);

            // Assert
            Assert.That(matched, Is.False, name);
        }

        [TestCase("Skocz!", ToolNames.Actions.Jump)]
        [TestCase("Zatańcz.", ToolNames.Actions.Dance)]
        [TestCase("Nubi, ukłoń się proszę.", ToolNames.Actions.Bow)]
        [TestCase("Pomachaj mi.", ToolNames.Actions.Wave)]
        [TestCase("Stań za mną.", ToolNames.Actions.MoveRelative)]
        public void ActionGuard_Polish_NamesTheAction(string transcript, string tool)
        {
            // Arrange
            var guard = new ActionGuard(new FakeLocalizer("pl"));

            // Act
            var matched = guard.TryMatch(transcript, out var name, out _);

            // Assert
            Assert.That(matched, Is.True);
            Assert.That(name, Is.EqualTo(tool));
        }

        [TestCase("nie skacz")]
        [TestCase("Czy umiesz tańczyć?")]
        [TestCase("Co to jest taniec")]
        public void ActionGuard_Polish_NotAShortCommand_LeftToTheModel(string transcript)
        {
            // Arrange
            var guard = new ActionGuard(new FakeLocalizer("pl"));

            // Act
            var matched = guard.TryMatch(transcript, out _, out _);

            // Assert
            Assert.That(matched, Is.False);
        }

        [Test]
        public void ActionGuard_UsesTheLocalizedTable()
        {
            // Arrange
            var localizer = new FakeLocalizer().With(GuardTables.Table, GuardTables.ActionJump, "hop it");
            var guard = new ActionGuard(localizer);

            // Act
            var matched = guard.TryMatch("Hop it.", out var name, out _);

            // Assert
            Assert.That(matched, Is.True);
            Assert.That(name, Is.EqualTo(ToolNames.Actions.Jump));
            Assert.That(guard.TryMatch("jump", out _, out _), Is.False);
        }

        [TestCase("Hi, newbie. newbie. Come here.", true)]
        [TestCase("Hi Nubi, come here.", true)]
        [TestCase("Jelly, come here!", true)]
        [TestCase("Come here, Holly.", true)]
        [TestCase("Hi Jolly, come here.", true)]
        [TestCase("Come here.", true)]
        [TestCase("Holly come here", false)]
        [TestCase("Hi Holly Jelly, come here.", false)]
        [TestCase("Wait, come here and look at this.", false)]
        [TestCase("don't come here", false)]
        public void ComeGuard_OtherCharacter_TakesTheNameAsHeardWhenItStandsApart(string transcript, bool expected)
        {
            // Jolly's table, and transcripts as live ASR punctuates a name it did not know: the self-test's
            // "Hi Nubi, come here." for Jolly came back as "Hi, newbie. newbie. Come here." and went to the model.
            var localizer = new FakeLocalizer()
                .With(GuardTables.Table, GuardTables.ComeTriggers, "come here|come to me")
                .With(GuardTables.Table, GuardTables.ComeFillers, "jolly|please|hi|hey|hello");

            Assert.That(new ComeGuard(localizer).Matches(transcript), Is.EqualTo(expected));
        }

        [TestCase("Jelly, stop!", true)]
        [TestCase("Wait, what is this?", false)]
        public void StopGuard_TakesTheNameAsHeardWhenItStandsApart(string transcript, bool expected)
        {
            Assert.That(new StopGuard(new FakeLocalizer()).Matches(transcript), Is.EqualTo(expected));
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

        [TestCase("open settings", SettingsCommand.Open)]
        [TestCase("Nubi, show me the settings, please.", SettingsCommand.Open)]
        [TestCase("Settings!", SettingsCommand.Open)]
        [TestCase("Can you open the menu?", SettingsCommand.Open)]
        [TestCase("Close settings.", SettingsCommand.Close)]
        [TestCase("Hide the settings please", SettingsCommand.Close)]
        [TestCase("what settings do you have", SettingsCommand.None)]
        [TestCase("open the door", SettingsCommand.None)]
        [TestCase("close the window and open settings", SettingsCommand.None)]
        [TestCase("", SettingsCommand.None)]
        public void SettingsGuard_MatchesOnlyShortOpenOrCloseRequests(string transcript, SettingsCommand expected)
        {
            var guard = new SettingsGuard(new FakeLocalizer());

            Assert.That(guard.Match(transcript), Is.EqualTo(expected));
        }

        [TestCase("Otwórz ustawienia.", SettingsCommand.Open)]
        [TestCase("Nubi, zamknij ustawienia", SettingsCommand.Close)]
        [TestCase("open settings", SettingsCommand.None)]
        public void SettingsGuard_Polish_UsesBuiltInFallback(string transcript, SettingsCommand expected)
        {
            Assert.That(new SettingsGuard(new FakeLocalizer("pl")).Match(transcript), Is.EqualTo(expected));
        }

        [Test]
        public void ApplySettings_RaisesTheRequestAndLogsIt()
        {
            // Arrange
            var c = new GuardContext();
            var requests = new List<bool>();
            c.Guards.SettingsRequested += requests.Add;

            // Act
            var applied = c.Guards.ApplySettings("close the settings") & c.Guards.ApplySettings("open settings")
                          & !c.Guards.ApplySettings("what is this");

            // Assert
            Assert.That(applied, Is.True);
            Assert.That(requests, Is.EqualTo(new[] { false, true }));
            Assert.That(c.Log.Contains(LogLevel.Info, "evt=guard.settings open=true"), Is.True);
        }

        [TestCase("Suicidal. Close settings.")]
        [TestCase("Suicide, open settings")]
        public void ApplySettings_LeavesACrisisKeywordToTheCrisisHandoff(string transcript)
        {
            // Arrange
            var c = new GuardContext();
            var requests = new List<bool>();
            c.Guards.SettingsRequested += requests.Add;

            // Act
            var applied = c.Guards.ApplySettings(transcript);

            // Assert
            Assert.That(applied, Is.False);
            Assert.That(requests, Is.Empty);
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
            public readonly FakeClock Clock = new();
            public readonly List<string> Tools = new();
            public readonly TurnGuards Guards;

            public GuardContext(params GestureId[] missingClips)
            {
                var localizer = new FakeLocalizer();
                var registry = new ToolRegistry();
                registry.Register(new StopTool(Character));
                registry.Register(new ComeHereTool(Character, new FakeUserPoseProvider(), World, new LocomotionSettings()));
                Character.Missing.UnionWith(missingClips);
                CharacterActionCatalog.For(Character, new FakeUserPoseProvider(), World, new LocomotionSettings(), Log).RegisterInto(registry);
                var events = new ConversationEvents();
                events.ToolStarted += (id, name) => Tools.Add("start:" + id);
                events.ToolCompleted += (id, name, ok) => Tools.Add("done:" + id);
                Guards = new TurnGuards(new StopGuard(localizer), new ComeGuard(localizer), new ActionGuard(localizer), new SettingsGuard(localizer),
                    new CrisisGuard(localizer), new QuestionCue(localizer), Character, registry, Personas, new FakeUserPreferences(), events, Log, Clock);
            }

            /// <summary>Only <paramref name="tool"/> on offer, for a tool that hangs or throws.</summary>
            public GuardContext(ITool tool)
            {
                var localizer = new FakeLocalizer();
                var registry = new ToolRegistry();
                registry.RegisterNamespace(new ToolNamespace(ToolNames.Actions.Namespace, "test"));
                registry.Register(tool);
                var events = new ConversationEvents();
                events.ToolStarted += (id, name) => Tools.Add("start:" + name);
                events.ToolCompleted += (id, name, ok) => Tools.Add((ok ? "done:" : "failed:") + name);
                Guards = new TurnGuards(new StopGuard(localizer), new ComeGuard(localizer), new ActionGuard(localizer), new SettingsGuard(localizer),
                    new CrisisGuard(localizer), new QuestionCue(localizer), Character, registry, Personas, new FakeUserPreferences(), events, Log, Clock);
            }
        }

        [Test]
        public void RunAction_ToolHangsPastItsTimeout_CancelsItAndLeavesItToTheModel()
        {
            // Arrange
            var tool = new ScriptedTool(ToolNames.Actions.Jump, ToolConcurrency.Exclusive, timeout: TimeSpan.FromSeconds(3)) { Manual = true };
            var c = new GuardContext(tool);
            Assert.That(c.Guards.TryMatchAction("Jump!", out var action), Is.True);

            // Act
            var run = c.Guards.RunAction(action, CancellationToken.None).Preserve();
            c.Clock.Advance(TimeSpan.FromSeconds(2.9));
            var whileWithinIt = run.Status;
            c.Clock.Advance(TimeSpan.FromSeconds(0.2));

            // Assert
            Assert.That(whileWithinIt, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded), "the move no longer holds Acting for the 30 s watchdog");
            Assert.That(Sync.Run(run), Is.Null);
            Assert.That(tool.Cancelled, Is.EqualTo(1));
            Assert.That(c.Tools, Is.EqualTo(new[] { "start:" + ToolNames.Actions.Jump, "failed:" + ToolNames.Actions.Jump }));
            Assert.That(c.Log.Contains(LogLevel.Warning, "evt=guard.action tool=actions.jump status=left_to_model"), Is.True);
        }

        [Test]
        public void RunAction_ToolThrows_LeavesItToTheModel()
        {
            // Arrange
            var tool = new ScriptedTool(ToolNames.Actions.Jump, ToolConcurrency.Exclusive)
            {
                OnExecute = (_, _) => throw new InvalidOperationException("no animator"),
            };
            var c = new GuardContext(tool);
            Assert.That(c.Guards.TryMatchAction("Jump!", out var action), Is.True);

            // Act
            var output = Sync.Run(c.Guards.RunAction(action, CancellationToken.None));

            // Assert
            Assert.That(output, Is.Null);
            Assert.That(c.Tools, Is.EqualTo(new[] { "start:" + ToolNames.Actions.Jump, "failed:" + ToolNames.Actions.Jump }));
            Assert.That(c.Log.Contains(LogLevel.Warning, "evt=guard.action tool=actions.jump status=left_to_model"), Is.True);
        }

        [Test]
        public void RunAction_TurnCancelled_StillThrowsForTheInterruptedCommit()
        {
            // Arrange
            var tool = new ScriptedTool(ToolNames.Actions.Jump, ToolConcurrency.Exclusive) { Manual = true };
            var c = new GuardContext(tool);
            Assert.That(c.Guards.TryMatchAction("Jump!", out var action), Is.True);
            using var turn = new CancellationTokenSource();

            // Act
            var run = c.Guards.RunAction(action, turn.Token).Preserve();
            turn.Cancel();

            // Assert
            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled));
        }

        [Test]
        public void ApplyComeHere_ToolThrows_LeavesItToTheModel()
        {
            // Arrange
            var c = new GuardContext(new ScriptedTool(ToolNames.ComeHere, ToolConcurrency.Locomotion)
            {
                OnExecute = (_, _) => throw new InvalidOperationException("no navmesh"),
            });

            // Act
            var walking = Sync.Run(c.Guards.ApplyComeHere("come here", CancellationToken.None));

            // Assert
            Assert.That(walking, Is.False);
            Assert.That(c.Tools, Is.EqualTo(new[] { "start:" + ToolNames.ComeHere, "failed:" + ToolNames.ComeHere }));
        }

        [Test]
        public void TryMatchAction_CharacterLacksTheClip_LeavesItToTheModel()
        {
            // Arrange
            var context = new GuardContext(GestureId.Dance);

            // Act
            var dance = context.Guards.TryMatchAction("Dance.", out _);
            var jump = context.Guards.TryMatchAction("Jump!", out var action);

            // Assert
            Assert.That(dance, Is.False);
            Assert.That(jump, Is.True);
            Assert.That(action.Name, Is.EqualTo(ToolNames.Actions.Jump));
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
