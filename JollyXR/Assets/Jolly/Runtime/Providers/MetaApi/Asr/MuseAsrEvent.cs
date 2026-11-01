namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>Ack is the untyped first frame <c>{"sessionId":…}</c>. Other covers speechStart/End/Complete,
    /// speaker and audioProgress, which push-to-talk does not use.</summary>
    public enum MuseAsrEventKind { Ack, Transcript, Error, Other }

    /// <summary>One server frame. Transcript fields are set for Transcript, error fields for Error.</summary>
    public readonly struct MuseAsrEvent
    {
        public MuseAsrEvent(MuseAsrEventKind kind, string sessionId, string text, bool final, double audioMs,
                            string message, string errorType, string errorCode, string errorParam)
        {
            Kind = kind; SessionId = sessionId; Text = text; Final = final; AudioMs = audioMs;
            Message = message; ErrorType = errorType; ErrorCode = errorCode; ErrorParam = errorParam;
        }

        public MuseAsrEventKind Kind { get; }
        public string SessionId { get; }
        public string Text { get; }
        public bool Final { get; }
        public double AudioMs { get; }
        public string Message { get; }
        public string ErrorType { get; }
        public string ErrorCode { get; }
        public string ErrorParam { get; }
    }
}
