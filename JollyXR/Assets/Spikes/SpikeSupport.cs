using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Jolly.Spikes
{
    /// <summary>Logcat-friendly logging (filter on "[NubiSpike]") plus a results JSON written to persistentDataPath.</summary>
    static class SpikeLog
    {
        public const string Tag = "[NubiSpike]";
        static readonly Stopwatch Clock = Stopwatch.StartNew();
        static readonly JObject Results = new();

        public static long NowMs => Clock.ElapsedMilliseconds;

        public static void Info(string message) => Debug.Log($"{Tag} t={NowMs}ms {message}");
        public static void Warn(string message) => Debug.LogWarning($"{Tag} t={NowMs}ms WARN {message}");
        public static void Error(string message) => Debug.LogError($"{Tag} t={NowMs}ms ERROR {message}");

        public static void Result(string key, JToken value)
        {
            Results[key] = value;
            Info($"RESULT {key}={value.ToString(Formatting.None)}");
        }

        public static void Save()
        {
            var path = Path.Combine(Application.persistentDataPath, "spike_results.json");
            File.WriteAllText(path, Results.ToString(Formatting.Indented));
            Info($"results saved: {path}");
        }
    }

    /// <summary>
    /// Reads persistentDataPath/secrets.json (pushed via tools/device/push-secrets.sh). Values are never logged.
    /// </summary>
    sealed class SpikeSecrets
    {
        public string MetaApiKey { get; private set; }
        public string WitClientToken { get; private set; }

        public static SpikeSecrets Load()
        {
            var secrets = new SpikeSecrets();
            var path = Path.Combine(Application.persistentDataPath, "secrets.json");
            if (!File.Exists(path))
            {
                SpikeLog.Warn($"secrets.json not found at {path}");
                return secrets;
            }

            var json = JObject.Parse(File.ReadAllText(path));
            secrets.MetaApiKey = (string)json["META_API_KEY"];
            secrets.WitClientToken = (string)json["WIT_AI_CLIENT_TOKEN"];
            SpikeLog.Info($"secrets.json loaded: META_API_KEY={!string.IsNullOrEmpty(secrets.MetaApiKey)} " +
                          $"WIT_AI_CLIENT_TOKEN={!string.IsNullOrEmpty(secrets.WitClientToken)}");
            return secrets;
        }
    }

    static class Wav
    {
        public static void WritePcm16(string path, byte[] pcm16, int sampleRate, int channels = 1)
        {
            using var stream = File.Create(path);
            using var w = new BinaryWriter(stream);
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + pcm16.Length);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(sampleRate);
            w.Write(sampleRate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(pcm16.Length);
            w.Write(pcm16);
        }

        public static byte[] FloatToPcm16(float[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            for (var i = 0; i < samples.Length; i++)
            {
                var s = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * short.MaxValue), short.MinValue, short.MaxValue);
                bytes[2 * i] = (byte)s;
                bytes[2 * i + 1] = (byte)(s >> 8);
            }
            return bytes;
        }

        /// <summary>Linear-interpolation resampler; adequate for speech into an ASR, not for playback quality.</summary>
        public static float[] Resample(float[] input, int fromRate, int toRate)
        {
            if (fromRate == toRate)
                return input;
            var outLength = (int)((long)input.Length * toRate / fromRate);
            var output = new float[outLength];
            var step = (double)fromRate / toRate;
            for (var i = 0; i < outLength; i++)
            {
                var pos = i * step;
                var i0 = (int)pos;
                var i1 = Math.Min(i0 + 1, input.Length - 1);
                var frac = (float)(pos - i0);
                output[i] = input[i0] + (input[i1] - input[i0]) * frac;
            }
            return output;
        }
    }
}
