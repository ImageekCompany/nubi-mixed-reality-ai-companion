// C# port of the payload packing in meta-sam-parser 0.1.0 (_mask_codec.py: _unpack).
// Copyright (c) Meta Platforms, Inc. and affiliates. Licensed under the SAM License, see LICENSE-SAM.txt.

using System;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>SAM mask payload: a 5-digit length prefix, then big-endian 4-byte groups as 5 base-85 digits over
    /// ASCII 0x21..0x7E minus <c>" \ , ; &lt; &gt; |</c>; a tail of n bytes uses n+1 digits.</summary>
    static class SamBase85
    {
        const int Radix = 85;
        const int PrefixLength = 5;
        const string Excluded = "\"\\,;<>|";

        static readonly sbyte[] Values = BuildValues();

        public static int DecodedLength(ReadOnlySpan<char> payload)
        {
            if (payload.Length < PrefixLength)
                throw new FormatException("SAM mask payload is missing its length prefix.");
            long length = 0;
            for (var i = 0; i < PrefixLength; i++)
                length = length * Radix + Digit(payload[i]);

            var remainder = (int)(length % 4);
            var expected = PrefixLength + length / 4 * 5 + (remainder == 0 ? 0 : remainder + 1);
            if (payload.Length != expected)
                throw new FormatException("SAM mask payload length does not match its prefix.");
            return (int)length;
        }

        /// <summary>Unpacks a payload already validated by <see cref="DecodedLength"/> into exactly that many bytes.</summary>
        public static void Unpack(ReadOnlySpan<char> payload, Span<byte> output)
        {
            var source = PrefixLength;
            var destination = 0;
            var full = output.Length - output.Length % 4;
            for (; destination < full; destination += 4, source += 5)
                WriteGroup(Group(payload, source, 5), output.Slice(destination, 4));

            var remainder = output.Length - full;
            if (remainder > 0)
                WriteGroup(Group(payload, source, remainder + 1), output.Slice(destination, remainder));
        }

        static uint Group(ReadOnlySpan<char> payload, int start, int digits)
        {
            ulong value = 0;
            for (var i = 0; i < 5; i++)
                value = value * Radix + (ulong)(i < digits ? Digit(payload[start + i]) : Radix - 1);
            if (value > uint.MaxValue)
                throw new FormatException("SAM mask payload contains an out-of-range group.");
            return (uint)value;
        }

        static void WriteGroup(uint value, Span<byte> output)
        {
            for (var i = 0; i < output.Length; i++)
                output[i] = (byte)(value >> (24 - i * 8));
        }

        static int Digit(char c)
        {
            var value = c < Values.Length ? Values[c] : -1;
            if (value < 0)
                throw new FormatException("SAM mask payload contains an invalid character.");
            return value;
        }

        static sbyte[] BuildValues()
        {
            var values = new sbyte[128];
            Array.Fill(values, (sbyte)-1);
            sbyte next = 0;
            for (var c = (char)0x21; c < 0x7F; c++)
            {
                if (Excluded.IndexOf(c) < 0)
                    values[c] = next++;
            }
            return values;
        }
    }
}
