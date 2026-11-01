using System;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>The Core side of <see cref="IConversationEvents"/>: Core raises, views subscribe. Main thread only.</summary>
    public sealed class ConversationEvents : IConversationEvents
    {
        public event Action<ConversationState, ConversationState> StateChanged;
        public event Action<string> UserPartial;
        public event Action<string> UserFinal;
        public event Action<int, string> CaptionStarted;
        public event Action<string, string> ToolStarted;
        public event Action<string, string, bool> ToolCompleted;
        public event Action<string, double> ModelCallCompleted;

        public void RaiseStateChanged(ConversationState from, ConversationState to) => StateChanged?.Invoke(from, to);
        public void RaiseUserPartial(string text) => UserPartial?.Invoke(text);
        public void RaiseUserFinal(string text) => UserFinal?.Invoke(text);
        public void RaiseCaptionStarted(int utteranceId, string text) => CaptionStarted?.Invoke(utteranceId, text);
        public void RaiseToolStarted(string callId, string name) => ToolStarted?.Invoke(callId, name);
        public void RaiseToolCompleted(string callId, string name, bool ok) => ToolCompleted?.Invoke(callId, name, ok);
        public void RaiseModelCallCompleted(string model, double ms) => ModelCallCompleted?.Invoke(model, ms);
    }
}
