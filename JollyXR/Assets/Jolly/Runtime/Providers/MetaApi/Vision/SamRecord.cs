using System;
using UnityEngine;

namespace Jolly.Providers.MetaApi.Vision
{
    public enum SamMaskEncoding { Lossless, OneBit }

    /// <summary>One parsed SAM output record. The mask payload is a view into the source text, not a copy.</summary>
    public readonly struct SamRecord
    {
        readonly string _source;
        readonly int _payloadStart;
        readonly int _payloadLength;

        public SamRecord(int id, RectInt box, float confidence, int maskWidth, int maskHeight, SamMaskEncoding encoding,
                         string source, int payloadStart, int payloadLength)
        {
            Id = id; Box = box; Confidence = confidence; MaskWidth = maskWidth; MaskHeight = maskHeight;
            Encoding = encoding; _source = source; _payloadStart = payloadStart; _payloadLength = payloadLength;
        }

        public int Id { get; }
        /// <summary>Encoded-image pixels; SAM's inclusive x2/y2 already converted, so width = x2 - x1 + 1.</summary>
        public RectInt Box { get; }
        public float Confidence { get; }
        public int MaskWidth { get; }
        public int MaskHeight { get; }
        public SamMaskEncoding Encoding { get; }
        /// <summary>Payload after the encoding marker.</summary>
        public ReadOnlySpan<char> Payload => _source.AsSpan(_payloadStart, _payloadLength);
    }
}
