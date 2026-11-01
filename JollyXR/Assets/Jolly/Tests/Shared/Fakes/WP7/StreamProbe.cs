using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.Testing
{
    /// <summary>Consumes a stream in the background and records what happened, so synchronous tests can assert on it.</summary>
    public sealed class StreamProbe<T>
    {
        readonly List<T> _items = new();

        StreamProbe() { }

        public IReadOnlyList<T> Items => _items;
        public bool Completed { get; private set; }
        public bool Canceled { get; private set; }
        public Exception Error { get; private set; }
        public bool IsRunning => !Completed && !Canceled && Error == null;

        public static StreamProbe<T> Start(IUniTaskAsyncEnumerable<T> stream, CancellationToken ct = default)
        {
            var probe = new StreamProbe<T>();
            probe.Run(stream, ct).Forget();
            return probe;
        }

        async UniTaskVoid Run(IUniTaskAsyncEnumerable<T> stream, CancellationToken ct)
        {
            try
            {
                await foreach (var item in stream.WithCancellation(ct))
                    _items.Add(item);
                Completed = true;
            }
            catch (OperationCanceledException)
            {
                Canceled = true;
            }
            catch (Exception e)
            {
                Error = e;
            }
        }
    }
}
