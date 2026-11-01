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
            foreach (var field in new[] { "localeVoices", "characterVoice", "editorCameraStill" })
            {
                if (so.FindProperty(field).objectReferenceValue == null)
                    problems.Add($"JollyConfig.{field} is not set");
            }
            if (config.CharacterVoice != null && string.IsNullOrWhiteSpace(config.CharacterVoice.VoiceFor("en").VoiceId))
                problems.Add("JollyConfig.characterVoice has no English Wit voice");
            CheckLineClips(config.lineClips, problems);
        }

        static void CheckLineClips(LineClipLibrary clips, List<string> problems)
        {
            if (clips == null)
            {
                problems.Add("JollyConfig.lineClips is not set");
                return;
            }
            foreach (var entry in CannedLineCatalog.English)
            {
                if (!clips.TryGet("en", entry.Key, out var clip))
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
            foreach (var u in new[] { assets.greeting, assets.comeHere, assets.whatIsThis, assets.longAnswer })
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
