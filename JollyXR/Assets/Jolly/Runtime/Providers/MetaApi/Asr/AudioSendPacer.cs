using System;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// Keeps the audio sent no more than <c>maxAheadSeconds</c> ahead of the wall clock, measured from the first send
    /// (the server's limit; 16 s sent at once was rejected live with "Audio processing backlog too large"). The
    /// handshake backlog (≤ 5 s by the ack timeout) therefore goes out at once, and live audio, which arrives in real
    /// time, never waits. Strict 1× pacing is deliberately absent: it would delay the final by the whole handshake.
    /// </summary>
    public sealed class AudioSendPacer
    {
        readonly double _maxAheadSeconds;
        double _firstSendTime = double.NaN;

        public AudioSendPacer(double maxAheadSeconds) => _maxAheadSeconds = maxAheadSeconds;

        public double SentSeconds { get; private set; }

        /// <summary>Seconds to wait before sending <paramref name="durationSeconds"/> more audio at time now.</summary>
        public double DelayBeforeSending(double durationSeconds, double now)
        {
            if (double.IsNaN(_firstSendTime))
                return 0;
            var ahead = SentSeconds + durationSeconds - (now - _firstSendTime);
            return Math.Max(0, ahead - _maxAheadSeconds);
        }

        public void OnSent(double durationSeconds, double now)
        {
            if (double.IsNaN(_firstSendTime))
                _firstSendTime = now;
            SentSeconds += durationSeconds;
        }
    }
}
