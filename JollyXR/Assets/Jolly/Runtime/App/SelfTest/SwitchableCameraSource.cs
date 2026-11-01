using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// The live camera, or a bundled still when the room is too dark to identify anything (the self-test then reports
    /// DEGRADED instead of failing on the environment).
    /// </summary>
    public sealed class SwitchableCameraSource : ICameraFrameSource
    {
        readonly ICameraFrameSource _live;
        readonly ICameraFrameSource _still;

        public SwitchableCameraSource(ICameraFrameSource live, ICameraFrameSource still)
        {
            _live = live;
            _still = still;
        }

        public bool UseStill { get; set; }
        /// <summary>Whether the last <see cref="Capture"/> came from the still.</summary>
        public bool LastCaptureWasStill { get; private set; }
        public ICameraFrameSource Live => _live;
        public CameraAvailability Availability => Current.Availability;

        public UniTask<CameraFrame> Capture(int maxSide, CancellationToken ct)
        {
            LastCaptureWasStill = UseStill;
            return Current.Capture(maxSide, ct);
        }

        public void Dispose()
        {
            _live.Dispose();
            _still.Dispose();
        }

        ICameraFrameSource Current => UseStill ? _still : _live;
    }
}
