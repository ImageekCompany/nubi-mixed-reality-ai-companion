using System;
using System.Collections.Generic;
using Jolly.App;
using Jolly.App.SelfTest;
using Jolly.Core.Speech;
using UnityEditor;
using UnityEngine;

namespace Jolly.Editor.Tools.Validators
{
    /// <summary>JollyConfig, the baked line clips and the self-test assets.</summary>
    public static class ConfigRules
    {
        const string ContributorSuffix = "-contributor";

        public static void Check(JollyConfig config, List<string> problems)
        {
            foreach (var (name, id) in new[]
                     {
                         ("chatModel", config.chatModel), ("samModel", config.samModel), ("asrModel", config.asrModel),
                     })
            {
                if (string.IsNullOrWhiteSpace(id))
                    problems.Add($"JollyConfig.{name} is empty");
                else if (id.EndsWith(ContributorSuffix, StringComparison.OrdinalIgnoreCase))
                    problems.Add($"JollyConfig.{name} '{id}' is a contributor model; live user data must not reach it");
            }
            if (config.hardCapUsd <= 0 || config.hardCapUsd > 140)
                problems.Add("JollyConfig.hardCapUsd must be within the project's $140 budget");
            var so = new SerializedObject(config);
            foreach (var field in new[] { "localeVoices", "editorCameraStill" })
            {
                if (so.FindProperty(field).objectReferenceValue == null)
                    problems.Add($"JollyConfig.{field} is not set");
            }
            CheckLineClips(config, problems);
        }

        /// <summary>
        /// Lines without a name come from the shared library; lines that say the name come from the character, so the
        /// default character must have every one of them and the shared library none (it would be the wrong voice).
        /// </summary>
        static void CheckLineClips(JollyConfig config, List<string> problems)
        {
            var clips = config.lineClips;
            if (clips == null)
            {
                problems.Add("JollyConfig.lineClips is not set");
                return;
            }
            var character = config.defaultCharacter != null ? new LineClipIndex(config.defaultCharacter.lineClips) : null;
            foreach (var entry in CannedLineCatalog.English)
            {
                if (entry.SaysName)
                {
                    if (clips.TryGet("en", entry.Key, out _))
                        problems.Add($"JollyConfig.lineClips has '{entry.Key}', which says a name; it belongs in the character's lineClips");
                    if (character == null)
                        continue;
                    if (!character.TryGet("en", entry.Key, out var own))
                        problems.Add($"default character has no baked en clip for canned line '{entry.Key}'");
                    else
                        CheckReadable(own, problems);
                }
                else if (!clips.TryGet("en", entry.Key, out var clip))
                    problems.Add($"no baked en clip for canned line '{entry.Key}'");
                else
                    CheckReadable(clip, problems);
            }
        }

        public static void CheckSelfTest(SelfTestAssets assets, List<string> problems)
        {
            if (assets == null)
            {
                problems.Add("JollyBootstrap.selfTestAssets is not set");
                return;
            }
            var utterances = new List<SelfTestAssets.Utterance> { assets.greeting, assets.comeHere, assets.whatIsThis, assets.longAnswer };
            foreach (var entry in assets.comeHereByCharacter)
            {
                if (string.IsNullOrWhiteSpace(entry.characterId))
                    problems.Add("a SelfTestAssets.comeHereByCharacter entry has no character id");
                utterances.Add(entry.utterance);
            }
            foreach (var u in utterances)
            {
                if (u.clip == null || string.IsNullOrWhiteSpace(u.expectedText))
                    problems.Add("a self-test utterance has no clip or expected text");
                else
                    CheckReadable(u.clip, problems);
            }
            if (assets.fallbackStill == null)
                problems.Add("SelfTestAssets.fallbackStill is not set");
        }

        /// <summary>The runtime reads samples with GetData (ClipPcm, ClipMicrophone): Decompress On Load only.</summary>
        static void CheckReadable(AudioClip clip, List<string> problems)
        {
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) is AudioImporter importer
                && importer.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad)
                problems.Add($"clip '{clip.name}' must be imported as Decompress On Load");
        }
    }
}
