using System;
using Jolly.Contracts;
using S = Jolly.Contracts.ConversationState;
using T = Jolly.Core.Conversation.ConversationTrigger;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Pure, table-driven conversation state (architecture §6). Triggers first update the three latched flags, then the
    /// first matching row whose guard holds is taken, so <see cref="Fire"/> needs no payload.
    /// Beyond the architecture table it adds: Speaking → Thinking when speech drains mid-turn, Thinking → Idle when a turn
    /// ends with nothing queued or running, busy → Speaking on an offline press, and Idle → Speaking on Announce.
    /// </summary>
    public sealed class ConversationStateMachine
    {
        enum Guard { None, ToolsRunning, NotTurnEnded, TurnEndedNoTools, TurnEndedNoSpeech, NotEndedNoTools, Quiet }

        readonly struct Row
        {
            public Row(S from, T trigger, S to, Guard guard = Guard.None) { From = from; Trigger = trigger; To = to; Guard = guard; }
            public S From { get; }
            public T Trigger { get; }
            public S To { get; }
            public Guard Guard { get; }
        }

        static readonly Row[] Table =
        {
            new(S.Idle, T.PttPressed, S.Listening),
            new(S.Idle, T.OfflinePttPressed, S.Speaking),
            new(S.Idle, T.Announce, S.Speaking),

            new(S.Listening, T.FinalText, S.Thinking),
            new(S.Listening, T.FinalEmpty, S.Idle),
            new(S.Listening, T.SttFailed, S.Idle),
            new(S.Listening, T.Watchdog, S.Idle),

            new(S.Thinking, T.PttPressed, S.Listening),
            new(S.Thinking, T.OfflinePttPressed, S.Speaking),
            new(S.Thinking, T.FirstAudio, S.Speaking),
            new(S.Thinking, T.ToolStarted, S.Acting),
            new(S.Thinking, T.TurnFailed, S.Speaking),
            new(S.Thinking, T.TurnCompleted, S.Idle, Guard.Quiet),
            new(S.Thinking, T.SpeechDrained, S.Idle, Guard.TurnEndedNoTools),
            new(S.Thinking, T.Watchdog, S.Idle),

            new(S.Acting, T.PttPressed, S.Listening),
            new(S.Acting, T.OfflinePttPressed, S.Speaking),
            new(S.Acting, T.FirstAudio, S.Speaking),
            new(S.Acting, T.ToolsDone, S.Thinking, Guard.NotTurnEnded),
            new(S.Acting, T.ToolsDone, S.Idle, Guard.TurnEndedNoSpeech),
            new(S.Acting, T.TurnFailed, S.Speaking),
            new(S.Acting, T.Watchdog, S.Idle),

            new(S.Speaking, T.PttPressed, S.Listening),
            new(S.Speaking, T.OfflinePttPressed, S.Speaking),
            new(S.Speaking, T.SpeechDrained, S.Acting, Guard.ToolsRunning),
            new(S.Speaking, T.SpeechDrained, S.Idle, Guard.TurnEndedNoTools),
            new(S.Speaking, T.SpeechDrained, S.Thinking, Guard.NotEndedNoTools),
            new(S.Speaking, T.TurnFailed, S.Speaking),
            new(S.Speaking, T.Watchdog, S.Idle),
        };

        readonly ILog _log;

        public ConversationStateMachine(ILog log = null) => _log = log;

        public ConversationState State { get; private set; } = S.Idle;
        public bool ToolsRunning { get; private set; }
        public bool SpeechPending { get; private set; }
        public bool TurnEnded { get; private set; }

        public event Action<ConversationState, ConversationState> Changed;

        public bool Fire(ConversationTrigger trigger)
        {
            Latch(trigger);
            foreach (var row in Table)
            {
                if (row.From != State || row.Trigger != trigger || !Holds(row.Guard))
                    continue;
                if (trigger is T.PttPressed or T.OfflinePttPressed or T.Announce)
                    ResetLatches(trigger);
                var from = State;
                State = row.To;
                if (from != row.To)
                    Changed?.Invoke(from, row.To);
                return true;
            }
            if (_log != null && _log.IsEnabled(LogLevel.Debug))
                _log.Write(LogLevel.Debug, "conversation", $"evt=state.ignored state={State} trigger={trigger}");
            return false;
        }

        void Latch(T trigger)
        {
            switch (trigger)
            {
                case T.ToolStarted: ToolsRunning = true; break;
                case T.ToolsDone: ToolsRunning = false; break;
                case T.SpeechQueued:
                case T.FirstAudio: SpeechPending = true; break;
                case T.SpeechDrained: SpeechPending = false; break;
                case T.TurnCompleted:
                case T.TurnFailed: TurnEnded = true; break;
            }
        }

        void ResetLatches(T trigger)
        {
            ToolsRunning = false;
            TurnEnded = false;
            SpeechPending = trigger == T.OfflinePttPressed || trigger == T.Announce;
        }

        bool Holds(Guard guard) => guard switch
        {
            Guard.ToolsRunning => ToolsRunning,
            Guard.NotTurnEnded => !TurnEnded,
            Guard.TurnEndedNoTools => TurnEnded && !ToolsRunning,
            Guard.TurnEndedNoSpeech => TurnEnded && !SpeechPending,
            Guard.NotEndedNoTools => !TurnEnded && !ToolsRunning,
            Guard.Quiet => !SpeechPending && !ToolsRunning,
            _ => true,
        };
    }
}
