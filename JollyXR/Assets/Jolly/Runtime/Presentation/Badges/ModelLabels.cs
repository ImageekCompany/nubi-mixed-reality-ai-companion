using Jolly.Contracts;

namespace Jolly.Presentation
{
    /// <summary>
    /// The product name a viewer knows for a Meta model id ("sam-3.1" → "SAM 3.1"), so the badges and the HUD name the
    /// tooling the way the video does. Spark answering about a camera frame (<see cref="ModelCallIds.VisionSuffix"/>)
    /// reads "Muse Spark · vision". An id it does not know is shown as is.
    /// </summary>
    public static class ModelLabels
    {
        public static string Of(string modelId, ILocalizer localizer)
        {
            if (string.IsNullOrEmpty(modelId))
                return string.Empty;
            if (modelId.EndsWith(ModelCallIds.VisionSuffix, System.StringComparison.Ordinal))
                return Name(modelId.Substring(0, modelId.Length - ModelCallIds.VisionSuffix.Length)) + " · "
                       + UiText.Get(localizer, "badge.vision");
            return Name(modelId);
        }

        static string Name(string id)
        {
            if (id.StartsWith("muse-spark", System.StringComparison.Ordinal))
                return "Muse Spark";
            if (id.StartsWith("muse-voice-transcribe", System.StringComparison.Ordinal))
                return "Muse Voice Transcribe";
            if (id.StartsWith("sam-", System.StringComparison.Ordinal))
                return "SAM " + id.Substring(4);
            return id;
        }
    }
}
