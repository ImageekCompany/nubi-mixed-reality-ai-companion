using System.Collections.Generic;
using System.Globalization;

namespace Jolly.App.SelfTest
{
    public enum ScenarioStatus { Pass, Fail, Degraded, Skipped }

    /// <summary>One self-test scenario's verdict with its measurements (numbers as invariant strings).</summary>
    public sealed class ScenarioResult
    {
        public ScenarioResult(string name) => Name = name;

        public string Name { get; }
        public ScenarioStatus Status { get; set; } = ScenarioStatus.Pass;
        public double DurationMs { get; set; }
        public List<string> Notes { get; } = new();
        public SortedDictionary<string, string> Metrics { get; } = new();

        public ScenarioResult Metric(string key, double value)
        {
            Metrics[key] = double.IsNaN(value) ? "null" : value.ToString("0.###", CultureInfo.InvariantCulture);
            return this;
        }

        public ScenarioResult Metric(string key, string value)
        {
            Metrics[key] = value ?? string.Empty;
            return this;
        }

        /// <summary>Records a failed check; the scenario fails unless every check passes.</summary>
        public bool Check(bool ok, string failure)
        {
            if (!ok)
            {
                Status = ScenarioStatus.Fail;
                Notes.Add(failure);
            }
            return ok;
        }

        public void Degrade(string reason)
        {
            if (Status == ScenarioStatus.Pass)
                Status = ScenarioStatus.Degraded;
            Notes.Add(reason);
        }
    }
}
