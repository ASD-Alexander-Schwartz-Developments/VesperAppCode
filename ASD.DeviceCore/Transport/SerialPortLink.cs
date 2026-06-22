using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace ASD.DeviceCore.Transport
{
    /// <summary>
    /// <see cref="ISerialLink"/> over a physical serial port (System.IO.Ports). Used for
    /// the Silicon Labs BGAPI NCP adapter. Cross-platform: COMn on Windows,
    /// /dev/ttyACMx or /dev/ttyUSBx on Linux.
    /// </summary>
    public sealed class SerialPortLink : ISerialLink
    {
        private readonly SerialPort _port;

        public SerialPortLink(string portName, int baudRate = 115200)
        {
            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 2000,
            };
        }

        public bool IsOpen => _port.IsOpen;

        public void Open()
        {
            if (!_port.IsOpen)
                _port.Open();
        }

        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => _port.BaseStream.WriteAsync(data, ct).AsTask();

        public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            => _port.BaseStream.ReadAsync(buffer, ct).AsTask();

        public void Close()
        {
            if (_port.IsOpen)
                _port.Close();
        }

        public ValueTask DisposeAsync()
        {
            _port.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
