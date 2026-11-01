using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jolly.Core.Usage
{
    /// <summary>usage.json: {"version":1,"spentUsd":…,"models":{"&lt;id&gt;":{…UsageTotals}}}. Written atomically.</summary>
    static class UsageFile
    {
        const int Version = 1;

        /// <summary>False when the file is missing or unreadable; the meter then starts from zero.</summary>
        public static bool TryLoad(string path, out decimal spentUsd, Dictionary<string, UsageTotals> models)
        {
            spentUsd = 0m;
            if (!File.Exists(path))
                return false;
            try
            {
                using var reader = new JsonTextReader(new StreamReader(path)) { FloatParseHandling = FloatParseHandling.Decimal };
                var root = JObject.Load(reader);
                spentUsd = (decimal?)root["spentUsd"] ?? 0m;
                if (root["models"] is JObject entries)
                {
                    foreach (var entry in entries.Properties())
                        models[entry.Name] = Read(entry.Value);
                }
                return true;
            }
            catch (JsonException)
            {
                spentUsd = 0m;
                models.Clear();
                return false;
            }
        }

        public static void Save(string path, decimal spentUsd, Dictionary<string, UsageTotals> models)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            using (var writer = new JsonTextWriter(new StreamWriter(tmp)) { Culture = CultureInfo.InvariantCulture })
            {
                writer.WriteStartObject();
                writer.WritePropertyName("version");
                writer.WriteValue(Version);
                writer.WritePropertyName("spentUsd");
                writer.WriteValue(spentUsd);
                writer.WritePropertyName("models");
                writer.WriteStartObject();
                foreach (var pair in models)
                {
                    writer.WritePropertyName(pair.Key);
                    Write(writer, pair.Value);
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            // Same protocol as Core/Diagnostics AtomicFile (WP2): File.Replace throws on a missing destination.
            if (File.Exists(path))
                File.Replace(tmp, path, null);
            else
                File.Move(tmp, path);
        }

        static UsageTotals Read(JToken t) => new()
        {
            InputTokens = (long?)t["inputTokens"] ?? 0,
            CachedInputTokens = (long?)t["cachedInputTokens"] ?? 0,
            OutputTokens = (long?)t["outputTokens"] ?? 0,
            Images = (long?)t["images"] ?? 0,
            AudioSeconds = (double?)t["audioSeconds"] ?? 0,
            Requests = (long?)t["requests"] ?? 0,
            EstimatedRequests = (long?)t["estimatedRequests"] ?? 0,
            Usd = (decimal?)t["usd"] ?? 0m,
        };

        static void Write(JsonWriter w, UsageTotals t)
        {
            w.WriteStartObject();
            w.WritePropertyName("inputTokens");
            w.WriteValue(t.InputTokens);
            w.WritePropertyName("cachedInputTokens");
            w.WriteValue(t.CachedInputTokens);
            w.WritePropertyName("outputTokens");
            w.WriteValue(t.OutputTokens);
            w.WritePropertyName("images");
            w.WriteValue(t.Images);
            w.WritePropertyName("audioSeconds");
            w.WriteValue(t.AudioSeconds);
            w.WritePropertyName("requests");
            w.WriteValue(t.Requests);
            w.WritePropertyName("estimatedRequests");
            w.WriteValue(t.EstimatedRequests);
            w.WritePropertyName("usd");
            w.WriteValue(t.Usd);
            w.WriteEndObject();
        }
    }
}
