// C# port of the one_bit mask decoder from meta-sam-parser 0.1.0 (_mask_codec.py: _decode_raster, _context_at,
// _update_counts). Copyright (c) Meta Platforms, Inc. and affiliates. Licensed under the SAM License, see LICENSE-SAM.txt.

using System;
using System.Buffers;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// Decodes a SAM 3.1 <c>one_bit</c> mask (the payload after the '!' marker) into a row-major <see cref="BitMask"/>.
    /// Adaptive binary range coding with a 12-pixel causal context over a raster padded by 2 pixels left, right and above.
    /// Pure and thread-safe; scratch buffers come from <see cref="ArrayPool{T}"/>, so only the result is allocated.
    /// </summary>
    public static class OneBitMaskDecoder
    {
        const int Padding = 2;
        const int ContextCount = 1 << 12;
        const int Increment = 14;
        const int CountLimit = 4096;
        // Bounds the scratch rent and keeps every index computation inside int; device frames are at most 1280 px.
        const int MaxSide = 4096;

        public static BitMask Decode(ReadOnlySpan<char> payload, int width, int height)
        {
            if (width <= 0 || height <= 0 || width > MaxSide || height > MaxSide)
                throw new FormatException($"SAM mask size must be 1..{MaxSide} per side, got {width}x{height}.");

            var packedLength = SamBase85.DecodedLength(payload);
            var paddedWidth = width + 2 * Padding;
            var paddedLength = paddedWidth * (height + Padding);
            var packed = ArrayPool<byte>.Shared.Rent(packedLength);
            var padded = ArrayPool<byte>.Shared.Rent(paddedLength);
            var counts = ArrayPool<ushort>.Shared.Rent(ContextCount * 2);
            try
            {
                SamBase85.Unpack(payload, packed.AsSpan(0, packedLength));
                Array.Clear(padded, 0, paddedLength);
                counts.AsSpan(0, ContextCount * 2).Fill(1);
                var bits = new ulong[(width * height + 63) >> 6];
                var decoder = new SamRangeDecoder(packed.AsSpan(0, packedLength));

                for (var y = 0; y < height; y++)
                {
                    var row = (y + Padding) * paddedWidth + Padding;
                    for (var x = 0; x < width; x++)
                    {
                        var position = row + x;
                        var offset = Context(padded, position, paddedWidth) << 1;
                        uint zero = counts[offset], one = counts[offset + 1];
                        var bit = decoder.Frequency(zero + one) < zero ? 0 : 1;
                        if (bit == 0)
                            decoder.Decode(0, zero);
                        else
                            decoder.Decode(zero, one);

                        padded[position] = (byte)bit;
                        if (bit != 0)
                        {
                            var index = y * width + x;
                            bits[index >> 6] |= 1UL << (index & 63);
                        }
                        UpdateCounts(counts, offset, bit);
                    }
                }
                return new BitMask(width, height, bits);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(packed);
                ArrayPool<byte>.Shared.Return(padded);
                ArrayPool<ushort>.Shared.Return(counts);
            }
        }

        static int Context(byte[] padded, int position, int paddedWidth)
        {
            var prior = position - paddedWidth;
            var earlier = prior - paddedWidth;
            return padded[position - 1]
                   | (padded[position - 2] << 1)
                   | (padded[prior - 2] << 2)
                   | (padded[prior - 1] << 3)
                   | (padded[prior] << 4)
                   | (padded[prior + 1] << 5)
                   | (padded[prior + 2] << 6)
                   | (padded[earlier - 2] << 7)
                   | (padded[earlier - 1] << 8)
                   | (padded[earlier] << 9)
                   | (padded[earlier + 1] << 10)
                   | (padded[earlier + 2] << 11);
        }

        static void UpdateCounts(ushort[] counts, int offset, int bit)
        {
            counts[offset + bit] = (ushort)(counts[offset + bit] + Increment);
            if (counts[offset] + counts[offset + 1] < CountLimit)
                return;
            counts[offset] = (ushort)Math.Max(counts[offset] >> 1, 1);
            counts[offset + 1] = (ushort)Math.Max(counts[offset + 1] >> 1, 1);
        }
    }
}
