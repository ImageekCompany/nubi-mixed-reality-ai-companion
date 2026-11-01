using System;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>Shows every state change to the views (<see cref="IConversationEvents.StateChanged"/>) and to the character.</summary>
    public sealed class ConversationStateRelay : IDisposable
    {
        readonly ConversationStateMachine _machine;
        readonly ConversationEvents _events;
        readonly ICharacterController _character;

        public ConversationStateRelay(ConversationStateMachine machine, ConversationEvents events, ICharacterController character)
        {
            _machine = machine;
            _events = events;
            _character = character;
            _machine.Changed += OnChanged;
        }

        public void Dispose() => _machine.Changed -= OnChanged;

        void OnChanged(ConversationState from, ConversationState to)
        {
            _character.SetConversationState(to);
            _events.RaiseStateChanged(from, to);
        }
    }
}
