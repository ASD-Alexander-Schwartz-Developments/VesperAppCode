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
        readonly List<Models.LoggerDevice> _loggerDevices;

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
            _loggerDevices = new List<Models.LoggerDevice>();

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
            if (forceRefresh == true)
                this._loggerDevices.Clear();

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

                            if (this._loggerDevices.Exists( (d) => d.Equals(dev) ) == false)
                                this._loggerDevices.Add(dev);
                        }
                    }
                }
            }

            return await Task.FromResult(this._loggerDevices);
        }




        /// <summary>
        /// Cross-platform VID/PID-filtered discovery: list ports WITH their USB
        /// identity (without opening them) and keep only ASD CDC loggers. This
        /// replaces "open and probe every COM port". Ports whose USB id the
        /// platform couldn't resolve (null) are still probed as a safety net
        /// (e.g. macOS usbmodem nodes), but known non-ASB USB devices are skipped.
        /// </summary>
        private static string[] ListLoggerComPorts() =>
            ASD.DeviceCore.Transport.SerialEnumerator.List()
                .Where(p => ASD.Devices.AsdDeviceIds.IsCdcLogger(p.Vid, p.Pid)
                            || (p.Vid is null && p.Pid is null))
                .Select(p => p.PortName)
                .ToArray();

        public async Task<IEnumerable<LoggerDevice>> ScanComPortsAsync(bool forceRefresh = false)
        {
            if (forceRefresh == true)
                this._loggerDevices.Clear();

            // One scan at a time: a scan already running (periodic refresh, re-enumeration wait) owns the ports.
            if (Interlocked.CompareExchange(ref _comScanRunning, 1, 0) == 0)
            {
                try
                {
                    string[] comports = _listComPorts();

                    foreach (string s in comports)
                    {
                        /* GET_VER is: VER_MAJOR, VER_MINOR, UID0, UID1, UID2, UID3, type, reserved */
                        GetVerReply? reply = await _comPortProbe.ProbeAsync(s);
                        if (reply != null)
                        {
                            var dev = new LoggerDevice(s, ComportBaudRate, reply.Type, reply.Serial, new System.Version(reply.Major, reply.Minor));

                            if (this._loggerDevices.Exists((d) => d.Equals(dev)) == false)
                                this._loggerDevices.Add(dev);
                        }
                    }

                    List<string> _names = comports.ToList();

                    for(int i = 0; i < this._loggerDevices.Count; i++)
                    {
                        if (_loggerDevices[i].IsComportDevice)
                        {
                            if (_names.Exists((d) => d.Equals(_loggerDevices[i].ComPort)) == false) 
                            {
                               // Debug.WriteLine("Remove " + _loggerDevices[i].Name);
                                _loggerDevices.RemoveAt(i);
                                i = 0;
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _comScanRunning, 0);
                }
            }

            return await Task.FromResult(this._loggerDevices);
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
            LoggerDevice? r = null;

            if (_loggerDevices.Exists(x => x.SerialNumber?.ToUpper() == serialnum.ToUpper()) == true)
            {
                r = _loggerDevices.Find(x => x.SerialNumber?.ToUpper() == serialnum.ToUpper());
            }

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
