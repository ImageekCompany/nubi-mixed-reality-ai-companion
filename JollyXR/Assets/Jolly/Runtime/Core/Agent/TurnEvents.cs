using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Agent
{
    interface ITurnEventSink
    {
        void Write(AgentEvent e);
    }

    /// <summary>
    /// The single queue of one turn's events: model deltas, tool results and speech that tools stream through
    /// <see cref="ToolContext.Speech"/>. Writes after the turn completed are dropped.
    /// </summary>
    sealed class TurnEvents : ITurnEventSink, ISpokenTextSink
    {
        readonly Channel<AgentEvent> _channel = Channel.CreateSingleConsumerUnbounded<AgentEvent>();

        public ChannelReader<AgentEvent> Reader => _channel.Reader;

        public void Write(AgentEvent e) => _channel.Writer.TryWrite(e);

        public void Append(string delta)
        {
            if (!string.IsNullOrEmpty(delta))
                Write(new AgentEvent(AgentEventKind.SpeechDelta, delta));
        }

        public void Complete() => _channel.Writer.TryComplete();
    }
}
