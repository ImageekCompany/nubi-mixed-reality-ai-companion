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
    /// with the reported usage on completion, otherwise with an estimate (barge-in, timeout, failure). A hedged second
    /// request (ADR-0038) has its own reservation; the leg that loses is settled with an input estimate.
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
        readonly IClock _clock;
        readonly FirstEventHedge _hedge;
        readonly ResponsesRequestWriter _writer = new();

        public ResponsesChatModel(MetaApiEndpoint endpoint, MetaSseClient client, IClock clock, IApiUsageMeter meter,
                                  ResponsesChatOptions options, ILog log)
        {
            _endpoint = endpoint;
            _client = client;
            _meter = meter;
            _options = options;
            _log = log;
            _clock = clock;
            _hedge = new FirstEventHedge(clock, log);
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
            if (!_meter.TryReserve(_options.ReservationUsd, out var original))
                throw new ProviderException(MetaErrors.Provider, ProviderErrorKind.BudgetExceeded, "Usage cap reached.");

            var hedge = 0L;
            var originalAttempts = new OpenAttempts();
            var hedgeAttempts = new OpenAttempts();
            var inputTokens = (long)(body.InputChars / CharsPerToken);
            var reducer = new ResponsesStreamReducer(model);
            var opened = false;
            var settled = false;
            long reservation = 0;
            OpenAttempts attempts = null;
            try
            {
                var (leg, stream) = await _hedge.Open(
                    (l, firstEventTimeout, t) => _client.Open(http, firstEventTimeout, _options.IdleTimeout, _options.RetryPolicy,
                        l == HedgeLeg.Original ? originalAttempts : hedgeAttempts, t),
                    () => _meter.TryReserve(_options.ReservationUsd, out hedge),
                    l => _meter.RecordEstimate(l == HedgeLeg.Original ? original : hedge, model,
                        (l == HedgeLeg.Original ? originalAttempts : hedgeAttempts).PossiblyBilled * inputTokens, 0),
                    _options.HedgeAfter, _options.FirstEventTimeout, ct);
                opened = true;
                reservation = leg == HedgeLeg.Original ? original : hedge;
                attempts = leg == HedgeLeg.Original ? originalAttempts : hedgeAttempts;
                try
                {
                    var events = new List<ModelEvent>(4);
                    var searchStartedAt = double.NaN;
                    do
                    {
                        reducer.Apply(stream.Current, events);
                        foreach (var e in events)
                        {
                            switch (e.Kind)
                            {
                                case ModelEventKind.Completed:
                                    settled = true;
                                    Settle(reservation, e.Result);
                                    break;
                                case ModelEventKind.WebSearchStarted:
                                    searchStartedAt = _clock.Now;
                                    break;
                                case ModelEventKind.WebSearchCompleted:
                                    LogWebSearch(model, e.Text, searchStartedAt);
                                    searchStartedAt = double.NaN;
                                    break;
                            }
                            await writer.YieldAsync(e);
                        }
                        events.Clear();
                    }
                    while (!reducer.IsCompleted && await stream.MoveNext());
                    if (reducer.IsCompleted)
                        await DrainAfterCompletion(stream);
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
                LogFailure(model, e);
                throw;
            }
            finally
            {
                // A leg that never opened was settled by the hedge's abandoned callback.
                if (opened && !settled)
                    _meter.RecordEstimate(reservation, model, attempts.PossiblyBilled * inputTokens, reducer.OutputChars / CharsPerToken);
            }
        }

        /// <summary>
        /// Reads on to [DONE] and the end of the body instead of aborting, so the connection can be reused by the next
        /// round. The turn has already succeeded: a failure here, including the idle timeout, is ignored.
        /// </summary>
        static async UniTask DrainAfterCompletion(MetaSseStream stream)
        {
            try
            {
                while (await stream.MoveNext())
                {
                }
            }
            catch (ProviderException)
            {
            }
        }

        void Settle(long reservation, ModelTurnResult result)
        {
            var usage = result.Usage;
            var usd = _meter.Record(reservation, usage);
            if (_log.IsEnabled(LogLevel.Info))
                _log.Write(LogLevel.Info, Category, $"evt=llm.completed model={usage.Model} req={result.ResponseId} " +
                                                    $"finish={result.FinishReason} in={usage.InputTokens} cached={usage.CachedInputTokens} " +
                                                    $"out={usage.OutputTokens} searches={usage.WebSearches} usd={usd}");
        }

        /// <summary>ms is NaN when the search's in_progress event was not seen.</summary>
        void LogWebSearch(string model, string itemId, double startedAt)
        {
            if (_log.IsEnabled(LogLevel.Info))
                _log.Write(LogLevel.Info, Category, $"evt=llm.web_search model={model} id={itemId} " +
                                                    $"ms={(_clock.Now - startedAt) * 1000:0}");
        }

        void LogFailure(string model, ProviderException e)
        {
            if (_log.IsEnabled(LogLevel.Warning))
                _log.Write(LogLevel.Warning, Category, $"evt=llm.failed model={model} kind={e.Kind} status={e.HttpStatus} req={e.RequestId}", e);
        }
    }
}
