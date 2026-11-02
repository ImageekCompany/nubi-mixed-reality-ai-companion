using System;
using Jolly.Contracts;
using Jolly.Core.Location;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Location
{
    public sealed class UserLocationTests
    {
        FakeUserPreferences _preferences;
        FakeDeviceRegion _device;
        UserLocation _location;

        [SetUp]
        public void SetUp()
        {
            _preferences = new FakeUserPreferences();
            _device = new FakeDeviceRegion();
            _location = new UserLocation(_preferences, _device, new FakeClock(new DateTimeOffset(2026, 11, 2, 9, 5, 0, TimeSpan.Zero)));
        }

        [Test]
        public void SearchOptions_NoHomeCity_UsesTheDeviceCountryAndTimeZone()
        {
            // Act
            var options = _location.SearchOptions();

            // Assert
            Assert.That(options.Country, Is.EqualTo("GB"));
            Assert.That(options.City, Is.Null);
            Assert.That(options.Region, Is.Null);
            Assert.That(options.Timezone, Is.EqualTo("Europe/London"));
        }

        [Test]
        public void SearchOptions_HomeCityWithoutCountry_DropsTheDeviceCountry()
        {
            // Arrange: a headset set up in the US, used in Warsaw.
            _device.CountryCode = "US";
            _preferences.SetHomeLocation("Warsaw", null);

            // Act
            var options = _location.SearchOptions();

            // Assert
            Assert.That(options.City, Is.EqualTo("Warsaw"));
            Assert.That(options.Country, Is.Null);
        }

        [Test]
        public void SearchOptions_HomeCityAndCountry_WinOverTheDevice()
        {
            // Arrange
            _device.CountryCode = "US";
            _preferences.SetHomeLocation("Warsaw", "PL");

            // Act
            var options = _location.SearchOptions();

            // Assert
            Assert.That(options.City, Is.EqualTo("Warsaw"));
            Assert.That(options.Country, Is.EqualTo("PL"));
        }

        [TestCase("GMT Standard Time")]
        [TestCase("Local")]
        [TestCase("")]
        [TestCase(null)]
        public void TimeZone_NotAnIanaId_IsLeftOut(string id)
        {
            // Arrange
            _device.TimeZoneId = id;

            // Act
            var zone = _location.TimeZone;

            // Assert
            Assert.That(zone, Is.Null);
        }

        [TestCase("gb", "GB")]
        [TestCase(" PL ", "PL")]
        [TestCase("GBR", null)]
        [TestCase("1A", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void CleanCountry_KeepsTwoLettersOnly(string code, string expected)
        {
            // Act
            var clean = UserLocation.CleanCountry(code);

            // Assert
            Assert.That(clean, Is.EqualTo(expected));
        }

        [Test]
        public void PromptLine_UsesTheDeviceOffsetAndNamesTheHome()
        {
            // Arrange
            _device.Offset = TimeSpan.FromHours(1);
            _preferences.SetHomeLocation("Kraków", "PL");

            // Act
            var line = _location.PromptLine();

            // Assert
            Assert.That(line, Is.EqualTo("Local time: Monday, 2 November 2026, 10:05. Home: Kraków, PL."));
        }

        [Test]
        public void PromptLine_WithoutAHome_HasOnlyTheTime()
        {
            // Act
            var line = _location.PromptLine();

            // Assert
            Assert.That(line, Is.EqualTo("Local time: Monday, 2 November 2026, 09:05."));
        }

        [Test]
        public void LocalNow_AnOffsetOutsideWhatDateTimeOffsetTakes_FallsBackToUtc()
        {
            // Arrange
            _device.Offset = TimeSpan.FromHours(15);

            // Act
            var now = _location.LocalNow;

            // Assert
            Assert.That(now.Offset, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void For_DisabledPolicy_OffersNothing()
        {
            // Arrange
            var policy = new WebSearchPolicy(_location, new FakeConnectivityMonitor(), enabled: false);

            // Act
            var options = policy.For(new PersonaProfile());

            // Assert
            Assert.That(options, Is.Null);
        }

        [Test]
        public void For_NullPersona_IsAllowedLikeANullToolList()
        {
            // Arrange
            var policy = new WebSearchPolicy(_location, new FakeConnectivityMonitor());

            // Act
            var options = policy.For(null);

            // Assert
            Assert.That(options, Is.Not.Null);
        }
    }
}
