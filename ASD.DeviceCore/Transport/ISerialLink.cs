using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASD.DeviceCore.Transport
{
    /// <summary>
    /// A raw byte-stream serial link. Abstracts <see cref="SerialPortLink"/> so the
    /// BGAPI NCP host can be unit-tested over an in-memory pipe and so the daemon can
    /// run on Linux (<c>/dev/ttyACM0</c>) or Windows (<c>COMn</c>) unchanged.
    /// </summary>
    public interface ISerialLink : IAsyncDisposable
    {
        bool IsOpen { get; }
        void Open();
        Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
        /// <summary>Read at least one byte; returns the number read into <paramref name="buffer"/>.</summary>
        Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default);
        void Close();
    }
}
