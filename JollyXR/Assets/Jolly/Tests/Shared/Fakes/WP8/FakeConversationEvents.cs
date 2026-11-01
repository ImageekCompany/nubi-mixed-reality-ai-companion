using System;
using Jolly.Contracts;

namespace Jolly.Testing
{
    public sealed class FakeConversationEvents : IConversationEvents
    {
        public event Action<ConversationState, ConversationState> StateChanged;
        public event Action<string> UserPartial;
        public event Action<string> UserFinal;
        public event Action<int, string> CaptionStarted;
        public event Action<string, string> ToolStarted;
        public event Action<string, string, bool> ToolCompleted;
        public event Action<string, double> ModelCallCompleted;

        public int SubscriberCount =>
            Count(StateChanged) + Count(UserPartial) + Count(UserFinal) + Count(CaptionStarted)
            + Count(ToolStarted) + Count(ToolCompleted) + Count(ModelCallCompleted);

        public void RaiseState(ConversationState from, ConversationState to) => StateChanged?.Invoke(from, to);
        public void RaisePartial(string text) => UserPartial?.Invoke(text);
        public void RaiseFinal(string text) => UserFinal?.Invoke(text);
        public void RaiseCaption(int utteranceId, string text) => CaptionStarted?.Invoke(utteranceId, text);
        public void RaiseToolStarted(string callId, string tool) => ToolStarted?.Invoke(callId, tool);
        public void RaiseToolCompleted(string callId, string tool, bool ok) => ToolCompleted?.Invoke(callId, tool, ok);
        public void RaiseModelCall(string model, double ms) => ModelCallCompleted?.Invoke(model, ms);

        static int Count(Delegate d) => d == null ? 0 : d.GetInvocationList().Length;
    }
}
