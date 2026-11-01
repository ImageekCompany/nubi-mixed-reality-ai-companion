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
        public const string SettingsOpen = "settings.open";
        public const string SettingsClose = "settings.close";
        public const string SettingsFillers = "settings.fillers";
        public const string QuestionOpeners = "question.openers";
        public const string QuestionRequests = "question.requests";
        public const string QuestionFillers = "question.fillers";

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
            (SettingsOpen, "pl") => "otwórz ustawienia|pokaż ustawienia|ustawienia|otwórz menu|pokaż menu",
            (SettingsOpen, _) => "open settings|show settings|open the settings|show the settings|show me the settings|settings|open menu|show menu|open the menu|show the menu",
            (SettingsClose, "pl") => "zamknij ustawienia|ukryj ustawienia|zamknij menu|ukryj menu",
            (SettingsClose, _) => "close settings|hide settings|close the settings|hide the settings|close menu|hide menu|close the menu|hide the menu",
            (SettingsFillers, "pl") => "nubi|proszę|hej|teraz|ok|okej|możesz",
            (SettingsFillers, _) => "nubi|please|hey|now|ok|okay|can|could|you",
            (QuestionOpeners, "pl") => "co|czy|gdzie|kto|kogo|dlaczego|czemu|jak|jaki|jaka|jakie|kiedy|który|która|które|ile|skąd|dokąd",
            (QuestionOpeners, _) => "what|where|who|whom|whose|why|how|when|which|is|isn|are|aren|am|was|were|do|does|did|has|should|shall|may|might",
            (QuestionRequests, "pl") => "możesz|czy możesz|czy mógłbyś|czy mogłabyś|mógłbyś|mogłabyś",
            (QuestionRequests, _) => "can you|could you|would you|will you|can u|could u",
            (QuestionFillers, "pl") => "nubi|hej|cześć|no|okej|ok|proszę",
            (QuestionFillers, _) => "nubi|hey|hi|hello|so|um|uh|oh|okay|ok|and|please",
            _ => "",
        };
    }
}
