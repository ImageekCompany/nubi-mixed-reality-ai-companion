using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>IRoomScan with a settable status that counts scan requests.</summary>
    public sealed class FakeRoomScan : IRoomScan
    {
        public RoomScanStatus Status { get; set; } = RoomScanStatus.Scanned;

        public bool CanRequestScan { get; set; } = true;
        public int ScanRequests { get; private set; }

        public bool RequestScan()
        {
            if (!CanRequestScan)
                return false;
            ScanRequests++;
            return true;
        }
    }
}
