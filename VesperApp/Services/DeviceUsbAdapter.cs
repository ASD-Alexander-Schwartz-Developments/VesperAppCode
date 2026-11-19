using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VesperApp.Models;
using ASDLibUSBWrapper;
using System.Diagnostics;
using System.Threading;
using System.IO.Ports;
using System.Linq;


namespace VesperApp.Services
{
    public class DeviceUsbAdapter : IDisposable
    {
        // The two discoveries keep their own results, each replaced as a whole when a scan completes:
        // a scan never changes a list another thread is reading, and the Nanotag scan (which starts
        // from nothing every time) no longer empties the COM result under the COM scan.
        volatile IReadOnlyList<LoggerDevice> _usbDevices = Array.Empty<LoggerDevice>();
        volatile IReadOnlyList<LoggerDevice> _comDevices = Array.Empty<LoggerDevice>();
        readonly Dictionary<string, int> _comMisses = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Scans in a row a listed port may fail to answer GET_VER before its logger leaves the list.</summary>
        const int MaxMissedProbes = 2;

        UsbContext? _context;
        LoggerDevice ? _device;

        Task ? DisconnectDeviceTestTask;
        CancellationTokenSource tokenSource;
        CancellationToken token;

        // COM discovery: the ports to look at, and the GET_VER probe (a fake port list / probe in the tests).
        readonly Func<string[]> _listComPorts;
        readonly ComPortProbe _comPortProbe;
        int _comScanRunning;

        /// <summary>Baud rate of a discovered CDC logger (the SerialPort default; a CDC port ignores it).</summary>
        const int ComportBaudRate = 9600;


        public DeviceUsbAdapter() : this(null, null) { }

        internal DeviceUsbAdapter(Func<string[]>? listComPorts, ComPortProbe? comPortProbe)
        {
            _listComPorts = listComPorts ?? ListLoggerComPorts;
            _comPortProbe = comPortProbe ?? new ComPortProbe();

            // libusb is only needed for non-serial devices (Nanotag bulk); if the
            // native library can't load, degrade to serial-only discovery instead of
            // killing startup (seen on a fresh Windows: a libusb build that depended
            // on the absent VC++ runtime took the whole app down here).
            try
            {
                _context = new UsbContext();
            }
            catch (Exception ex)
            {
                _context = null;
                Debug.WriteLine("libusb unavailable - USB (non-serial) device discovery disabled: " + ex.Message);
                System.Diagnostics.Trace.TraceWarning(
                    "libusb unavailable - USB (non-serial) device discovery disabled: " + ex.Message);
            }
            _device = null;
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;

            DisconnectDeviceTestTask = Task.Run(() => DisconnectDeviceTest(token), token);
        }



