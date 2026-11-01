using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Speech;
using Jolly.Providers.Wit;
using UnityEditor;
using UnityEngine;

namespace Jolly.Editor.Tools
{
    /// <summary>
    /// Bakes <see cref="CannedLineCatalog.English"/> through the runtime <see cref="WitTextToSpeech"/> client into
    /// 24 kHz mono WAVs (ADR-0006), so offline lines sound like live speech. Existing files are kept unless forced.
    /// The Wit client token comes from WIT_AI_CLIENT_TOKEN, else from the .env at JOLLY_DOTENV or the repo root.
    /// Batch: <c>Unity -batchmode -projectPath JollyXR -executeMethod Jolly.Editor.Tools.CannedLineBaker.BakeBatch [-jollyForce]</c>
    /// (without -quit: it exits itself once the requests finish).
    /// </summary>
    public static class CannedLineBaker
    {
        const string OutputFolder = "Assets/Jolly/Content/Audio/Lines/en";
        static readonly VoiceSpec Voice = new() { VoiceId = "Charlie" };

        [MenuItem("Jolly/Bake English Canned Lines")]
        static void BakeFromMenu() => Bake(force: false, exitWhenDone: false).Forget();

        public static void BakeBatch() => Bake(Array.IndexOf(Environment.GetCommandLineArgs(), "-jollyForce") >= 0, true).Forget();

        static async UniTaskVoid Bake(bool force, bool exitWhenDone)
        {
            var failures = 0;
            try
            {
                var tts = new WitTextToSpeech(new DotEnvSecrets(), new EditorClock());
                Directory.CreateDirectory(OutputFolder);
                foreach (var line in CannedLineCatalog.English)
                {
                    var path = $"{OutputFolder}/{line.Key}.wav";
                    if (!force && File.Exists(path))
                        continue;
                    try
                    {
                        var samples = await Collect(tts.Synthesize(line.Text, Voice, CancellationToken.None));
                        WriteWav(path, samples, tts.SampleRate);
                        Debug.Log($"evt=bake.line key={line.Key} seconds={samples.Count / (float)tts.SampleRate:0.00}");
                    }
                    catch (ProviderException e)
                    {
                        failures++;
                        Debug.LogError($"evt=bake.failed key={line.Key} kind={e.Kind} status={e.HttpStatus}");
                    }
                }
                AssetDatabase.Refresh();
                ConfigureImporters();
            }
            catch (Exception e)
            {
                failures++;
                Debug.LogException(e);
            }

            Debug.Log($"evt=bake.done failures={failures}");
            if (exitWhenDone)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static async UniTask<List<short>> Collect(IUniTaskAsyncEnumerable<PcmChunk> pcm)
        {
            var samples = new List<short>();
            await foreach (var chunk in pcm)
            {
                using (chunk)
                {
                    for (var i = 0; i < chunk.Length; i++)
                        samples.Add(chunk.Buffer[i]);
                }
            }
            return samples;
        }

        static void WriteWav(string path, List<short> samples, int sampleRate)
        {
            const short channels = 1;
            const short bitsPerSample = 16;
            var dataBytes = samples.Count * 2;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + dataBytes);
            writer.Write("WAVEfmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write("data".ToCharArray());
            writer.Write(dataBytes);
            foreach (var sample in samples)
                writer.Write(sample);
        }

        /// <summary>Decompress On Load keeps AudioClip.GetData working, which ClipPcm needs for the speech ring.</summary>
        static void ConfigureImporters()
        {
            foreach (var line in CannedLineCatalog.English)
            {
                if (AssetImporter.GetAtPath($"{OutputFolder}/{line.Key}.wav") is not AudioImporter importer)
                    continue;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
        }

        sealed class DotEnvSecrets : ISecretsProvider
        {
            readonly Dictionary<string, string> _values = new();

            public DotEnvSecrets()
            {
                var path = Environment.GetEnvironmentVariable("JOLLY_DOTENV");
                if (string.IsNullOrEmpty(path))
                    path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".env"));
                if (!File.Exists(path))
                    return;
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    var equals = line.IndexOf('=');
                    if (line.StartsWith("#", StringComparison.Ordinal) || equals <= 0)
                        continue;
                    _values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim().Trim('"', '\'');
                }
            }

            public bool TryGet(string key, out string value)
            {
                value = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrEmpty(value))
                    return true;
                return _values.TryGetValue(key, out value) && !string.IsNullOrEmpty(value);
            }
        }

        sealed class EditorClock : IClock
        {
            public double Now => EditorApplication.timeSinceStartup;
            public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
            public UniTask Delay(TimeSpan delay, CancellationToken ct) => UniTask.Delay(delay, DelayType.Realtime, cancellationToken: ct);
        }
    }
}
