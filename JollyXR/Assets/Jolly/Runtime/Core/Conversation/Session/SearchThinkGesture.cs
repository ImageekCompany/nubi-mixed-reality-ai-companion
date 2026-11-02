using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// While the provider searches the web (ADR-0048) the character strikes its Think gesture, so the pause before the
    /// answer reads as working, not stalled. The gesture fades out when the search ends or the turn is interrupted
    /// (Listening) or over (Idle); the Thinking pose of the next state takes over from there.
    /// </summary>
    public sealed class SearchThinkGesture : IDisposable
    {
        readonly IConversationEvents _events;
        readonly ICharacterController _character;
        CancellationTokenSource _gesture;

        public SearchThinkGesture(IConversationEvents events, ICharacterController character)
        {
            _events = events;
            _character = character;
            _events.ToolStarted += OnToolStarted;
            _events.ToolCompleted += OnToolCompleted;
            _events.StateChanged += OnStateChanged;
        }

        public void Dispose()
        {
            _events.ToolStarted -= OnToolStarted;
            _events.ToolCompleted -= OnToolCompleted;
            _events.StateChanged -= OnStateChanged;
            End();
        }

        void OnToolStarted(string callId, string name)
        {
            if (name != ToolNames.WebSearch || !_character.CanPlay(GestureId.Think))
                return;
            End();
            _gesture = new CancellationTokenSource();
            _character.PlayGesture(GestureId.Think, _gesture.Token).SuppressCancellationThrow().Forget();
        }

        void OnToolCompleted(string callId, string name, bool ok)
        {
            if (name == ToolNames.WebSearch)
                End();
        }

        void OnStateChanged(ConversationState from, ConversationState to)
        {
            if (to is ConversationState.Listening or ConversationState.Idle)
                End();
        }

        void End()
        {
            if (_gesture == null)
                return;
            _gesture.Cancel();
            _gesture.Dispose();
            _gesture = null;
        }
    }
}
