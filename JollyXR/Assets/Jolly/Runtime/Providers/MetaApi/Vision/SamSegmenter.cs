using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.Http;
using Jolly.Providers.MetaApi.Common;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// <see cref="ISegmenter"/> over SAM 3.1 on <c>/v1/responses</c>: reserves one image in the usage meter, streams the
    /// prompt and inline JPEG through <see cref="MetaSseClient"/>, stops at the output text, parses the box/mask grammar
    /// and decodes the <c>one_bit</c> masks on the thread pool.
    /// </summary>
    public sealed class SamSegmenter : ISegmenter
    {
        const string ResponsesPath = "responses";
        const string LogCategory = "vision";

        readonly MetaApiEndpoint _endpoint;
        readonly MetaSseClient _client;
        readonly IApiUsageMeter _usage;
        readonly IClock _clock;
        readonly ILog _log;
        readonly SamOptions _options;

        public SamSegmenter(MetaApiEndpoint endpoint, MetaSseClient client, IApiUsageMeter usage, IClock clock, ILog log,
                            SamOptions options)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _client = client ?? throw new ArgumentNullException(nameof(client));
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
            if (!SamPromptFormatter.TryFormat(prompt, image.Width, image.Height, out var promptText))
                return Array.Empty<SegmentedObject>();
            var request = _endpoint.PostStreaming(ResponsesPath, SamRequestWriter.Write(_options.ModelId, promptText, image));
            if (!_usage.TryReserve(_options.UsdPerImage, out var reservation))
                throw new ProviderException(MetaErrors.Provider, ProviderErrorKind.BudgetExceeded, "SAM request refused: API budget reached.");

            var started = _clock.Now;
            var output = await Receive(request, reservation, ct);
            var objects = await Decode(output);
            ct.ThrowIfCancellationRequested();

            if (_log.IsEnabled(LogLevel.Info))
            {
                _log.Write(LogLevel.Info, LogCategory, string.Format(CultureInfo.InvariantCulture,
                    "evt=sam.done ms={0:F0} prompt={1} objects={2} req={3}",
                    (_clock.Now - started) * 1000, prompt.Kind, objects.Count, output.ResponseId));
            }
            return objects;
        }

        /// <summary>
        /// Settles the reservation on every path. Only a request the server rejected with a 4xx status is known to be
        /// unbilled; a 5xx, timeout, abort or broken stream may have reached SAM, so it is settled as one billed image.
        /// </summary>
        async UniTask<SamStreamReader> Receive(HttpRequestSpec request, long reservation, CancellationToken ct)
        {
            var reader = new SamStreamReader();
            try
            {
                var stream = await _client.Open(request, _options.Timeout, _options.Timeout, _options.RetryPolicy, new OpenAttempts(), ct);
                try
                {
                    while (!reader.Apply(stream.Current) && await stream.MoveNext()) { }
                }
                finally
                {
                    await stream.DisposeAsync();
                }
                if (!reader.IsFinished)
                    throw MetaErrors.Protocol("SAM stream ended before its output");
            }
            catch (ProviderException e) when (e.HttpStatus is >= 400 and < 500)
            {
                _usage.RecordEstimate(reservation, _options.ModelId, 0, 0);
                throw;
            }
            catch
            {
                _usage.Record(reservation, Billed(1));
                throw;
            }
            _usage.Record(reservation, Billed(reader.ImagesProcessed));
            return reader;
        }

        UsageRecord Billed(int images) => new(_options.ModelId, 0, 0, 0, images);

        static async UniTask<IReadOnlyList<SegmentedObject>> Decode(SamStreamReader output)
        {
            var text = output.OutputText;
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<SegmentedObject>();
            try
            {
                return await DecodeOffMainThread(text);
            }
            catch (FormatException e)
            {
                throw new ProviderException(MetaErrors.Provider, ProviderErrorKind.Protocol, e.Message, requestId: output.ResponseId, inner: e);
            }
        }

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
