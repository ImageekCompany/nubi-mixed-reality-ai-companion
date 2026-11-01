using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// <see cref="IObjectIdentifier"/> over the chat model (Spark vision, LiveUser route): asks for a
    /// <c>LABEL|NOUN_PHRASE</c> first line and streams the rest as speakable answer text. Usage is recorded by the chat
    /// model. A hand-written enumerator rather than <c>UniTaskAsyncEnumerable.Create</c>, so a consumer that stops early
    /// disposes the model stream (and aborts its request) instead of leaving a producer parked on a yield.
    /// </summary>
    public sealed class SparkObjectIdentifier : IObjectIdentifier
    {
        const string Provider = "meta.vision";
        const string LogCategory = "vision";

        readonly IChatModel _chat;
        readonly IClock _clock;
        readonly ILog _log;
        readonly SparkIdentifierOptions _options;

        public SparkObjectIdentifier(IChatModel chat, IClock clock, ILog log, SparkIdentifierOptions options)
        {
            _chat = chat ?? throw new ArgumentNullException(nameof(chat));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IUniTaskAsyncEnumerable<IdentificationEvent> Identify(EncodedImage image, IdentifyRequest request, CancellationToken ct)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return new IdentificationStream(this, VisionRequestBuilder.Build(image, request, _options.MaxOutputTokens), ct);
        }

        sealed class IdentificationStream : IUniTaskAsyncEnumerable<IdentificationEvent>
        {
            readonly SparkObjectIdentifier _owner;
            readonly ModelRequest _request;
            readonly CancellationToken _ct;

            public IdentificationStream(SparkObjectIdentifier owner, ModelRequest request, CancellationToken ct)
            { _owner = owner; _request = request; _ct = ct; }

            public IUniTaskAsyncEnumerator<IdentificationEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
                new Enumerator(_owner, _request, _ct, cancellationToken);
        }

        sealed class Enumerator : IUniTaskAsyncEnumerator<IdentificationEvent>
        {
            readonly SparkObjectIdentifier _owner;
            readonly ClockDeadline _deadline;
            readonly IUniTaskAsyncEnumerator<ModelEvent> _model;
            readonly IdentificationStreamParser _parser = new();
            readonly double _started;
            bool _modelDone;

            public Enumerator(SparkObjectIdentifier owner, ModelRequest request, CancellationToken ct, CancellationToken enumeratorCt)
            {
                _owner = owner;
                _started = owner._clock.Now;
                _deadline = new ClockDeadline(owner._clock, owner._options.Timeout, ct, enumeratorCt);
                try
                {
                    _model = owner._chat.Stream(request, _deadline.Token).GetAsyncEnumerator(_deadline.Token);
                }
                catch
                {
                    _deadline.Dispose();
                    throw;
                }
            }

            public IdentificationEvent Current { get; private set; }

            public async UniTask<bool> MoveNextAsync()
            {
                while (true)
                {
                    if (_parser.TryTake(out var ready))
                    {
                        Current = ready;
                        if (ready.Kind == IdentificationEventKind.Header)
                            LogHeader(ready);
                        return true;
                    }
                    if (_modelDone)
                        return false;
                    await PullModel();
                }
            }

            async UniTask PullModel()
            {
                bool hasNext;
                try
                {
                    hasNext = await _model.MoveNextAsync();
                }
                catch (OperationCanceledException) when (_deadline.IsExpired)
                {
                    throw new ProviderException(Provider, ProviderErrorKind.Timeout,
                        $"Vision answer did not complete within {_owner._options.Timeout.TotalSeconds:F1} s.");
                }

                if (!hasNext || _model.Current.Kind == ModelEventKind.Completed)
                {
                    _modelDone = true;
                    _parser.Complete();
                }
                else if (_model.Current.Kind == ModelEventKind.TextDelta)
                {
                    _parser.Append(_model.Current.Text);
                }
            }

            void LogHeader(in IdentificationEvent header)
            {
                if (!_owner._log.IsEnabled(LogLevel.Info))
                    return;
                _owner._log.Write(LogLevel.Info, LogCategory, string.Format(CultureInfo.InvariantCulture,
                    "evt=vision.header ms={0:F0} parsed={1}", (_owner._clock.Now - _started) * 1000, header.NounPhrase != null));
            }

            public async UniTask DisposeAsync()
            {
                try
                {
                    await _model.DisposeAsync();
                }
                finally
                {
                    _deadline.Dispose();
                }
            }
        }
    }
}
