using System;

namespace Jolly.Core.Location
{
    /// <summary>What the device's settings say about where it is: no GPS, no network lookup (ADR-0048).</summary>
    public interface IDeviceRegion
    {
        /// <summary>The system locale's region, e.g. "GB"; null when unknown. A headset set up abroad reports that country.</summary>
        string CountryCode { get; }
        /// <summary>The system time zone, ideally IANA ("Europe/London"); null when unknown.</summary>
        string TimeZoneId { get; }
        /// <summary>The local offset from UTC at <paramref name="utc"/>.</summary>
        TimeSpan UtcOffset(DateTimeOffset utc);
    }
}
