using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jolly.Net.Sse;
using UnityEngine;

namespace Jolly.Tests.EditMode.Net
{
    /// <summary>
    /// Loads Tests/Fixtures/sse/*.txt through the real parser. Provenance: responses_text, responses_commentary_tool,
    /// responses_parallel_tools and responses_incomplete are raw bodies recorded live on 2026-11-01 from
    /// muse-spark-1.3-contributor with synthetic prompts (docs/api-spend.md). responses_failed and responses_error_event
    /// are hand-written from the documented terminal-event shapes (docs "Error handling → Streaming errors"), because a
    /// mid-stream failure cannot be provoked on demand.
    /// </summary>
    static class SseFixtures
    {
        public static string PathOf(string name) =>
            System.IO.Path.Combine(Application.dataPath, "Jolly", "Tests", "Fixtures", "sse", name + ".txt");

        public static List<SseEvent> Parse(string name, int chunkSize = 7)
        {
            var bytes = File.ReadAllBytes(PathOf(name));
            var parser = new SseLineParser();
            var events = new Queue<SseEvent>();
            for (var offset = 0; offset < bytes.Length; offset += chunkSize)
                parser.Feed(bytes.AsSpan(offset, Math.Min(chunkSize, bytes.Length - offset)), events);
            parser.Complete(events);
            return events.ToList();
        }
    }
}
