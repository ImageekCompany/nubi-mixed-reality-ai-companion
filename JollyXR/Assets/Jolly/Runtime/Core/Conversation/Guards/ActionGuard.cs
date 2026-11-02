using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// A short spoken body command ("dance", "jump!", "go behind me", ADR-0047): a final made only of one command's
    /// phrases and fillers (<see cref="ShortCommand"/>) names the action to run before the model is called. A question
    /// mark or a negation word sends it to the model whatever the words: "Dance?" asks, and the name-as-heard rule would
    /// read "Never. Jump." as someone called Never being told to jump. Two commands in one final ("Jump. Spin.") are the
    /// model's to order, since each reads the other as the name.
    /// </summary>
    public sealed class ActionGuard
    {
        const int MaxWords = 7;

        sealed class Command
        {
            public ShortCommand Phrases;
            public string Tool;
            public string ArgumentsJson;
        }

        readonly Command[] _commands;
        readonly LocalizedPhrases _negations;
        readonly List<string> _tokens = new();

        public ActionGuard(ILocalizer localizer)
        {
            _negations = new LocalizedPhrases(localizer, GuardTables.ActionNegations);
            Command Of(string key, string tool, string direction = null) => new()
            {
                Phrases = new ShortCommand(localizer, key, GuardTables.ActionFillers, MaxWords),
                Tool = tool,
                ArgumentsJson = direction == null ? "{}" : "{\"direction\":\"" + direction + "\"}",
            };
            _commands = new[]
            {
                Of(GuardTables.ActionJump, ToolNames.Actions.Jump),
                Of(GuardTables.ActionDance, ToolNames.Actions.Dance),
                Of(GuardTables.ActionSpin, ToolNames.Actions.Spin),
                Of(GuardTables.ActionBow, ToolNames.Actions.Bow),
                Of(GuardTables.ActionWave, ToolNames.Actions.Wave),
                Of(GuardTables.ActionNod, ToolNames.Actions.Nod),
                Of(GuardTables.ActionCelebrate, ToolNames.Actions.Celebrate),
                Of(GuardTables.ActionBehind, ToolNames.Actions.MoveRelative, "behind"),
                Of(GuardTables.ActionFront, ToolNames.Actions.MoveRelative, "front"),
                Of(GuardTables.ActionLeft, ToolNames.Actions.MoveRelative, "left"),
                Of(GuardTables.ActionRight, ToolNames.Actions.MoveRelative, "right"),
                Of(GuardTables.ActionCloser, ToolNames.Actions.MoveRelative, "closer"),
                Of(GuardTables.ActionAway, ToolNames.Actions.MoveRelative, "away"),
            };
        }

        /// <summary>The action <paramref name="transcript"/> commands, if it is exactly one short command.</summary>
        public bool TryMatch(string transcript, out string tool, out string argumentsJson)
        {
            tool = argumentsJson = null;
            if (string.IsNullOrEmpty(transcript) || transcript.IndexOf('?') >= 0 || HasNegation(transcript))
                return false;
            foreach (var command in _commands)
            {
                if (!command.Phrases.Matches(transcript))
                    continue;
                if (tool != null)
                {
                    tool = argumentsJson = null;
                    return false;
                }
                tool = command.Tool;
                argumentsJson = command.ArgumentsJson;
            }
            return tool != null;
        }

        bool HasNegation(string transcript)
        {
            TextNormalizer.Tokenize(transcript, _tokens);
            foreach (var phrase in _negations.Current)
            {
                if (TextNormalizer.IndexOf(_tokens, phrase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
