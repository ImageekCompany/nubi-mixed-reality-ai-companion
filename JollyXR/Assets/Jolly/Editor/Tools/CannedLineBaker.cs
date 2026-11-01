using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.App;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Core.Personas;
using Jolly.Core.Speech;
using Jolly.Providers.Wit;
using UnityEditor;
using UnityEngine;

namespace Jolly.Editor.Tools
{
    /// <summary>
    /// Bakes <see cref="CannedLineCatalog.English"/> through the runtime <see cref="WitTextToSpeech"/> client into
    /// 24 kHz mono WAVs (ADR-0006) in a character's English <see cref="CharacterDefinition.voice"/> (ADR-0031), so
    /// offline lines sound like live speech. Rebake with -jollyForce after changing a voice: existing files are kept
    /// unless forced.
    /// Lines without a name go to the shared library folder in the default character's voice. Lines that say the name
    /// are baked per character with its display name into <c>Lines/en/</c> next to its definition, and assigned to its
    /// <c>lineClips</c>; a character whose voice is not the default character's gets every line there, so it never
    /// falls back to a shared clip in another voice.
    /// The Wit client token comes from WIT_AI_CLIENT_TOKEN, else from the .env at JOLLY_DOTENV or the repo root.
    /// Batch (without -quit: it exits itself once the requests finish):
    /// <c>Unity -batchmode -projectPath JollyXR -executeMethod Jolly.Editor.Tools.CannedLineBaker.BakeBatch [-jollyForce]</c>
    /// and <c>… -executeMethod Jolly.Editor.Tools.CannedLineBaker.BakeCharacterBatch -jollyCharacter Assets/…/X.asset [-jollyForce]</c>.
    /// </summary>
    public static class CannedLineBaker
    {
        const string OutputFolder = "Assets/Jolly/Content/Audio/Lines/en";
        const string Language = "en";
        const string ConfigPath = "Assets/Jolly/Content/Config/JollyConfig.asset";

        [MenuItem("Jolly/Bake English Canned Lines")]
        static void BakeFromMenu() => Bake(force: false, exitWhenDone: false).Forget();

        [MenuItem("Jolly/Character/Bake Named Lines For Selected Definition")]
        static void BakeCharacterFromMenu()
        {
            if (Selection.activeObject is not CharacterDefinition definition)
            {
                Debug.LogError("[CannedLineBaker] Select a CharacterDefinition asset first.");
                return;
            }
            BakeCharacter(definition, force: false, exitWhenDone: false).Forget();
        }

        public static void BakeBatch() => Bake(Forced(), true).Forget();

        public static void BakeCharacterBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-jollyCharacter");
            var definition = at >= 0 && at + 1 < args.Length
                ? AssetDatabase.LoadAssetAtPath<CharacterDefinition>(args[at + 1])
                : null;
            if (definition == null)
            {
                Debug.LogError("[CannedLineBaker] -jollyCharacter needs the asset path of a CharacterDefinition.");
                EditorApplication.Exit(2);
                return;
            }
            BakeCharacter(definition, Forced(), true).Forget();
        }

        static bool Forced() => Array.IndexOf(Environment.GetCommandLineArgs(), "-jollyForce") >= 0;

        static async UniTaskVoid Bake(bool force, bool exitWhenDone)
        {
            var lines = new List<(string Key, string Text)>();
            foreach (var line in CannedLineCatalog.English)
            {
                if (!line.SaysName)
                    lines.Add((line.Key, line.Text));
            }
            var config = AssetDatabase.LoadAssetAtPath<JollyConfig>(ConfigPath);
            var voice = config != null && config.defaultCharacter != null ? config.defaultCharacter.voice : null;
            if (voice == null)
            {
                Debug.LogError($"[CannedLineBaker] {ConfigPath} has no default character with a voice.");
                Finish(1, exitWhenDone);
                return;
            }
            Finish(await BakeLines(lines, voice.VoiceFor(Language), OutputFolder, force), exitWhenDone);
        }

