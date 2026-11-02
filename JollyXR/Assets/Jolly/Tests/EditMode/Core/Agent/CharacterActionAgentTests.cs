using Jolly.Contracts;
using Jolly.Core.Tools;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Agent
{
    /// <summary>The body-action rule follows the actions namespace into the instructions (ADR-0047).</summary>
    public sealed class CharacterActionAgentTests
    {
        const string ActionRule = "never say or imply you did an action you did not call";

        [Test]
        public void RunTurn_ActionsOffered_InstructionsSayToCallThem()
        {
            // Arrange
            var f = new AgentFixture();
            f.Registry.RegisterNamespace(new ToolNamespace(ToolNames.Actions.Namespace, "Body actions."));
            f.Tool(ToolNames.Actions.MoveRelative);
            f.Model.Enqueue().Text("Sure.");

            // Act
            AgentFixture.Completed(f.Run("Go stand behind me"));

            // Assert
            Assert.That(f.Model.Requests[0].Instructions, Does.Contain(ActionRule));
            Assert.That(f.Model.Requests[0].Instructions, Does.Contain("asks to see it, so do it"));
        }

        [Test]
        public void RunTurn_ActionsWithheld_InstructionsLeaveTheRuleOut()
        {
            // Arrange: the persona allows no action, so the namespace is not sent either
            var f = new AgentFixture();
            f.Registry.RegisterNamespace(new ToolNamespace(ToolNames.Actions.Namespace, "Body actions."));
            f.Tool(ToolNames.Actions.MoveRelative);
            f.Tool(ToolNames.Stop);
            f.Personas.Current.AllowedTools = new[] { ToolNames.Stop };
            f.Model.Enqueue().Text("I'm right here.");

            // Act
            AgentFixture.Completed(f.Run("Go stand behind me"));

            // Assert
            Assert.That(f.Model.Requests[0].Namespaces, Is.Empty);
            Assert.That(f.Model.Requests[0].Instructions, Does.Not.Contain(ActionRule));
        }
    }
}
