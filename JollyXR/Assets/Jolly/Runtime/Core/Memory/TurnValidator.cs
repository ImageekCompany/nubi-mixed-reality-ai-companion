using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Memory
{
    /// <summary>
    /// Rules for one committed turn: no Opaque (turn-local reasoning) or image items, and every ToolCall paired with a
    /// ToolResult in the same batch; the Responses API rejects an orphan function_call with 400.
    /// </summary>
    static class TurnValidator
    {
        public static void Validate(IReadOnlyList<ConversationItem> items)
        {
            var open = new HashSet<string>();
            foreach (var item in items)
            {
                switch (item.Kind)
                {
                    case ItemKind.Opaque:
                    case ItemKind.UserImage:
                        throw new ArgumentException($"{item.Kind} items are never committed to memory");
                    case ItemKind.ToolCall:
                        open.Add(item.CallId);
                        break;
                    case ItemKind.ToolResult when !open.Remove(item.CallId):
                        throw new InvalidOperationException($"tool result {item.CallId} has no call in this turn");
                }
            }
            if (open.Count > 0)
                throw new InvalidOperationException("tool call without a result: " + string.Join(", ", open));
        }
    }
}
