using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Capture + JPEG encode at the target size, shared by the PTT-release pre-capture and the vision tools. Remembers
    /// its last image so a tool can tell whether <see cref="UserTurn.PreCaptured"/> is still fresh (architecture §5.3).
    /// </summary>
    public sealed class FrameGrabber
    {
        readonly ICameraFrameSource _source;
        readonly IImageEncoder _encoder;
        readonly IClock _clock;
        readonly VisionSettings _settings;
        EncodedImage _last;
        double _lastAt;

        public FrameGrabber(ICameraFrameSource source, IImageEncoder encoder, IClock clock, VisionSettings settings)
        {
            _source = source;
            _encoder = encoder;
            _clock = clock;
            _settings = settings;
        }

        public bool IsReady => _source.Availability == CameraAvailability.Ready;

        /// <summary>Null when the camera is not Ready. Throws ProviderException when no frame arrives (contracts §6).</summary>
        public async UniTask<EncodedImage> Capture(CancellationToken ct)
        {
            if (!IsReady)
                return null;
            var frame = await _source.Capture(_settings.MaxSide, ct);
            try
            {
                _last = await _encoder.EncodeJpeg(frame, _settings.JpegQuality, ct);
                _lastAt = _clock.Now;
                return _last;
            }
            finally
            {
                frame.Dispose();
            }
        }

        public bool IsFresh(EncodedImage image) =>
            image != null && ReferenceEquals(image, _last) && _clock.Now - _lastAt <= _settings.PreCaptureMaxAge.TotalSeconds;
    }
}
