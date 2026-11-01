using System;

namespace Jolly.Audio.Resampling
{
    /// <summary>
    /// Streaming mono sample-rate converter: linear interpolation, preceded by a windowed-sinc low-pass when
    /// downsampling so that content above the output Nyquist does not fold back into the speech band. State carries
    /// across calls, so any split of the input gives the same output; the newest input sample is held until the next
    /// call. Allocation-free after construction. One instance per stream, used from one thread at a time.
    /// </summary>
    public sealed class Resampler
    {
        readonly double _step;
        readonly LowPassFilter _antiAlias;
        double _position;
        float _previous;
        bool _primed;

        public Resampler(int inputRate, int outputRate)
        {
            if (inputRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(inputRate), inputRate, "Sample rate must be positive.");
            if (outputRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(outputRate), outputRate, "Sample rate must be positive.");

            InputRate = inputRate;
            OutputRate = outputRate;
            _step = (double)inputRate / outputRate;
            if (outputRate < inputRate)
                _antiAlias = new LowPassFilter(0.45 * outputRate / inputRate);
        }

        public int InputRate { get; }
        public int OutputRate { get; }

        /// <summary>The output capacity that <see cref="Process"/> requires for inputLength samples.</summary>
        public int MaxOutputLength(int inputLength) =>
            (int)Math.Ceiling(inputLength * (double)OutputRate / InputRate) + 1;

        /// <summary>Converts input and returns the number of samples written to output.</summary>
        public int Process(ReadOnlySpan<float> input, Span<float> output)
        {
            if (output.Length < MaxOutputLength(input.Length))
                throw new ArgumentException("Output is smaller than MaxOutputLength(input.Length).", nameof(output));

            if (InputRate == OutputRate)
            {
                input.CopyTo(output);
                return input.Length;
            }

            var written = 0;
            for (var i = 0; i < input.Length; i++)
            {
                var current = _antiAlias == null ? input[i] : _antiAlias.Next(input[i]);
                if (!_primed)
                {
                    _primed = true;
                    _previous = current;
                    continue;
                }

                // Outputs between the previous sample (t = 0) and this one (t = 1); t = 1 itself is emitted next round.
                while (_position < 1.0)
                {
                    output[written++] = _previous + (current - _previous) * (float)_position;
                    _position += _step;
                }
                _position -= 1.0;
                _previous = current;
            }
            return written;
        }

        public void Reset()
        {
            _position = 0;
            _previous = 0;
            _primed = false;
            _antiAlias?.Reset();
        }
    }
}
