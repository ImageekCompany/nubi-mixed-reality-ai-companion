using Jolly.Contracts;

namespace Jolly.Providers.Wit
{
    /// <summary>
    /// Turns arbitrary byte slices of an <c>audio/raw</c> (s16le mono) response into pooled <see cref="PcmChunk"/>s.
    /// HTTP chunk boundaries can split a sample, so an odd trailing byte is carried into the next call.
    /// </summary>
    public sealed class WitPcmChunker
    {
        readonly int _sampleRate;
        byte _carry;
        bool _hasCarry;

        public WitPcmChunker(int sampleRate) => _sampleRate = sampleRate;

        /// <summary>Returns null when the bytes complete no sample. The caller owns the returned chunk.</summary>
        public PcmChunk Feed(byte[] data, int length)
        {
            var offset = 0;
            var available = length + (_hasCarry ? 1 : 0);
            var samples = available / 2;
            if (samples == 0)
            {
                if (length > 0)
                {
                    _carry = data[0];
                    _hasCarry = true;
                }
                return null;
            }

            var chunk = PcmChunk.Rent(samples, _sampleRate);
            var buffer = chunk.Buffer;
            var index = 0;
            if (_hasCarry)
            {
                buffer[index++] = (short)(_carry | (data[0] << 8));
                offset = 1;
            }
            for (; index < samples; index++, offset += 2)
                buffer[index] = (short)(data[offset] | (data[offset + 1] << 8));

            _hasCarry = offset < length;
            if (_hasCarry)
                _carry = data[offset];
            return chunk;
        }
    }
}
