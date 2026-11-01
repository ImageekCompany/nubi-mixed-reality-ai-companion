using System.Threading;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Silences a turn (architecture §5.2): cancel the turn token first, so no late audio is queued, then flush the output
    /// and clear the speech queue, forgetting first samples still in flight. The interrupted memory commit is the agent
    /// loop's job.
    /// </summary>
    public sealed class BargeInCoordinator
    {
        readonly ISpeechOutput _output;
        readonly ISpeechQueue _queue;
        readonly TurnSpeech _speech;
        readonly ILatencyTracker _latency;

        public BargeInCoordinator(ISpeechOutput output, ISpeechQueue queue, TurnSpeech speech, ILatencyTracker latency)
        {
            _output = output;
            _queue = queue;
            _speech = speech;
            _latency = latency;
        }

        /// <summary>A user pinch during a busy turn.</summary>
        public void BargeIn(CancellationTokenSource turnCts, int turnId)
        {
            _latency.Mark(turnId, LatencyMark.BargeIn);
            Interrupt(turnCts);
        }

        /// <summary>Watchdogs and new turns: the same silencing without the latency mark.</summary>
        public void Interrupt(CancellationTokenSource turnCts)
        {
            if (turnCts != null && !turnCts.IsCancellationRequested)
                turnCts.Cancel();
            _output.Flush();
            _queue.Clear();
            _speech.Cancel();
        }
    }
}
