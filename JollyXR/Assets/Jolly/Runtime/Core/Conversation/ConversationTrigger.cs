namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Inputs of <see cref="ConversationStateMachine"/> (contracts §9). <see cref="SpeechQueued"/> latches SpeechPending
    /// when a sentence is queued (architecture §6); <see cref="Announce"/> starts unprompted speech from Idle, such as the
    /// greeting after a persona switch.
    /// </summary>
    public enum ConversationTrigger
    {
        PttPressed, OfflinePttPressed, FinalText, FinalEmpty, SttFailed, ToolStarted, ToolsDone, FirstAudio,
        SpeechDrained, TurnCompleted, TurnFailed, Watchdog, SpeechQueued, Announce,
    }
}
