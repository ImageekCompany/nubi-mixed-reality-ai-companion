using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Jolly.Spikes
{
    /// <summary>
    /// Spike (d): System.Net.WebSockets.ClientWebSocket under IL2CPP against Muse Voice Transcribe realtime.
    /// Protocol per docs/research/meta-model-api.md §5.2: handshake text frame (auth inside JSON), binary PCM16 frames
    /// paced in real time, {"type":"endStream"}, read until close 1000.
    /// </summary>
    static class AsrWebSocketProbe
    {
        const string Endpoint = "wss://api.meta.ai/v1/asr/realtime";
        const int FrameMs = 80;

        public static async UniTask RunAsync(string apiKey, byte[] pcm16, int sampleRate, string label, CancellationToken ct)
        {
            var json = new JObject { ["source"] = label, ["audioSeconds"] = pcm16.Length / (2.0 * sampleRate) };
            if (string.IsNullOrEmpty(apiKey))
            {
                json["error"] = "no META_API_KEY";
                SpikeLog.Result("asrWs", json);
                return;
            }

            var encoding = sampleRate switch
            {
                24000 => "PCM_24KHZ",
                16000 => "PCM_16KHZ",
                _ => throw new ArgumentException($"unsupported rate {sampleRate}"),
            };
            var events = new JArray();
            json["events"] = events;
            var t0 = SpikeLog.NowMs;
            long endStreamMs = -1;
            string finalTranscript = null;
            long finalMs = -1;

            using var ws = new ClientWebSocket();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var token = timeout.Token;
            try
            {
                var uri = new Uri($"{Endpoint}?sessionId=nubi-spike-{Guid.NewGuid().ToString("N").Substring(0, 8)}");
                await ws.ConnectAsync(uri, token);
                json["connectMs"] = SpikeLog.NowMs - t0;

                var handshake = new JObject
                {
                    ["authorization"] = new JObject { ["accessToken"] = "Bearer " + apiKey },
                    ["audioEncoding"] = encoding,
                    ["model"] = "muse-voice-transcribe-1.0",
                    ["mode"] = "PUSH_TO_TALK",
                    ["partialMode"] = "CUMULATIVE",
                    ["emitAudioProgress"] = false,
                    ["languageBias"] = new JArray("English"),
                    ["keywords"] = new JArray(),
                }.ToString(Newtonsoft.Json.Formatting.None);
                await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(handshake)), WebSocketMessageType.Text, true, token);
                json["handshakeSentMs"] = SpikeLog.NowMs - t0;

                var receiveLoop = UniTask.Create(async () =>
                {
                    var buffer = new byte[16 * 1024];
                    using var message = new MemoryStream();
                    while (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseSent)
                    {
                        var r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (r.MessageType == WebSocketMessageType.Close)
                        {
                            json["close"] = $"{(int?)ws.CloseStatus} {ws.CloseStatusDescription}";
                            json["closeMs"] = SpikeLog.NowMs - t0;
                            break;
                        }
                        message.Write(buffer, 0, r.Count);
                        if (!r.EndOfMessage)
                            continue;
                        var text = Encoding.UTF8.GetString(message.ToArray());
                        message.SetLength(0);
                        var at = SpikeLog.NowMs - t0;
                        SpikeLog.Info($"asr event +{at}ms {text}");
                        events.Add($"+{at}ms {text}");
                        var evt = JObject.Parse(text);
                        if ((string)evt["type"] == "transcript" && (bool?)evt["final"] == true)
                        {
                            finalTranscript = (string)evt["transcript"];
                            finalMs = SpikeLog.NowMs;
                        }
                    }
                });

                var bytesPerFrame = sampleRate * 2 * FrameMs / 1000;
                json["firstAudioMs"] = SpikeLog.NowMs - t0;
                var sendStart = SpikeLog.NowMs;
                var frames = 0;
                for (var offset = 0; offset < pcm16.Length; offset += bytesPerFrame)
                {
                    var count = Math.Min(bytesPerFrame, pcm16.Length - offset);
                    await ws.SendAsync(new ArraySegment<byte>(pcm16, offset, count), WebSocketMessageType.Binary, true, token);
                    frames++;
                    // Real-time pacing against the wall clock; the server rejects audio far ahead of real time.
                    var dueMs = sendStart + (long)frames * FrameMs;
                    var waitMs = dueMs - SpikeLog.NowMs;
                    if (waitMs > 0)
                        await UniTask.Delay((int)waitMs, cancellationToken: token);
                }
                json["framesSent"] = frames;
                endStreamMs = SpikeLog.NowMs;
                await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"type\":\"endStream\"}")),
                    WebSocketMessageType.Text, true, token);

                await receiveLoop;
                if (ws.State == WebSocketState.CloseReceived)
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
            }
            catch (Exception e)
            {
                json["error"] = $"{e.GetType().Name}: {e.Message}";
                SpikeLog.Error($"asr websocket: {e}");
            }

            json["state"] = ws.State.ToString();
            json["finalTranscript"] = finalTranscript;
            json["finalAfterEndStreamMs"] = finalMs >= 0 && endStreamMs >= 0 ? finalMs - endStreamMs : -1;
            json["totalMs"] = SpikeLog.NowMs - t0;
            SpikeLog.Result("asrWs_" + label, json);
        }
    }
}
