using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>Single-enumeration PCM stream driven by the test: chunks, completion and errors arrive on demand.</summary>
    public sealed class ControlledPcmStream : IUniTaskAsyncEnumerable<PcmChunk>
    {
        readonly Channel<PcmChunk> _channel = Channel.CreateSingleConsumerUnbounded<PcmChunk>();

        public ControlledPcmStream(int sampleRate = 24000) => SampleRate = sampleRate;

        public int SampleRate { get; }
        public bool Started { get; private set; }
        public CancellationToken Token { get; private set; }

        public ControlledPcmStream Emit(params short[] samples)
        {
            var chunk = PcmChunk.Rent(samples.Length, SampleRate);
            samples.CopyTo(chunk.Buffer, 0);
            _channel.Writer.TryWrite(chunk);
            return this;
        }

        public ControlledPcmStream EmitConstant(int length, short value)
        {
            var chunk = PcmChunk.Rent(length, SampleRate);
            Array.Fill(chunk.Buffer, value, 0, length);
            _channel.Writer.TryWrite(chunk);
            return this;
        }

        public void Complete() => _channel.Writer.TryComplete();

        public void Fail(Exception error)
        {
            _channel.Writer.TryComplete(error);
            // Observe the faulted Completion so its finalizer doesn't report the error as unobserved in a later test.
            var completion = _channel.Reader.Completion;
            if (!completion.Status.IsCompleted())
                return;
            try { completion.GetAwaiter().GetResult(); }
            catch (Exception) { }
        }

        public IUniTaskAsyncEnumerator<PcmChunk> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            Started = true;
            Token = cancellationToken;
            return _channel.Reader.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        }
    }
}
