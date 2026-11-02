using System;
using System.Linq;
using Jolly.Contracts;
using Jolly.Core.Tools;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Tools
{
    public sealed class ToolRegistryTests
    {
        static ToolRegistry WithTools(params string[] names)
        {
            var registry = new ToolRegistry();
            foreach (var name in names)
                registry.Register(new ScriptedTool(name));
            return registry;
        }

        [Test]
        public void Register_DottedNameWithoutItsNamespace_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new ToolRegistry().Register(new ScriptedTool("actions.jump")));
        }

        [TestCase("with.dot")]
        [TestCase("has space")]
        [TestCase("browser")]
        public void RegisterNamespace_InvalidName_Throws(string name)
        {
            Assert.Throws<ArgumentException>(() => new ToolRegistry().RegisterNamespace(new ToolNamespace(name, "d")));
        }

        [Test]
        public void NamespacesOf_ReturnsOnlyTheNamespacesOfTheGivenTools()
        {
            // Arrange
            var registry = new ToolRegistry();
            registry.RegisterNamespace(new ToolNamespace("actions", "body actions"));
            registry.RegisterNamespace(new ToolNamespace("unused", "nothing in it is offered"));
            registry.Register(new ScriptedTool("actions.jump"));
            registry.Register(new ScriptedTool("stop"));

            // Act
            var namespaces = registry.NamespacesOf(registry.DefinitionsFor(null, null));

            // Assert
            Assert.That(namespaces.Select(n => n.Name), Is.EqualTo(new[] { "actions" }));
        }

        [Test]
        public void Register_DuplicateName_Throws()
        {
            var registry = WithTools("stop");

            Assert.Throws<InvalidOperationException>(() => registry.Register(new ScriptedTool("stop")));
        }

        [Test]
        public void Register_InvalidSchema_Throws()
        {
            var tool = new ScriptedTool("bad", schema: "{\"type\":\"object\"}");

            Assert.Throws<ArgumentException>(() => new ToolRegistry().Register(tool));
        }

        [TestCase("has space")]
        [TestCase("")]
        [TestCase("slash/name")]
        public void Register_InvalidName_Throws(string name)
        {
            Assert.Throws<ArgumentException>(() => new ToolRegistry().Register(new ScriptedTool(name)));
        }

        [Test]
        public void DefinitionsFor_FiltersByPersonaAllowedToolsInRegistrationOrder()
        {
            var registry = WithTools("walk_to", "look_at", "stop", "play_gesture");
            var persona = new PersonaProfile { AllowedTools = new[] { "stop", "walk_to" } };

            var names = registry.DefinitionsFor(persona, new FakeUserPreferences()).Select(d => d.Name);

            Assert.That(names, Is.EqualTo(new[] { "walk_to", "stop" }));
        }

        [Test]
        public void DefinitionsFor_NullAllowedTools_OffersEverything()
        {
            var registry = WithTools("walk_to", "stop");

            var names = registry.DefinitionsFor(new PersonaProfile(), new FakeUserPreferences()).Select(d => d.Name);

            Assert.That(names, Is.EqualTo(new[] { "walk_to", "stop" }));
        }

        [Test]
        public void DefinitionsFor_Seated_RemovesWalkToAndFollowUserButKeepsComeHere()
        {
            var registry = WithTools(ToolNames.WalkTo, ToolNames.FollowUser, ToolNames.ComeHere, ToolNames.Stop);
            var preferences = new FakeUserPreferences();
            preferences.Current.Seated = true;

            var names = registry.DefinitionsFor(new PersonaProfile(), preferences).Select(d => d.Name);

            Assert.That(names, Is.EqualTo(new[] { ToolNames.ComeHere, ToolNames.Stop }));
        }

        [Test]
        public void TryValidate_UnknownTool_FailsWithMessage()
        {
            var ok = WithTools("stop").TryValidate("fly", "{}", out var args, out var error);

            Assert.That(ok, Is.False);
            Assert.That(args, Is.Null);
            Assert.That(error, Is.EqualTo("unknown tool 'fly'"));
        }

        [Test]
        public void TryValidate_UsesTheToolSchema()
        {
            var registry = WithTools("stop");

            Assert.That(registry.TryValidate("stop", "{}", out _, out _), Is.True);
            Assert.That(registry.TryValidate("stop", "{\"x\":1}", out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo("unknown property 'x'"));
        }

        [Test]
        public void TryGet_FindsRegisteredTool()
        {
            var registry = WithTools("stop");

            Assert.That(registry.TryGet("stop", out var tool) && tool.Definition.Name == "stop", Is.True);
            Assert.That(registry.TryGet("nope", out _), Is.False);
        }
    }
}
