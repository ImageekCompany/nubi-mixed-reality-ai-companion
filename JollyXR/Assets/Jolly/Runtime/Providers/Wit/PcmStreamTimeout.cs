using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;

namespace Jolly.Providers.Wit
{
    /// <summary>
    /// Time-to-first-chunk and inter-chunk idle timeouts for a PCM stream, on <see cref="IClock"/> so they run on the
    /// PlayerLoop (never <c>CancelAfter</c>) and are testable. Time the consumer spends on a chunk does not count.
    /// A timeout cancels the source and surfaces as <see cref="ProviderException"/> with
    /// <see cref="ProviderErrorKind.Timeout"/>; the caller's cancellation stays an <see cref="OperationCanceledException"/>.
    /// </summary>
    public static class PcmStreamTimeout
    {
        public static IUniTaskAsyncEnumerable<PcmChunk> Apply(IUniTaskAsyncEnumerable<PcmChunk> source, IClock clock,
                                                              TimeSpan firstChunk, TimeSpan idle, string provider)
            => UniTaskAsyncEnumerable.Create<PcmChunk>((writer, ct) => Run(source, clock, firstChunk, idle, provider, writer, ct));

        sealed class Deadline
        {
            public double At;
            public bool Expired;
            public bool Received;
        }

        static async UniTask Run(IUniTaskAsyncEnumerable<PcmChunk> source, IClock clock, TimeSpan firstChunk, TimeSpan idle,
                                 string provider, IAsyncWriter<PcmChunk> writer, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var deadline = new Deadline { At = clock.Now + firstChunk.TotalSeconds };
            Watch(clock, deadline, idle, cts).Forget();
            try
            {
                await foreach (var chunk in source.WithCancellation(cts.Token))
                {
                    deadline.At = double.PositiveInfinity;
                    await writer.YieldAsync(chunk);
                    deadline.At = clock.Now + idle.TotalSeconds;
                    deadline.Received = true;
                }
            }
            catch (OperationCanceledException e) when (deadline.Expired && !ct.IsCancellationRequested)
            {
                var stage = deadline.Received ? "inter-chunk idle" : "first chunk";
                throw new ProviderException(provider, ProviderErrorKind.Timeout, stage + " timeout", inner: e);
            }
            finally
            {
                cts.Cancel();
            }
        }

        static async UniTaskVoid Watch(IClock clock, Deadline deadline, TimeSpan idle, CancellationTokenSource cts)
        {
            try
            {
                while (true)
                {
                    var wait = deadline.At - clock.Now;
                    if (wait <= 0)
                        break;
                    var delay = double.IsPositiveInfinity(wait) ? idle : TimeSpan.FromSeconds(wait);
                    await clock.Delay(delay, cts.Token);
                }
                deadline.Expired = true;
                cts.Cancel();
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
