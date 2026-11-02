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
    /// without them the time zone's city ("Europe/Warsaw") stands in. The time zone and the clock always come from the
    /// device.
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
        public string HomeCity => CleanCity(_preferences?.Current?.HomeCity);

        /// <summary>The home city, else the time zone's; null when neither is known.</summary>
        public string City => HomeCity ?? TimeZoneCity(TimeZone);

        /// <summary>With a city, only the country said with it: "London" with the locale's "US" would point elsewhere, and
        /// a time-zone city comes with none.</summary>
        public string Country => CleanCountry(HomeCity != null ? _preferences.Current.HomeCountry
                                              : City != null ? null : _device.CountryCode);

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

        /// <summary>
        /// "Local time: Monday, 2 November 2026, 10:15. Home: London, GB." for the context block; without a home the time
        /// zone's city as "Probably near: Warsaw (device time zone)." Leaving that out made the model ask for the town
        /// instead of searching (device self-test 2026-11-02), even with the city in the search's user_location.
        /// </summary>
        /// <param name="withHome">False leaves any city out (a persona that may not search, ADR-0048).</param>
        public string PromptLine(bool withHome = true)
        {
            var sb = new StringBuilder("Local time: ")
                .Append(LocalNow.ToString("dddd, d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture)).Append('.');
            if (!withHome)
                return sb.ToString();
            var home = HomeCity;
            if (home != null)
            {
                sb.Append(" Home: ").Append(home);
                var country = Country;
                if (country != null)
                    sb.Append(", ").Append(country);
                sb.Append('.');
            }
            else if (City is { } near)
            {
                sb.Append(" Probably near: ").Append(near).Append(" (device time zone).");
            }
            return sb.ToString();
        }

        /// <summary>The model writes the city from what it heard; it also reaches the prompt, so it is sanitised.</summary>
        public static string CleanCity(string city)
        {
            var clean = PromptText.Sanitize(city, MaxCityChars);
            return clean.Length == 0 ? null : clean;
        }

        /// <summary>
        /// "London" for "Europe/London", "New York" for "America/New_York", "Buenos Aires" for
        /// "America/Argentina/Buenos_Aires". Null for an id outside the geographic areas: "Etc/GMT+2", "UTC", "US/Eastern"
        /// name no city.
        /// </summary>
        public static string TimeZoneCity(string ianaId)
        {
            if (ianaId == null)
                return null;
            var slash = ianaId.IndexOf('/');
            if (slash <= 0 || Array.IndexOf(CityAreas, ianaId.Substring(0, slash)) < 0)
                return null;
            return CleanCity(ianaId.Substring(ianaId.LastIndexOf('/') + 1).Replace('_', ' '));
        }

        /// <summary>The tz database's geographic areas, whose ids end in a city.</summary>
        static readonly string[] CityAreas =
            { "Africa", "America", "Antarctica", "Arctic", "Asia", "Atlantic", "Australia", "Europe", "Indian", "Pacific" };

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
