using System;
using UnityEngine;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// Bundled inputs of the device self-test: spoken questions (macOS <c>say</c>, 24 kHz mono, Decompress On Load) with
    /// the words each should transcribe to, and a CC-licensed still for a room too dark for the camera.
    /// </summary>
    [CreateAssetMenu(menuName = "Jolly/Self-Test Assets", fileName = "SelfTestAssets")]
    public sealed class SelfTestAssets : ScriptableObject
    {
        [Serializable]
        public sealed class Utterance
        {
            public AudioClip clip;
            public string expectedText;
        }

        public Utterance greeting = new();
        public Utterance comeHere = new();
        public Utterance whatIsThis = new();
        [Tooltip("Asks for a long answer, so there is speech to interrupt.")]
        public Utterance longAnswer = new();
        [Tooltip("A part question about the object in the fallback still, for highlight_part.")]
        public Utterance whichPart = new();
        [Tooltip("Asks for the object in the fallback still, for find_object.")]
        public Utterance findObject = new();
        [Tooltip("JPEG bytes (.bytes) of a CC-licensed still, see Content/SelfTest/ATTRIBUTION.md.")]
        public TextAsset fallbackStill;
    }
}
