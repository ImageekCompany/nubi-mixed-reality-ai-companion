using System;
using Jolly.Core.Location;

namespace Jolly.Testing
{
    public sealed class FakeDeviceRegion : IDeviceRegion
    {
        public string CountryCode { get; set; } = "GB";
        public string TimeZoneId { get; set; } = "Europe/London";
        public TimeSpan Offset { get; set; }

        public TimeSpan UtcOffset(DateTimeOffset utc) => Offset;
    }
}
