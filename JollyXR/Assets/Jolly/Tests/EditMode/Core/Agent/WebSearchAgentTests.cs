using System.Collections.Generic;
using System.Linq;
using Jolly.Contracts;
using Jolly.Core.Tools;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Agent
{
    /// <summary>How a live turn offers namespaces and Meta's web search (ADR-0047, ADR-0048).</summary>
    public sealed class WebSearchAgentTests
    {
        [Test]
        public void RunTurn_PersonaAllowingEverythingWhileOnline_OffersWebSearchWithTheTimeZonesCity()
        {
            // Arrange
            var f = new AgentFixture(webSearch: true);
            f.Model.Enqueue().Text("Sunny.");

            // Act
            AgentFixture.Completed(f.Run("What's the weather?"));

            // Assert
            var request = f.Model.Requests.Single();
            Assert.That(request.WebSearch, Is.Not.Null);
            Assert.That(request.WebSearch.City, Is.EqualTo("London"), "the time zone's city");
            Assert.That(request.WebSearch.Country, Is.Null, "a time-zone city comes with no locale country");
            Assert.That(request.WebSearch.Timezone, Is.EqualTo("Europe/London"));
            Assert.That(request.Instructions, Does.Contain("search the web"));
        }

        [Test]
        public void RunTurn_PersonaListingToolsWithoutWebSearch_OffersNone()
        {
            // Arrange
            var f = new AgentFixture(webSearch: true);
            f.Personas.Current.AllowedTools = new[] { ToolNames.Stop };
            f.Model.Enqueue().Text("I can't check that.");

            // Act
            AgentFixture.Completed(f.Run("What's the weather?"));

            // Assert
            var request = f.Model.Requests.Single();
            Assert.That(request.WebSearch, Is.Null);
            Assert.That(request.Instructions, Does.Not.Contain("search the web"));
        }

        [Test]
        public void RunTurn_PersonaListingWebSearch_OffersIt()
        {
            // Arrange
            var f = new AgentFixture(webSearch: true);
            f.Personas.Current.AllowedTools = new[] { ToolNames.Stop, ToolNames.WebSearch };
            f.Model.Enqueue().Text("Sunny.");

            // Act
            AgentFixture.Completed(f.Run("What's the weather?"));

            // Assert
            Assert.That(f.Model.Requests.Single().WebSearch, Is.Not.Null);
        }

        [TestCase(Connectivity.Degraded)]
        [TestCase(Connectivity.Offline)]
        public void RunTurn_NotOnline_OffersNoWebSearch(Connectivity state)
        {
            // Arrange
            var f = new AgentFixture(webSearch: true);
            f.Connectivity.State = state;
            f.Model.Enqueue().Text("Hm.");

            // Act
            AgentFixture.Completed(f.Run("What's the news?"));

            // Assert
            Assert.That(f.Model.Requests.Single().WebSearch, Is.Null);
        }

        [Test]
        public void RunTurn_WithoutAPolicy_OffersNoWebSearchAndNoLocalTime()
        {
            // Arrange
            var f = new AgentFixture();
            f.Model.Enqueue().Text("Hi.");

            // Act
            AgentFixture.Completed(f.Run("hi"));

            // Assert
            Assert.That(f.Model.Requests.Single().WebSearch, Is.Null);
            Assert.That(f.Model.Inputs[0].Single().Text, Is.EqualTo("hi"));
        }

        [Test]
        public void RunTurn_ContextBlock_CarriesTheLocalTimeAndTheHomeCity()
        {
            // Arrange: FakeClock starts at 2026-11-01 00:00 UTC, a Sunday.
            var f = new AgentFixture(webSearch: true);
            f.Region.Offset = System.TimeSpan.FromHours(1);
            f.Preferences.SetHomeLocation("Kraków", "PL");
            f.Model.Enqueue().Text("Hi.");

            // Act
            AgentFixture.Completed(f.Run("what day is it?"));

            // Assert
            var context = f.Model.Inputs[0].Single(i => i.Text.StartsWith("Context data", System.StringComparison.Ordinal)).Text;
            Assert.That(context, Does.Contain("Local time: Sunday, 1 November 2026, 01:00. Home: Kraków, PL."));
            Assert.That(f.Model.Requests.Single().Instructions, Does.Not.Contain("2026"), "the cached prefix holds no time");
        }

        [Test]
        public void RunTurn_HomeLocationSetInTheTurn_ReachesTheNextRoundsSearch()
        {
            // Arrange
            var f = new AgentFixture(webSearch: true);
            f.Registry.Register(new SetHomeLocationTool());
            f.Model.Enqueue().Call("c1", ToolNames.SetHomeLocation, "{\"city\":\"Leeds\",\"country\":\"GB\"}");
            f.Model.Enqueue().Text("Rainy in Leeds.");

            // Act
            AgentFixture.Completed(f.Run("I live in Leeds. What's the weather?"));

            // Assert
            Assert.That(f.Model.Requests[1].WebSearch.City, Is.EqualTo("Leeds"));
        }

        [Test]
        public void RunTurn_NamespacedTools_SendTheirNamespaces()
        {
            // Arrange
            var f = new AgentFixture();
            f.Registry.RegisterNamespace(new ToolNamespace("actions", "Body actions."));
            f.Registry.RegisterNamespace(new ToolNamespace("empty", "Nothing offered."));
            f.Tool("actions.dance");
            f.Tool(ToolNames.Stop);
            f.Model.Enqueue().Call("c1", "actions.dance");
            f.Model.Enqueue().Text("Ta-da!");

            // Act
            var run = f.Run("Show me how to dance");

            // Assert
            AgentFixture.Completed(run);
            Assert.That(f.Model.Requests[0].Namespaces.Select(n => n.Name), Is.EqualTo(new[] { "actions" }));
            Assert.That(AgentFixture.Describe(run.Items), Does.Contain("start:actions.dance").And.Contain("done:actions.dance"));
        }

        [Test]
        public void RunTurn_WebSearchInTheStream_RunsLikeAToolForThePipelineButExecutesNone()
        {
            // Arrange: the pipeline turns these events into the Acting state, the badge and the Think gesture.
            var f = new AgentFixture(webSearch: true);
            var raisedByTheLoop = new List<string>();
            f.Events.ToolStarted += (id, name) => raisedByTheLoop.Add(name);
            f.Model.Enqueue().Text("Let me check.").TextDone().WebSearch("ws_9").Text(" Sunny, 12 degrees.");

            // Act
            var run = f.Run("What's the weather?");

            // Assert
            AgentFixture.Completed(run);
            Assert.That(AgentFixture.Describe(run.Items),
                        Does.StartWith("say:Let me check. SpeechBreak start:web_search done:web_search say: Sunny, 12 degrees."));
            Assert.That(run.Items.Where(e => e.ToolName == ToolNames.WebSearch).Select(e => e.CallId), Is.EqualTo(new[] { "ws_9", "ws_9" }));
            Assert.That(raisedByTheLoop, Is.Empty, "the pipeline relays the badge, once");
            Assert.That(f.Model.Requests.Count, Is.EqualTo(1), "a server-side search needs no second round");
            Assert.That(f.MemoryKinds(), Is.EqualTo("UserText,AssistantText"), "the search item is turn-local");
        }

        [Test]
        public void RunTurn_SearchThatDidNotComplete_EndsAsAFailedTool()
        {
            // Arrange
            var f = new AgentFixture(webSearch: true);
            f.Model.Enqueue().WebSearch("ws_1", ok: false).Text("I couldn't check that.");

            // Act
            var run = f.Run("What's the weather?");

            // Assert
            AgentFixture.Completed(run);
            Assert.That(AgentFixture.Describe(run.Items), Does.Contain("start:web_search done:web_search!"));
        }

        [Test]
        public void RunTurn_PersonaWithoutWebSearch_GetsTheTimeButNotTheHomeCity()
        {
            // Arrange
            var f = new AgentFixture(webSearch: true);
            f.Personas.Current.AllowedTools = new[] { ToolNames.Stop };
            f.Preferences.SetHomeLocation("Kraków", "PL");
            f.Model.Enqueue().Text("Hi.");

            // Act
            AgentFixture.Completed(f.Run("what day is it?"));

            // Assert
            var context = f.Model.Inputs[0].Single(i => i.Text.StartsWith("Context data", System.StringComparison.Ordinal)).Text;
            Assert.That(context, Does.Contain("Local time: Sunday, 1 November 2026, 00:00."));
            Assert.That(context, Does.Not.Contain("Kraków").And.Not.Contain("Home"));
        }

        [Test]
        public void RunTurn_OfflinePersonaAllowedToSearch_StillGetsTheHomeCity()
        {
            // Arrange: Degraded offers no search this turn, but the persona may know where the user lives.
            var f = new AgentFixture(webSearch: true);
            f.Connectivity.State = Connectivity.Degraded;
            f.Preferences.SetHomeLocation("Kraków", "PL");
            f.Model.Enqueue().Text("Hi.");

            // Act
            AgentFixture.Completed(f.Run("what day is it?"));

            // Assert
            Assert.That(f.Model.Inputs[0].Single(i => i.Text.StartsWith("Context data", System.StringComparison.Ordinal)).Text,
                        Does.Contain("Home: Kraków, PL."));
        }
    }
}
