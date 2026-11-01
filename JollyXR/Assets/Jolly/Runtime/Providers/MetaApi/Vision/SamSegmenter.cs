using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// <see cref="ISegmenter"/> over SAM 3.1 on <c>/v1/responses</c>: reserves one image in the usage meter, posts the
    /// prompt and inline JPEG, parses the box/mask grammar and decodes the <c>one_bit</c> masks on the thread pool.
    /// </summary>
    public sealed class SamSegmenter : ISegmenter
    {
        const string Provider = "meta.sam";
        const string ResponsesPath = "responses";
        const string LogCategory = "vision";

        readonly IMetaApiJsonClient _http;
        readonly IApiUsageMeter _usage;
        readonly IClock _clock;
        readonly ILog _log;
        readonly SamOptions _options;

        public SamSegmenter(IMetaApiJsonClient http, IApiUsageMeter usage, IClock clock, ILog log, SamOptions options)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _usage = usage ?? throw new ArgumentNullException(nameof(usage));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async UniTask<IReadOnlyList<SegmentedObject>> Segment(EncodedImage image, SegmentPrompt prompt, CancellationToken ct)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            ct.ThrowIfCancellationRequested();
            var body = SamRequestWriter.Write(_options.ModelId, SamPromptFormatter.Format(prompt, image.Width, image.Height), image);
            if (!_usage.TryReserve(_options.UsdPerImage, out var reservation))
                throw new ProviderException(Provider, ProviderErrorKind.BudgetExceeded, "SAM request refused: API budget reached.");

            var started = _clock.Now;
            var response = await Post(body, reservation, ct);
            if (!response.IsCompleted)
            {
                throw new ProviderException(Provider, response.Status == "failed" ? ProviderErrorKind.Server : ProviderErrorKind.Protocol,
                    $"SAM response {response.Status}: {response.ErrorMessage}", requestId: response.Id);
            }

            IReadOnlyList<SegmentedObject> objects;
            try
            {
                objects = string.IsNullOrWhiteSpace(response.OutputText)
                    ? Array.Empty<SegmentedObject>()
                    : await DecodeOffMainThread(response.OutputText);
            }
            catch (FormatException e)
            {
                throw new ProviderException(Provider, ProviderErrorKind.Protocol, e.Message, requestId: response.Id, inner: e);
            }
            ct.ThrowIfCancellationRequested();

            if (_log.IsEnabled(LogLevel.Info))
            {
                _log.Write(LogLevel.Info, LogCategory, string.Format(CultureInfo.InvariantCulture,
                    "evt=sam.done ms={0:F0} prompt={1} objects={2} req={3}",
                    (_clock.Now - started) * 1000, prompt.Kind, objects.Count, response.Id));
            }
            return objects;
        }

        /// <summary>Settles the reservation on every path: actual images once a body arrived, a zero estimate otherwise.</summary>
        async UniTask<SamResponse> Post(string body, long reservation, CancellationToken ct)
        {
            string json;
            using (var deadline = new ClockDeadline(_clock, _options.Timeout, ct))
            {
                try
                {
                    json = await _http.PostJson(ResponsesPath, body, deadline.Token);
                }
                catch (Exception e)
                {
                    _usage.RecordEstimate(reservation, _options.ModelId, 0, 0);
                    if (e is OperationCanceledException && deadline.IsExpired)
                        throw new ProviderException(Provider, ProviderErrorKind.Timeout, $"SAM gave no answer within {_options.Timeout.TotalSeconds:F1} s.");
                    throw;
                }
            }

            try
            {
                var response = SamResponseReader.Read(json);
                _usage.Record(reservation, Billed(response.ImagesProcessed));
                return response;
            }
            catch (FormatException e)
            {
                _usage.Record(reservation, Billed(1));
                throw new ProviderException(Provider, ProviderErrorKind.Protocol, e.Message, inner: e);
            }
        }

        UsageRecord Billed(int images) => new(_options.ModelId, 0, 0, 0, images);

        static async UniTask<IReadOnlyList<SegmentedObject>> DecodeOffMainThread(string outputText)
        {
            await UniTask.SwitchToThreadPool();
            try
            {
                return SamObjectDecoder.Decode(outputText);
            }
            finally
            {
                await UniTask.SwitchToMainThread();
            }
        }
    }
}
