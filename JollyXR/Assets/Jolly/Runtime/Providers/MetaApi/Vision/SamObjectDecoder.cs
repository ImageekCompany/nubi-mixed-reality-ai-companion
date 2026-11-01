using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Vision
{
    static class SamObjectDecoder
    {
        /// <summary>Parses SAM output text and decodes every mask. Pure; safe on a worker thread.</summary>
        /// <exception cref="FormatException">Malformed output, or a mask that is not <c>one_bit</c>.</exception>
        public static IReadOnlyList<SegmentedObject> Decode(string outputText)
        {
            var records = SamOutputParser.Parse(outputText);
            var objects = new SegmentedObject[records.Count];
            for (var i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r.Encoding != SamMaskEncoding.OneBit)
                    throw new FormatException($"SAM returned a {r.Encoding} mask although one_bit was requested.");
                objects[i] = new SegmentedObject(r.Id, r.Box, r.Confidence, OneBitMaskDecoder.Decode(r.Payload, r.MaskWidth, r.MaskHeight));
            }
            return objects;
        }
    }
}
