using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Local "come here" (ADR-0036): a short final made only of come triggers ("come here", "come to me") and fillers
    /// (the name, "please", a greeting). The pipeline starts come_here on a match before the model is called, so the
    /// demo's opening line moves Nubi whatever the model does. A greeting alone has no trigger and stays with the model
    /// (ADR-0032).
    /// </summary>
    public sealed class ComeGuard
    {
        const int MaxWords = 6;

        readonly ShortCommand _command;

        public ComeGuard(ILocalizer localizer) =>
            _command = new ShortCommand(localizer, GuardTables.ComeTriggers, GuardTables.ComeFillers, MaxWords);

        public bool Matches(string transcript) => _command.Matches(transcript);
    }
}
