using System;
using ASD.DeviceCore.Protocol;
using ASD.DeviceCore.Transport;

namespace VesperApp.Services
{
    /// <summary>
    /// Drop-in replacement for the legacy <see cref="SerialMessage"/> instance API used
    /// by <see cref="Models.LoggerDevice"/>, backed by the cross-platform async
    /// <see cref="AsdConsoleClient"/> (single cancellable read loop) instead of the old
    /// three-thread reader/sender/dispatcher. Exposes the same surface
    /// (Start/Stop/IsRunning/PortName/SendToDevice/SendMessage/MessageEvent/ErrorEvent)
    /// and reuses the same <see cref="MessageEventArgs"/> so call sites are unchanged.
    /// The wire framing is identical (see <see cref="AsdFrame"/>).
    /// </summary>
    public sealed class ConsoleTransport
    {
        private readonly string _port;
        private readonly int _baud;
        private AsdConsoleClient? _client;
        private volatile bool _running;

        public ConsoleTransport(string port, int baudRate)
        {
            _port = port;
            _baud = baudRate;
        }

        public string PortName => _port;

        public bool IsRunning => _running && _client?.IsRunning == true;

        public event EventHandler<MessageEventArgs>? MessageEvent;
        public event EventHandler<ErrorEventArgs>? ErrorEvent;

        public void Start()
        {
            if (_running) return;
            try
            {
                var link = new SerialPortLink(_port, _baud);
                var client = new AsdConsoleClient(link);
                client.MessageReceived += OnMessage;
                client.Start();
                _client = client;
                _running = true;
            }
            catch (Exception ex)
            {
                _running = false;
                ErrorEvent?.Invoke(this, new ErrorEventArgs
                {
                    typeOfMessage = ErrorTypes.PORT_CLOSED,
                    DebugMessage = ex.Message,
                });
            }
        }

        public void Stop()
        {
            _running = false;
            AsdConsoleClient? client = _client;
            _client = null;
            if (client != null)
            {
                client.MessageReceived -= OnMessage;
                _ = client.DisposeAsync(); // cancels the read loop + closes the port
            }
        }

        /// <summary>Send a pre-built frame (legacy raw-buffer path).</summary>
        public void SendToDevice(byte[] buffer, int offset, int count)
        {
            ReadOnlyMemory<byte> frame =
                (offset == 0 && count == buffer.Length) ? buffer : buffer.AsMemory(offset, count);
            _ = _client?.SendRawAsync(frame);
        }

        /// <summary>Send a pre-built frame wrapped in the legacy out-event args.</summary>
        public void SendMessage(MessageOutEventArgs mo)
        {
            if (mo.MessageData == null) return;
            ReadOnlyMemory<byte> frame = mo.offset == 0
                ? mo.MessageData
                : mo.MessageData.AsMemory(mo.offset);
            _ = _client?.SendRawAsync(frame);
        }

        private void OnMessage(AsdMessage m)
        {
            MessageEvent?.Invoke(this, new MessageEventArgs
            {
                typeOfMessage = (MessageTypes)m.Type,
                MessageData = m.Payload,
            });
        }
    }
}
