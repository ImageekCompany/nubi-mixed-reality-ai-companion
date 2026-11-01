using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Tests.EditMode.Vision.Doubles
{
    /// <summary>Replays scripted <see cref="ModelEvent"/>s; optionally hangs afterwards until cancelled.</summary>
    sealed class ScriptedVisionChatModel : IChatModel
    {
        readonly IReadOnlyList<ModelEvent> _events;
        readonly bool _hangAtEnd;

        public ScriptedVisionChatModel(IReadOnlyList<ModelEvent> events, bool hangAtEnd = false)
        {
            _events = events;
            _hangAtEnd = hangAtEnd;
        }

        public ModelRequest LastRequest { get; private set; }
        public bool StreamDisposed { get; private set; }

        public static ScriptedVisionChatModel Replying(params string[] deltas)
        {
            var events = new List<ModelEvent> { new(ModelEventKind.Started) };
            foreach (var d in deltas)
                events.Add(new ModelEvent(ModelEventKind.TextDelta, d));
            events.Add(new ModelEvent(ModelEventKind.Completed, result: new ModelTurnResult { FinishReason = FinishReason.Stop }));
            return new ScriptedVisionChatModel(events);
        }

        public IUniTaskAsyncEnumerable<ModelEvent> Stream(ModelRequest request, CancellationToken ct)
        {
            LastRequest = request;
            return new Enumerable(this, ct);
        }

        sealed class Enumerable : IUniTaskAsyncEnumerable<ModelEvent>
        {
            readonly ScriptedVisionChatModel _owner;
            readonly CancellationToken _ct;

            public Enumerable(ScriptedVisionChatModel owner, CancellationToken ct) { _owner = owner; _ct = ct; }

            public IUniTaskAsyncEnumerator<ModelEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
                new Enumerator(_owner, CancellationTokenSource.CreateLinkedTokenSource(_ct, cancellationToken));
        }

        sealed class Enumerator : IUniTaskAsyncEnumerator<ModelEvent>
        {
            readonly ScriptedVisionChatModel _owner;
            readonly CancellationTokenSource _cts;
            int _index = -1;

            public Enumerator(ScriptedVisionChatModel owner, CancellationTokenSource cts) { _owner = owner; _cts = cts; }

            public ModelEvent Current => _owner._events[_index];

            public UniTask<bool> MoveNextAsync()
            {
                if (_cts.IsCancellationRequested)
                    return UniTask.FromCanceled<bool>(_cts.Token);
                if (_index + 1 < _owner._events.Count)
                {
                    _index++;
                    return UniTask.FromResult(true);
                }
                return _owner._hangAtEnd ? UniTask.Never<bool>(_cts.Token) : UniTask.FromResult(false);
            }

            public UniTask DisposeAsync()
            {
                _owner.StreamDisposed = true;
                _cts.Dispose();
                return UniTask.CompletedTask;
            }
        }
    }
}
