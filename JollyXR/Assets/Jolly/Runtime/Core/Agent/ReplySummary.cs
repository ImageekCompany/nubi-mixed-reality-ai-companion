using System.Text;
using Jolly.Contracts;

namespace Jolly.Core.Agent
{
    /// <summary>One model round for diagnosing a missed tool call: what it called, why it stopped and how its reply began.</summary>
    static class ReplySummary
    {
        public const int MaxTextChars = 120;

        public static string Format(int round, ModelTurnResult result)
        {
            var tools = new StringBuilder();
            var text = new StringBuilder();
            foreach (var item in result.OutputItems)
            {
                if (item.Kind == ItemKind.ToolCall)
                    tools.Append(tools.Length == 0 ? "" : ",").Append(item.ToolName);
                else if (item.Kind == ItemKind.AssistantText)
                    text.Append(item.Text);
            }
            if (text.Length > MaxTextChars)
                text.Length = MaxTextChars;
            // One logfmt value on one logcat line.
            text.Replace('\n', ' ').Replace('\r', ' ').Replace('"', '\'');
            return $"round={round} tools={(tools.Length == 0 ? "none" : tools.ToString())} finish={result.FinishReason} " +
                   $"text=\"{text}\"";
        }
    }
}
