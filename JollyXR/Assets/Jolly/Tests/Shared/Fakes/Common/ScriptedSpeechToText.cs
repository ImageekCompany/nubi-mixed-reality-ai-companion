using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>Yields <see cref="Partials"/> at once, then the <see cref="Final"/> when the audio completes (PTT release).</summary>
    public sealed class ScriptedSpeechToText : ISpeechToText
    {
        public List<string> Partials { get; } = new();
        public string Final { get; set; } = "Hi newbie";
        public ProviderException Failure { get; set; }
        /// <summary>Never sends the final (the Listening watchdog case).</summary>
        public bool Hang { get; set; }
        public SttOptions LastOptions { get; private set; }

        public IUniTaskAsyncEnumerable<TranscriptEvent> Transcribe(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options,
                                                                   CancellationToken ct)
        {
            LastOptions = options;
            return UniTaskAsyncEnumerable.Create<TranscriptEvent>(async (writer, enumeratorCt) =>
            {
                foreach (var partial in Partials)
                    await writer.YieldAsync(new TranscriptEvent(TranscriptKind.Partial, partial, 0));
                await foreach (var chunk in audio.WithCancellation(enumeratorCt))
                    chunk.Dispose();
                if (Failure != null)
                    throw Failure;
                if (Hang)
                    await UniTask.Never(enumeratorCt);
                await writer.YieldAsync(new TranscriptEvent(TranscriptKind.Final, Final, 0));
            });
        }
    }
}
