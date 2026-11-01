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
        public Uri FileEndpoint { get; set; } = new("https://api.meta.ai/v1/asr/transcribe");
        /// <summary>Architecture §11 (the server allows 10 s). Measured on Quest: ack 1.0–2.9 s after session start.</summary>
        public TimeSpan AckTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan FileTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public double MaxAheadSeconds { get; set; } = 5.0;
        /// <summary>Reserved per request before connecting: one minute at $0.18/h.</summary>
        public decimal ReserveUsd { get; set; } = 0.003m;
    }
}
