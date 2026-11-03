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
        public const string ActionFillers = "action.fillers";
        public const string ActionNegations = "action.negations";
        public const string ActionJump = "action.jump";
        public const string ActionDance = "action.dance";
        public const string ActionSpin = "action.spin";
        public const string ActionBow = "action.bow";
        public const string ActionWave = "action.wave";
        public const string ActionNod = "action.nod";
        public const string ActionCelebrate = "action.celebrate";
        public const string ActionBehind = "action.behind";
        public const string ActionFront = "action.front";
        public const string ActionLeft = "action.left";
        public const string ActionRight = "action.right";
        public const string ActionCloser = "action.closer";
        public const string ActionAway = "action.away";
        public const string ActionConnectors = "action.connectors";
        public const string SettingsOpen = "settings.open";
        public const string SettingsClose = "settings.close";
        public const string SettingsFillers = "settings.fillers";
        public const string PlaceRooms = "place.rooms";
        public const string PlaceShows = "place.shows";
        public const string PlaceFillers = "place.fillers";
        public const string PlacePlaced = "place.placed";
        public const string PlaceAsks = "place.asks";
        public const string PlaceWhereItGoes = "place.where_it_goes";
        public const string QuestionOpeners = "question.openers";
        public const string QuestionRequests = "question.requests";
        public const string QuestionFillers = "question.fillers";

        internal static string Fallback(string key, string languageTag) => (key, languageTag) switch
        {
            (PlaceRooms, "pl") => "kuchnia|salon|sypialnia|łazienka|jadalnia|biuro|garaż|korytarz|spiżarnia|balkon|ogród",
            (PlaceRooms, _) => "living room|dining room|laundry room|kitchen|bedroom|bathroom|office|garage|hallway|pantry|balcony|garden",
            (PlaceShows, "pl") => "to jest|tam jest|tu jest|tutaj jest|oto",
            (PlaceShows, _) => "this is|this s|that is|that s|here is|here s|over there|over here",
            (PlaceFillers, "pl") => "jest|mój|moja|moje|nasz|nasza|nasze",
            (PlaceFillers, _) => "is|the|my|our|a|an",
            (PlacePlaced, "pl") => "jest tam|jest tu|jest tutaj",
            (PlacePlaced, _) => "is there|is here|is right there|is right here|is over there|is over here|is right over there",
            (PlaceAsks, "pl") => "gdzie jest|pokaż mi|pokaż|zaprowadź mnie",
            (PlaceAsks, _) => "where is|where s|show me|take me to|lead me to|which way is|point to|point at",
            (PlaceWhereItGoes, "pl") => "gdzie mogę położyć|gdzie mogę odłożyć|gdzie mogę to położyć|gdzie mogę to odłożyć|gdzie to położyć|gdzie to odłożyć|gdzie położyć|gdzie odłożyć|gdzie to schować|gdzie schować",
            (PlaceWhereItGoes, _) => "where can i put|where should i put|where do i put|where to put|where do i keep|where should i keep|where do these go|where do they go|where does this go|where does it go|where can these go|where should these go|where do these belong|where do they belong|where does this belong|where does it belong",
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
            (ActionFillers, "pl") => "nubi|proszę|cześć|hej|ok|okej|teraz|jeszcze raz|dla mnie",
            (ActionFillers, _) => "nubi|please|hi|hey|hello|ok|okay|now|again|for me",
            (ActionNegations, "pl") => "nie|nigdy|bez|stop",
            (ActionNegations, _) => "no|not|never|don|dont|without|stop",
            (ActionJump, "pl") => "skocz|podskocz|skacz|zrób skok",
            (ActionJump, _) => "jump|jump up|do a jump",
            (ActionDance, "pl") => "zatańcz|tańcz|pokaż jak tańczysz|pokaż jak się tańczy|pokaż taniec|zatańczmy",
            (ActionDance, _) => "dance|show me how to dance|show me a dance|show me your dance|do a dance|let's dance|lets dance",
            (ActionSpin, "pl") => "obróć się|zakręć się|zrób obrót|zrób piruet",
            (ActionSpin, _) => "spin|spin around|do a spin|turn around|twirl|do a twirl",
            (ActionBow, "pl") => "ukłoń się|pokłoń się|zrób ukłon",
            (ActionBow, _) => "bow|take a bow|do a bow",
            (ActionWave, "pl") => "pomachaj|pomachaj mi|pomachaj do mnie|zamachaj",
            (ActionWave, _) => "wave|wave at me|wave to me|wave hello|give me a wave",
            (ActionNod, "pl") => "kiwnij głową|skiń głową|pokiwaj głową",
            (ActionNod, _) => "nod|nod your head|give me a nod",
            (ActionCelebrate, "pl") => "świętuj|świętujmy|poświętuj",
            (ActionCelebrate, _) => "celebrate|let's celebrate|lets celebrate",
            (ActionBehind, "pl") => "stań za mną|idź za mnie|przejdź za mnie|stań za mnie",
            (ActionBehind, _) => "go behind me|stand behind me|get behind me|move behind me|go stand behind me",
            (ActionFront, "pl") => "stań przede mną|idź przede mnie|przejdź przede mnie",
            (ActionFront, _) => "go in front of me|stand in front of me|get in front of me|move in front of me|go stand in front of me",
            (ActionLeft, "pl") => "stań po mojej lewej|stań po lewej|idź na moją lewą|stań z lewej|przejdź na lewo ode mnie",
            (ActionLeft, _) => "stand on my left|stand to my left|go to my left|go on my left|move to my left|go stand on my left",
            (ActionRight, "pl") => "stań po mojej prawej|stań po prawej|idź na moją prawą|stań z prawej|przejdź na prawo ode mnie",
            (ActionRight, _) => "stand on my right|stand to my right|go to my right|go on my right|move to my right|go stand on my right",
            (ActionCloser, "pl") => "bliżej|chodź bliżej|podejdź bliżej|przysuń się",
            (ActionCloser, _) => "come closer|move closer|get closer|step closer|closer",
            (ActionAway, "pl") => "odsuń się|cofnij się|odejdź trochę|zrób krok w tył",
            (ActionAway, _) => "back off|back up|step back|move back|move away|step away",
            (ActionConnectors, "pl") => "i|potem|później|następnie|oraz|także|też",
            (ActionConnectors, _) => "and|then|after|also|plus|while|before",
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
