using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using VesperApp.Models;

namespace VesperApp.Services
{
    /// <summary>GET_VER identity of a console logger found on a COM port: VER_MAJOR, VER_MINOR, UID, type.</summary>
    internal sealed record GetVerReply(byte Major, byte Minor, uint Serial, DeviceTypes Type);

    /// <summary>The serial-port operations of the COM scan: <see cref="SerialProbePort"/> in the app, a fake in the tests.</summary>
    internal interface IProbePort
    {
        string PortName { get; }
        void Open();
        void Write(byte[] buffer, int offset, int count);
        /// <summary>Bytes received; 0 or a <see cref="TimeoutException"/> when nothing arrived within the read timeout.</summary>
        int Read(byte[] buffer, int offset, int count);
        void Close();
    }

    internal sealed class SerialProbePort : IProbePort
    {
        private readonly SerialPort _port;

        public SerialProbePort(string portName)
        {
            _port = new SerialPort(portName)
            {
                ReadTimeout = 100,
                WriteTimeout = 100,
                // STM32 CDC firmware may hold TX until the host asserts DTR;
                // Windows drivers often mask this, Linux cdc_acm does not.
                DtrEnable = true,
            };
        }

        public string PortName => _port.PortName;
        public void Open() => _port.Open();
        public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);
        public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);

        public void Close()
        {
            if (_port.IsOpen) _port.Close();
            _port.Dispose();
        }
    }

    /// <summary>
    /// COM-port discovery probe: open the port, send GET_VER, collect the reply, close.
    /// Open, Write, Read and Close of System.IO.Ports can each block on a USB CDC port whose device is busy
    /// (KOL bring-up 2026-10-26, B3: the scan hung and kept the COM port open while the KOL was in Live Audio),
    /// so the exchange runs on a worker thread with an overall budget. A port that exceeds it is reported as
    /// not answering and left to its worker (which closes the port when the blocking call returns); it is not
    /// probed again until that worker has finished.
    /// </summary>
    internal sealed class ComPortProbe
    {
        public static readonly TimeSpan DefaultReplyWindow = TimeSpan.FromMilliseconds(500);
        public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(3);

        private readonly Func<string, IProbePort> _open;
        private readonly TimeSpan _budget;
        private readonly TimeSpan _replyWindow;
        private readonly ConcurrentDictionary<string, Task> _stuck = new(StringComparer.OrdinalIgnoreCase);

        public ComPortProbe(Func<string, IProbePort>? open = null, TimeSpan? budget = null, TimeSpan? replyWindow = null)
        {
            _open = open ?? (name => new SerialProbePort(name));
            _budget = budget ?? DefaultBudget;
            _replyWindow = replyWindow ?? DefaultReplyWindow;
        }

        /// <summary>A port whose earlier probe is still blocked in the driver.</summary>
        public bool IsStuck(string portName) => _stuck.TryGetValue(portName, out Task? t) && !t.IsCompleted;

        /// <summary>The logger on <paramref name="portName"/>; null when none answered within the budget.</summary>
        public async Task<GetVerReply?> ProbeAsync(string portName)
        {
            if (_stuck.TryGetValue(portName, out Task? previous))
            {
                if (!previous.IsCompleted) return null;
                _stuck.TryRemove(portName, out _);
            }

            Task<GetVerReply?> work = Task.Run(() => Exchange(_open(portName), _replyWindow));
            if (await Task.WhenAny(work, Task.Delay(_budget)).ConfigureAwait(false) == work)
                return work.IsCompletedSuccessfully ? work.Result : null;

            _stuck[portName] = work;
            Trace.TraceWarning($"COM scan: {portName} did not complete the GET_VER probe within {_budget.TotalSeconds:0.#} s; skipped until it does.");
            return null;
        }

        /// <summary>The blocking exchange. Any failure (port in use, no reply, write timeout) is "no logger here".</summary>
        internal static GetVerReply? Exchange(IProbePort port, TimeSpan replyWindow)
        {
            try
            {
                port.Open();
                byte[] request = SerialMessage.PROTO_MsgBuild((byte)MessageTypes.VESPER_GET_VER, 0, new byte[0], 0);
                port.Write(request, 0, request.Length);

                // Accumulate the reply: serial data may arrive in chunks (especially on Linux cdc_acm),
                // and other frames may precede it, so keep reading until a GET_VER frame is in the buffer.
                var buffer = new byte[256];
                int got = 0;
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < replyWindow)
                {
                    Thread.Sleep(25);
                    try
                    {
                        int n = port.Read(buffer, got, buffer.Length - got);
                        if (n > 0) got += n;
                    }
                    catch (TimeoutException) { }

                    GetVerReply? reply = FindGetVer(buffer, got);
                    if (reply != null) return reply;
                    if (got == buffer.Length)
                    {
                        /* keep the tail: a frame may straddle the end */
                        Array.Copy(buffer, got - 16, buffer, 0, 16);
                        got = 16;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                try { port.Close(); } catch { /* the port vanished */ }
            }
        }

        /// <summary>The first GET_VER reply in <paramref name="buffer"/>[0..count):
        /// SOH, id, type, len, MAJOR, MINOR, UID0..3, device type, ...</summary>
        internal static GetVerReply? FindGetVer(byte[] buffer, int count)
        {
            for (int i = 0; i + 10 < count; i++)
            {
                if (buffer[i] != SerialMessage.SOH || buffer[i + 2] != (byte)MessageTypes.VESPER_GET_VER) continue;
                uint serial = buffer[i + 6] | ((uint)buffer[i + 7] << 8) | ((uint)buffer[i + 8] << 16) | ((uint)buffer[i + 9] << 24);
                return new GetVerReply(buffer[i + 4], buffer[i + 5], serial, (DeviceTypes)buffer[i + 10]);
            }
            return null;
        }
    }
}
