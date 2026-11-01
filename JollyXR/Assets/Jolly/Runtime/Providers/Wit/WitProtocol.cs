using System;
using System.Globalization;
using System.Text;
using Jolly.Contracts;

namespace Jolly.Providers.Wit
{
    /// <summary>Wire details of Wit <c>/synthesize</c> (ADR-0006, verified live 2026-11-01).</summary>
    public static class WitProtocol
    {
        public const string Provider = "wit";
        public const string SynthesizeUrl = "https://api.wit.ai/synthesize?v=20250213";
        public const int SampleRate = 24000;

        /// <summary>
        /// Sends only what Wit honours: no <c>gain</c> (accepted but ignored, live 2026-11-01). Effects exist only as SSML
        /// <c>&lt;sfx&gt;</c> (Voice SDK sample <c>TTSSpeakerEffectSelect</c>), so the text is XML-escaped and wrapped only
        /// when the voice has one. A wrapped 240-character sentence (~290) was accepted live; the 280 limit is the SDK's.
        /// </summary>
        public static string SynthesizeBody(string text, VoiceSpec voice)
        {
            var sb = new StringBuilder(text.Length + 160)
                .Append("{\"q\":").Append(ToolResult.JsonString(Ssml(text, voice)))
                .Append(",\"voice\":").Append(ToolResult.JsonString(voice.VoiceId));
            if (!string.IsNullOrEmpty(voice.Style))
                sb.Append(",\"style\":").Append(ToolResult.JsonString(voice.Style));
            return sb.Append(",\"speed\":").Append(voice.Speed.ToString(CultureInfo.InvariantCulture))
                     .Append(",\"pitch\":").Append(voice.Pitch.ToString(CultureInfo.InvariantCulture))
                     .Append('}').ToString();
        }

        static string Ssml(string text, VoiceSpec voice)
        {
            var character = !string.IsNullOrEmpty(voice.SfxCharacter);
            var environment = !string.IsNullOrEmpty(voice.SfxEnvironment);
            if (!character && !environment)
                return text;
            var sb = new StringBuilder(text.Length + 64).Append("<speak><sfx");
            if (character)
                sb.Append(" character=\"").Append(XmlEscape(voice.SfxCharacter)).Append('"');
            if (environment)
                sb.Append(" environment=\"").Append(XmlEscape(voice.SfxEnvironment)).Append('"');
            return sb.Append('>').Append(XmlEscape(text)).Append("</sfx></speak>").ToString();
        }

        /// <summary>Apostrophes stay literal: "you're" is the common case and needs no entity in content or "-quoted values.</summary>
        static string XmlEscape(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        public static ProviderException Error(long httpStatus, bool connectionError, string retryAfter, string message)
        {
            var kind = connectionError ? ProviderErrorKind.Network : httpStatus switch
            {
                401 or 403 => ProviderErrorKind.Unauthorized,
                429 => ProviderErrorKind.RateLimited,
                >= 500 => ProviderErrorKind.Server,
                >= 400 => ProviderErrorKind.BadRequest,
                _ => ProviderErrorKind.Protocol,
            };
            TimeSpan? delay = int.TryParse(retryAfter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? TimeSpan.FromSeconds(seconds)
                : null;
            return new ProviderException(Provider, kind, message, (int)httpStatus, retryAfter: delay);
        }
    }
}
