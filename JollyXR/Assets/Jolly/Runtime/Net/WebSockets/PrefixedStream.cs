using System;
using System.IO;
using System.Threading;

namespace Jolly.Net.WebSockets
{
    /// <summary>
    /// Replays bytes already read past the upgrade response before reading on from the inner stream. Used only in the
    /// rare case that a server sends a frame together with its 101; writes go straight through. Disposing disposes
    /// the inner stream. The async overrides keep the System.Threading.Tasks types Stream declares (the UniTask rule of
    /// tools/ci/test.sh is about Jolly's own APIs).
    /// </summary>
    sealed class PrefixedStream : Stream
    {
        readonly Stream _inner;
        readonly byte[] _prefix;
        int _offset;

        public PrefixedStream(ArraySegment<byte> prefix, Stream inner)
        {
            _prefix = new byte[prefix.Count];
            Array.Copy(prefix.Array!, prefix.Offset, _prefix, 0, prefix.Count);
            _inner = inner;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) =>
            TakePrefix(buffer, offset, count, out var taken) ? taken : _inner.Read(buffer, offset, count);

        public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            TakePrefix(buffer, offset, count, out var taken)
                ? System.Threading.Tasks.Task.FromResult(taken)
                : _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        public override System.Threading.Tasks.Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.WriteAsync(buffer, offset, count, cancellationToken);

        public override void Flush() => _inner.Flush();
        public override System.Threading.Tasks.Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }

        bool TakePrefix(byte[] buffer, int offset, int count, out int taken)
        {
            taken = Math.Min(count, _prefix.Length - _offset);
            if (taken <= 0)
                return false;
            Array.Copy(_prefix, _offset, buffer, offset, taken);
            _offset += taken;
            return true;
        }
    }
}
