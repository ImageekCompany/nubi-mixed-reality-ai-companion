using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Agent
{
    /// <summary>
    /// The items of one turn. Model input replays every finished round verbatim (reasoning Opaque items included), then
    /// the round's function_call_outputs; memory receives the same items without Opaque, committed once.
    /// </summary>
    sealed class TurnTranscript
    {
        readonly ConversationItem _user;
        readonly List<ConversationItem> _rounds = new();
        readonly List<ConversationItem> _extraAssistant = new();

        public TurnTranscript(string userText) => _user = ConversationItem.User(userText);

        /// <summary>
        /// Recent items, then the context block, then this turn. The context block changes every turn (scene ages such as
        /// "2 min ago"), so it sits after the history: the prompt-cache prefix then covers instructions, tools and every
        /// earlier turn (prefix caching, [docs:prompt-caching]).
        /// </summary>
        public List<ConversationItem> Input(ConversationItem context, IReadOnlyList<ConversationItem> recent)
        {
            var input = new List<ConversationItem>(recent.Count + _rounds.Count + 2);
            input.AddRange(recent);
            if (context != null)
                input.Add(context);
            input.Add(_user);
            input.AddRange(_rounds);
            return input;
        }

        public void AddRound(IReadOnlyList<ConversationItem> output, ToolRoundExecutor tools)
        {
            _rounds.AddRange(output);
            tools.CollectResults(_rounds, includeCalls: false);
        }

        /// <summary>Text Nubi spoke that the model did not write (e.g. the round-cap line).</summary>
        public void AddSpoken(string text) => _extraAssistant.Add(ConversationItem.Assistant(text));

        public List<ConversationItem> ForCommit()
        {
            var items = new List<ConversationItem> { _user };
            foreach (var item in _rounds)
            {
                if (item.Kind != ItemKind.Opaque)
                    items.Add(item);
            }
            items.AddRange(_extraAssistant);
            return items;
        }

        /// <summary>
        /// Commit after a cancel or failure: finished tool calls and results, the unfinished round's calls with
        /// interrupted outputs, and only the assistant text actually spoken, marked "[interrupted]".
        /// </summary>
        public List<ConversationItem> ForInterruptedCommit(string spokenSoFar, ToolRoundExecutor openRound)
        {
            var items = new List<ConversationItem> { _user };
            foreach (var item in _rounds)
            {
                if (item.Kind is ItemKind.ToolCall or ItemKind.ToolResult)
                    items.Add(item);
            }
            openRound?.CollectResults(items, includeCalls: true);
            var spoken = string.IsNullOrWhiteSpace(spokenSoFar) ? "[interrupted]" : spokenSoFar.TrimEnd() + " [interrupted]";
            items.Add(ConversationItem.Assistant(spoken));
            return items;
        }
    }
}
