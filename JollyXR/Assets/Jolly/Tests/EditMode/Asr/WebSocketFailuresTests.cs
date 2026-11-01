using System;
using System.Net.WebSockets;
using System.Threading;
using Jolly.Contracts;
using Jolly.Net.WebSockets;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Asr
{
    public sealed class WebSocketFailuresTests
    {
        [Test]
        public void Map_WhenTheCallerCancelled_IsCancellationNotTheSocketAbort()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var e = WebSocketFailures.Map("p", "receive", new WebSocketException("aborted"), cts.Token);

            Assert.That(e, Is.InstanceOf<OperationCanceledException>());
            Assert.That(((OperationCanceledException)e).CancellationToken, Is.EqualTo(cts.Token));
        }

        [Test]
        public void Map_ASocketError_IsNetworkWithTheCause()
        {
            var cause = new WebSocketException("reset");

            var e = (ProviderException)WebSocketFailures.Map("meta.asr", "send", cause, CancellationToken.None);

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Network));
            Assert.That(e.Provider, Is.EqualTo("meta.asr"));
            Assert.That(e.InnerException, Is.SameAs(cause));
        }

        [Test]
        public void Map_AProviderException_PassesThrough()
        {
            var own = new ProviderException("p", ProviderErrorKind.Protocol, "too big");

            Assert.That(WebSocketFailures.Map("p", "receive", own, CancellationToken.None), Is.SameAs(own));
        }

        [Test]
        public void GrowReceiveBuffer_DoublesUpToTheCap()
        {
            Assert.That(WebSocketFailures.GrowReceiveBuffer("p", 16 * 1024), Is.EqualTo(32 * 1024));
            Assert.That(WebSocketFailures.GrowReceiveBuffer("p", 768 * 1024), Is.EqualTo(WebSocketFailures.MaxMessageBytes));
        }

        [Test]
        public void GrowReceiveBuffer_AtTheCap_FailsProtocol()
        {
            var e = Assert.Throws<ProviderException>(() =>
                WebSocketFailures.GrowReceiveBuffer("p", WebSocketFailures.MaxMessageBytes));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Protocol));
        }
    }
}
