using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PotopopiCamSync.Services
{
    /// <summary>
    /// Stream wrapper untuk throttle bandwidth.
    /// Limit speed: bytesPerSecond (e.g., 5MB/s = 5 * 1024 * 1024)
    /// </summary>
    public class ThrottledStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _bytesPerSecond;
        private long _bytesTransferred;
        private DateTime _lastResetTime;
        private const int ResetIntervalMs = 100; // Check setiap 100ms

        public ThrottledStream(Stream baseStream, long bytesPerSecond)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _bytesPerSecond = bytesPerSecond > 0 ? bytesPerSecond : long.MaxValue;
            _bytesTransferred = 0;
            _lastResetTime = DateTime.UtcNow;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;

        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrottleIfNeeded(count);
            int read = _baseStream.Read(buffer, offset, count);
            _bytesTransferred += read;
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await ThrottleIfNeededAsync(count, cancellationToken);
            int read = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
            _bytesTransferred += read;
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrottleIfNeeded(count);
            _baseStream.Write(buffer, offset, count);
            _bytesTransferred += count;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await ThrottleIfNeededAsync(count, cancellationToken);
            await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
            _bytesTransferred += count;
        }

        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

        public override void SetLength(long value) => _baseStream.SetLength(value);

        private void ThrottleIfNeeded(long requestedBytes)
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastResetTime;

            if (elapsed.TotalMilliseconds >= ResetIntervalMs)
            {
                _bytesTransferred = 0;
                _lastResetTime = now;
            }

            long allowedBytes = (long)(_bytesPerSecond * (elapsed.TotalMilliseconds / 1000.0));
            if (_bytesTransferred >= allowedBytes)
            {
                long delayMs = (long)((_bytesTransferred * 1000.0 / _bytesPerSecond) - elapsed.TotalMilliseconds);
                if (delayMs > 0)
                {
                    Thread.Sleep((int)Math.Min(delayMs, ResetIntervalMs));
                }
            }
        }

        private async Task ThrottleIfNeededAsync(long requestedBytes, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastResetTime;

            if (elapsed.TotalMilliseconds >= ResetIntervalMs)
            {
                _bytesTransferred = 0;
                _lastResetTime = now;
            }

            long allowedBytes = (long)(_bytesPerSecond * (elapsed.TotalMilliseconds / 1000.0));
            if (_bytesTransferred >= allowedBytes)
            {
                long delayMs = (long)((_bytesTransferred * 1000.0 / _bytesPerSecond) - elapsed.TotalMilliseconds);
                if (delayMs > 0)
                {
                    await Task.Delay((int)Math.Min(delayMs, ResetIntervalMs), cancellationToken);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
