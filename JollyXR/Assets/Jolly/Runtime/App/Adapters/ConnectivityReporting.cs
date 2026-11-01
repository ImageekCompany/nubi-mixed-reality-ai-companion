using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;

namespace Jolly.App.Adapters
{
    /// <summary>
    /// Reports every provider call's outcome to <see cref="IConnectivityMonitor"/> (architecture §5.4). The providers stay
    /// unaware of connectivity; the composition root wraps them with the decorators below.
    /// </summary>
    public static class ConnectivityReporting
    {
        /// <summary>
        /// The first item proves the provider reachable: consumers such as the ASR listener stop reading at the final
        /// transcript, so the end of the stream is often never reached.
        /// </summary>
        public static IUniTaskAsyncEnumerable<T> Observe<T>(IUniTaskAsyncEnumerable<T> source, string provider,
                                                            IConnectivityMonitor monitor) =>
            UniTaskAsyncEnumerable.Create<T>(async (writer, ct) =>
            {
                var reported = false;
                try
                {
                    await foreach (var item in source.WithCancellation(ct))
                    {
                        if (!reported)
                        {
                            reported = true;
                            monitor.ReportSuccess(provider);
                        }
                        await writer.YieldAsync(item);
                    }
                }
                catch (ProviderException e)
                {
                    monitor.ReportFailure(e.Provider ?? provider, e.Kind);
                    throw;
                }
                if (!reported)
                    monitor.ReportSuccess(provider);
            });

        public static async UniTask<T> Observe<T>(UniTask<T> call, string provider, IConnectivityMonitor monitor)
        {
            T result;
            try
            {
                result = await call;
            }
            catch (ProviderException e)
            {
                monitor.ReportFailure(e.Provider ?? provider, e.Kind);
                throw;
            }
            monitor.ReportSuccess(provider);
            return result;
        }
    }

    public sealed class ReportingChatModel : IChatModel
    {
        readonly IChatModel _inner;
        readonly string _provider;
        readonly IConnectivityMonitor _monitor;

        public ReportingChatModel(IChatModel inner, string provider, IConnectivityMonitor monitor)
        {
            _inner = inner;
            _provider = provider;
            _monitor = monitor;
        }

        public IUniTaskAsyncEnumerable<ModelEvent> Stream(ModelRequest request, CancellationToken ct) =>
            ConnectivityReporting.Observe(_inner.Stream(request, ct), _provider, _monitor);
    }

    public sealed class ReportingSpeechToText : ISpeechToText
    {
        readonly ISpeechToText _inner;
        readonly string _provider;
        readonly IConnectivityMonitor _monitor;

        public ReportingSpeechToText(ISpeechToText inner, string provider, IConnectivityMonitor monitor)
        {
            _inner = inner;
            _provider = provider;
            _monitor = monitor;
        }

        public IUniTaskAsyncEnumerable<TranscriptEvent> Transcribe(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options,
                                                                    CancellationToken ct) =>
            ConnectivityReporting.Observe(_inner.Transcribe(audio, options, ct), _provider, _monitor);
    }

    public sealed class ReportingTextToSpeech : ITextToSpeech
    {
        readonly ITextToSpeech _inner;
        readonly string _provider;
        readonly IConnectivityMonitor _monitor;

        public ReportingTextToSpeech(ITextToSpeech inner, string provider, IConnectivityMonitor monitor)
        {
            _inner = inner;
            _provider = provider;
            _monitor = monitor;
        }

        public int SampleRate => _inner.SampleRate;
        public int MaxCharacters => _inner.MaxCharacters;
        public bool Supports(string languageTag) => _inner.Supports(languageTag);

        public IUniTaskAsyncEnumerable<PcmChunk> Synthesize(string text, VoiceSpec voice, CancellationToken ct) =>
            ConnectivityReporting.Observe(_inner.Synthesize(text, voice, ct), _provider, _monitor);
    }

    public sealed class ReportingSegmenter : ISegmenter
    {
        readonly ISegmenter _inner;
        readonly string _provider;
        readonly IConnectivityMonitor _monitor;

        public ReportingSegmenter(ISegmenter inner, string provider, IConnectivityMonitor monitor)
        {
            _inner = inner;
            _provider = provider;
            _monitor = monitor;
        }

        public UniTask<IReadOnlyList<SegmentedObject>> Segment(EncodedImage image, SegmentPrompt prompt, CancellationToken ct) =>
            ConnectivityReporting.Observe(_inner.Segment(image, prompt, ct), _provider, _monitor);
    }
}
