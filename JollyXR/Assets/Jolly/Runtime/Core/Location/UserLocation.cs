using System;
using System.Globalization;
using System.Text;
using Jolly.Contracts;
using Jolly.Core.Memory;

namespace Jolly.Core.Location
{
    /// <summary>
    /// Where and when the user is, as far as the model may know: never finer than a city (ADR-0048). The city and country
    /// the user said (set_home_location) win over the device's locale region, which a headset set up abroad gets wrong;
    /// the time zone and the clock always come from the device.
    /// </summary>
    public sealed class UserLocation
    {
        public const int MaxCityChars = 64;

        readonly IUserPreferences _preferences;
        readonly IDeviceRegion _device;
        readonly IClock _clock;

        public UserLocation(IUserPreferences preferences, IDeviceRegion device, IClock clock)
        {
            _preferences = preferences;
            _device = device;
            _clock = clock;
        }

        /// <summary>Null until the user has said where they live.</summary>
        public string City => CleanCity(_preferences?.Current?.HomeCity);

        /// <summary>With a city, only the country said with it: "London" with the locale's "US" would point elsewhere.</summary>
        public string Country => CleanCountry(City != null ? _preferences.Current.HomeCountry : _device.CountryCode);

        /// <summary>IANA only: a Windows id ("GMT Standard Time") means nothing to the search.</summary>
        public string TimeZone => IsIana(_device.TimeZoneId) ? _device.TimeZoneId : null;

        public DateTimeOffset LocalNow
        {
            get
            {
                var utc = _clock.UtcNow;
                var offset = _device.UtcOffset(utc);
                // DateTimeOffset takes whole minutes within ±14 h only.
                return offset.Ticks % TimeSpan.TicksPerMinute == 0 && offset.Duration() <= TimeSpan.FromHours(14)
                    ? utc.ToOffset(offset)
                    : utc;
            }
        }

        public WebSearchOptions SearchOptions() => new() { Country = Country, City = City, Timezone = TimeZone };

        /// <summary>"Local time: Monday, 2 November 2026, 10:15. Home: London, GB." for the context block.</summary>
        public string PromptLine()
        {
            var sb = new StringBuilder("Local time: ")
                .Append(LocalNow.ToString("dddd, d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture)).Append('.');
            var city = City;
            if (city != null)
            {
                sb.Append(" Home: ").Append(city);
                var country = Country;
                if (country != null)
                    sb.Append(", ").Append(country);
                sb.Append('.');
            }
            return sb.ToString();
        }

        /// <summary>The model writes the city from what it heard; it also reaches the prompt, so it is sanitised.</summary>
        public static string CleanCity(string city)
        {
            var clean = PromptText.Sanitize(city, MaxCityChars);
            return clean.Length == 0 ? null : clean;
        }

        /// <summary>Two ASCII letters, upper-cased; anything else is dropped.</summary>
        public static string CleanCountry(string code)
        {
            if (code == null)
                return null;
            code = code.Trim();
            if (code.Length != 2 || !IsAsciiLetter(code[0]) || !IsAsciiLetter(code[1]))
                return null;
            return code.ToUpperInvariant();
        }

        static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

        static bool IsIana(string id) => !string.IsNullOrEmpty(id) && id.IndexOf('/') > 0 && id.IndexOf(' ') < 0;
    }
}
