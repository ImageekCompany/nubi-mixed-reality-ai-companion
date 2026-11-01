using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    public enum SettingsCommand { None, Open, Close }

    /// <summary>
    /// Local "open settings" / "close settings" (ADR-0042): a short final made only of the command and fillers. The
    /// panel is the answer, so the turn ends without a model round. Close is checked first: "close settings" also
    /// contains the open trigger "settings".
    /// </summary>
    public sealed class SettingsGuard
    {
        const int MaxWords = 6;

        readonly ShortCommand _open;
        readonly ShortCommand _close;

        public SettingsGuard(ILocalizer localizer)
        {
            _open = new ShortCommand(localizer, GuardTables.SettingsOpen, GuardTables.SettingsFillers, MaxWords);
            _close = new ShortCommand(localizer, GuardTables.SettingsClose, GuardTables.SettingsFillers, MaxWords);
        }

        public SettingsCommand Match(string transcript) =>
            _close.Matches(transcript) ? SettingsCommand.Close
            : _open.Matches(transcript) ? SettingsCommand.Open
            : SettingsCommand.None;
    }
}
