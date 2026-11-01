using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Only records the switch and ends the turn; the controller applies it once the conversation is Idle, and the new
    /// persona greets in its own voice (architecture §3). The psychologist persona can only be chosen in Settings.
    /// </summary>
    public sealed class SetPersonaTool : ITool
    {
        public const string SettingsOnlyPersona = "psychologist";

        readonly IPersonaProvider _personas;

        public SetPersonaTool(IPersonaProvider personas)
        {
            _personas = personas;
            Definition = new ToolDefinition(ToolNames.SetPersona, "Switch Nubi's personality when the user asks.",
                SchemaFor(personas.AvailableIds), TimeSpan.FromSeconds(1), ToolConcurrency.Exclusive);
        }

        public ToolDefinition Definition { get; }

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("id", out var id);
            var result = id != SettingsOnlyPersona && _personas.RequestSwitch(id)
                ? ToolResult.Success(new ToolJson().Add("status", "switching").Add("id", id).ToString(), endsTurn: true)
                : ToolResult.Failure("unknown_persona", id);
            return UniTask.FromResult(result);
        }

        static string SchemaFor(IReadOnlyList<string> ids)
        {
            var values = new StringBuilder();
            foreach (var id in ids)
            {
                if (id == SettingsOnlyPersona)
                    continue;
                if (values.Length > 0)
                    values.Append(',');
                values.Append(ToolResult.JsonString(id));
            }
            return "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\",\"enum\":[" + values +
                   "]}},\"required\":[\"id\"],\"additionalProperties\":false}";
        }
    }
}
