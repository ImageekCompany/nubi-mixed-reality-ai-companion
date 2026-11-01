using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.Testing
{
    /// <summary>
    /// Consumes a stream from the moment it is constructed. Producers that complete synchronously (channels, completion
    /// sources) deliver straight into <see cref="Items"/>, so a test can assert between steps without a PlayerLoop.
    /// </summary>
    public sealed class StreamCollector<T>
    {
        readonly List<T> _items = new();
        readonly UniTask _completion;

        public StreamCollector(IUniTaskAsyncEnumerable<T> stream, CancellationToken ct = default)
        {
            _completion = RunAsync(stream, ct).Preserve();
        }

        public IReadOnlyList<T> Items => _items;
        public bool IsCompleted => _completion.Status != UniTaskStatus.Pending;
        public bool Succeeded => _completion.Status == UniTaskStatus.Succeeded;

        /// <summary>The exception the stream ended with, or null while pending or after a normal end.</summary>
        public Exception Error
        {
            get
            {
                if (_completion.Status is UniTaskStatus.Pending or UniTaskStatus.Succeeded)
                    return null;
                try
                {
                    _completion.GetAwaiter().GetResult();
                    return null;
                }
                catch (Exception e)
                {
                    return e;
                }
            }
        }

        async UniTask RunAsync(IUniTaskAsyncEnumerable<T> stream, CancellationToken ct)
        {
            await foreach (var item in stream.WithCancellation(ct))
                _items.Add(item);
        }
    }
}
