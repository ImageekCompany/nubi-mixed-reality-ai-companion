using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Jolly.Tests.PlayMode.Eval
{
    /// <summary>One eval run as a markdown section: a row per utterance with the tools called and the verdict.</summary>
    public sealed class ToolSelectionReport
    {
        readonly string _commit;
        readonly string _effort;
        readonly string _model;
        readonly List<string> _rows = new();

        public ToolSelectionReport(string commit, string effort, string model)
        {
            _commit = commit;
            _effort = effort;
            _model = model;
        }

        public int Passed { get; private set; }
        public int Total => _rows.Count;

        public void Add(ToolSelectionCase c, IReadOnlyCollection<string> called, long responseMs)
        {
            var pass = c.Passes(called);
            if (pass)
                Passed++;
            var utterance = c.Utterance.Replace("|", "\\|") + (c.SeenLabel == null ? "" : $" ({c.SeenLabel} already seen)");
            var tools = called.Count == 0 ? "(none)" : string.Join(", ", called.Select(t => $"`{t}`"));
            _rows.Add($"| {utterance} | {c.ExpectedText} | {tools} | {responseMs} | {(pass ? "PASS" : "**FAIL**")} |");
        }

        public string ToMarkdown(decimal usd)
        {
            var sb = new StringBuilder()
                .Append("commit `").Append(_commit).Append("`, effort `").Append(_effort).Append("`, model `").Append(_model)
                .Append("`\n\n| Utterance | Expected | Called | Response ms | Result |\n|---|---|---|---|---|\n");
            foreach (var row in _rows)
                sb.Append(row).Append('\n');
            var percent = Total == 0 ? 0 : 100 * Passed / Total;
            return sb.Append("\nPass rate: ").Append(Passed).Append(" / ").Append(Total).Append(" (").Append(percent)
                .Append(" %). Metered cost: $").Append(usd.ToString("0.0000", CultureInfo.InvariantCulture)).Append('\n')
                .ToString();
        }
    }
}
