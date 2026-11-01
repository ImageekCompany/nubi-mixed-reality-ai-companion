using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Jolly.Spikes
{
    /// <summary>Spike (a): enumerate microphones, record a few seconds, report RMS / peak.</summary>
    static class MicProbe
    {
        public sealed class Result
        {
            public float[] Samples;
            public int SampleRate;
        }

        public static async UniTask<Result> RunAsync(float seconds, CancellationToken ct)
        {
            var devices = Microphone.devices;
            var json = new JObject { ["devices"] = new JArray(devices) };
            if (devices.Length == 0)
            {
                SpikeLog.Result("mic", json);
                return null;
            }

            var device = devices[0];
            var caps = new JArray();
            foreach (var d in devices)
            {
                Microphone.GetDeviceCaps(d, out var minFreq, out var maxFreq);
                caps.Add(new JObject { ["device"] = d, ["minFreq"] = minFreq, ["maxFreq"] = maxFreq });
            }
            json["caps"] = caps;

            // GetDeviceCaps returns 0/0 when any rate is supported; 48 kHz is Quest's native rate (voice.md §6).
            Microphone.GetDeviceCaps(device, out _, out var max);
            var requestedRate = max > 0 ? max : 48000;
            var lengthSec = Mathf.CeilToInt(seconds) + 1;

            var startMs = SpikeLog.NowMs;
            var clip = Microphone.Start(device, false, lengthSec, requestedRate);
            if (clip == null)
            {
                json["error"] = "Microphone.Start returned null";
                SpikeLog.Result("mic", json);
                return null;
            }

            var timedOut = await UniTask.WaitUntil(() => Microphone.GetPosition(device) > 0, cancellationToken: ct)
                .TimeoutWithoutException(System.TimeSpan.FromSeconds(3));
            json["firstSampleMs"] = timedOut ? -1 : SpikeLog.NowMs - startMs;

            await UniTask.Delay(System.TimeSpan.FromSeconds(seconds), cancellationToken: ct);
            var position = Microphone.GetPosition(device);
            Microphone.End(device);

            var samples = new float[Mathf.Max(0, position) * clip.channels];
            if (samples.Length > 0)
                clip.GetData(samples, 0);

            double sumSq = 0;
            var peak = 0f;
            foreach (var s in samples)
            {
                sumSq += s * s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            var rms = samples.Length > 0 ? (float)System.Math.Sqrt(sumSq / samples.Length) : 0f;

            json["device"] = device;
            json["requestedRate"] = requestedRate;
            json["clipFrequency"] = clip.frequency;
            json["clipChannels"] = clip.channels;
            json["samplesRecorded"] = samples.Length;
            json["rms"] = rms;
            json["rmsDbfs"] = rms > 0 ? 20f * Mathf.Log10(rms) : -999f;
            json["peak"] = peak;
            json["allZero"] = peak == 0f;
            SpikeLog.Result("mic", json);

            var wavPath = Path.Combine(Application.persistentDataPath, "mic.wav");
            Wav.WritePcm16(wavPath, Wav.FloatToPcm16(samples), clip.frequency, clip.channels);
            var result = new Result { Samples = samples, SampleRate = clip.frequency };
            Object.Destroy(clip);
            return result;
        }
    }
}
