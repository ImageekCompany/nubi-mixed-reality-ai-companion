using System;
using System.Collections.Generic;
using System.Linq;
using Jolly.Core.Tools;

namespace Jolly.Tests.PlayMode.Eval
{
    /// <summary>
    /// One utterance of the tool-selection eval (docs/perf/tool-selection-eval.md). A case either expects one of
    /// <see cref="Expected"/> among the first round's calls, or, with none expected, any answer without a movement tool:
    /// a greeting or small talk is answered in words, with at most a gesture (ADR-0032).
    /// </summary>
    public sealed class ToolSelectionCase
    {
        static readonly string[] Movement =
        {
            ToolNames.ComeHere, ToolNames.WalkTo, ToolNames.FollowUser, ToolNames.GoToSurface, ToolNames.Actions.MoveRelative,
        };

        public const string Greeting = "greeting";
        public const string SmallTalk = "small_talk";

        ToolSelectionCase(string utterance, string[] expected, string seenLabel, string tag)
        {
            Utterance = utterance;
            Expected = expected;
            SeenLabel = seenLabel;
            Tag = tag;
        }

        public string Utterance { get; }
        /// <summary>The expected tool, or <see cref="Greeting"/> / <see cref="SmallTalk"/>; selects a subset of a run.</summary>
        public string Tag { get; }
        public IReadOnlyList<string> Expected { get; }
        /// <summary>An object already in scene memory for this turn, as after an identify turn on device; null for none.</summary>
        public string SeenLabel { get; }

        public static ToolSelectionCase Calls(string utterance, params string[] anyOf) => new(utterance, anyOf, null, anyOf[0]);
        public static ToolSelectionCase CallsWithSeen(string utterance, string seenLabel, params string[] anyOf) =>
            new(utterance, anyOf, seenLabel, anyOf[0]);
        public static ToolSelectionCase StaysPut(string utterance, string tag = SmallTalk) =>
            new(utterance, Array.Empty<string>(), null, tag);

        public string ExpectedText => Expected.Count == 0
            ? "no movement tool"
            : string.Join(" or ", Expected.Select(t => $"`{t}`"));

        /// <summary>Every case when <paramref name="tags"/> is empty, else those whose <see cref="Tag"/> it lists.</summary>
        public static IReadOnlyList<ToolSelectionCase> Select(IReadOnlyCollection<string> tags) =>
            tags.Count == 0 ? All : All.Where(c => tags.Contains(c.Tag)).ToArray();

        public bool Passes(IReadOnlyCollection<string> called) =>
            Expected.Count == 0 ? !called.Any(Movement.Contains) : called.Any(Expected.Contains);

        // The device misses at f311081 (selftest-report.md): "come here" answered in words, "where is my mug?" with the
        // mug already seen routed to look_at.
        public static readonly IReadOnlyList<ToolSelectionCase> All = new[]
        {
            StaysPut("Hi Nubi", Greeting),
            StaysPut("Hello, who are you?", Greeting),
            StaysPut("Good morning Nubi, how are you today?", Greeting),
            Calls("Nubi, come here", ToolNames.ComeHere),
            Calls("Hi Nubi, come here", ToolNames.ComeHere),
            // Both walk up to the user; move_relative "closer" is the same move.
            Calls("come closer", ToolNames.ComeHere, ToolNames.Actions.MoveRelative),
            Calls("come to me please", ToolNames.ComeHere),
            Calls("can you come over here?", ToolNames.ComeHere),
            Calls("follow me", ToolNames.FollowUser),
            Calls("where is my mug?", ToolNames.FindObject),
            Calls("can you find my glasses", ToolNames.FindObject),
            Calls("where did I put my keys", ToolNames.FindObject),
            CallsWithSeen("Nubi, where is my mug? Show me.", "white mug", ToolNames.FindObject),
            Calls("what is this?", ToolNames.CaptureAndIdentify),
            Calls("look at me", ToolNames.LookAt),
            Calls("stop", ToolNames.Stop),
            Calls("Show me how to dance!", ToolNames.Actions.Dance),
            Calls("Can you jump?", ToolNames.Actions.Jump, ToolNames.Actions.Celebrate),
            Calls("Spin around for me", ToolNames.Actions.Spin),
            Calls("Go stand behind me", ToolNames.Actions.MoveRelative),
            StaysPut("what's your favourite colour?"),
            StaysPut("tell me a joke"),
            StaysPut("it's a lovely day today, isn't it?"),
            StaysPut("thank you, that's all for now"),
        };
    }
}
