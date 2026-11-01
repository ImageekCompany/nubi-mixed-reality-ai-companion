using System;
using System.Text;
using Jolly.Contracts;
using Jolly.Net.Http;
using Jolly.Providers.MetaApi.Common;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Chat
{
    public sealed class MetaApiCommonTests
    {
        const string FakeKey = "fake-key";

        [Test]
        public void Resolve_ContributorModelOnLiveUserRoute_Throws()
        {
            var e = Assert.Throws<ProviderException>(() =>
                MetaModelResolver.Resolve(ModelRoute.LiveUser, "muse-spark-1.3-contributor"));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.BadRequest));
            Assert.That(e.IsTransient, Is.False);
        }

        [Test]
        public void Resolve_ContributorSuffixIsMatchedCaseInsensitively()
        {
            Assert.Throws<ProviderException>(() => MetaModelResolver.Resolve(ModelRoute.LiveUser, "muse-spark-1.3-CONTRIBUTOR"));
        }

        [TestCase(ModelRoute.LiveUser, "muse-spark-1.3")]
        [TestCase(ModelRoute.Synthetic, "muse-spark-1.3-contributor")]
        [TestCase(ModelRoute.Synthetic, "muse-spark-1.3")]
        public void Resolve_AllowedCombination_ReturnsTheId(ModelRoute route, string id)
        {
            Assert.That(MetaModelResolver.Resolve(route, id), Is.EqualTo(id));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Resolve_MissingModelId_Throws(string id)
        {
            Assert.Throws<ProviderException>(() => MetaModelResolver.Resolve(ModelRoute.Synthetic, id));
        }

        [Test]
        public void PostStreaming_WithKey_BuildsAnAuthenticatedJsonRequest()
        {
            var endpoint = new MetaApiEndpoint(new InMemorySecrets().With(SecretKeys.MetaApiKey, FakeKey), "https://example.test/v1/");

            var spec = endpoint.PostStreaming("/responses", "{\"a\":1}");

            Assert.That(spec.Method, Is.EqualTo(HttpRequestSpec.Post));
            Assert.That(spec.Url, Is.EqualTo("https://example.test/v1/responses"));
            Assert.That(Encoding.UTF8.GetString(spec.Body), Is.EqualTo("{\"a\":1}"));
            Assert.That(spec.ContentType, Is.EqualTo("application/json"));
            Assert.That(spec.TryGetHeader("Authorization", out var auth) && auth == "Bearer " + FakeKey, Is.True);
            Assert.That(spec.TryGetHeader("Accept", out var accept) && accept == "text/event-stream", Is.True);
        }

        [Test]
        public void PostStreaming_WithoutKey_ThrowsUnauthorizedBeforeAnyRequest()
        {
            var endpoint = new MetaApiEndpoint(new InMemorySecrets());

            var e = Assert.Throws<ProviderException>(() => endpoint.PostStreaming("/responses", "{}"));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Unauthorized));
        }

        [TestCase(400, ProviderErrorKind.BadRequest)]
        [TestCase(401, ProviderErrorKind.Unauthorized)]
        [TestCase(402, ProviderErrorKind.BudgetExceeded)]
        [TestCase(403, ProviderErrorKind.Unauthorized)]
        [TestCase(404, ProviderErrorKind.BadRequest)]
        [TestCase(408, ProviderErrorKind.Timeout)]
        [TestCase(413, ProviderErrorKind.BadRequest)]
        [TestCase(429, ProviderErrorKind.RateLimited)]
        [TestCase(500, ProviderErrorKind.Server)]
        [TestCase(503, ProviderErrorKind.Server)]
        [TestCase(504, ProviderErrorKind.Server)]
        public void FromHttp_MapsStatusToKind(int status, ProviderErrorKind kind)
        {
            var e = MetaErrors.FromHttp(new HttpFailureException(status, ""));

            Assert.That(e.Kind, Is.EqualTo(kind));
            Assert.That(e.HttpStatus, Is.EqualTo(status));
            Assert.That(e.Provider, Is.EqualTo(MetaErrors.Provider));
        }

        [Test]
        public void FromHttp_ErrorEnvelope_CarriesMessageRequestIdAndRetryAfter()
        {
            const string body = "{\"error\":{\"code\":\"rate_limit_exceeded\",\"message\":\"Too many requests\",\"param\":null,\"type\":\"rate_limit_error\"}}";

            var e = MetaErrors.FromHttp(new HttpFailureException(429, body, "req_1", TimeSpan.FromSeconds(2)));

            Assert.That(e.Message, Does.Contain("Too many requests"));
            Assert.That(e.RequestId, Is.EqualTo("req_1"));
            Assert.That(e.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void FromHttp_EmptyOrNonJsonBody_StillMaps()
        {
            Assert.That(MetaErrors.FromHttp(new HttpFailureException(404, "")).Message, Does.Contain("404"));
            Assert.That(MetaErrors.FromHttp(new HttpFailureException(502, "<html>bad gateway</html>")).Kind, Is.EqualTo(ProviderErrorKind.Server));
        }

        [Test]
        public void FromHttp_NetworkError_IsNetworkKind()
        {
            var e = MetaErrors.FromHttp(HttpFailureException.Network("Cannot resolve destination host"));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Network));
            Assert.That(e.HttpStatus, Is.Zero);
        }

        [TestCase("rate_limit_exceeded", ProviderErrorKind.RateLimited)]
        [TestCase("server_error", ProviderErrorKind.Server)]
        [TestCase("server_shutting_down", ProviderErrorKind.Server)]
        [TestCase("invalid_api_key", ProviderErrorKind.Unauthorized)]
        [TestCase("gateway_timeout", ProviderErrorKind.Timeout)]
        [TestCase(null, ProviderErrorKind.Server)]
        public void FromStreamError_MapsCodeToKind(string code, ProviderErrorKind kind)
        {
            Assert.That(MetaErrors.FromStreamError(code, "m", null).Kind, Is.EqualTo(kind));
        }
    }
}
