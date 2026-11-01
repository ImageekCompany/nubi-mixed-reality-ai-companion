using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Jolly.Net.WebSockets;
using Jolly.Tests.EditMode.Core;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Net
{
    public sealed class WebSocketUpgradeTests
    {
        // RFC 6455 §1.3 sample handshake.
        const string Key = "dGhlIHNhbXBsZSBub25jZQ==";
        const string Accept = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";
        const string Head = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                            "sec-websocket-accept: " + Accept + "\r\n\r\n";

        [Test]
        public void Request_IsAnHttp11UpgradeForThePathAndQuery()
        {
            var request = Encoding.ASCII.GetString(new WebSocketUpgrade(Key).Request(new Uri("wss://api.meta.ai/v1/asr/realtime?x=1")));

            Assert.That(request, Does.StartWith("GET /v1/asr/realtime?x=1 HTTP/1.1\r\nHost: api.meta.ai\r\n"));
            Assert.That(request, Does.Contain("Upgrade: websocket\r\nConnection: Upgrade\r\n"));
            Assert.That(request, Does.Contain("Sec-WebSocket-Key: " + Key + "\r\nSec-WebSocket-Version: 13\r\n"));
            Assert.That(request, Does.EndWith("\r\n\r\n"));
        }

        [Test]
        public void ReadResponse_AcceptedHead_IsReadInOneReadNotBytePerByte()
        {
            var stream = new CountingStream(Encoding.ASCII.GetBytes(Head));

            var early = Sync.Run(new WebSocketUpgrade(Key).ReadResponseAsync(stream, CancellationToken.None));

            Assert.That(early.Count, Is.Zero);
            Assert.That(stream.Reads, Is.EqualTo(1), "Mono's ClientWebSocket read this head one byte per TLS read (ADR-0023)");
        }

        [Test]
        public void ReadResponse_HeadSplitAcrossReads_FindsTheEndAcrossTheBoundary()
        {
            var bytes = Encoding.ASCII.GetBytes(Head);
            var stream = new CountingStream(bytes, maxPerRead: bytes.Length - 2);

            var early = Sync.Run(new WebSocketUpgrade(Key).ReadResponseAsync(stream, CancellationToken.None));

            Assert.That(early.Count, Is.Zero);
            Assert.That(stream.Reads, Is.EqualTo(2));
        }

        [Test]
        public void ReadResponse_BytesAfterTheHead_AreReturnedForTheFirstFrame()
        {
            var stream = new CountingStream(Encoding.ASCII.GetBytes(Head).Concat(new byte[] { 0x81, 0x00 }).ToArray());

            var early = Sync.Run(new WebSocketUpgrade(Key).ReadResponseAsync(stream, CancellationToken.None));

            Assert.That(early.ToArray(), Is.EqualTo(new byte[] { 0x81, 0x00 }));
        }

        [Test]
        public void Validate_RefusedUpgrade_ThrowsWithTheStatus()
        {
            var e = Assert.Throws<WebSocketUpgradeException>(() =>
                new WebSocketUpgrade(Key).Validate("HTTP/1.1 401 Unauthorized\r\nContent-Length: 0"));

            Assert.That(e.HttpStatus, Is.EqualTo(401));
        }

        [Test]
        public void Validate_WrongAccept_Throws()
        {
            Assert.Throws<WebSocketUpgradeException>(() =>
                new WebSocketUpgrade(Key).Validate("HTTP/1.1 101 Switching Protocols\r\nSec-WebSocket-Accept: AAAA"));
        }

        [Test]
        public void ReadResponse_ClosedBeforeTheHeadEnds_Throws()
        {
            var stream = new CountingStream(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\n"));

            Assert.Throws<WebSocketUpgradeException>(() =>
                Sync.Run(new WebSocketUpgrade(Key).ReadResponseAsync(stream, CancellationToken.None)));
        }

        /// <summary>A read-only stream that serves at most <c>maxPerRead</c> bytes per read and counts the reads.</summary>
        sealed class CountingStream : MemoryStream
        {
            readonly int _maxPerRead;

            public CountingStream(byte[] content, int maxPerRead = int.MaxValue) : base(content) => _maxPerRead = maxPerRead;

            public int Reads { get; private set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                Reads++;
                return base.Read(buffer, offset, Math.Min(count, _maxPerRead));
            }

            public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count,
                                                                        CancellationToken cancellationToken) =>
                System.Threading.Tasks.Task.FromResult(Read(buffer, offset, count));
        }
    }
}
