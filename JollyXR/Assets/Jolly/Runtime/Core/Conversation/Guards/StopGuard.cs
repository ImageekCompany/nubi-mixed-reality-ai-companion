using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Local "stop" (architecture §5.1): a short final made only of stop triggers ("stop", "wait", "hold on") and
    /// fillers ("Nubi", "please"). The pipeline halts locomotion on a match before the model is called.
    /// </summary>
    public sealed class StopGuard
    {
        const int MaxWords = 4;

        readonly ShortCommand _command;

        public StopGuard(ILocalizer localizer) =>
            _command = new ShortCommand(localizer, GuardTables.StopTriggers, GuardTables.StopFillers, MaxWords);

        public bool Matches(string transcript) => _command.Matches(transcript);
    }
}
