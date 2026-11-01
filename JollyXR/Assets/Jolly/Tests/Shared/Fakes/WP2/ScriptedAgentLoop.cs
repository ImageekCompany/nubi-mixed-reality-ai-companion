using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;

namespace Jolly.Testing.Conversation
{
    /// <summary>
    /// Plays a script of agent events. <see cref="Hold"/> inserts a gate that waits for <see cref="Release"/> (or the
    /// token), so a test can act mid-turn; a cancelled turn records the spoken prefix like the real loop commits it.
    /// </summary>
    public sealed class ScriptedAgentLoop : IAgentLoop
    {
        readonly List<AgentEvent?> _script = new();
        readonly Queue<UniTaskCompletionSource> _gates = new();

        public List<UserTurn> Turns { get; } = new();
        public string InterruptedSpoken { get; private set; }
        public Func<string> SpokenSoFar { get; private set; }
        public int Waiting => _gates.Count;

        public ScriptedAgentLoop Say(string text) => Add(new AgentEvent(AgentEventKind.SpeechDelta, text));

        public ScriptedAgentLoop ToolStarted(string name, string callId = "call_1") =>
            Add(new AgentEvent(AgentEventKind.ToolStarted, toolName: name, callId: callId));

        public ScriptedAgentLoop ToolCompleted(string name, string callId = "call_1", bool ok = true) =>
            Add(new AgentEvent(AgentEventKind.ToolCompleted, toolName: name, callId: callId,
                toolResult: ok ? ToolResult.Success("{}") : ToolResult.Failure("failed")));

        public ScriptedAgentLoop Fail(ProviderErrorKind kind = ProviderErrorKind.Server) =>
            Add(new AgentEvent(AgentEventKind.Failed, error: new ProviderException("chat", kind, "scripted")));

        public ScriptedAgentLoop Hold()
        {
            _script.Add(null);
            return this;
        }

        public void Release() => _gates.Dequeue().TrySetResult();

        public IUniTaskAsyncEnumerable<AgentEvent> RunTurn(UserTurn turn, Func<string> spokenSoFar, CancellationToken ct)
        {
            Turns.Add(turn);
            SpokenSoFar = spokenSoFar;
            var script = new List<AgentEvent?>(_script);
            _script.Clear();
            return UniTaskAsyncEnumerable.Create<AgentEvent>(async (writer, enumeratorCt) =>
            {
                try
                {
                    foreach (var step in script)
                    {
                        enumeratorCt.ThrowIfCancellationRequested();
                        if (step.HasValue)
                        {
                            await writer.YieldAsync(step.Value);
                            continue;
                        }
                        var gate = new UniTaskCompletionSource();
                        _gates.Enqueue(gate);
                        await gate.Task.AttachExternalCancellation(enumeratorCt);
                    }
                    var ended = !script.Exists(s => s.HasValue && s.Value.Kind == AgentEventKind.Failed);
                    if (ended)
                        await writer.YieldAsync(new AgentEvent(AgentEventKind.Completed));
                }
                catch (OperationCanceledException)
                {
                    InterruptedSpoken = spokenSoFar();
                    throw;
                }
            });
        }

        ScriptedAgentLoop Add(AgentEvent e)
        {
            _script.Add(e);
            return this;
        }
    }

    public sealed class FakeConnectivityMonitor : IConnectivityMonitor
    {
        public Connectivity State { get; set; } = Connectivity.Online;
        public event Action<Connectivity> Changed { add { } remove { } }
        public void ReportSuccess(string provider) { }
        public void ReportFailure(string provider, ProviderErrorKind kind) { }
        public UniTask<Connectivity> ProbeNow(CancellationToken ct) => UniTask.FromResult(State);
        public void Dispose() { }
    }

    public sealed class FakeUsageMeter : IApiUsageMeter
    {
        public decimal SpentUsd => 0m;
        public decimal HardCapUsd => 1m;
        public bool IsCapReached { get; set; }
        public event Action<decimal> SpentChanged { add { } remove { } }

        public bool TryReserve(decimal estimatedUsd, out long reservationId)
        {
            reservationId = 1;
            return !IsCapReached;
        }

        public decimal Record(long reservationId, in UsageRecord usage) => 0m;
        public decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens) => 0m;
        public void Flush() { }
        public void Dispose() { }
    }
}
