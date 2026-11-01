using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jolly.Core.Diagnostics;
using Newtonsoft.Json;

namespace Jolly.App.SelfTest
{
    /// <summary>Writes selftest_results.json, the channel tools/device/selftest.sh reads (logcat rotates, the file stays).</summary>
    public static class ResultsWriter
    {
        public static void Write(string path, string device, string startedUtc, decimal spentUsd, string overall,
                                 IReadOnlyList<ScenarioResult> results)
        {
            using var text = new StringWriter(CultureInfo.InvariantCulture);
            using (var json = new JsonTextWriter(text) { Formatting = Formatting.Indented })
            {
                json.WriteStartObject();
                json.WritePropertyName("version");
                json.WriteValue(1);
                json.WritePropertyName("device");
                json.WriteValue(device);
                json.WritePropertyName("startedUtc");
                json.WriteValue(startedUtc);
                json.WritePropertyName("overall");
                json.WriteValue(overall);
                json.WritePropertyName("spentUsd");
                json.WriteValue(spentUsd);
                json.WritePropertyName("scenarios");
                json.WriteStartArray();
                foreach (var r in results)
                    WriteScenario(json, r);
                json.WriteEndArray();
                json.WriteEndObject();
            }
            AtomicFile.WriteAllText(path, text.ToString());
        }

        static void WriteScenario(JsonTextWriter json, ScenarioResult r)
        {
            json.WriteStartObject();
            json.WritePropertyName("name");
            json.WriteValue(r.Name);
            json.WritePropertyName("status");
            json.WriteValue(r.Status.ToString().ToUpperInvariant());
            json.WritePropertyName("durationMs");
            json.WriteValue(System.Math.Round(r.DurationMs));
            json.WritePropertyName("notes");
            json.WriteStartArray();
            foreach (var note in r.Notes)
                json.WriteValue(note);
            json.WriteEndArray();
            json.WritePropertyName("metrics");
            json.WriteStartObject();
            foreach (var pair in r.Metrics)
            {
                json.WritePropertyName(pair.Key);
                json.WriteValue(pair.Value);
            }
            json.WriteEndObject();
            json.WriteEndObject();
        }
    }
}
