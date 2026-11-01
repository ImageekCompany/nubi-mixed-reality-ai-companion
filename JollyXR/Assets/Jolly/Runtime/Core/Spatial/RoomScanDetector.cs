using System;
using Jolly.Contracts;

namespace Jolly.Core.Spatial
{
    /// <summary>
    /// Decides whether the room has Space Setup data. Meta's runtime reads planes from the stored scene model instead of
    /// detecting them live, so a floor that has not appeared within the timeout of the scene permission being granted
    /// will not appear without a new Space Setup. <see cref="Restart"/> re-arms the timeout after the app resumes,
    /// because a scan just finished in Space Setup arrives a little after the app is back.
    /// </summary>
    public sealed class RoomScanDetector
    {
        readonly double _timeoutSeconds;
        double? _waitingSince;

        public RoomScanDetector(TimeSpan timeout) => _timeoutSeconds = timeout.TotalSeconds;

        public RoomScanStatus Update(double now, bool permissionGranted, bool hasFloor)
        {
            if (!permissionGranted)
            {
                _waitingSince = null;
                return RoomScanStatus.NoPermission;
            }
            if (hasFloor)
            {
                _waitingSince = null;
                return RoomScanStatus.Scanned;
            }
            _waitingSince ??= now;
            return now - _waitingSince.Value >= _timeoutSeconds ? RoomScanStatus.Unscanned : RoomScanStatus.Pending;
        }

        public void Restart(double now) => _waitingSince = now;
    }
}
