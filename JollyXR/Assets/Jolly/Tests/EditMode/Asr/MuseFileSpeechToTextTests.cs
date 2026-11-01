using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Asr;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Asr
{
    public sealed class MuseFileSpeechToTextTests
    {
        FakeClock _clock;
        FakeFileClient _client;
        FakeUsageMeter _meter;
        InMemorySecrets _secrets;
        Channel<PcmChunk> _audio;
        MuseFileSpeechToText _stt;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _client = new FakeFileClient();
            _meter = new FakeUsageMeter();
            _secrets = new InMemorySecrets().With(SecretKeys.MetaApiKey, "KEY");
            _audio = Channel.CreateSingleConsumerUnbounded<PcmChunk>();
            _stt = new MuseFileSpeechToText(_client, _secrets, _meter, _clock, new MuseAsrOptions("muse-voice-transcribe-1.0"));
        }

        [Test]
        public void Audio_IsBufferedUntilTheSourceCompletesThenPostedOnceAsWav()
        {
            Transcribe();
            WriteChunk(1920);
            WriteChunk(960);
            var postedEarly = _client.Calls;

            _audio.Writer.TryComplete();

            Assert.That(postedEarly, Is.Zero);
            Assert.That(_client.Calls, Is.EqualTo(1));
            Assert.That(_client.Uri.AbsoluteUri, Is.EqualTo("https://api.meta.ai/v1/asr/transcribe"));
            Assert.That(_client.ApiKey, Is.EqualTo("KEY"));
            Assert.That(_client.ContentType, Does.StartWith("multipart/form-data; boundary="));
            Assert.That(_client.Body.Length, Is.GreaterThan(44 + 2880 * 2));
        }

        [Test]
        public void Response_YieldsExactlyOneNormalisedFinal()
        {
            var events = Transcribe();
            _audio.Writer.TryComplete();

            _client.Respond("{\"transcript\":\"what is this。\",\"audioDurationMs\":1200}");

            Assert.That(events.Succeeded, Is.True);
            Assert.That(events.Items.Single().Kind, Is.EqualTo(TranscriptKind.Final));
            Assert.That(events.Items.Single().Text, Is.EqualTo("what is this."));
        }

        [Test]
        public void Usage_IsSettledWithTheBufferedAudioSeconds()
        {
            Transcribe();
            WriteChunk(24000);
            _audio.Writer.TryComplete();

            _client.Respond("{\"transcript\":\"\"}");

            Assert.That(_meter.Records.Single().AudioSeconds, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(_meter.OpenReservations, Is.Zero);
        }

        [Test]
        public void SlowServer_TimesOut()
        {
            var events = Transcribe();
            _audio.Writer.TryComplete();

            _clock.Advance(TimeSpan.FromSeconds(10));

            Assert.That(events.Error, Is.InstanceOf<ProviderException>());
            Assert.That(((ProviderException)events.Error).Kind, Is.EqualTo(ProviderErrorKind.Timeout));
        }

        [Test]
        public void ClientError_Propagates()
        {
            var events = Transcribe();
            _audio.Writer.TryComplete();

            _client.Fail(new ProviderException("meta.asr", ProviderErrorKind.Unauthorized, "401"));

            Assert.That(((ProviderException)events.Error).Kind, Is.EqualTo(ProviderErrorKind.Unauthorized));
        }

        [Test]
        public void MissingKey_FailsUnauthorizedWithoutPosting()
        {
            _stt = new MuseFileSpeechToText(_client, new InMemorySecrets(), _meter, _clock, new MuseAsrOptions("m"));

            var events = Transcribe();

            Assert.That(((ProviderException)events.Error).Kind, Is.EqualTo(ProviderErrorKind.Unauthorized));
            Assert.That(_client.Calls, Is.Zero);
        }

        StreamCollector<TranscriptEvent> Transcribe() =>
            new(_stt.Transcribe(_audio.Reader.ReadAllAsync(), new SttOptions(), CancellationToken.None));

        void WriteChunk(int samples) => _audio.Writer.TryWrite(PcmChunk.Rent(samples, 24000));

        sealed class FakeFileClient : IAsrFileClient
        {
            UniTaskCompletionSource<string> _response;

            public int Calls { get; private set; }
            public Uri Uri { get; private set; }
            public byte[] Body { get; private set; }
            public string ContentType { get; private set; }
            public string ApiKey { get; private set; }

            public UniTask<string> PostAsync(Uri uri, byte[] body, string contentType, string apiKey, CancellationToken ct)
            {
                Calls++;
                Uri = uri;
                Body = body;
                ContentType = contentType;
                ApiKey = apiKey;
                var response = new UniTaskCompletionSource<string>();
                ct.Register(() => response.TrySetCanceled(ct));
                _response = response;
                return response.Task;
            }

            public void Respond(string json) => _response.TrySetResult(json);
            public void Fail(Exception e) => _response.TrySetException(e);
        }
    }
}
