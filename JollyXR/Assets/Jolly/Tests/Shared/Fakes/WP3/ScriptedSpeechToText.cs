using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>
    /// ISpeechToText that drains the audio (disposing every chunk), then yields the scripted partials and a Final, or
    /// throws the scripted failure. Mirrors the real adapter's ordering: the Final comes only after the source completes.
    /// </summary>
    public sealed class ScriptedSpeechToText : ISpeechToText
    {
        readonly List<string> _partials = new();
        string _final = string.Empty;
        Exception _failure;

        public int Calls { get; private set; }
        public int SamplesReceived { get; private set; }
        public SttOptions LastOptions { get; private set; }

        public ScriptedSpeechToText WithPartials(params string[] partials)
        {
            _partials.AddRange(partials);
            return this;
        }

        public ScriptedSpeechToText WithFinal(string text)
        {
            _final = text;
            return this;
        }

        public ScriptedSpeechToText Failing(ProviderErrorKind kind)
        {
            _failure = new ProviderException("scripted.asr", kind, "scripted failure");
            return this;
        }

        public IUniTaskAsyncEnumerable<TranscriptEvent> Transcribe(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options,
                                                                   CancellationToken ct)
        {
            Calls++;
            LastOptions = options;
            return UniTaskAsyncEnumerable.Create<TranscriptEvent>(async (writer, token) =>
            {
                await foreach (var chunk in audio.WithCancellation(ct))
                {
                    SamplesReceived += chunk.Length;
                    chunk.Dispose();
                }
                if (_failure != null)
                    throw _failure;
                foreach (var p in _partials)
                    await writer.YieldAsync(new TranscriptEvent(TranscriptKind.Partial, p, 0));
                await writer.YieldAsync(new TranscriptEvent(TranscriptKind.Final, _final, 0));
            });
        }
    }
}
