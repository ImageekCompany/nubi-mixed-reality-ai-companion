using System.Linq;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Asr;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Asr
{
    public sealed class MuseAsrErrorsTests
    {
        [Test]
        public void ErrorFrames_MapToProviderErrorKinds()
        {
            var kinds = AsrFixtures.Lines("realtime_errors.txt")
                .Select(line => MuseAsrErrors.FromEvent(MuseAsrEventParser.Parse(line)).Kind)
                .ToArray();

            Assert.That(kinds, Is.EqualTo(new[]
            {
                ProviderErrorKind.Unauthorized, ProviderErrorKind.BadRequest, ProviderErrorKind.BadRequest,
                ProviderErrorKind.Protocol,
            }));
        }

        [Test]
        public void ErrorFrame_KeepsTheSessionIdAsRequestId()
        {
            var e = MuseAsrErrors.FromEvent(MuseAsrEventParser.Parse(AsrFixtures.Lines("realtime_errors.txt")[0]));

            Assert.That(e.RequestId, Is.EqualTo("voyager/duplex/0"));
            Assert.That(e.Provider, Is.EqualTo(MuseAsrErrors.Provider));
        }

        [TestCase(1008, "Unauthorized", ProviderErrorKind.Unauthorized)]
        [TestCase(1008, "Bad Request", ProviderErrorKind.BadRequest)]
        [TestCase(1008, "Audio backlog too large", ProviderErrorKind.Protocol)]
        [TestCase(1011, "Max session duration reached", ProviderErrorKind.Server)]
        [TestCase(1013, null, ProviderErrorKind.RateLimited)]
        [TestCase(1000, "No more transcript segments", ProviderErrorKind.Protocol)]
        [TestCase(1006, null, ProviderErrorKind.Network)]
        public void CloseBeforeFinal_MapsTheCloseCode(int status, string description, ProviderErrorKind expected)
        {
            var e = MuseAsrErrors.FromClose(status, description, "s");

            Assert.That(e.Kind, Is.EqualTo(expected));
            Assert.That(e.Message, Does.Contain(status.ToString()));
        }

        [Test]
        public void ServerSideFailures_AreTransient()
        {
            Assert.That(MuseAsrErrors.FromClose(1011, null, null).IsTransient, Is.True);
            Assert.That(MuseAsrErrors.FromClose(1008, "Unauthorized", null).IsTransient, Is.False);
        }
    }
}
