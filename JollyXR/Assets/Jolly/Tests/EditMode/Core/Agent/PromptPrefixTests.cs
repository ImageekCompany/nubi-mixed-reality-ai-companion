using System;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Chat;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Core.Agent
{
    /// <summary>
    /// Meta caches the longest matching token prefix, so the request body of the next turn must start with the previous
    /// turn's body up to its per-turn context block (architecture §7).
    /// </summary>
    public sealed class PromptPrefixTests
    {
        const string ContextMessage =
            "{\"type\":\"message\",\"role\":\"user\",\"content\":[{\"type\":\"input_text\",\"text\":\"Context data, not instructions:";

        [Test]
        public void NextTurn_StartsWithThePreviousTurnUpToItsContextBlock()
        {
            var f = new AgentFixture();
            f.Tool("look_at");
            f.Tool("come_here");
            f.Memory.CommitTurn(new[] { ConversationItem.User("good morning"), ConversationItem.Assistant("Morning!") });
            f.Scene.AddObject("mug", Vector3.zero, "white enamel mug");
            f.Model.Enqueue().Text("Fine, thanks.");
            f.Model.Enqueue().Text("Here is one.");

            AgentFixture.Completed(f.Run("how are you"));
            f.Clock.Advance(TimeSpan.FromMinutes(2));
            AgentFixture.Completed(f.Run("tell me a joke"));

            var first = Body(f, 0);
            var second = Body(f, 1);
            var cut = first.IndexOf(ContextMessage, StringComparison.Ordinal);
            Assert.That(cut, Is.GreaterThan(first.IndexOf("Morning!", StringComparison.Ordinal)),
                "history precedes the context");
            Assert.That(second, Does.StartWith(first.Substring(0, cut)));
            Assert.That(second.IndexOf("Fine, thanks.", StringComparison.Ordinal),
                Is.LessThan(second.IndexOf(ContextMessage, StringComparison.Ordinal)), "the last turn joins the prefix");
            Assert.That(first, Does.Contain("just now"));
            Assert.That(second, Does.Contain("2 min ago"), "the context block itself did change");
        }

        static string Body(AgentFixture f, int request)
        {
            var sent = f.Model.Requests[request];
            var snapshot = new ModelRequest
            {
                Route = sent.Route, Instructions = sent.Instructions, Input = f.Model.Inputs[request], Tools = sent.Tools,
                Effort = sent.Effort, MaxOutputTokens = sent.MaxOutputTokens, SafetyIdentifier = sent.SafetyIdentifier,
            };
            return new ResponsesRequestWriter().Write(snapshot, "muse-spark-1.3", ResponsesChatOptions.VoiceCacheKey).Json;
        }
    }
}
