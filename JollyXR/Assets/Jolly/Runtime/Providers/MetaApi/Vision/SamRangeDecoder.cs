// C# port of _RangeDecoder from meta-sam-parser 0.1.0 (_mask_codec.py).
// Copyright (c) Meta Platforms, Inc. and affiliates. Licensed under the SAM License, see LICENSE-SAM.txt.

using System;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>Carry-less 32-bit range decoder. All arithmetic is deliberately modulo 2^32 (unchecked uint).</summary>
    ref struct SamRangeDecoder
    {
        const uint Top = 1u << 24;
        const uint Bottom = 1u << 16;

        readonly ReadOnlySpan<byte> _input;
        int _position;
        uint _low;
        uint _range;
        uint _code;
        uint _scaled;

        public SamRangeDecoder(ReadOnlySpan<byte> input)
        {
            if (input.Length < 4)
                throw new FormatException("SAM mask payload is missing its finalization.");
            _input = input;
            _position = 0;
            _low = 0;
            _range = uint.MaxValue;
            _scaled = 0;
            _code = 0;
            for (var i = 0; i < 4; i++)
                _code = (_code << 8) | Read();
        }

        public uint Frequency(uint total)
        {
            _scaled = _range / total;
            if (_scaled == 0)
                throw new FormatException("SAM mask payload has an invalid coding state.");
            var value = (_code - _low) / _scaled;
            return value >= total ? total - 1 : value;
        }

        public void Decode(uint cumulative, uint frequency)
        {
            _low += _scaled * cumulative;
            _range = _scaled * frequency;
            while (true)
            {
                if ((_low ^ (_low + _range)) < Top) { }
                else if (_range < Bottom) _range = (0u - _low) & (Bottom - 1);
                else break;
                _code = (_code << 8) | Read();
                _low <<= 8;
                _range <<= 8;
            }
        }

        uint Read()
        {
            if (_position >= _input.Length)
                throw new FormatException("SAM mask payload ended before decoding completed.");
            return _input[_position++];
        }
    }
}
