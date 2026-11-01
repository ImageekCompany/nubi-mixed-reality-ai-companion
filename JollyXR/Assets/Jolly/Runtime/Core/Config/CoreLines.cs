using Jolly.Contracts;

namespace Jolly.Core.Config
{
    /// <summary>
    /// Lines that Core speaks through TTS (not baked clips). They live in WP7's <c>Lines</c> table; the English text below
    /// is used when an entry is missing, so the user never hears a raw key.
    /// </summary>
    public static class CoreLines
    {
        public const string Table = "Lines";
        public const string Tangled = "agent.tangled";
        public const string HoldCloser = "vision.hold_closer";
        public const string ScanOpening = "room.scan_opening";

        public static string Get(ILocalizer localizer, string key)
        {
            var text = localizer.Get(Table, key);
            return string.IsNullOrEmpty(text) || text == key ? Fallback(key) : text;
        }

        static string Fallback(string key) => key switch
        {
            Tangled => "I got a bit tangled, could you say that again?",
            HoldCloser => "I couldn't see it clearly, can you hold it closer?",
            ScanOpening => "Okay! Look around your room, and I'll be right here when you're done.",
            _ => string.Empty,
        };
    }
}
