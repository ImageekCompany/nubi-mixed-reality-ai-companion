using System;
using System.Globalization;
using Jolly.Contracts;
using Jolly.Core.Location;
using UnityEngine;

namespace Jolly.App.Platform
{
    /// <summary>
    /// The device's locale region and time zone (ADR-0048). On Android both come from Java (java.util.Locale and
    /// java.util.TimeZone): .NET's TimeZoneInfo.Local is not relied on under IL2CPP there, and the startup log line
    /// records what it returned beside Java's answer. Elsewhere TimeZoneInfo and RegionInfo are used. Main thread only.
    /// </summary>
    public sealed class DeviceRegion : IDeviceRegion
    {
        public DeviceRegion(ILog log)
        {
            CountryCode = ReadCountry();
            TimeZoneId = ReadTimeZoneId();
            log.Write(LogLevel.Info, "location",
                $"evt=location.device country={CountryCode ?? "none"} tz={TimeZoneId ?? "none"} tz_clr={ClrTimeZoneId() ?? "none"}");
        }

        public string CountryCode { get; }
        public string TimeZoneId { get; }

        public TimeSpan UtcOffset(DateTimeOffset utc)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var timeZones = new AndroidJavaClass("java.util.TimeZone");
            using var zone = timeZones.CallStatic<AndroidJavaObject>("getDefault");
            return TimeSpan.FromMilliseconds(zone.Call<int>("getOffset", utc.ToUnixTimeMilliseconds()));
#else
            return TimeZoneInfo.Local.GetUtcOffset(utc);
#endif
        }

        static string ReadTimeZoneId()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var timeZones = new AndroidJavaClass("java.util.TimeZone");
                using var zone = timeZones.CallStatic<AndroidJavaObject>("getDefault");
                return zone.Call<string>("getID");
            }
            catch (AndroidJavaException)
            {
                return ClrTimeZoneId();
            }
#else
            return ClrTimeZoneId();
#endif
        }

        static string ClrTimeZoneId()
        {
            try
            {
                return TimeZoneInfo.Local.Id;
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or NotSupportedException)
            {
                return null;
            }
        }

        static string ReadCountry()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var locales = new AndroidJavaClass("java.util.Locale");
                using var locale = locales.CallStatic<AndroidJavaObject>("getDefault");
                return UserLocation.CleanCountry(locale.Call<string>("getCountry"));
            }
            catch (AndroidJavaException)
            {
                return null;
            }
#else
            try
            {
                // The invariant culture's region is "IV", which is no country.
                var code = RegionInfo.CurrentRegion.TwoLetterISORegionName;
                return code == "IV" ? null : UserLocation.CleanCountry(code);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            {
                return null;
            }
#endif
        }
    }
}
