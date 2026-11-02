using System;
using UnityEngine;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// Bundled inputs of the device self-test: spoken questions (Wit <c>/synthesize</c>, 24 kHz mono, Decompress On Load;
    /// Content/SelfTest/ATTRIBUTION.md) with the words each should transcribe to, and a CC-licensed still for a room too
    /// dark for the camera.
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

        /// <summary>A line for one character only: it says that character's name.</summary>
        [Serializable]
        public sealed class CharacterUtterance
        {
            [Tooltip("CharacterDefinition.id")]
            public string characterId;
            public Utterance utterance = new();
        }

        public Utterance greeting = new();
        public Utterance comeHere = new();
        [Tooltip("The come-here line for a character other than the one comeHere names; the rest use comeHere.")]
        public CharacterUtterance[] comeHereByCharacter = Array.Empty<CharacterUtterance>();
        public Utterance whatIsThis = new();
        [Tooltip("Asks for a long answer, so there is speech to interrupt.")]
        public Utterance longAnswer = new();
        [Tooltip("A part question about the object in the fallback still, for highlight_part.")]
        public Utterance whichPart = new();
        [Tooltip("Asks for the object in the fallback still, for find_object.")]
        public Utterance findObject = new();
        [Tooltip("A question about today that needs a web search (ADR-0048).")]
        public Utterance webSearch = new();
        [Tooltip("JPEG bytes (.bytes) of a CC-licensed still, see Content/SelfTest/ATTRIBUTION.md.")]
        public TextAsset fallbackStill;

        /// <summary>The character's own come-here line if there is one, else <see cref="comeHere"/>.</summary>
        public Utterance ComeHereFor(string characterId)
        {
            foreach (var entry in comeHereByCharacter)
            {
                if (entry != null && entry.utterance?.clip != null && entry.characterId == characterId)
                    return entry.utterance;
            }
            return comeHere;
        }
    }
}
