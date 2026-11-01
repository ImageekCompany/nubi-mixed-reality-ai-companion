using System;
using Jolly.Contracts;
using Jolly.Core.Personas;
using UnityEngine;

namespace Jolly.App
{
    /// <summary>
    /// A persona as a swappable data asset (architecture §9). Text lives in the <c>Personas</c> string table under
    /// <c>{id}.name</c>, <c>{id}.prompt</c>, <c>{id}.greeting</c> and, when <see cref="hasSafetyNotice"/>, <c>{id}.safety</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Jolly/Persona", fileName = "Persona")]
    public sealed class PersonaProfileAsset : ScriptableObject
    {
        public string id;
        public bool hasSafetyNotice;
        [Header("Voice: how this persona speaks in the character's voice (ADR-0031)")]
        [Tooltip("Empty keeps the character's style.")]
        public string voiceStyle = "";
        [Tooltip("Percent of the character's Wit speed.")]
        [Range(50, 150)] public int voiceSpeedPercent = 100;
        [Tooltip("Percent of the character's Wit pitch.")]
        [Range(50, 150)] public int voicePitchPercent = 100;
        [Tooltip("Empty offers every tool.")]
        public string[] allowedTools = Array.Empty<string>();
        public bool persistMemory = true;
        [Tooltip("A persona built for difficult conversations: the content validator requires no memory, a safety notice " +
                 "and no camera tools. The deterministic crisis hand-off itself runs for every persona.")]
        public bool crisisHandoff;
        public Mood defaultMood;
        public Color accentColor = Color.white;

        public string NameKey => id + ".name";
        public string PromptKey => id + ".prompt";
        public string GreetingKey => id + ".greeting";
        public string SafetyKey => id + ".safety";

        public PersonaDefinition ToDefinition()
        {
            return new PersonaDefinition
            {
                Id = id,
                DisplayNameKey = NameKey,
                SystemPromptKey = PromptKey,
                GreetingKey = GreetingKey,
                SafetyNoticeKey = hasSafetyNotice ? SafetyKey : null,
                Delivery = new VoiceDelivery
                {
                    Style = string.IsNullOrEmpty(voiceStyle) ? null : voiceStyle,
                    SpeedPercent = voiceSpeedPercent,
                    PitchPercent = voicePitchPercent,
                },
                AllowedTools = allowedTools.Length == 0 ? null : allowedTools,
                PersistMemory = persistMemory,
                DefaultMood = defaultMood,
            };
        }
    }
}
