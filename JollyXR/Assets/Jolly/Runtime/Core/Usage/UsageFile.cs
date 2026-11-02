using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jolly.Core.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jolly.Core.Usage
{
    /// <summary>usage.json: {"version":1,"spentUsd":…,"models":{"&lt;id&gt;":{…UsageTotals}}}. Written atomically.</summary>
    static class UsageFile
    {
        const int Version = 1;

        /// <summary>
        /// False when the file is missing or unreadable; the meter then starts from zero. An unreadable file is moved
        /// to usage.json.bad first, so the next save cannot overwrite the only record of past spend.
        /// </summary>
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
            catch (Exception e) when (IsUnreadable(e))
            {
                spentUsd = 0m;
                models.Clear();
                MoveAside(path);
                return false;
            }
        }

        public static void Save(string path, decimal spentUsd, Dictionary<string, UsageTotals> models)
        {
            var text = new StringWriter(CultureInfo.InvariantCulture);
            using (var writer = new JsonTextWriter(text) { Culture = CultureInfo.InvariantCulture })
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
            AtomicFile.WriteAllText(path, text.ToString());
        }

        // JToken casts of a well-formed document with the wrong shape throw the BCL exceptions, not JsonException.
        static bool IsUnreadable(Exception e) =>
            e is JsonException or FormatException or InvalidCastException or ArgumentException or InvalidOperationException
                or OverflowException or IOException or UnauthorizedAccessException;

        static void MoveAside(string path)
        {
            var bad = path + ".bad";
            try
            {
                if (File.Exists(bad))
                    File.Delete(bad);
                File.Move(path, bad);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Still in use or read-only: leave it where it is.
            }
        }

        static UsageTotals Read(JToken t) => new()
        {
            InputTokens = (long?)t["inputTokens"] ?? 0,
            CachedInputTokens = (long?)t["cachedInputTokens"] ?? 0,
            OutputTokens = (long?)t["outputTokens"] ?? 0,
            Images = (long?)t["images"] ?? 0,
            AudioSeconds = (double?)t["audioSeconds"] ?? 0,
            WebSearches = (long?)t["webSearches"] ?? 0,
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
            w.WritePropertyName("webSearches");
            w.WriteValue(t.WebSearches);
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
