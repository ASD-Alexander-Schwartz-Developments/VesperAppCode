using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VesperApp.Models;
using ASDLibUSBWrapper;
using System.Diagnostics;
using System.Threading;
using System.IO.Ports;


namespace VesperApp.Services
{
    public class DeviceUsbAdapter : IDisposable
    {
        readonly List<Models.LoggerDevice> _loggerDevices;

        DockDevice _connectedDock;
        UsbContext _context;
        LoggerDevice ? _device;

        Task ? DisconnectDeviceTestTask;
        CancellationTokenSource tokenSource;
        CancellationToken token;

        SerialPort? _serialPort;


        public DeviceUsbAdapter(DockDevice connectedDock)
        {
            _loggerDevices = new List<Models.LoggerDevice>();
            _connectedDock = connectedDock;

            _context = new UsbContext();
            _device = null;
            _serialPort = new SerialPort();
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;

           // callBack_connection_done = new AsyncCallback(ProcessConnectionDone);

            DisconnectDeviceTestTask = Task.Run(() => DisconnectDeviceTest(token), token);
        }



        private async void DisconnectDeviceTest(CancellationToken ct)
        {
            bool prevcon = this.IsConnected;

            //if (ct.IsCancellationRequested)
            //{
            //    ct.ThrowIfCancellationRequested();
            //}

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
            
            //ct.ThrowIfCancellationRequested();
        }

        public bool IsConnected 
        {
            get
            {
                if (_device == null) return false;

                return _device.IsConnected;
            }
        }



        //AsyncCallback callBack_connection_done;

        private readonly object someEventLock = new object();
        private readonly object errorEventLock = new object();

        /*private void ProcessConnectionDone(IAsyncResult result)
        {
            if (result.IsCompleted == true)
            {
            }
        }*/


        protected void OnConnectionEvent(DeviceConnectionEventArgs e)
        {
            EventHandler<DeviceConnectionEventArgs>? handler;

            lock (this.someEventLock)
            {
                handler = this.connectEvent;
            }
            if (handler != null)
            {
                handler(this, e);//, callBack_connection_done, null);
            }
        }


        private EventHandler<DeviceConnectionEventArgs>? connectEvent;
        //private EventHandler<ErrorEventArgs> errEvent;

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
        /*
        public event EventHandler<ErrorEventArgs> ErrorEvent
        {
            add
            {
                lock (this.errorEventLock)
                {
                    this.errEvent += value;
                }
            }

            remove
            {
                lock (this.errorEventLock)
                {
                    this.errEvent -= value;
                }
            }
        }
        */

        public async void Dispose()
        {
            tokenSource.Cancel();

            try
            {
                DisconnectDeviceTestTask?.Wait(token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"\n{nameof(OperationCanceledException)} thrown\n");
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
            Debug.WriteLine("Stop Scan");

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
                        Debug.WriteLine("Match " + device.VendorId.ToString("X") + "/" + device.ProductId.ToString("X"));

                        if (device.TryOpen() == true)
                        {
                            int i = 10;
                            do
                            {
                                await Task.Delay(100);
                                if (device.Info.Product != null || device.Info.SerialNumber != null)
                                    break;
                            } while (--i > 0);

                            Debug.WriteLine("Opened " + device.VendorId.ToString("X") + "/" + device.ProductId.ToString("X") + "SN:" + device.Info.SerialNumber);
                            device.Close();

                            var dev = new LoggerDevice(this._context, (UsbDevice)device);

                            if (this._loggerDevices.Exists( (d) => d.Equals(dev) ) == false)
                                this._loggerDevices.Add(dev);
                        }
                    }
                }
/*
                LoggerDevice[] arrayDevices = new LoggerDevice[this._loggerDevices.Count];
                this._loggerDevices.CopyTo(arrayDevices, 0);

                foreach (var dev in arrayDevices)
                {
                    bool f = false;
                    foreach (var usbdevice in usbdevices)
                    {
                        if (usbdevice.VendorId == vendorid && usbdevice.ProductId == prodid)
                        {
                            var devi = new LoggerDevice(this._context, (UsbDevice)usbdevice);
                            if (dev.USBDevice?.Equals(devi) == true)
                            {
                                f = true;
                                break;
                            }
                        }
                    }
                    if (f == false)
                    {
                        this._loggerDevices.Remove(dev);
                        Debug.WriteLine("Removing from adapter " + dev.SerialNumber);
                    }
                }*/

            }

            return await Task.FromResult(this._loggerDevices);
        }




        public async Task<IEnumerable<LoggerDevice>> ScanComPortsAsync(bool forceRefresh = false)
        {
            if (forceRefresh == true)
                this._loggerDevices.Clear();

            if (this._serialPort != null && this._serialPort.IsOpen == false)
            {
                string []comports = SerialPort.GetPortNames();

                foreach (string s in comports)
                {
                    try
                    {
                        if(this._serialPort.IsOpen == true)
                            this._serialPort.Close();

                        this._serialPort.PortName = s;
                        this._serialPort.ReadTimeout = 100;
                        this._serialPort.WriteTimeout = 100;
                        this._serialPort.Open();
                        Debug.WriteLine("Opened " + s);
                        /* GET_VER is: VER_MAJOR, VER_MINOR, UID0, UID1, UID2, UID3, type, reserved */

                        byte[] buffer = SerialMessage.PROTO_MsgBuild((byte)MessageTypes.VESPER_GET_VER,
                            0, new byte[0], 0);
                        this._serialPort.Write(buffer, 0, buffer.Length);
                        
                        await Task.Delay(75);

                        buffer = new byte[16];
                        if (this._serialPort.Read(buffer, 0, buffer.Length) >= 8)
                        {
                            int i;
                            for(i = 0; i < buffer.Length; i++)
                            {
                                if (buffer[i] == 0x5A)
                                    break;
                            }
                            if (buffer[i] == 0x5A && buffer[i+2] == (byte)MessageTypes.VESPER_GET_VER)             /// need to implement something real here
                            {
                                byte major = (byte)(buffer[i + 4]);
                                byte minor = (byte)(buffer[i + 5]);
                                uint serial = (uint)((uint)buffer[i+6] + ((uint)buffer[i+7] << 8) + ((uint)buffer[i+8] << 16) + ((uint)buffer[i+9] << 24));
                                DeviceTypes type = (DeviceTypes)buffer[i+10];

                                var dev = new LoggerDevice(_serialPort.PortName, _serialPort.BaudRate, type, serial);

                                if (this._loggerDevices.Exists((d) => d.Equals(dev)) == false)
                                    this._loggerDevices.Add(dev);
                            }
                        }
                    }
                    catch(Exception ex)
                    { 
                        Debug.WriteLine(ex.Message);
                    }
                    finally
                    { 
                        this._serialPort.Close();
                        Debug.WriteLine("Closed " + s);
                    }
                }
            }

            return await Task.FromResult(this._loggerDevices);
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
