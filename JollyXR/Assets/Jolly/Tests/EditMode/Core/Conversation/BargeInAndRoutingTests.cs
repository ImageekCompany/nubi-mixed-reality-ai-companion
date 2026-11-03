using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Core.Tools;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class BargeInAndRoutingTests
    {
        ConversationFixture _f;

        [SetUp]
        public void SetUp() => _f = new ConversationFixture().Start();

        [TearDown]
        public void TearDown() => _f.Dispose();

        [Test]
        public void PressWhileSpeaking_CancelsBeforeFlushing_AndStartsListening()
        {
            _f.Agent.Say("First sentence. ").Hold().Say("Never said.");
            _f.Talk();
            _f.Queue.PlayNext();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));

            _f.Voice.Press();

            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            Assert.That(_f.Agent.InterruptedSpoken, Is.EqualTo("First sentence. "),
                "the turn's spoken record survives the queue clear");
            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.Queue.Clears, Is.EqualTo(1));
            Assert.That(_f.Mic.Sessions.Count, Is.EqualTo(2));
            Assert.That(_f.Log.Contains(LogLevel.Info, "BargeIn="), Is.True);
        }

        [Test]
        public void SpokenRecord_CoversOnlyTheCurrentTurn()
        {
            _f.Agent.Say("First answer.");
            _f.Talk("one");
            _f.Queue.Drain();
            _f.Agent.Say("Second answer. ").Hold();

            _f.Talk("two");
            Assert.That(_f.Agent.SpokenSoFar(), Is.Empty);
            _f.Queue.PlayNext();

            Assert.That(_f.Agent.SpokenSoFar(), Is.EqualTo("Second answer. "));
        }

        [Test]
        public void FirstSampleOfFlushedSpeech_ArrivingAfterTheBargeIn_IsIgnored()
        {
            _f.Agent.Say("One. Two.").Hold();
            _f.Talk();
            _f.Queue.PlayNext();
            _f.Voice.Press();

            _f.Output.RaiseFirstSample(_f.Queue.Enqueued[1].utteranceId);

            Assert.That(_f.Captions, Is.EqualTo(new[] { "One." }));
            Assert.That(_f.Machine.SpeechPending, Is.False);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
        }

        [Test]
        public void PressWhileActing_KeepsLocomotionRunning()
        {
            _f.Agent.ToolStarted("walk_to").Hold();
            _f.Talk("walk to the sofa");
            Assert.That(_f.State, Is.EqualTo(ConversationState.Acting));

            _f.Voice.Press();

            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            Assert.That(_f.Character.StopCount, Is.Zero);
        }

        [Test]
        public void TurnAfterBargeIn_RunsNormally()
        {
            _f.Agent.Say("Long answer. ").Hold();
            _f.Talk("first");
            _f.Voice.Press();
            _f.Agent.Say("Short.");
            _f.Stt.Final = "second";
            _f.Voice.Release();
            _f.Queue.Drain();

            Assert.That(_f.Agent.Turns.Select(t => t.Text), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(_f.Agent.Turns[1].TurnId, Is.EqualTo(2));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void PressWhileListening_IsIgnored()
        {
            _f.Stt.Hang = true;
            _f.Talk();

            _f.Voice.Press();

            Assert.That(_f.Mic.Sessions.Count, Is.EqualTo(1));
            Assert.That(_f.Output.Flushes, Is.Zero);
        }

        [TestCase(Connectivity.Offline, LineCategory.Offline)]
        [TestCase(Connectivity.Unauthorized, LineCategory.Error)]
        [TestCase(Connectivity.BudgetReached, LineCategory.BudgetReached)]
        public void NotOnline_PlaysACannedLineWithoutListening(Connectivity state, LineCategory expected)
        {
            _f.Connectivity.State = state;
            _f.Offline.Hold = true;

            _f.Voice.Press();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));
            _f.Offline.Release();
            _f.Voice.Release();

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { expected }));
            Assert.That(_f.StatePath(), Is.EqualTo("Speaking>Idle"));
            Assert.That(_f.Mic.Sessions, Is.Empty);
            Assert.That(_f.Agent.Turns, Is.Empty);
        }

        [Test]
        public void BudgetCap_WinsOverOnlineConnectivity()
        {
            _f.Usage.ReachCap();

            _f.Talk();

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.BudgetReached }));
        }

        [Test]
        public void MicUnavailable_PlaysTheErrorLineNotTheOfflineOne()
        {
            _f.Mic.IsAvailable = false;

            _f.Talk();

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.Error }));
            Assert.That(_f.Mic.Sessions, Is.Empty);
        }

        [Test]
        public void PressWhileAnInteractionLinePlaysAtIdle_SilencesItAndListens()
        {
            _f.Output.IsPlaying = true;

            _f.Voice.Press();

            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.Queue.Clears, Is.EqualTo(1));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
        }

        [Test]
        public void PressAtIdleWithNothingPlaying_FlushesNothing()
        {
            _f.Voice.Press();

            Assert.That(_f.Output.Flushes, Is.Zero);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
        }

        [Test]
        public void OfflinePressWhileSpeaking_BargesInWithTheCannedLine()
        {
            _f.Agent.Say("Online answer. ").Hold();
            _f.Talk();
            _f.Queue.PlayNext();
            _f.Connectivity.State = Connectivity.Offline;

            _f.Voice.Press();

            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.Offline }));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void CrisisWords_TheDefaultCompanionSpeaksTheHandoffBeforeTheModelAndAnnotatesTheTurn()
        {
            _f.Talk("Sometimes I want to die.");

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.CrisisHandoff }));
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Sometimes I want to die." + TurnGuards.CrisisNote));
        }

        [Test]
        public void ComeHere_GuardWalks_AnswersWithTheComingOverLineAndNoModelRound()
        {
            _f.Talk("Hi Nubi, come here.");

            Assert.That(_f.Character.Mode, Is.EqualTo(LocomotionMode.MovingTo));
            Assert.That(_f.Agent.Turns, Is.Empty, "the guard's walk turn never reaches the agent");
            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.ComingOver }));
            Assert.That(_f.StatePath(), Is.EqualTo("Listening>Thinking>Speaking>Idle"),
                "the line's first sample is the turn's FirstAudio, then the turn completes");
            Assert.That(_f.Log.Contains(LogLevel.Info, "FirstAudioPlayed="), Is.True);
            Assert.That(_f.Log.Contains(LogLevel.Info, "LlmRequestSent="), Is.False);
        }

        [Test]
        public void ComeHere_GuardWalks_NoAcknowledgementBeforeTheComingOverLine()
        {
            _f.Timings.AcknowledgeQuestions = true;

            _f.Talk("Nubi, come here.");

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.ComingOver }),
                "\"Good question. On my way!\" on the demo's hero line (8bd9560 device run 1)");
        }

        [Test]
        public void ComeHere_GuardWalks_BothSidesLandInTheHistoryTheNextTurnReplays()
        {
            _f.Talk("Hi Nubi, come here.");
            _f.Agent.Say("You're welcome.");
            _f.Talk("Thank you.");

            var recent = _f.Memory.Recent;
            Assert.That(recent.Select(i => i.Kind), Is.EqualTo(new[] { ItemKind.UserText, ItemKind.AssistantText }));
            Assert.That(recent[0].Text, Is.EqualTo("Hi Nubi, come here."));
            Assert.That(recent[1].Text, Is.EqualTo(RecordingOfflineResponder.TextOf(LineCategory.ComingOver)));
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Thank you."));
        }

        [Test]
        public void ComeHere_BargeInDuringTheLine_KeepsTheUserTextInTheHistory()
        {
            _f.Offline.Hold = true;
            _f.Talk("come here");

            _f.Voice.Press();

            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            Assert.That(_f.Memory.Recent.Select(i => i.Text), Is.EqualTo(new[] { "come here" }));
        }

        [Test]
        public void ComeHere_Unreachable_LeavesTheTurnToTheModel()
        {
            _f.World.Walkable = false;

            _f.Talk("Hi Nubi, come here.");

            Assert.That(_f.Offline.Categories, Is.Empty);
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Hi Nubi, come here."));
        }

        [Test]
        public void ComeHereAndCrisisWords_TheHandoffWinsAndTheModelStillAnswers()
        {
            _f.Localizer.With(GuardTables.Table, GuardTables.ComeFillers, "nubi|please|suicidal");

            _f.Talk("Nubi suicidal come here");

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.CrisisHandoff }));
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Nubi suicidal come here" + TurnGuards.CrisisNote));
        }

        [Test]
        public void ShowingAPlace_ThenAskingAfterIt_RunsWithNoModelRound_AndLeadsThere()
        {
            // Act: the demo's teaching and asking lines (ADR-0051).
            _f.Talk("That over there is the kitchen.");
            _f.Queue.Drain();
            var taught = _f.Scene.TryGetPlace("kitchen", out _);
            _f.Talk("Where is the kitchen?");
            _f.Queue.Drain();

            // Assert
            Assert.That(taught, Is.True);
            Assert.That(_f.Agent.Turns, Is.Empty, "no model round");
            Assert.That(_f.Queue.Enqueued.Select(e => e.sentence), Is.EqualTo(new[] { "Got it, the kitchen!", "There!" }));
            Assert.That(_f.Character.Gestures, Is.EqualTo(new[] { GestureId.Point }));
            Assert.That(_f.Machine.State, Is.EqualTo(ConversationState.Idle));
        }

        const string QuestionSchema =
            "{\"type\":\"object\",\"properties\":{\"question\":{\"type\":\"string\"}},\"required\":[\"question\"],\"additionalProperties\":false}";

        ScriptedTool LookTool()
        {
            var tool = new ScriptedTool(ToolNames.CaptureAndIdentify, ToolConcurrency.Exclusive, QuestionSchema);
            _f.Tools.Register(tool);
            return tool;
        }

        [Test]
        public void WhereDoTheseGo_WithAPlaceRemembered_LooksWithNoModelRound_AndSpeaksAfterTheAnswer()
        {
            // Arrange
            var look = LookTool();
            look.OnExecute = (args, context) => context.Speech.Append("Those are dishes. ");
            look.Result = new ToolResult(true, "{\"place\":\"kitchen\"}", "There!", endsTurn: true);
            _f.Scene.RememberPlace(new PlaceRecord { Name = "kitchen" });

            // Act
            _f.Talk("Where can I put these things?");
            _f.Queue.Drain();

            // Assert
            Assert.That(look.Started, Is.EqualTo(1));
            Assert.That(look.LastContext.Turn.Text, Is.EqualTo("Where can I put these things?"));
            Assert.That(_f.Agent.Turns, Is.Empty, "no model round");
            Assert.That(_f.Queue.Enqueued.Select(e => e.sentence), Is.EqualTo(new[] { "Those are dishes.", "There!" }));
            Assert.That(_f.Machine.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void WhereDoTheseGo_LookFailsWithNoWords_GoesToTheModelToExplain()
        {
            var look = LookTool();
            look.Result = ToolResult.Failure("too_dark");
            _f.Scene.RememberPlace(new PlaceRecord { Name = "kitchen" });

            _f.Talk("Where can I put these things?");

            Assert.That(look.Started, Is.EqualTo(1));
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Where can I put these things?"));
        }

        [Test]
        public void WhereDoTheseGo_NoPlaceRemembered_IsAnOrdinaryModelTurn()
        {
            var look = LookTool();

            _f.Talk("Where can I put these things?");

            Assert.That(look.Started, Is.Zero);
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Where can I put these things?"));
        }

        [Test]
        public void GuardedAction_RunsWithNoModelRoundAndNoWords()
        {
            // Act
            _f.Talk("Dance.");

            // Assert
            Assert.That(_f.Character.Looped.Single().gesture, Is.EqualTo(GestureId.Dance));
            Assert.That(_f.Agent.Turns, Is.Empty, "no model round");
            Assert.That(_f.Offline.Categories, Is.Empty, "the move is the answer");
            Assert.That(_f.StatePath(), Is.EqualTo("Listening>Thinking>Acting>Idle"), "no Thinking pose between the move and rest");
            Assert.That(_f.Log.Contains(LogLevel.Info, "evt=guard.action tool=actions.dance status=done"), Is.True);
            Assert.That(_f.Log.Contains(LogLevel.Info, "LlmRequestSent="), Is.False);
        }

        [Test]
        public void GuardedAction_TheNextTurnReplaysTheCall()
        {
            // Act
            _f.Talk("Go behind me.");

            // Assert
            var recent = _f.Memory.Recent;
            Assert.That(recent.Select(i => i.Kind), Is.EqualTo(new[] { ItemKind.UserText, ItemKind.ToolCall, ItemKind.ToolResult }));
            Assert.That(recent[0].Text, Is.EqualTo("Go behind me."));
            Assert.That(recent[1].ToolName, Is.EqualTo(ToolNames.Actions.MoveRelative));
            Assert.That(recent[1].Json, Is.EqualTo("{\"direction\":\"behind\"}"));
            Assert.That(recent[2].CallId, Is.EqualTo(recent[1].CallId));
            Assert.That(recent[2].Json, Does.Contain("walking"));
        }

        [Test]
        public void GuardedAction_CallIdsAreUnique()
        {
            // Act
            _f.Talk("jump");
            _f.Talk("jump");

            // Assert
            var ids = _f.Memory.Recent.Where(i => i.Kind == ItemKind.ToolCall).Select(i => i.CallId).ToList();
            Assert.That(ids, Has.Count.EqualTo(2));
            Assert.That(ids, Is.Unique);
            Assert.That(ids, Is.All.StartsWith(TurnGuards.ActionCallPrefix));
        }

        [Test]
        public void GuardedAction_NoAcknowledgementEvenWhenItStartsLikeAQuestion()
        {
            // Arrange
            _f.Timings.AcknowledgeQuestions = true;

            // Act
            _f.Talk("Do a spin.");

            // Assert
            Assert.That(_f.Offline.Categories, Is.Empty);
            Assert.That(_f.Character.Spins, Has.Count.EqualTo(1));
        }

        [Test]
        public void GuardedAction_PersonaNotAllowedIt_LeftToTheModel()
        {
            // Arrange
            _f.Personas.Current.AllowedTools = new[] { ToolNames.Actions.Wave };

            // Act
            _f.Talk("Jump!");

            // Assert
            Assert.That(_f.Character.Gestures, Is.Empty);
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Jump!"));
        }

        [Test]
        public void GuardedAction_PersonaAllowedIt_Runs()
        {
            // Arrange
            _f.Personas.Current.AllowedTools = new[] { ToolNames.Actions.Wave };

            // Act
            _f.Talk("Wave.");

            // Assert
            Assert.That(_f.Character.Gestures, Is.EqualTo(new[] { GestureId.Wave }));
            Assert.That(_f.Agent.Turns, Is.Empty);
        }

        [TestCase("Go behind me.")]
        [TestCase("Back off.")]
        public void GuardedAction_NoWalkStarted_TheModelExplains(string command)
        {
            // Arrange
            _f.World.Walkable = false;

            // Act
            _f.Talk(command);

            // Assert
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo(command));
            Assert.That(_f.Log.Contains(LogLevel.Warning, "status=left_to_model"), Is.True);
        }

        [Test]
        public void GuardedAction_CrisisWordsWin()
        {
            // Act
            _f.Talk("Suicidal. Dance.");

            // Assert
            Assert.That(_f.Character.Looped, Is.Empty);
            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.CrisisHandoff }));
        }

        [Test]
        public void GuardedAction_BargeIn_CancelsTheMoveAndCommitsTheInterruptedCall()
        {
            // Arrange
            var move = new UniTaskCompletionSource();
            CancellationToken token = default;
            _f.Character.Playing = (ct) =>
            {
                token = ct;
                return move.Task;
            };
            _f.Talk("Take a bow.");
            Assert.That(_f.State, Is.EqualTo(ConversationState.Acting));

            // Act
            _f.Voice.Press();

            // Assert
            Assert.That(token.IsCancellationRequested, Is.True);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            move.TrySetCanceled(token);
            var recent = _f.Memory.Recent;
            Assert.That(recent.Select(i => i.Kind),
                Is.EqualTo(new[] { ItemKind.UserText, ItemKind.ToolCall, ItemKind.ToolResult, ItemKind.AssistantText }));
            Assert.That(recent[2].Json, Is.EqualTo(ToolResult.InterruptedOutput));
        }

        [Test]
        public void Greeting_LeavesMovementToTheModel()
        {
            _f.Talk("Hi Nubi, who are you?");

            Assert.That(_f.Character.Mode, Is.EqualTo(LocomotionMode.None));
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Hi Nubi, who are you?"));
        }

        [Test]
        public void NoCrisisWords_NeverHandsOff()
        {
            _f.Talk("Sometimes I want a nap.");

            Assert.That(_f.Offline.Categories, Is.Empty);
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Sometimes I want a nap."));
        }
    }
}
