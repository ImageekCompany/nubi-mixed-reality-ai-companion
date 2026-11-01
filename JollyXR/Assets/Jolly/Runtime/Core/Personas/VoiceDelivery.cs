using System;
using Jolly.Contracts;

namespace Jolly.Core.Personas
{
    /// <summary>
    /// How a persona speaks in the character's voice (ADR-0031). The voice belongs to the character, so swapping the
    /// character changes every persona's voice; a persona only scales speed and pitch and may pick another Wit style.
    /// </summary>
    public sealed class VoiceDelivery
    {
        /// <summary>Null keeps the character's style.</summary>
        public string Style { get; set; }
        public int SpeedPercent { get; set; } = 100;
        public int PitchPercent { get; set; } = 100;

        /// <summary>A new spec; the character's is not modified. Results are clamped to Wit's ranges.</summary>
        public VoiceSpec ApplyTo(VoiceSpec characterVoice)
        {
            var voice = characterVoice.Copy();
            if (!string.IsNullOrEmpty(Style))
                voice.Style = Style;
            voice.Speed = Scale(voice.Speed, SpeedPercent, VoiceSpec.MinSpeed, VoiceSpec.MaxSpeed);
            voice.Pitch = Scale(voice.Pitch, PitchPercent, VoiceSpec.MinPitch, VoiceSpec.MaxPitch);
            return voice;
        }

        static int Scale(int value, int percent, int min, int max) =>
            Math.Clamp((int)Math.Round(value * percent / 100.0, MidpointRounding.AwayFromZero), min, max);
    }
}
