using System;

namespace Jolly.Audio.Resampling
{
    /// <summary>
    /// Linear-phase FIR low-pass (Blackman-windowed sinc, unity DC gain). 63 taps give a transition band of about
    /// 0.09 × the sample rate: at 48 kHz with a 10.8 kHz cutoff the passband is flat to ~8.7 kHz and the stopband
    /// starts at ~12.9 kHz, so whatever still aliases lands above 11 kHz, outside the speech band. Group delay is 31
    /// samples (0.65 ms at 48 kHz).
    /// </summary>
    sealed class LowPassFilter
    {
        const int Taps = 63;

        readonly float[] _kernel = new float[Taps];
        // Every sample is stored twice, Taps apart, so the newest Taps samples are always contiguous.
        readonly float[] _history = new float[2 * Taps];
        int _next;

        /// <param name="cutoff">Cutoff in cycles per sample, in (0, 0.5).</param>
        public LowPassFilter(double cutoff)
        {
            if (cutoff <= 0 || cutoff >= 0.5)
                throw new ArgumentOutOfRangeException(nameof(cutoff), cutoff, "Cutoff must be in (0, 0.5) cycles per sample.");

            const int middle = (Taps - 1) / 2;
            double sum = 0;
            for (var n = 0; n < Taps; n++)
            {
                var x = n - middle;
                var sinc = x == 0 ? 2 * cutoff : Math.Sin(2 * Math.PI * cutoff * x) / (Math.PI * x);
                var window = 0.42 - 0.5 * Math.Cos(2 * Math.PI * n / (Taps - 1)) + 0.08 * Math.Cos(4 * Math.PI * n / (Taps - 1));
                _kernel[n] = (float)(sinc * window);
                sum += _kernel[n];
            }
            for (var n = 0; n < Taps; n++)
                _kernel[n] = (float)(_kernel[n] / sum);
        }

        public float Next(float sample)
        {
            _history[_next] = sample;
            _history[_next + Taps] = sample;
            _next = (_next + 1) % Taps;

            var acc = 0f;
            for (var k = 0; k < Taps; k++)
                acc += _kernel[k] * _history[_next + k];
            return acc;
        }

        public void Reset()
        {
            Array.Clear(_history, 0, _history.Length);
            _next = 0;
        }
    }
}
