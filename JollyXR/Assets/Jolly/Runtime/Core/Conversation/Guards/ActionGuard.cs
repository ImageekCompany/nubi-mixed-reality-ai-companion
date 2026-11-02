using System.Collections.Generic;
using System.Text.RegularExpressions;
using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// A short spoken body command ("dance", "jump!", "go behind me", ADR-0047): a final made only of one command's
    /// phrases and fillers (<see cref="ShortCommand"/>) names the action to run before the model is called. The real name
    /// is a filler (<c>{name}</c>) and a known mishearing is the name by now (<see cref="TranscriptAliases"/>), but unlike
    /// the other guards this one takes no name as heard (ADR-0043): a word alone in its clause was as often a second
    /// command or a refusal, and "Dance. Go behind me." ran one move and dropped the other, "Nope. Dance." danced. Any such
    /// word goes to the model, which also orders two commands. A question mark or a negation word sends it to the model
    /// whatever the words and the tables: "Dance?" asks.
    /// </summary>
    public sealed class ActionGuard
    {
        public const int MaxWords = 7;

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
                Phrases = new ShortCommand(localizer, key, GuardTables.ActionFillers, MaxWords, nameAsHeard: false),
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

        /// <summary>One to three words said twice in a row, with no punctuation between: "Jump. Jump." stays two clauses.</summary>
        static readonly Regex Stutter = new(@"\b(\w+(?:\s+\w+){0,2})\s+\1\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The action <paramref name="transcript"/> commands, if it is exactly one short command.</summary>
        public bool TryMatch(string transcript, out string tool, out string argumentsJson)
        {
            tool = argumentsJson = null;
            if (string.IsNullOrEmpty(transcript) || transcript.IndexOf('?') >= 0 || HasNegation(transcript))
                return false;
            // The headset's ASR sometimes stutters ("Show me how to how to dance."), which sent the turn to the model.
            transcript = Stutter.Replace(transcript, "$1");
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
