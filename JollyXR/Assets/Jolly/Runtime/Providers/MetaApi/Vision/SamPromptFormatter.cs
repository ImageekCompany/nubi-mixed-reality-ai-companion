using System;
using System.Text;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// Builds the SAM 3.1 <c>input_text</c>: either a concept noun phrase or one object (id 1) on frame 0 with point
    /// or box tokens. Every token carries the encoded image size as <c>w</c>/<c>h</c>; any mismatch is a 400 from the API.
    /// </summary>
    public static class SamPromptFormatter
    {
        const int MaxPhraseLength = 60;

        /// <returns>
        /// False when a text prompt has no usable characters: the phrase comes from the model (find_object,
        /// highlight_part), so an empty one is a no-match, not a programmer error.
        /// </returns>
        public static bool TryFormat(SegmentPrompt prompt, int imageWidth, int imageHeight, out string text)
        {
            if (prompt == null)
                throw new ArgumentNullException(nameof(prompt));
            text = prompt.Kind switch
            {
                SegmentPromptKind.Text => NounPhrase(prompt.Text),
                SegmentPromptKind.Points => Points(prompt, imageWidth, imageHeight),
                SegmentPromptKind.Box => Box(prompt.Box, imageWidth, imageHeight),
                _ => throw new ArgumentOutOfRangeException(nameof(prompt), prompt.Kind, "Unknown SAM prompt kind."),
            };
            return text.Length > 0;
        }

        /// <summary>Only letters, digits, apostrophes and hyphens survive, so grammar characters cannot inject tokens.</summary>
        static string NounPhrase(string text)
        {
            var sb = new StringBuilder(MaxPhraseLength);
            if (text != null)
            {
                foreach (var c in text)
                {
                    if (char.IsLetterOrDigit(c) || c == '\'' || c == '-')
                        sb.Append(c);
                    else if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                        sb.Append(' ');
                }
            }
            if (sb.Length > MaxPhraseLength)
            {
                var cut = sb.ToString(0, MaxPhraseLength + 1).LastIndexOf(' ');
                sb.Length = cut > 0 ? cut : MaxPhraseLength;
            }
            return sb.ToString().Trim();
        }

        static string Points(SegmentPrompt prompt, int width, int height)
        {
            if (prompt.Points == null)
                throw new ArgumentException("SAM point prompt has no points.", nameof(prompt));
            var sb = new StringBuilder("<0f>1", 48 * prompt.Points.Count);
            var positives = 0;
            foreach (var p in prompt.Points)
            {
                if (!p.Positive)
                    continue;
                AppendPoint(sb, Mathf.Clamp(p.X, 0, width - 1), Mathf.Clamp(p.Y, 0, height - 1), width, height);
                positives++;
            }
            if (positives == 0)
                throw new ArgumentException("SAM point prompt needs at least one positive point.", nameof(prompt));

            foreach (var p in prompt.Points)
            {
                if (p.Positive || p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height)
                    continue;
                sb.Append('-');
                AppendPoint(sb, p.X, p.Y, width, height);
            }
            return sb.ToString();
        }

        static void AppendPoint(StringBuilder sb, int x, int y, int width, int height) =>
            sb.Append("<|point;x=").Append(x).Append(";y=").Append(y).Append(";w=").Append(width).Append(";h=").Append(height).Append("|>");

        static string Box(RectInt box, int width, int height)
        {
            var x1 = Mathf.Clamp(box.xMin, 0, width - 1);
            var y1 = Mathf.Clamp(box.yMin, 0, height - 1);
            var x2 = Mathf.Clamp(box.xMax - 1, x1, width - 1);
            var y2 = Mathf.Clamp(box.yMax - 1, y1, height - 1);
            return new StringBuilder("<0f>1<|box;x1=", 64).Append(x1).Append(";y1=").Append(y1).Append(";x2=").Append(x2)
                .Append(";y2=").Append(y2).Append(";w=").Append(width).Append(";h=").Append(height).Append("|>").ToString();
        }
    }
}
