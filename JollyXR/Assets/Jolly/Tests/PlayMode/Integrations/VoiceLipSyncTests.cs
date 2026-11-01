#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jolly.App;
using Jolly.Tests.PlayMode.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.Integrations
{
    /// <summary>
    /// Runs the shipped baked lines (the character's voice, ADR-0031) through the uLipSync profile the scene uses, offline,
    /// the way uLipSync's own BakedData editor does, and checks the mouth would not be stuck on one or two vowels.
    /// A brighter, higher voice moves formants, which can collapse a profile calibrated on another voice.
    /// </summary>
    public sealed class VoiceLipSyncTests
    {
        const string Scene = "Assets/Jolly/Scenes/Main.unity";
        const string LineClips = "Assets/Jolly/Content/Config/LineClips.asset";
        /// <summary>log10(RMS) ≥ -2 (uLipSync's normalised volume ≥ 0.5): speech, not breaths or tails.</summary>
        const float VoicedVolume = 0.5f;
        static readonly string[] Vowels = { "A", "I", "U", "E", "O" };

        [Test]
        public void ShippedLines_DriveEveryVowelThroughTheSceneProfile()
        {
            var profile = SceneProfile();
            var clips = PresentationAssets.Load<LineClipLibrary>(LineClips).languages.Single(l => l.languageTag == "en").clips;
            Assert.That(AudioSettings.outputSampleRate, Is.GreaterThan(0), "uLipSync analyses at the output rate");

            var share = MainPhonemeShare(profile, clips, out var voicedFrames);
            Debug.Log($"evt=lipsync.distribution profile={profile.name} rate={AudioSettings.outputSampleRate} frames={voicedFrames} " +
                      string.Join(" ", share.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value:0.000}")));

            Assert.That(voicedFrames, Is.GreaterThan(1000), "the baked lines hold enough speech to judge");
            Assert.That(share.Values.Max(), Is.LessThan(0.5f), "no phoneme takes over the mouth");
            foreach (var vowel in Vowels)
                Assert.That(share.TryGetValue(vowel, out var s) ? s : 0f, Is.GreaterThan(0.05f), $"vowel {vowel} is reached");
        }

        /// <summary>Share of voiced frames whose main phoneme is each name (a profile may calibrate a name twice).</summary>
        static Dictionary<string, float> MainPhonemeShare(uLipSync.Profile profile, IEnumerable<AudioClip> clips, out int voiced)
        {
            var counts = new Dictionary<string, int>();
            voiced = 0;
            var go = new GameObject("lipsync-bake");
            go.SetActive(false);
            var lipSync = go.AddComponent<uLipSync.uLipSync>();
            lipSync.OnBakeStart(profile);
            try
            {
                foreach (var clip in clips)
                {
                    // uLipSync takes its input to be at the output rate, as the speech output plays it on device.
                    var rate = AudioSettings.outputSampleRate;
                    var samples = Resample(Samples(clip), clip.frequency, rate);
                    var frame = new float[rate / 60];
                    for (var offset = 0; offset + frame.Length <= samples.Length; offset += frame.Length)
                    {
                        System.Array.Copy(samples, offset, frame, 0, frame.Length);
                        lipSync.OnBakeUpdate(frame, 1);
                        if (lipSync.result.volume < VoicedVolume)
                            continue;
                        voiced++;
                        counts.TryGetValue(lipSync.result.phoneme, out var n);
                        counts[lipSync.result.phoneme] = n + 1;
                    }
                }
            }
            finally
            {
                lipSync.OnBakeEnd();
                Object.DestroyImmediate(go);
            }
            var total = voiced;
            return counts.ToDictionary(c => c.Key, c => c.Value / (float)total);
        }

        static uLipSync.Profile SceneProfile()
        {
            var match = Regex.Match(File.ReadAllText(Scene),
                @"m_EditorClassIdentifier: uLipSync\.Runtime::uLipSync\.uLipSync\s+profile: \{fileID: 11400000, guid: ([0-9a-f]{32})");
            Assert.That(match.Success, Is.True, "Main.unity has a uLipSync component with a profile");
            var path = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
            return PresentationAssets.Load<uLipSync.Profile>(path);
        }

        static float[] Samples(AudioClip clip)
        {
            Assert.That(clip.channels, Is.EqualTo(1), clip.name);
            var data = new float[clip.samples];
            Assert.That(clip.GetData(data, 0), Is.True, clip.name);
            return data;
        }

        static float[] Resample(float[] input, int from, int to)
        {
            var output = new float[(long)input.Length * to / from];
            for (var i = 0; i < output.Length; i++)
            {
                var x = (double)i * from / to;
                var i0 = (int)x;
                var i1 = System.Math.Min(i0 + 1, input.Length - 1);
                output[i] = (float)(input[i0] + (input[i1] - input[i0]) * (x - i0));
            }
            return output;
        }
    }
}
#endif
