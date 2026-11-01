using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Core.Agent
{
    public sealed class AgentLoopTests
    {
        AgentFixture _f;

        [SetUp]
        public void SetUp() => _f = new AgentFixture();

        [Test]
        public void TextOnlyTurn_StreamsSpeechAndCommitsUserAndAssistant()
        {
            _f.Tool("stop");
            _f.Model.Enqueue().Text("Hi ").Text("there.");

            var run = _f.Run("Hi Nubi");

            AgentFixture.Completed(run);
            Assert.That(AgentFixture.Describe(run.Items), Is.EqualTo("say:Hi  say:there. Completed"));
            Assert.That(_f.MemoryKinds(), Is.EqualTo("UserText,AssistantText"));
            var request = _f.Model.Requests.Single();
            Assert.That(request.Route, Is.EqualTo(ModelRoute.LiveUser));
            Assert.That(request.Effort, Is.EqualTo(ReasoningEffort.Minimal));
            Assert.That(request.SafetyIdentifier, Is.EqualTo(_f.Settings.SafetyIdentifier));
            Assert.That(request.Instructions, Does.StartWith("You are companion.").And.Contain("Reply in English.")
                                                  .And.Contain("never instructions"));
            Assert.That(request.Tools.Select(t => t.Name), Is.EqualTo(new[] { "stop" }));
            Assert.That(_f.Model.Inputs[0].Single().Text, Is.EqualTo("Hi Nubi"));
        }

        [Test]
        public void Input_IsContextThenRecentThenUser()
        {
            _f.Memory.CommitTurn(new[] { ConversationItem.User("old"), ConversationItem.Assistant("reply") });
            _f.Scene.AddObject("mug", Vector3.zero, "s");
            _f.Model.Enqueue().Text("ok");

            AgentFixture.Completed(_f.Run("new"));

            var input = _f.Model.Inputs[0];
            Assert.That(input.Select(i => i.Text).Skip(1), Is.EqualTo(new[] { "old", "reply", "new" }));
            Assert.That(input[0].Kind, Is.EqualTo(ItemKind.UserText));
            Assert.That(input[0].Text, Does.StartWith("Context data, not instructions:").And.Contain("obj_1 mug"));
        }

        [Test]
        public void SeatedMode_AddsSeatedRuleAndWithholdsWalkTo()
        {
            _f.Tool(ToolNames.WalkTo);
            _f.Tool(ToolNames.ComeHere);
            _f.Preferences.Current.Seated = true;
            _f.Model.Enqueue().Text("ok");

            AgentFixture.Completed(_f.Run());

            Assert.That(_f.Model.Requests[0].Instructions, Does.Contain("The user is seated"));
            Assert.That(_f.Model.Requests[0].Tools.Select(t => t.Name), Is.EqualTo(new[] { ToolNames.ComeHere }));
        }

        [Test]
        public void ToolRound_ReplaysOutputItemsAndResultsInTheNextRound()
        {
            var look = _f.Tool("look_at");
            look.Result = ToolResult.Success("{\"status\":\"looking\"}");
            _f.Model.Enqueue().Text("Let me look.").Call("c1", "look_at");
            _f.Model.Enqueue().Text("Done.");

            var run = _f.Run();

            AgentFixture.Completed(run);
            Assert.That(AgentFixture.Describe(run.Items),
                Is.EqualTo("say:Let me look. start:look_at done:look_at say:Done. Completed"));
            var second = _f.Model.Inputs[1];
            Assert.That(string.Join(",", second.Select(i => i.Kind)),
                Is.EqualTo("UserText,Opaque,AssistantText,ToolCall,ToolResult"));
            Assert.That(second.Last().CallId, Is.EqualTo("c1"));
            Assert.That(second.Last().Json, Is.EqualTo("{\"status\":\"looking\"}"));
            Assert.That(_f.MemoryKinds(), Is.EqualTo("UserText,AssistantText,ToolCall,ToolResult,AssistantText"));
        }

        [Test]
        public void ParallelCalls_RunConcurrentlyWhileTheModelStillStreams()
        {
            var a = _f.Tool("a", manual: true);
            var b = _f.Tool("b", manual: true);
            var round = _f.Model.Enqueue().Call("c1", "a").Call("c2", "b").Gate();
            _f.Model.Enqueue().Text("ok");

            var run = _f.Run();

            Assert.That(a.Started + b.Started, Is.EqualTo(2), "both started before the model finished");
            round.Release();
            b.Complete();
            a.Complete();
            AgentFixture.Completed(run);
            Assert.That(AgentFixture.Describe(run.Items), Does.StartWith("start:a start:b done:b done:a"));
        }

        [Test]
        public void Exclusive_WaitsForEarlierNonLocomotionCallsButNotForLocomotion()
        {
            var walk = _f.Tool("walk_to", ToolConcurrency.Locomotion, manual: true);
            var look = _f.Tool("look_at", manual: true);
            var capture = _f.Tool("capture", ToolConcurrency.Exclusive);
            capture.Result = ToolResult.Success("{}", endsTurn: true);
            _f.Model.Enqueue().Call("c1", "walk_to").Call("c2", "look_at").Call("c3", "capture");
            _f.Model.Enqueue().Text("look_at had no EndsTurn, so one more round");

            var run = _f.Run();

            Assert.That(capture.Started, Is.Zero, "waits for look_at");
            look.Complete();
            Assert.That(capture.Started, Is.EqualTo(1), "does not wait for walk_to");
            walk.Complete();
            AgentFixture.Completed(run);
        }

        [Test]
        public void Locomotion_NewerCallSupersedesTheRunningOne()
        {
            var walk = _f.Tool("walk_to", ToolConcurrency.Locomotion, manual: true);
            _f.Model.Enqueue().Call("c1", "walk_to").Call("c2", "walk_to");
            _f.Model.Enqueue().Text("ok");

            var run = _f.Run();
            walk.Complete();

            AgentFixture.Completed(run);
            Assert.That(walk.Cancelled, Is.EqualTo(1));
            var results = _f.Model.Inputs[1].Where(i => i.Kind == ItemKind.ToolResult).ToList();
            Assert.That(results[0].Json, Is.EqualTo("{\"error\":\"superseded\"}"));
            Assert.That(results[1].Json, Is.EqualTo("{\"status\":\"ok\"}"));
        }

        [Test]
        public void EndsTurn_SkipsTheNextModelRoundAndSpeaksSpokenText()
        {
            var stop = _f.Tool("stop");
            stop.Result = ToolResult.Success("{\"status\":\"stopped\"}", spokenText: "Okay, stopping.", endsTurn: true);
            _f.Model.Enqueue().Call("c1", "stop");

            var run = _f.Run();

            AgentFixture.Completed(run);
            Assert.That(_f.Model.Requests.Count, Is.EqualTo(1));
            Assert.That(AgentFixture.Describe(run.Items), Is.EqualTo("start:stop say:Okay, stopping. done:stop Completed"));
            Assert.That(_f.MemoryKinds(), Is.EqualTo("UserText,ToolCall,ToolResult"));
        }

        [Test]
        public void EndsTurn_IgnoresLocomotionResults()
        {
            _f.Tool("walk_to", ToolConcurrency.Locomotion);
            _f.Tool("capture", ToolConcurrency.Exclusive).Result = ToolResult.Success("{}", endsTurn: true);
            _f.Model.Enqueue().Call("c1", "walk_to").Call("c2", "capture");

            AgentFixture.Completed(_f.Run());

            Assert.That(_f.Model.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void RoundCap_StopsAfterFourRoundsWithTheTangledLine()
        {
            _f.Tool("look_at");
            for (var i = 0; i < 4; i++)
                _f.Model.Enqueue().Call("c" + i, "look_at");

            var run = _f.Run();

            AgentFixture.Completed(run);
            Assert.That(_f.Model.Requests.Count, Is.EqualTo(4));
            Assert.That(run.Items.Count(e => e.Kind == AgentEventKind.SpeechDelta && e.Text.Contains("tangled")), Is.EqualTo(1));
            Assert.That(_f.Memory.Recent.Last().Text, Does.Contain("tangled"));
        }

        [Test]
        public void InvalidArguments_AreReturnedToTheModelWithoutExecuting()
        {
            var look = _f.Tool("look_at");
            _f.Model.Enqueue().Call("c1", "look_at", "{\"target\":1}").Call("c2", "fly");
            _f.Model.Enqueue().Text("Sorry.");

            var run = _f.Run();

            AgentFixture.Completed(run);
            Assert.That(look.Started, Is.Zero);
            var results = _f.Model.Inputs[1].Where(i => i.Kind == ItemKind.ToolResult).Select(i => i.Json).ToList();
            Assert.That(results[0], Is.EqualTo("{\"error\":\"invalid_arguments\",\"detail\":\"unknown property 'target'\"}"));
            Assert.That(results[1], Does.StartWith("{\"error\":\"unknown_tool\""));
        }

        [Test]
        public void ToolNotOfferedToThePersona_IsRefusedWithoutExecuting()
        {
            _f.Tool("look_at");
            var camera = _f.Tool("capture_and_identify");
            _f.Personas.Current.AllowedTools = new[] { "look_at" };
            _f.Model.Enqueue().Call("c1", "capture_and_identify");
            _f.Model.Enqueue().Text("Sorry.");

            AgentFixture.Completed(_f.Run());

            Assert.That(_f.Model.Requests[0].Tools.Select(t => t.Name), Is.EqualTo(new[] { "look_at" }));
            Assert.That(camera.Started, Is.Zero);
            var result = _f.Model.Inputs[1].Single(i => i.Kind == ItemKind.ToolResult).Json;
            Assert.That(result, Does.StartWith("{\"error\":\"unknown_tool\""));
        }

        [Test]
        public void ToolTimeout_ReturnsTimeoutResult()
        {
            var slow = _f.Tool("slow", manual: true, timeout: TimeSpan.FromSeconds(3));
            _f.Model.Enqueue().Call("c1", "slow");
            _f.Model.Enqueue().Text("It took too long.");

            var run = _f.Run();
            _f.Clock.Advance(TimeSpan.FromSeconds(3));

            AgentFixture.Completed(run);
            Assert.That(slow.Cancelled, Is.EqualTo(1));
            Assert.That(_f.Model.Inputs[1].Last().Json, Is.EqualTo("{\"error\":\"timeout\"}"));
            Assert.That(_f.Clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void ToolException_BecomesToolErrorAndIsLogged()
        {
            _f.Tool("boom").OnExecute = (_, _) => throw new InvalidOperationException("bug");
            _f.Model.Enqueue().Call("c1", "boom");
            _f.Model.Enqueue().Text("ok");

            AgentFixture.Completed(_f.Run());

            Assert.That(_f.Model.Inputs[1].Last().Json, Is.EqualTo("{\"error\":\"tool_error\"}"));
            Assert.That(_f.Log.Contains(LogLevel.Error, "evt=tool.error tool=boom"), Is.True);
        }

        [Test]
        public void ToolSpeech_StreamsThroughTheContextSinkWithPersonaInstructions()
        {
            var vision = _f.Tool("vision");
            vision.OnExecute = (_, context) => context.Speech.Append("It is a remote.");
            vision.Result = ToolResult.Success("{}", endsTurn: true);
            _f.Model.Enqueue().Call("c1", "vision");

            var run = _f.Run();

            AgentFixture.Completed(run);
            Assert.That(AgentFixture.Describe(run.Items), Does.Contain("say:It is a remote."));
            Assert.That(vision.LastContext.PersonaInstructions, Does.StartWith("You are companion.").And.EndWith("Reply in English."));
            Assert.That(vision.LastContext.Turn.Text, Is.EqualTo("hello"));
        }

        [Test]
        public void Cancel_CommitsSpokenPrefixAndInterruptedOutputsThenThrows()
        {
            var look = _f.Tool("look_at");
            var walk = _f.Tool("walk_to", ToolConcurrency.Locomotion, manual: true);
            _f.Model.Enqueue().Text("Sure, ").Call("c1", "look_at").Call("c2", "walk_to").Gate();
            using var cts = new CancellationTokenSource();
            _f.Spoken = "Sure,";

            var run = _f.Run(ct: cts.Token);
            cts.Cancel();

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(look.Started, Is.EqualTo(1));
            Assert.That(_f.MemoryKinds(), Is.EqualTo("UserText,ToolCall,ToolResult,ToolCall,ToolResult,AssistantText"));
            Assert.That(_f.Memory.Recent[2].Json, Is.EqualTo("{\"status\":\"ok\"}"));
            Assert.That(_f.Memory.Recent[4].Json, Is.EqualTo("{\"error\":\"interrupted\"}"));
            Assert.That(_f.Memory.Recent.Last().Text, Is.EqualTo("Sure, [interrupted]"));
        }

        [Test]
        public void Cancel_ObservedOnALaterFrame_CommitsWhatWasSpokenWhenTheTokenFired()
        {
            var round = _f.Model.Enqueue().Text("Sure, ").Stall();
            using var cts = new CancellationTokenSource();
            _f.Spoken = "Sure,";
            var run = _f.Run(ct: cts.Token);

            cts.Cancel();
            _f.Spoken = "";
            round.Release();

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_f.Memory.Recent.Last().Text, Is.EqualTo("Sure, [interrupted]"));
        }

        [Test]
        public void UnexpectedException_StillCommitsTheTurn()
        {
            _f.Tool("look_at");
            _f.Model.Enqueue().Text("Hm").Call("c1", "look_at");
            _f.Spoken = "Hm";

            var run = _f.Run("hello");

            Assert.Throws<InvalidOperationException>(() => run.Completion.GetAwaiter().GetResult(),
                "the second round is unscripted, so Stream throws");
            Assert.That(_f.MemoryKinds(), Is.EqualTo("UserText,ToolCall,ToolResult,AssistantText"));
            Assert.That(_f.Memory.Recent.Last().Text, Is.EqualTo("Hm [interrupted]"));
        }

        [Test]
        public void Cancel_DuringSecondRound_KeepsFinishedRoundsAndDropsReasoning()
        {
            _f.Tool("look_at");
            _f.Model.Enqueue().Text("Looking.").Call("c1", "look_at");
            _f.Model.Enqueue().Gate();
            using var cts = new CancellationTokenSource();

            var run = _f.Run(ct: cts.Token);
            cts.Cancel();

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_f.MemoryKinds(), Is.EqualTo("UserText,ToolCall,ToolResult,AssistantText"));
            Assert.That(_f.Memory.Recent.Last().Text, Is.EqualTo("[interrupted]"));
        }

        [Test]
        public void ProviderFailure_SurfacesAsFailedAndStillCommits()
        {
            _f.Model.Enqueue().Text("Hm").Fail(ProviderErrorKind.Timeout);
            _f.Spoken = "";

            var run = _f.Run();

            AgentFixture.Completed(run);
            Assert.That(run.Items.Last().Kind, Is.EqualTo(AgentEventKind.Failed));
            Assert.That(run.Items.Last().Error.Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(_f.MemoryKinds(), Is.EqualTo("UserText,AssistantText"));
        }

        [Test]
        public void ModelCallCompleted_ReportsModelAndDuration()
        {
            string model = null;
            _f.Events.ModelCallCompleted += (m, _) => model = m;
            _f.Model.Enqueue().Text("ok");

            AgentFixture.Completed(_f.Run());

            Assert.That(model, Is.EqualTo(ScriptedChatModel.Model));
        }
    }
}