        private async void DisconnectDeviceTest(CancellationToken ct)
        {
            bool prevcon = this.IsConnected;

            while (!ct.IsCancellationRequested)
            {
                if (prevcon == false)
                {
                    if (this.IsConnected == true)
                    {
                        OnConnectionEvent(new DeviceConnectionEventArgs(true, "Device Connection established"));
                        prevcon = true;
                    }
                }
                else
                {
                    if (this.IsConnected == false)
                    {
                        OnConnectionEvent(new DeviceConnectionEventArgs(false, "Device Connection established"));
                        prevcon = false;
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
        }

        public bool IsConnected 
        {
            get
            {
                if (_device == null) return false;

                return _device.IsConnected;
            }
        }



        private readonly object someEventLock = new object();

        protected void OnConnectionEvent(DeviceConnectionEventArgs e)
        {
            EventHandler<DeviceConnectionEventArgs>? handler;

            lock (this.someEventLock)
            {
                handler = this.connectEvent;
            }
            if (handler != null)
            {
                handler(this, e);
            }
        }


        private EventHandler<DeviceConnectionEventArgs>? connectEvent;

        public event EventHandler<DeviceConnectionEventArgs> ConnectionEvent
        {
            add
            {
                lock (this.someEventLock)
                {
                    this.connectEvent += value;
                }
            }

            remove
            {
                lock (this.someEventLock)
                {
                    this.connectEvent -= value;
                }
            }
        }

        public async void Dispose()
        {
            tokenSource.Cancel();

            try
            {
                DisconnectDeviceTestTask?.Wait(token);
            }
            catch (OperationCanceledException)
            {
                //Console.WriteLine($"\n{nameof(OperationCanceledException)} thrown\n");
            }
            finally
            {
                tokenSource.Dispose();
                if (_device != null)
                {
                    await _device.Disconnect();
                    _device.Dispose();
                    _device = null;
                }
            }
        }

        public async Task<string> GetManufacturerName()
        {
            string r = string.Empty;

            return await Task.FromResult(r);
        }


        public async Task<string> GetSerialNumber()
        {
            string r = string.Empty;

            return await Task.FromResult(r);
        }


        public async Task StopDeviceScanAsync()
        {
            //Debug.WriteLine("Stop Scan");

            //return await Task.Run(() => { });

        }



        public async Task<IEnumerable<LoggerDevice>> ScanDevicesAsync(uint vendorid, uint prodid, bool forceRefresh = false)
        {
            var found = forceRefresh ? new List<LoggerDevice>() : new List<LoggerDevice>(_usbDevices);

            if (this._context != null)
            {
                _context.SetDebugLevel(LogLevel.Debug);
                var usbdevices = _context.UsbDevices();
                
                foreach(var device in usbdevices)
                {
                    //Debug.WriteLine("Iterate " + device.VendorId.ToString("X") + "/" + device.ProductId.ToString("X"));
                    if (device != null && device.VendorId == vendorid && device.ProductId == prodid)
                    {
                        //Debug.WriteLine("Match " + device.VendorId.ToString("X") + "/" + device.ProductId.ToString("X"));
                        if (device.TryOpen() == true)
                        {
                            int i = 10;
                            do
                            {
                                await Task.Delay(100);
                                if (device.Info.Product != null || device.Info.SerialNumber != null)
                                    break;
                            } while (--i > 0);

                            //Debug.WriteLine("Opened " + device.VendorId.ToString("X") + "/" + device.ProductId.ToString("X") + "SN:" + device.Info.SerialNumber);
                            device.Close();

                            var dev = new LoggerDevice(this._context, (UsbDevice)device);

                            if (found.Exists( (d) => d.Equals(dev) ) == false)
                                found.Add(dev);
                        }
                    }
                }
            }

            _usbDevices = found;
            return found;
        }




        /// <summary>
        /// Cross-platform VID/PID-filtered discovery: list ports WITH their USB
        /// identity (without opening them) and keep only ASD CDC loggers. This
        /// replaces "open and probe every COM port".
        /// </summary>
        private static string[] ListLoggerComPorts() =>
            ASD.DeviceCore.Transport.SerialEnumerator.List()
                .Where(p => IsProbeCandidate(p, OperatingSystem.IsWindows()))
                .Select(p => p.PortName)
                .ToArray();

        /// <summary>
        /// A port the COM scan opens. An ASD CDC logger by its USB id; a port without a USB id only where the
        /// platform may fail to resolve one (e.g. macOS usbmodem nodes). Windows always gives a USB port its id,
        /// so a port without one is not a logger there - and a "Standard Serial over Bluetooth link" port
        /// blocks in Open for seconds while Windows tries to reach the paired device.
        /// </summary>
        internal static bool IsProbeCandidate(ASD.DeviceCore.Transport.SerialPortInfo p, bool windows) =>
            ASD.Devices.AsdDeviceIds.IsCdcLogger(p.Vid, p.Pid)
            || (!windows && p.Vid is null && p.Pid is null);

        /// <summary>
        /// The loggers on the COM ports. One scan runs at a time; a call made while a scan is running
        /// (the periodic refresh is faster than a scan with a blocked port) gets the result of the last
        /// completed scan. A logger stays listed while its port is blocked in the driver, and through
        /// <see cref="MaxMissedProbes"/> scans without a GET_VER reply; it goes at once when its port does.
        /// </summary>
        public async Task<IEnumerable<LoggerDevice>> ScanComPortsAsync(bool forceRefresh = false)
        {
            // One scan at a time: a scan already running (periodic refresh, re-enumeration wait) owns the ports.
            if (Interlocked.CompareExchange(ref _comScanRunning, 1, 0) != 0)
                return _comDevices;

            try
            {
                if (forceRefresh) _comMisses.Clear();
                IReadOnlyList<LoggerDevice> previous = forceRefresh ? Array.Empty<LoggerDevice>() : _comDevices;

                string[] comports = _listComPorts();

                /* GET_VER is: VER_MAJOR, VER_MINOR, UID0, UID1, UID2, UID3, type, reserved.
                   The ports are probed side by side: one that does not answer costs its own time only. */
                GetVerReply?[] replies = await Task.WhenAll(comports.Select(s => _comPortProbe.ProbeAsync(s)));

                var found = new List<LoggerDevice>();
                for (int i = 0; i < comports.Length; i++)
                {
                    string s = comports[i];
                    GetVerReply? reply = replies[i];
                    LoggerDevice? known = previous.FirstOrDefault(d => string.Equals(d.ComPort, s, StringComparison.OrdinalIgnoreCase));

                    if (reply != null)
                    {
                        var dev = new LoggerDevice(s, ComportBaudRate, reply.Type, reply.Serial, new System.Version(reply.Major, reply.Minor));
                        if (known != null && known.Equals(dev)) dev = known;
                        if (found.Exists((d) => d.Equals(dev)) == false)
                            found.Add(dev);
                        _comMisses.Remove(s);
                    }
                    else if (known != null)
                    {
                        if (_comPortProbe.IsStuck(s))
                        {
                            found.Add(known);
                        }
                        else
                        {
                            int misses = _comMisses.GetValueOrDefault(s) + 1;
                            if (misses <= MaxMissedProbes)
                            {
                                _comMisses[s] = misses;
                                found.Add(known);
                            }
                            else
                            {
                                _comMisses.Remove(s);
                            }
                        }
                    }
                }

                foreach (string gone in _comMisses.Keys.Where(k => !comports.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList())
                    _comMisses.Remove(gone);

                _comDevices = found;
                return found;
            }
            finally
            {
                Interlocked.Exchange(ref _comScanRunning, 0);
            }
        }



        /// <summary>
        /// Poll CDC discovery until a logger with <paramref name="serial"/> is present
        /// (a device re-enumerating after a commanded USB mode switch or a firmware
        /// flash). Returns the discovered, not-yet-connected device, or null on timeout.
        /// Callers should pause the periodic scan while waiting.
        /// </summary>
        public async Task<LoggerDevice?> WaitForComportDeviceAsync(string serial, TimeSpan timeout,
            CancellationToken ct = default)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                List<LoggerDevice> devices;
                try
                {
                    devices = (await ScanComPortsAsync(false)).ToList();
                }
                catch
                {
                    devices = new List<LoggerDevice>();
                }

                LoggerDevice? hit = devices.FirstOrDefault(d => d.IsComportDevice
                    && string.Equals(d.SerialNumber, serial, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;

                try { await Task.Delay(500, ct); } catch (OperationCanceledException) { break; }
            }
            return null;
        }

        public Task<LoggerDevice?> GetDiviceBySerialNumberAsync(string serialnum)
        {
            LoggerDevice? r = _usbDevices.Concat(_comDevices)
                .FirstOrDefault(x => x.SerialNumber?.ToUpper() == serialnum.ToUpper());

            return Task.FromResult(r);
        }


        public async Task<bool> DeviceConnect(LoggerDevice d)
        {
            bool r = false;

            r = await d.Connect();
            _device = d;

            return await Task.FromResult(r);
        }



        public async Task<bool> DeviceDisconnect()
        {
            bool r = false;

            if (_device != null)
            {
                if(_device.IsConnected)
                    await _device.Disconnect();
            }

            return await Task.FromResult(r);
        }

    }




    public class DeviceConnectionEventArgs : EventArgs
    {
        public string DebugMessage { get; set; }

        public bool IsConnected { get; set; }


        public DeviceConnectionEventArgs(bool connection, string debugmsg) : base()
        {
            DebugMessage = debugmsg;
            IsConnected = connection;
        }
    }
}