        static async UniTaskVoid BakeCharacter(CharacterDefinition definition, bool force, bool exitWhenDone)
        {
            if (definition.voice == null)
            {
                Debug.LogError($"[CannedLineBaker] {definition.name} has no voice.");
                Finish(1, exitWhenDone);
                return;
            }
            var voice = definition.voice.VoiceFor(Language);
            var config = AssetDatabase.LoadAssetAtPath<JollyConfig>(ConfigPath);
            var shared = config != null && config.defaultCharacter != null && config.defaultCharacter.voice != null
                ? config.defaultCharacter.voice.VoiceFor(Language)
                : null;
            var everyLine = shared == null || !SameVoice(voice, shared);
            var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(definition))?.Replace('\\', '/') + "/Lines/" + Language;
            var lines = new List<(string Key, string Text)>();
            foreach (var line in CannedLineCatalog.English)
            {
                if (line.SaysName || everyLine)
                    lines.Add((line.Key, CharacterNamedLocalizer.Fill(line.Text, definition.displayName)));
            }
            var failures = await BakeLines(lines, voice, folder, force);
            try
            {
                Assign(definition, folder, lines);
            }
            catch (Exception e)
            {
                failures++;
                Debug.LogException(e);
            }
            Finish(failures, exitWhenDone);
        }

        static bool SameVoice(VoiceSpec a, VoiceSpec b) =>
            a.VoiceId == b.VoiceId && a.Style == b.Style && a.Speed == b.Speed && a.Pitch == b.Pitch
            && a.SfxCharacter == b.SfxCharacter && a.SfxEnvironment == b.SfxEnvironment;

        static async UniTask<int> BakeLines(List<(string Key, string Text)> lines, VoiceSpec voice, string folder, bool force)
        {
            var failures = 0;
            try
            {
                Debug.Log($"evt=bake.voice voice={voice.VoiceId} style={voice.Style} speed={voice.Speed} pitch={voice.Pitch} " +
                          $"sfx={voice.SfxCharacter}/{voice.SfxEnvironment}");
                var tts = new WitTextToSpeech(new DotEnvSecrets(), new EditorClock());
                // In batchmode the first request (cold TLS while the editor is still busy) overran the 3 s first-chunk window.
                await tts.WarmUp(voice, CancellationToken.None);
                Directory.CreateDirectory(folder);
                foreach (var line in lines)
                {
                    var path = $"{folder}/{line.Key}.wav";
                    if (!force && File.Exists(path))
                        continue;
                    try
                    {
                        var samples = await Collect(tts.Synthesize(line.Text, voice, CancellationToken.None));
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
                ConfigureImporters(folder, lines);
            }
            catch (Exception e)
            {
                failures++;
                Debug.LogException(e);
            }
            return failures;
        }

        /// <summary>Points the definition's English clip set at the baked files and keeps its other languages.</summary>
        static void Assign(CharacterDefinition definition, string folder, List<(string Key, string Text)> lines)
        {
            var clips = new List<AudioClip>();
            foreach (var line in lines)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{line.Key}.wav");
                if (clip != null)
                    clips.Add(clip);
            }
            var sets = new List<LineClipSet>(definition.lineClips ?? Array.Empty<LineClipSet>());
            sets.RemoveAll(set => set == null || string.Equals(set.languageTag, Language, StringComparison.OrdinalIgnoreCase));
            sets.Add(new LineClipSet { languageTag = Language, clips = clips.ToArray() });
            definition.lineClips = sets.ToArray();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Debug.Log($"evt=bake.assigned definition={definition.id} clips={clips.Count}");
        }

        static void Finish(int failures, bool exitWhenDone)
        {
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

        /// <summary>
        /// Decompress On Load keeps AudioClip.GetData working, which ClipPcm needs for the speech ring. Normalize stays
        /// off so offline lines keep live Wit loudness (ADR-0006); AudioImporter has no public property for it.
        /// </summary>
        static void ConfigureImporters(string folder, List<(string Key, string Text)> lines)
        {
            foreach (var line in lines)
            {
                if (AssetImporter.GetAtPath($"{folder}/{line.Key}.wav") is not AudioImporter importer)
                    continue;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = false;
                DisableNormalize(importer);
                importer.SaveAndReimport();
            }
        }

        static void DisableNormalize(AudioImporter importer)
        {
            using var serialized = new SerializedObject(importer);
            var normalize = serialized.FindProperty("m_Normalize")
                ?? throw new InvalidOperationException("AudioImporter.m_Normalize not found; Unity renamed it.");
            normalize.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
