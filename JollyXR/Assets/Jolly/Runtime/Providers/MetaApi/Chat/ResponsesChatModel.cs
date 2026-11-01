using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Common;

namespace Jolly.Providers.MetaApi.Chat
{
    /// <summary>
    /// IChatModel over POST /v1/responses (ADR-0004). Every request reserves budget first and is settled exactly once:
    /// with the reported usage on completion, otherwise with an estimate (barge-in, timeout, failure).
    /// Main thread only: the request writer is reused.
    /// </summary>
    public sealed class ResponsesChatModel : IChatModel
    {
        const string Category = "llm";
        const int CharsPerToken = 4;

        readonly MetaApiEndpoint _endpoint;
        readonly MetaSseClient _client;
        readonly IApiUsageMeter _meter;
        readonly ResponsesChatOptions _options;
        readonly ILog _log;
        readonly ResponsesRequestWriter _writer = new();

        public ResponsesChatModel(MetaApiEndpoint endpoint, MetaSseClient client, IApiUsageMeter meter,
                                  ResponsesChatOptions options, ILog log)
        {
            _endpoint = endpoint;
            _client = client;
            _meter = meter;
            _options = options;
            _log = log;
        }

        public IUniTaskAsyncEnumerable<ModelEvent> Stream(ModelRequest request, CancellationToken ct) =>
            UniTaskAsyncEnumerable.Create<ModelEvent>((writer, enumerationCt) => Run(request, writer, ct, enumerationCt));

        async UniTask Run(ModelRequest request, IAsyncWriter<ModelEvent> writer, CancellationToken callerCt, CancellationToken enumerationCt)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerCt, enumerationCt);
            var ct = linked.Token;
            ct.ThrowIfCancellationRequested();

            var model = MetaModelResolver.Resolve(request.Route, _options.ModelFor(request.Route));
            var body = _writer.Write(request, model);
            var http = _endpoint.PostStreaming("/responses", body.Json);
            if (!_meter.TryReserve(_options.ReservationUsd, out var reservation))
                throw new ProviderException(MetaErrors.Provider, ProviderErrorKind.BudgetExceeded, "Usage cap reached.");

            var reducer = new ResponsesStreamReducer(model);
            var settled = false;
            var rejected = false;
            try
            {
                var stream = await _client.Open(http, _options.FirstEventTimeout, _options.IdleTimeout, _options.RetryPolicy, ct);
                try
                {
                    var events = new List<ModelEvent>(4);
                    do
                    {
                        reducer.Apply(stream.Current, events);
                        foreach (var e in events)
                        {
                            if (e.Kind == ModelEventKind.Completed)
                                settled = Settle(reservation, e.Result);
                            await writer.YieldAsync(e);
                        }
                        events.Clear();
                    }
                    while (!reducer.IsCompleted && await stream.MoveNext());
                }
                finally
                {
                    await stream.DisposeAsync();
                }
                if (!reducer.IsCompleted)
                    throw MetaErrors.Protocol("stream ended before response.completed");
            }
            catch (ProviderException e)
            {
                rejected = e.HttpStatus >= 400;
                LogFailure(model, e);
                throw;
            }
            finally
            {
                // A request the server rejected with a status was not billed; anything else may have been.
                if (!settled)
                    _meter.RecordEstimate(reservation, model, rejected ? 0 : body.InputChars / CharsPerToken,
                        reducer.OutputChars / CharsPerToken);
            }
        }

        bool Settle(long reservation, ModelTurnResult result)
        {
            var usage = result.Usage;
            var usd = _meter.Record(reservation, usage);
            if (_log.IsEnabled(LogLevel.Info))
                _log.Write(LogLevel.Info, Category, $"evt=llm.completed model={usage.Model} req={result.ResponseId} " +
                                                    $"finish={result.FinishReason} in={usage.InputTokens} cached={usage.CachedInputTokens} " +
                                                    $"out={usage.OutputTokens} usd={usd}");
            return true;
        }

        void LogFailure(string model, ProviderException e)
        {
            if (_log.IsEnabled(LogLevel.Warning))
                _log.Write(LogLevel.Warning, Category, $"evt=llm.failed model={model} kind={e.Kind} status={e.HttpStatus} req={e.RequestId}", e);
        }
    }
}
