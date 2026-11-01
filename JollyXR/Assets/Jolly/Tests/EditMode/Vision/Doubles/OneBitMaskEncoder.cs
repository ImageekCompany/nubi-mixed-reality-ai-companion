// Test-only port of the one_bit encoder from meta-sam-parser 0.1.0 (_mask_codec.py: _pack, _RangeEncoder,
// _encode_raster). Copyright (c) Meta Platforms, Inc. and affiliates. Licensed under the SAM License, see
// Runtime/Providers/MetaApi/Vision/LICENSE-SAM.txt. Used to prove the decoder is the exact inverse of the reference.

using System;
using System.Collections.Generic;
using System.Text;

namespace Jolly.Tests.EditMode.Vision.Doubles
{
    static class OneBitMaskEncoder
    {
        const string Excluded = "\"\\,;<>|";
        const int Radix = 85;
        const uint Top = 1u << 24;
        const uint Bottom = 1u << 16;

        static readonly string Alphabet = BuildAlphabet();

        /// <summary>Returns the payload without the leading '!' marker; raster is row-major, one byte (0 or 1) per pixel.</summary>
        public static string Encode(byte[] raster, int width, int height)
        {
            var output = new List<byte>();
            uint low = 0, range = uint.MaxValue;
            var counts = new ushort[(1 << 12) * 2];
            Array.Fill(counts, (ushort)1);
            var paddedWidth = width + 4;
            var padded = new byte[paddedWidth * (height + 2)];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var position = (y + 2) * paddedWidth + 2 + x;
                    var prior = position - paddedWidth;
                    var earlier = prior - paddedWidth;
                    var context = padded[position - 1] | (padded[position - 2] << 1) | (padded[prior - 2] << 2)
                                  | (padded[prior - 1] << 3) | (padded[prior] << 4) | (padded[prior + 1] << 5)
                                  | (padded[prior + 2] << 6) | (padded[earlier - 2] << 7) | (padded[earlier - 1] << 8)
                                  | (padded[earlier] << 9) | (padded[earlier + 1] << 10) | (padded[earlier + 2] << 11);
                    var offset = context << 1;
                    uint zero = counts[offset], one = counts[offset + 1];
                    var bit = raster[y * width + x];
                    var scaled = range / (zero + one);
                    low += scaled * (bit == 0 ? 0 : zero);
                    range = scaled * (bit == 0 ? zero : one);
                    while (true)
                    {
                        if ((low ^ (low + range)) < Top) { }
                        else if (range < Bottom) range = (0u - low) & (Bottom - 1);
                        else break;
                        output.Add((byte)(low >> 24));
                        low <<= 8;
                        range <<= 8;
                    }
                    padded[position] = bit;
                    counts[offset + bit] = (ushort)(counts[offset + bit] + 14);
                    if (counts[offset] + counts[offset + 1] >= 4096)
                    {
                        counts[offset] = (ushort)Math.Max(counts[offset] >> 1, 1);
                        counts[offset + 1] = (ushort)Math.Max(counts[offset + 1] >> 1, 1);
                    }
                }
            }
            for (var i = 0; i < 4; i++)
            {
                output.Add((byte)(low >> 24));
                low <<= 8;
            }
            return Pack(output);
        }

        static string Pack(List<byte> input)
        {
            var sb = new StringBuilder();
            var length = input.Count;
            var prefix = new char[5];
            for (var i = 4; i >= 0; i--, length /= Radix)
                prefix[i] = Alphabet[length % Radix];
            sb.Append(prefix);

            var offset = 0;
            var full = input.Count - input.Count % 4;
            for (; offset < full; offset += 4)
                AppendGroup(sb, (uint)(input[offset] << 24 | input[offset + 1] << 16 | input[offset + 2] << 8 | input[offset + 3]), 5);

            var remainder = input.Count - offset;
            if (remainder > 0)
            {
                uint value = 0;
                for (var i = 0; i < 4; i++)
                    value = (value << 8) | (i < remainder ? input[offset + i] : 0u);
                AppendGroup(sb, value, remainder + 1);
            }
            return sb.ToString();
        }

        static void AppendGroup(StringBuilder sb, uint value, int count)
        {
            var digits = new char[5];
            for (var i = 4; i >= 0; i--, value /= Radix)
                digits[i] = Alphabet[(int)(value % Radix)];
            sb.Append(digits, 0, count);
        }

        static string BuildAlphabet()
        {
            var sb = new StringBuilder();
            for (var c = (char)0x21; c < 0x7F; c++)
            {
                if (Excluded.IndexOf(c) < 0)
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
