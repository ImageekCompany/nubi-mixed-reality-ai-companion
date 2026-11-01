using System;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>Muse ASR settings. The model id comes from JollyConfig (architecture §11: model ids are never in code).</summary>
    public sealed class MuseAsrOptions
    {
        public MuseAsrOptions(string model)
        {
            if (string.IsNullOrEmpty(model))
                throw new ArgumentException("A model id is required.", nameof(model));
            Model = model;
        }

        public string Model { get; }
        public Uri RealtimeEndpoint { get; set; } = new("wss://api.meta.ai/v1/asr/realtime");
        /// <summary>
        /// Connect + handshake + ack (architecture §11; the server allows 10 s for the ack). Measured on Quest: ack
        /// 1.0–2.9 s after session start.
        /// </summary>
        public TimeSpan AckTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public double MaxAheadSeconds { get; set; } = 5.0;
        /// <summary>
        /// Audio is sent in frames of at least this long (three 80 ms capture chunks): each send allocates 16 KB in
        /// Mono's TLS stream (ADR-0023). Partial transcripts arrive up to 160 ms later; the final does not wait. 0 sends
        /// every chunk as it comes.
        /// </summary>
        public double MinFrameSeconds { get; set; } = 0.24;
        /// <summary>Reserved per request before connecting: one minute at $0.18/h.</summary>
        public decimal ReserveUsd { get; set; } = 0.003m;
    }
}
