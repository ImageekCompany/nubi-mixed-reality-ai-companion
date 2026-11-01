using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Jolly.Spikes
{
    /// <summary>
    /// Spike (e): raw Wit.ai /synthesize (no Voice SDK), PCM16 24 kHz streamed through DownloadHandlerScript into a
    /// streaming AudioClip. Measures time to first byte and time to first audible sample.
    /// </summary>
    static class WitTtsProbe
    {
        const string Url = "https://api.wit.ai/synthesize?v=20250213";
        const int SampleRate = 24000;
        const int StartBufferBytes = SampleRate * 2 / 5; // 200 ms

        public static async UniTask<byte[]> RunAsync(string token, string text, AudioSource source, CancellationToken ct)
        {
            var json = new JObject { ["chars"] = text.Length };
            if (string.IsNullOrEmpty(token))
            {
                json["error"] = "no WIT_AI_CLIENT_TOKEN";
                SpikeLog.Result("witTts", json);
                return null;
            }

            var body = new JObject { ["q"] = text, ["voice"] = "Charlie" }.ToString();
            var handler = new PcmStreamHandler();
            using var request = new UnityWebRequest(Url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = handler,
            };
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "audio/raw");

            var clip = AudioClip.Create("wit-tts", SampleRate * 60, 1, SampleRate, true, handler.Read);
            source.clip = clip;
            source.loop = false;

            var t0 = SpikeLog.NowMs;
            handler.StartMs = t0;
            var op = request.SendWebRequest();
            long playStartMs = -1;
            while (!op.isDone)
            {
                ct.ThrowIfCancellationRequested();
                if (playStartMs < 0 && handler.BufferedBytes >= StartBufferBytes)
                {
                    source.Play();
                    playStartMs = SpikeLog.NowMs;
                }
                await UniTask.Yield(ct);
            }
            if (playStartMs < 0 && handler.BufferedBytes > 0)
            {
                source.Play();
                playStartMs = SpikeLog.NowMs;
            }

            json["http"] = request.responseCode;
            json["result"] = request.result.ToString();
            json["contentType"] = request.GetResponseHeader("Content-Type");
            json["ttfbMs"] = handler.FirstByteMs >= 0 ? handler.FirstByteMs - t0 : -1;
            json["playStartMs"] = playStartMs >= 0 ? playStartMs - t0 : -1;
            json["totalMs"] = SpikeLog.NowMs - t0;
            json["bytes"] = handler.TotalBytes;
            json["chunks"] = handler.Chunks;
            json["audioSeconds"] = handler.TotalBytes / (2.0 * SampleRate);
            if (request.result != UnityWebRequest.Result.Success)
                json["error"] = request.error + " " + handler.ErrorText();
            SpikeLog.Result("witTts", json);

            var pcm = handler.ToArray();
            if (pcm.Length > 0)
            {
                Wav.WritePcm16(Path.Combine(Application.persistentDataPath, "tts.wav"), pcm, SampleRate);
                var remaining = Mathf.Max(0f, pcm.Length / (2f * SampleRate) - source.time);
                await UniTask.Delay(TimeSpan.FromSeconds(remaining + 0.3f), cancellationToken: ct);
                SpikeLog.Info($"tts playback finished, isPlaying={source.isPlaying}");
            }
            source.Stop();
            return request.result == UnityWebRequest.Result.Success ? pcm : null;
        }

        /// <summary>Collects streamed PCM; Read() feeds the streaming AudioClip (audio thread).</summary>
        sealed class PcmStreamHandler : DownloadHandlerScript
        {
            readonly object _gate = new();
            readonly MemoryStream _all = new();
            long _readOffset;

            public long StartMs;
            public long FirstByteMs = -1;
            public int Chunks;
            public long TotalBytes { get { lock (_gate) return _all.Length; } }
            public long BufferedBytes { get { lock (_gate) return _all.Length - _readOffset; } }

            public PcmStreamHandler() : base(new byte[16 * 1024]) { }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (FirstByteMs < 0)
                    FirstByteMs = SpikeLog.NowMs;
                Chunks++;
                lock (_gate)
                {
                    _all.Write(data, 0, dataLength);
                }
                return true;
            }

            public byte[] ToArray()
            {
                lock (_gate)
                    return _all.ToArray();
            }

            public string ErrorText()
            {
                var bytes = ToArray();
                return bytes.Length > 0 && bytes.Length < 2048 ? Encoding.UTF8.GetString(bytes) : string.Empty;
            }

            public void Read(float[] output)
            {
                lock (_gate)
                {
                    var buffer = _all.GetBuffer();
                    var length = _all.Length;
                    for (var i = 0; i < output.Length; i++)
                    {
                        if (_readOffset + 1 < length)
                        {
                            var s = (short)(buffer[_readOffset] | (buffer[_readOffset + 1] << 8));
                            output[i] = s / 32768f;
                            _readOffset += 2;
                        }
                        else
                        {
                            output[i] = 0f;
                        }
                    }
                }
            }
        }
    }
}
