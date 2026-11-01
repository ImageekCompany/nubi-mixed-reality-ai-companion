using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>ITextToSpeech that hands out one <see cref="ControlledPcmStream"/> per request and records the requests.</summary>
    public sealed class FakeTextToSpeech : ITextToSpeech
    {
        public sealed class Request
        {
            public string Text;
            public VoiceSpec Voice;
            public CancellationToken Token;
            public ControlledPcmStream Stream;
        }

        readonly List<Request> _requests = new();

        public IReadOnlyList<Request> Requests => _requests;
        public int SampleRate => 24000;
        public int MaxCharacters => 240;
        public bool Supports(string languageTag) => languageTag == "en";

        public IUniTaskAsyncEnumerable<PcmChunk> Synthesize(string text, VoiceSpec voice, CancellationToken ct)
        {
            var request = new Request { Text = text, Voice = voice, Token = ct, Stream = new ControlledPcmStream(SampleRate) };
            _requests.Add(request);
            return request.Stream;
        }
    }
}
