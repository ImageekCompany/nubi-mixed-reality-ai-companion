using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// While the provider searches the web (ADR-0048) the character strikes its Think gesture, so the pause before the
    /// answer reads as working, not stalled. A search that ends in Acting leads to Thinking, whose pose is the same clip:
    /// the gesture keeps playing over it until Thinking ends. Fading it out at the search's end, while the pose was still
    /// fading in below, dropped the hands 10 cm of their 23 cm for a moment. A search that ends while the character
    /// speaks ends the gesture there, and any other state does at once.
    /// </summary>
    public sealed class SearchThinkGesture : IDisposable
    {
        readonly IConversationEvents _events;
        readonly ICharacterController _character;
        CancellationTokenSource _gesture;
        ConversationState _state;
        bool _searching;

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
            _searching = true;
            _gesture = new CancellationTokenSource();
            _character.PlayGesture(GestureId.Think, _gesture.Token).SuppressCancellationThrow().Forget();
        }

        void OnToolCompleted(string callId, string name, bool ok)
        {
            if (name != ToolNames.WebSearch)
                return;
            _searching = false;
            if (_state != ConversationState.Acting)
                End();
        }

        void OnStateChanged(ConversationState from, ConversationState to)
        {
            _state = to;
            if (to is ConversationState.Listening or ConversationState.Idle
                || (to == ConversationState.Speaking && !_searching))
                End();
        }

        void End()
        {
            _searching = false;
            if (_gesture == null)
                return;
            _gesture.Cancel();
            _gesture.Dispose();
            _gesture = null;
        }
    }
}
