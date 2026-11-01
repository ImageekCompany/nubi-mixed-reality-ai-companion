using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using System.Threading;
using Jolly.Contracts;

namespace Jolly.Tests.PlayMode.E2E.Doubles
{
    /// <summary>Wit at its edge: 40 ms of quiet tone per character, streamed in 100 ms chunks over frames.</summary>
    public sealed class ToneTextToSpeech : ITextToSpeech
    {
        const int Rate = 24000;
        const double SecondsPerChar = 0.04;
        const int ChunkSamples = Rate / 10;

        public List<string> Texts { get; } = new();
        public int SampleRate => Rate;
        public int MaxCharacters => 240;
        public bool Supports(string languageTag) => true;

        public IUniTaskAsyncEnumerable<PcmChunk> Synthesize(string text, VoiceSpec voice, CancellationToken ct)
        {
            Texts.Add(text);
            return UniTaskAsyncEnumerable.Create<PcmChunk>(async (writer, token) =>
            {
                var remaining = (int)(text.Length * SecondsPerChar * Rate);
                while (remaining > 0)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    var length = Math.Min(remaining, ChunkSamples);
                    var chunk = PcmChunk.Rent(length, Rate);
                    for (var i = 0; i < length; i++)
                        chunk.Buffer[i] = (short)((i & 64) == 0 ? 600 : -600);
                    remaining -= length;
                    await writer.YieldAsync(chunk);
                }
            });
        }
    }
}
