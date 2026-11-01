using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace Jolly.Contracts
{
    public readonly struct CameraIntrinsics
    {
        public CameraIntrinsics(Vector2 focalLength, Vector2 principalPoint, Vector2Int resolution)
        { FocalLength = focalLength; PrincipalPoint = principalPoint; Resolution = resolution; }
        public Vector2 FocalLength { get; }     // px
        public Vector2 PrincipalPoint { get; }  // px, top-left origin
        public Vector2Int Resolution { get; }
        public CameraIntrinsics Scaled(float s) => new(FocalLength * s, PrincipalPoint * s,
            new Vector2Int(Mathf.RoundToInt(Resolution.x * s), Mathf.RoundToInt(Resolution.y * s)));
        /// <summary>Camera-space direction (z = 1) through a top-left-origin pixel. The camera pose has +Y = image up,
        /// so pixel y flips (ADR-0017). The one projection CameraMath and the mask overlay share (ADR-0018).</summary>
        public Vector3 PixelToCameraDir(Vector2 px) =>
            new((px.x - PrincipalPoint.x) / FocalLength.x, -(px.y - PrincipalPoint.y) / FocalLength.y, 1f);
    }

    /// <summary>One captured frame, already converted to the requested size. Pixels live in the frame source's buffer;
    /// Dispose releases it back (idempotent). The source hands out at most one frame at a time.</summary>
    public sealed class CameraFrame : IDisposable
    {
        public CameraFrame(NativeArray<byte> rgba32, int width, int height, CameraIntrinsics intrinsics,
                           Pose cameraPoseWorld, double timestamp, Action<CameraFrame> release)
        { Rgba32 = rgba32.GetSubArray(0, width * height * 4); Width = width; Height = height; Intrinsics = intrinsics;
          CameraPoseWorld = cameraPoseWorld; Timestamp = timestamp; _release = release; }
        Action<CameraFrame> _release;
        /// <summary>Exactly Width*Height*4 bytes (a sub-array of the reused buffer); row-major, top row first.</summary>
        public NativeArray<byte> Rgba32 { get; }
        public int Width { get; }
        public int Height { get; }
        public CameraIntrinsics Intrinsics { get; }  // already scaled to Width/Height
        public Pose CameraPoseWorld { get; }         // Unity pose: +Z forward through the image, +Y image up, at acquisition
        public double Timestamp { get; }             // IClock.Now at acquisition
        public void Dispose() { var r = _release; _release = null; r?.Invoke(this); }
    }

    public enum CameraAvailability { Unknown, NoPermission, NotSupported, Starting, Ready }

    public interface ICameraFrameSource : IDisposable
    {
        CameraAvailability Availability { get; }
        /// <summary>
        /// Latest frame (20–40 ms old), converted by the platform straight to max(width,height) &lt;= maxSide with the
        /// correct mirror flag (meta-openxr: XRCpuImage.ConversionParams.outputDimensions + transformation, ConvertAsync).
        /// While a previous frame is still undisposed, waits for its release (the encoder may still read it on a worker).
        /// Throws ProviderException(Protocol) when no frame arrives within 1 s.
        /// </summary>
        UniTask<CameraFrame> Capture(int maxSide, CancellationToken ct);
    }

    public sealed class EncodedImage
    {
        public EncodedImage(byte[] bytes, string mimeType, int width, int height, CameraIntrinsics intrinsics, Pose cameraPoseWorld)
        { Bytes = bytes; MimeType = mimeType; Width = width; Height = height; Intrinsics = intrinsics; CameraPoseWorld = cameraPoseWorld; }
        public byte[] Bytes { get; }
        public string MimeType { get; }               // "image/jpeg"
        public int Width { get; }
        public int Height { get; }
        public CameraIntrinsics Intrinsics { get; }   // already scaled to Width/Height
        public Pose CameraPoseWorld { get; }
    }

    public interface IImageEncoder
    {
        /// <summary>Encodes JPEG (no resize; the frame source already delivered the target size). Does not dispose the frame,
        /// and completes only after the native encode has finished reading it, even when ct is cancelled.</summary>
        UniTask<EncodedImage> EncodeJpeg(CameraFrame frame, int quality, CancellationToken ct);
    }

    public enum SegmentPromptKind { Text, Points, Box }

    public readonly struct PromptPoint
    {
        public PromptPoint(int x, int y, bool positive = true) { X = x; Y = y; Positive = positive; }
        public int X { get; } public int Y { get; } public bool Positive { get; }
    }

    public sealed class SegmentPrompt
    {
        public SegmentPromptKind Kind { get; private set; }
        public string Text { get; private set; }
        public IReadOnlyList<PromptPoint> Points { get; private set; }
        public RectInt Box { get; private set; }
        public static SegmentPrompt ForText(string nounPhrase) => new() { Kind = SegmentPromptKind.Text, Text = nounPhrase };
        public static SegmentPrompt ForPoints(IReadOnlyList<PromptPoint> points) => new() { Kind = SegmentPromptKind.Points, Points = points };
        public static SegmentPrompt ForBox(RectInt box) => new() { Kind = SegmentPromptKind.Box, Box = box };
    }

    /// <summary>Row-major bit-packed binary mask.</summary>
    public sealed class BitMask
    {
        public BitMask(int width, int height, ulong[] bits) { Width = width; Height = height; Bits = bits; }
        public int Width { get; }
        public int Height { get; }
        public ulong[] Bits { get; }
        public bool this[int x, int y] { get { int i = y * Width + x; return (Bits[i >> 6] & (1UL << (i & 63))) != 0; } }
    }

    public sealed class SegmentedObject
    {
        public SegmentedObject(int id, RectInt box, float confidence, BitMask mask)
        { Id = id; Box = box; Confidence = confidence; Mask = mask; }
        public int Id { get; }
        /// <summary>Encoded-image pixels. SAM's x2/y2 are inclusive, so Box.width = x2-x1+1.</summary>
        public RectInt Box { get; }
        public float Confidence { get; }   // NaN if not returned
        public BitMask Mask { get; }       // box-local, Mask.Width == Box.width
    }

    public interface ISegmenter
    {
        /// <summary>An empty list means no match (not an error). Records one image in IApiUsageMeter.</summary>
        UniTask<IReadOnlyList<SegmentedObject>> Segment(EncodedImage image, SegmentPrompt prompt, CancellationToken ct);
    }

    public sealed class IdentifyRequest
    {
        public string Question { get; set; }
        /// <summary>Persona style + safety text + language directive (ToolContext.PersonaInstructions). The identifier
        /// appends its own output-format rules. Camera content is untrusted data, never instructions.</summary>
        public string PersonaInstructions { get; set; }
        public string LanguageTag { get; set; }
        public ReasoningEffort Effort { get; set; } = ReasoningEffort.Minimal;
    }

    public enum IdentificationEventKind { Header, AnswerDelta, Completed }

    /// <summary>
    /// Wire format (verified live 2026-11-01, effort minimal: first text 1.5 s, total 1.6 s on pen_640.jpg):
    /// first line "LABEL|NOUN_PHRASE", newline, then plain answer text that streams straight into speech.
    /// </summary>
    public readonly struct IdentificationEvent
    {
        public IdentificationEvent(IdentificationEventKind kind, string label = null, string nounPhrase = null, string text = null)
        { Kind = kind; Label = label; NounPhrase = nounPhrase; Text = text; }
        public IdentificationEventKind Kind { get; }
        public string Label { get; }        // Header: "ballpoint pen"
        public string NounPhrase { get; }   // Header: SAM-ready concept, e.g. "pen"
        public string Text { get; }         // AnswerDelta; Completed carries the full answer
    }

    public interface IObjectIdentifier
    {
        /// <summary>LiveUser route. A malformed header (no '|' in the first line) yields Header with Label = NounPhrase = null
        /// and treats the whole text as the answer.</summary>
        IUniTaskAsyncEnumerable<IdentificationEvent> Identify(EncodedImage image, IdentifyRequest request, CancellationToken ct);
    }
}
