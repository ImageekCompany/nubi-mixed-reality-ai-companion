using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Personas
{
    /// <summary>
    /// Locale-independent persona data that JollyBootstrap maps from <c>PersonaProfileAsset</c> (App), so Core stays free
    /// of ScriptableObjects. Text fields are keys in the <c>Personas</c> string table; a null key means "none".
    /// </summary>
    public sealed class PersonaDefinition
    {
        public string Id { get; set; }
        public string DisplayNameKey { get; set; }
        public string SystemPromptKey { get; set; }
        public string SafetyNoticeKey { get; set; }
        public string GreetingKey { get; set; }
        /// <summary>Applied to the character's voice for the current locale; null speaks it unchanged.</summary>
        public VoiceDelivery Delivery { get; set; }
        /// <summary>Null offers every registered tool.</summary>
        public IReadOnlyList<string> AllowedTools { get; set; }
        public bool PersistMemory { get; set; } = true;
        public Mood DefaultMood { get; set; }
    }
}
