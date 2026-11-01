namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Keys of the <c>Crisis</c> string table (ADR-0014; owned by WP2). Each entry is a '|'-separated phrase list.
    /// When an entry is missing for the current locale the built-in list below is used, so a safety guard never goes
    /// silent because a table failed to load.
    /// </summary>
    public static class GuardTables
    {
        public const string Table = "Crisis";
        public const string CrisisKeywords = "crisis.keywords";
        public const string StopTriggers = "stop.triggers";
        public const string StopFillers = "stop.fillers";
        public const string ComeTriggers = "come.triggers";
        public const string ComeFillers = "come.fillers";

        internal static string Fallback(string key, string languageTag) => (key, languageTag) switch
        {
            (CrisisKeywords, "pl") => "zabić się|zabiję się|samobójstwo|samobójcze|nie chcę żyć|chcę umrzeć|skrzywdzić się|odebrać sobie życie",
            (CrisisKeywords, _) => "kill myself|killing myself|suicide|suicidal|end my life|want to die|hurt myself|self harm|don't want to live|no reason to live",
            (StopTriggers, "pl") => "stop|stój|czekaj|zaczekaj|zostań|przestań",
            (StopTriggers, _) => "stop|wait|stay|halt|freeze|hold on|stand still",
            (StopFillers, "pl") => "nubi|proszę|teraz|tam|tu|no|hej|ok|okej",
            (StopFillers, _) => "nubi|please|now|ok|okay|hey|there|right|just|it|that",
            (ComeTriggers, "pl") => "chodź tu|chodź tutaj|chodź do mnie|chodź bliżej|podejdź|podejdź tu|podejdź bliżej|podejdź do mnie",
            (ComeTriggers, _) => "come here|come to me|come closer|come over here",
            (ComeFillers, "pl") => "nubi|proszę|cześć|hej|witaj",
            (ComeFillers, _) => "nubi|please|hi|hey|hello",
            _ => "",
        };
    }
}
