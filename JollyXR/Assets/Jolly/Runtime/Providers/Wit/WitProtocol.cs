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
        /// <summary>Lists the app's voices; synthesizes nothing. Used only to open the connection at launch.</summary>
        public const string VoicesUrl = "https://api.wit.ai/voices?v=20250213";
        public const int SampleRate = 24000;

        public static string SynthesizeBody(string text, VoiceSpec voice)
        {
            var sb = new StringBuilder(text.Length + 96)
                .Append("{\"q\":").Append(ToolResult.JsonString(text))
                .Append(",\"voice\":").Append(ToolResult.JsonString(voice.VoiceId));
            if (!string.IsNullOrEmpty(voice.Style))
                sb.Append(",\"style\":").Append(ToolResult.JsonString(voice.Style));
            return sb.Append(",\"speed\":").Append(voice.Speed.ToString(CultureInfo.InvariantCulture))
                     .Append(",\"pitch\":").Append(voice.Pitch.ToString(CultureInfo.InvariantCulture))
                     .Append('}').ToString();
        }

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
