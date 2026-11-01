using System;
using System.Threading;
using Jolly.Contracts;

namespace Jolly.Net.WebSockets
{
    /// <summary>The failure rules of <see cref="ClientWebSocketConnection"/>, apart from the socket so they are testable.</summary>
    public static class WebSocketFailures
    {
        /// <summary>A server that streams one endless message must not grow the receive buffer without bound on Quest.</summary>
        public const int MaxMessageBytes = 1024 * 1024;

        /// <summary>
        /// What a failed socket call throws: OperationCanceledException when the caller cancelled (the socket aborts on
        /// cancel, so its own error is noise), a ProviderException as is, anything else as ProviderException(Network).
        /// </summary>
        public static Exception Map(string provider, string operation, Exception failure, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return new OperationCanceledException(ct);
            if (failure is ProviderException)
                return failure;
            return new ProviderException(provider, ProviderErrorKind.Network, $"WebSocket {operation} failed: {failure.Message}",
                inner: failure);
        }

        /// <summary>The doubled size of a full receive buffer; ProviderException(Protocol) past <see cref="MaxMessageBytes"/>.</summary>
        public static int GrowReceiveBuffer(string provider, int length)
        {
            if (length >= MaxMessageBytes)
                throw new ProviderException(provider, ProviderErrorKind.Protocol,
                    $"WebSocket message larger than {MaxMessageBytes} bytes.");
            return Math.Min(length * 2, MaxMessageBytes);
        }
    }
}
