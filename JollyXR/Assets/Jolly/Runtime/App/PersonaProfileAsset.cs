using System;
using System.Collections.Generic;
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
        [Serializable]
        public sealed class VoiceOverride
        {
            public string languageTag = "en";
            public string voiceId = "Charlie";
            [Tooltip("Empty = provider default.")]
            public string style = "";
            [Range(50, 200)] public int speed = 100;
            [Range(25, 200)] public int pitch = 100;
        }

        public string id;
        public bool hasSafetyNotice;
        public VoiceOverride[] voices = Array.Empty<VoiceOverride>();
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
            var byLanguage = new Dictionary<string, VoiceSpec>(voices.Length, StringComparer.OrdinalIgnoreCase);
            foreach (var v in voices)
            {
                byLanguage[v.languageTag] = new VoiceSpec
                {
                    VoiceId = v.voiceId, Style = string.IsNullOrEmpty(v.style) ? null : v.style, Speed = v.speed, Pitch = v.pitch,
                };
            }
            return new PersonaDefinition
            {
                Id = id,
                DisplayNameKey = NameKey,
                SystemPromptKey = PromptKey,
                GreetingKey = GreetingKey,
                SafetyNoticeKey = hasSafetyNotice ? SafetyKey : null,
                Voices = byLanguage,
                AllowedTools = allowedTools.Length == 0 ? null : allowedTools,
                PersistMemory = persistMemory,
                DefaultMood = defaultMood,
            };
        }
    }
}
