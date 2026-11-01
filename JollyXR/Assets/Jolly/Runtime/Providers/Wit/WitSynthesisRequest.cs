using System;
using System.Buffers;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;
using UnityEngine.Networking;

namespace Jolly.Providers.Wit
{
    /// <summary>One <c>/synthesize</c> request: UnityWebRequest → <see cref="WitDownloadHandler"/> → channel → writer.</summary>
    static class WitSynthesisRequest
    {
        const int ReceiveBufferBytes = 32 * 1024;

        public static async UniTask Run(string token, string body, IAsyncWriter<PcmChunk> writer, CancellationToken ct)
        {
            var channel = Channel.CreateSingleConsumerUnbounded<PcmChunk>();
            var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferBytes);
            var request = Create(token, body, new WitDownloadHandler(buffer, channel.Writer));
            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var send = Send(request, channel.Writer, sendCts.Token);
            try
            {
                await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
                    await writer.YieldAsync(chunk);
            }
            finally
            {
                // The request must finish (or abort on the PlayerLoop) before the handler's pooled buffer is returned.
                sendCts.Cancel();
                await send;
                request.Dispose();
                ArrayPool<byte>.Shared.Return(buffer);
                while (channel.Reader.TryRead(out var undelivered))
                    undelivered.Dispose();
                ObserveCompletion(channel.Reader);
            }
        }

        /// <summary>
        /// A channel faulted by <see cref="Send"/> keeps an ExceptionResultSource as its Completion; unawaited, its
        /// finalizer reports the error to UniTaskScheduler as unobserved on a later GC. The reader already got it.
        /// </summary>
        static void ObserveCompletion(ChannelReader<PcmChunk> reader)
        {
            var completion = reader.Completion;
            if (!completion.Status.IsCompleted())
                return;
            try { completion.GetAwaiter().GetResult(); }
            catch (Exception) { }
        }

        static UnityWebRequest Create(string token, string body, WitDownloadHandler handler)
        {
            var request = new UnityWebRequest(WitProtocol.SynthesizeUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = handler,
            };
            handler.Owner = request;
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "audio/raw");
            return request;
        }

        static async UniTask Send(UnityWebRequest request, ChannelWriter<PcmChunk> writer, CancellationToken ct)
        {
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: ct);
                writer.TryComplete();
            }
            catch (UnityWebRequestException e)
            {
                var connectionError = e.Result == UnityWebRequest.Result.ConnectionError;
                writer.TryComplete(WitProtocol.Error(e.ResponseCode, connectionError, request.GetResponseHeader("Retry-After"), e.Error));
            }
            catch (Exception e)
            {
                writer.TryComplete(e);
            }
        }
    }
}
