using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// One timer per busy state (architecture §6). Entering a state re-arms it; Listening is armed only at PTT release,
    /// because a long hold is not a hang. Expiry reports the state it was armed for.
    /// </summary>
    public sealed class TurnWatchdog : IDisposable
    {
        readonly IClock _clock;
        readonly ConversationTimings _timings;
        readonly Action<ConversationState> _expired;
        CancellationTokenSource _cts;

        public TurnWatchdog(IClock clock, ConversationTimings timings, Action<ConversationState> expired)
        {
            _clock = clock;
            _timings = timings;
            _expired = expired;
        }

        public void Enter(ConversationState state)
        {
            switch (state)
            {
                case ConversationState.Thinking: Arm(state, _timings.Thinking); break;
                case ConversationState.Acting: Arm(state, _timings.Acting); break;
                case ConversationState.Speaking: Arm(state, _timings.Speaking); break;
                default: Disarm(); break;
            }
        }

        public void Released() => Arm(ConversationState.Listening, _timings.Listening);

        public void Disarm()
        {
            if (_cts == null)
                return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        public void Dispose() => Disarm();

        void Arm(ConversationState state, TimeSpan limit)
        {
            Disarm();
            _cts = new CancellationTokenSource();
            Wait(state, limit, _cts.Token).Forget();
        }

        async UniTaskVoid Wait(ConversationState state, TimeSpan limit, CancellationToken ct)
        {
            if (await _clock.Delay(limit, ct).SuppressCancellationThrow())
                return;
            _expired(state);
        }
    }
}
