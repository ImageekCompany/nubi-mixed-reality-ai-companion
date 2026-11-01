using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// Parses SAM 3.1 output text (meta-model-api.md §4.3):
    /// <c>&lt;Nf&gt;id&lt;|box;x1=;y1=;x2=;y2=;w=;h=[;c=]|&gt;&lt;|mask;x=0;y=0[;c=];data=H,W,ENC payload|&gt;,id…</c>, one line per frame.
    /// Empty text is a normal no-match result. Anything else malformed throws <see cref="FormatException"/>.
    /// </summary>
    public static class SamOutputParser
    {
        const string BoxOpen = "<|box;";
        const string MaskOpen = "<|mask;";
        const string TokenClose = "|>";
        const string DataKey = "data=";

        public static List<SamRecord> Parse(string text)
        {
            var records = new List<SamRecord>();
            if (text == null)
                return records;

            var i = 0;
            while (true)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                    i++;
                if (i == text.Length)
                    return records;

                Expect(text, ref i, "<");
                ReadInt(text, ref i);
                Expect(text, ref i, "f>");
                records.Add(ReadRecord(text, ref i));
                while (i < text.Length && text[i] == ',')
                {
                    i++;
                    records.Add(ReadRecord(text, ref i));
                }
            }
        }

        static SamRecord ReadRecord(string s, ref int i)
        {
            var id = ReadInt(s, ref i);
            var box = ReadToken(s, ref i, BoxOpen);
            var rect = ReadBox(s, box.start, box.end, out var confidence);

            var mask = ReadToken(s, ref i, MaskOpen);
            var data = s.IndexOf(DataKey, mask.start, mask.end - mask.start, StringComparison.Ordinal);
            if (data < 0)
                throw Malformed(s, i, "mask has no data");
            var cursor = data + DataKey.Length;
            var height = ReadInt(s, ref cursor);
            Expect(s, ref cursor, ",");
            var width = ReadInt(s, ref cursor);
            Expect(s, ref cursor, ",");
            if (width != rect.width || height != rect.height)
                throw Malformed(s, i, $"mask {width}x{height} does not match box {rect.width}x{rect.height}");
            if (cursor >= mask.end)
                throw Malformed(s, i, "mask has no encoding marker");

            var encoding = s[cursor] switch
            {
                '!' => SamMaskEncoding.OneBit,
                '~' => SamMaskEncoding.Lossless,
                _ => throw Malformed(s, cursor, "unknown mask encoding"),
            };
            cursor++;
            return new SamRecord(id, rect, confidence, width, height, encoding, s, cursor, mask.end - cursor);
        }

        static RectInt ReadBox(string s, int start, int end, out float confidence)
        {
            int x1 = -1, y1 = -1, x2 = -1, y2 = -1;
            confidence = float.NaN;
            for (var next = start; next < end;)
            {
                var stop = s.IndexOf(';', next, end - next);
                if (stop < 0)
                    stop = end;
                var pair = s.AsSpan(next, stop - next);
                next = stop + 1;
                var eq = pair.IndexOf('=');
                if (eq <= 0)
                    continue;
                var key = pair.Slice(0, eq);
                var value = pair.Slice(eq + 1);
                if (key.SequenceEqual("x1".AsSpan())) x1 = ParseInt(value);
                else if (key.SequenceEqual("y1".AsSpan())) y1 = ParseInt(value);
                else if (key.SequenceEqual("x2".AsSpan())) x2 = ParseInt(value);
                else if (key.SequenceEqual("y2".AsSpan())) y2 = ParseInt(value);
                else if (key.SequenceEqual("c".AsSpan()))
                    confidence = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            if (x1 < 0 || y1 < 0 || x2 < x1 || y2 < y1)
                throw Malformed(s, start, "box needs 0 <= x1 <= x2 and 0 <= y1 <= y2");
            return new RectInt(x1, y1, x2 - x1 + 1, y2 - y1 + 1);
        }

        static (int start, int end) ReadToken(string s, ref int i, string open)
        {
            if (string.CompareOrdinal(s, i, open, 0, open.Length) != 0)
                throw Malformed(s, i, $"expected '{open}'");
            var start = i + open.Length;
            var end = s.IndexOf(TokenClose, start, StringComparison.Ordinal);
            if (end < 0)
                throw Malformed(s, i, "unterminated token");
            i = end + TokenClose.Length;
            return (start, end);
        }

        static void Expect(string s, ref int i, string expected)
        {
            if (string.CompareOrdinal(s, i, expected, 0, expected.Length) != 0)
                throw Malformed(s, i, $"expected '{expected}'");
            i += expected.Length;
        }

        static int ReadInt(string s, ref int i)
        {
            var start = i;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
                i++;
            if (i == start)
                throw Malformed(s, start, "expected an integer");
            return ParseInt(s.AsSpan(start, i - start));
        }

        static int ParseInt(ReadOnlySpan<char> value) =>
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                ? n
                : throw new FormatException($"SAM output has an invalid integer '{value.ToString()}'.");

        static FormatException Malformed(string s, int position, string reason) =>
            new($"SAM output is malformed at {position} of {s.Length}: {reason}.");
    }
}
