using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Jolly.Tests.EditMode.Core.Tools
{
    /// <summary>Registers a tool (so its schema must pass the subset parser), validates the JSON arguments, executes.</summary>
    static class ToolHarness
    {
        public static UniTask<ToolResult> Start(ITool tool, string argsJson, ToolContext context = null, CancellationToken ct = default)
        {
            var registry = new ToolRegistry();
            registry.Register(tool);
            Assert.That(registry.TryValidate(tool.Definition.Name, argsJson, out var args, out var error), Is.True, error);
            return tool.Execute(args, context ?? Context(), ct);
        }

        public static ToolResult Run(ITool tool, string argsJson = "{}", ToolContext context = null) =>
            Sync.Run(Start(tool, argsJson, context));

        public static ToolContext Context(UserTurn turn = null) => new()
        {
            Turn = turn ?? new UserTurn { TurnId = 1, Text = "what is this", LanguageTag = "en" },
            Persona = FakePersonaProvider.Profile("companion"),
            PersonaInstructions = "You are companion. Reply in English.",
            Speech = new RecordingSpeechSink(),
            Preferences = new FakeUserPreferences(),
        };

        public static JObject Output(ToolResult result) => JObject.Parse(result.OutputJson);
    }
}
