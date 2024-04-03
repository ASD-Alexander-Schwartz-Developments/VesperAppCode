using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VesperApp.Models;
using ASDLibUSBWrapper;
using System.Diagnostics;
using System.Threading;

namespace VesperApp.Services
{
    public class DockAdapter : IDockInterface<Models.DockDevice>, IDisposable
    {
        readonly uint DockPid = 0x6001;
        readonly uint DockVid = 0x0403;

        readonly List<Models.DockDevice> _devices;

        DockDevice? _connectedDock;

        UsbContext? _context;
        FTD2XX_NET.FTDI? fTDI;

        Task ? DisconnectDockTestTask;
        CancellationTokenSource tokenSource;
        CancellationToken token;


        public DockAdapter()
        {
            _devices = new List<Models.DockDevice>();
            _connectedDock = null;
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;

            callBack_connection_done = new AsyncCallback(ProcessConnectionDone);
            
            if (OperatingSystem.IsWindows() == true)
            {
                fTDI = new FTD2XX_NET.FTDI();
                _context = null;
            }
            else
            {
                fTDI = null;
                _context = new UsbContext();
            }

            DisconnectDockTestTask = Task.Run(() => DisconnectDockTest(token), token);
        }



        private async void DisconnectDockTest(CancellationToken ct)
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
                        OnConnectionEvent(new DockConnectionEventArgs(true, "Dock Connection established", _connectedDock));
                        prevcon = true;
                    }
                }
                else
                {
                    if (this.IsConnected == false)
                    {
                        OnConnectionEvent(new DockConnectionEventArgs(false, "Dock Connection established", _connectedDock));
                        prevcon = false;
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
            
           // ct.ThrowIfCancellationRequested();
        }

        public bool IsConnected 
        {
            get
            {
                if (_connectedDock == null) return false;

                return _connectedDock.IsOpen;
            }
        }





        AsyncCallback callBack_connection_done;

        private readonly object someEventLock = new object();
        private readonly object errorEventLock = new object();

        private void ProcessConnectionDone(IAsyncResult result)
        {
            if (result.IsCompleted == true)
            {
            }
        }


        protected void OnConnectionEvent(DockConnectionEventArgs e)
        {
            EventHandler<DockConnectionEventArgs>? handler;

            lock (this.someEventLock)
            {
                handler = this.connectEvent;
            }
            if (handler != null)
            {
                handler(this, e);//, callBack_connection_done, null);
            }
        }


        private EventHandler<DockConnectionEventArgs>? connectEvent;
        //private EventHandler<ErrorEventArgs> errEvent;

        public event EventHandler<DockConnectionEventArgs> ConnectionEvent
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

        public void Dispose()
        {
            tokenSource.Cancel();

            try
            {
                DisconnectDockTestTask?.Wait(token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"\n{nameof(OperationCanceledException)} thrown\n");
            }
            finally
            {
                tokenSource.Dispose();
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


        public async Task StopDocksScanAsync()
        {
            Debug.WriteLine("Stop Scan");

            //return await Task.Run(() => { });

        }


        public async Task<IEnumerable<DockDevice>> ScanDocksAsync(bool forceRefresh = false)
        {
            if (forceRefresh == true)
                this._devices.Clear();

            if (this._context != null)
            {
                _context.SetDebugLevel(LogLevel.Debug);
                var usbdevices = _context.UsbDevices();
                
                foreach(var device in usbdevices)
                {
                    Debug.WriteLine("Iterate " + device.VendorId.ToString("X") + "/" + device.ProductId.ToString("X"));
                    if (device != null && device.VendorId == DockVid && device.ProductId == DockPid)
                    {
                        Debug.WriteLine("Match " + device.VendorId.ToString("X") + "/" + device.ProductId.ToString("X"));

                        if (device.TryOpen() == true)
                        {
                            Debug.WriteLine("Opened " + device.VendorId.ToString("X") + "/" + device.ProductId.ToString("X"));
                            DockDeviceInfo _info = new DockDeviceInfo();
                            _info.Id = device.Info.SerialNumber;
                            _info.Text = device.Info.Product;
                            _info.Description = device.Info.Manufacturer;
                            device.Close();

                            var dev = new DockDevice(this._context, (UsbDevice)device, _info);

                            if (this._devices.Exists( (d) => d.Equals(dev) ) == false)
                                this._devices.Add(dev);
                        }
                    }
                }

                DockDevice[] arrayDevices = new DockDevice[this._devices.Count];
                this._devices.CopyTo(arrayDevices, 0);

                foreach (var dev in this._devices)
                {
                    bool f = false;
                    foreach (var usbdevice in usbdevices)
                    {
                        if (dev.Equals(usbdevice) == true)
                            f = true;
                    }
                    if (f == false) this._devices.Remove(dev);
                }

            }
            else if(this.fTDI != null)
            {
                UInt32 ftdiDeviceCount = 0;
                FTD2XX_NET.FTDI.FT_STATUS ftStatus = FTD2XX_NET.FTDI.FT_STATUS.FT_OK;
                // Determine the number of FTDI devices connected to the machine.
                ftStatus = fTDI.GetNumberOfDevices(ref ftdiDeviceCount);

                // Check status.
                if ((ftStatus == FTD2XX_NET.FTDI.FT_STATUS.FT_OK))
                {
                    // If no devices available, return.
                    if (ftdiDeviceCount > 0)
                    {
                        // Allocate storage for device info list.
                        FTD2XX_NET.FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList = new FTD2XX_NET.FTDI.FT_DEVICE_INFO_NODE[ftdiDeviceCount];

                        // Populate our device list.
                        ftStatus = fTDI.GetDeviceList(ftdiDeviceList);

                        for (int i = 0; i < ftdiDeviceCount; i++)
                        {
                            // Debug.WriteLine("Opened FTDI " + device.VendorId.ToString("X") + "/" + device.ProductId.ToString("X"));
                            if (ftdiDeviceList[i].SerialNumber.Length > 0)
                            {
                                DockDeviceInfo _info = new DockDeviceInfo();
                                _info.Id = ftdiDeviceList[i].SerialNumber;//device.Info.SerialNumber;
                                _info.Text = ftdiDeviceList[i].Description;//device.Info.Product;
                                _info.Description = "FTDI";//device.Info.Manufacturer;

                                var dev = new DockDevice(fTDI, ftdiDeviceList[i], _info);

                                if (this._devices.Exists((d) => d.Equals(dev)) == false)
                                    this._devices.Add(dev);
                            }
                        }

                        DockDevice[] arrayDevices = new DockDevice[this._devices.Count];
                        this._devices.CopyTo(arrayDevices, 0);

                        foreach (var dev in arrayDevices)
                        {
                            bool f = false;
                            for (int i = 0; i < ftdiDeviceCount; i++)
                            {
                                if (dev.Info.Id == ftdiDeviceList[i].SerialNumber)
                                    f = true;
                            }
                            if (f == false) this._devices.Remove(dev);
                        }
                    }
                    else
                    {
                        this._devices.Clear();  
                    }
                }
            }

            return await Task.FromResult(this._devices);
        }

        public Task<DockDevice?> GetDockBySerialNumberAsync(string serialnum)
        {
            DockDevice? r = null;

            if (_devices.Exists(x => x?.Info?.Id?.ToUpper() == serialnum.ToUpper()) == true)
            {
                r = _devices.Find(x => x?.Info?.Id?.ToUpper() == serialnum.ToUpper());
            }

            return Task.FromResult(r);
        }


        public async Task<bool> DockConnect(DockDevice d)
        {
            bool r = false;

            if (_connectedDock != null) return await Task.FromResult(r);

            if (d.UsbDock != null)
            {
                r = d.UsbDock.TryOpen();


                if (r == true)
                {
                    IUsbDevice wholeUsbDevice = d.UsbDock as IUsbDevice;
                    if (!ReferenceEquals(wholeUsbDevice, null))
                    {
                        // This is a "whole" USB device. Before it can be used, 
                        // the desired configuration and interface must be selected.

                        try
                        {
                            d.UsbDock.SetAutoDetachKernelDriver(true);
                        }
                        catch (UsbException ue)
                        {
                            Console.WriteLine("Set autodetaching kernel driver: " + ue.Message);
                        }


                        d.UsbDock.SetConfiguration(1);

                        if (d.UsbDock.IsKernelDriverActive(2) == true)
                        {
                            try
                            {
                                d.UsbDock.DetachKernelDriver(2);
                            }
                            catch (UsbException ue)
                            {
                                Console.WriteLine("Error detaching kernel driver: " + ue.Message);
                            }
                        }

                        d.UsbDock.ClaimInterface(0);
                        _connectedDock = d;
                    }
                }
            }
            else if (d.FTDIDevice != null)
            {
                if (d.FTDIDevice.OpenBySerialNumber(d.Info.Id) == FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                {
                    _connectedDock = d;
                    r = await SetBits(false, false, false);
                }
            }

            return await Task.FromResult(r);
        }


        
        public async Task<bool> SetBits(bool ven, bool boot0, bool nrst)
        {
            if (_connectedDock == null) return await Task.FromResult(false);

            if(_connectedDock.FTDIDevice == null) return await Task.FromResult(false);               // TODO: Implement a non windows variant

            if (_connectedDock.FTDIDevice.IsOpen == true)
            {
                bool result = false;
                byte cbus_bits = 0;

                if (_connectedDock.FTDIDevice.GetPinStates(ref cbus_bits) == FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                {
                    result = true;


                    if (boot0 == false)
                    {
                        cbus_bits &= (byte)(0xFD);
                    }
                    else
                    {
                        cbus_bits |= (0x02);
                    }
                    if (ven == false)
                    {
                        cbus_bits &= (byte)(0xFB);
                    }
                    else
                    {
                        cbus_bits |= (0x04);
                    }
                    if (nrst == false)
                    {
                        cbus_bits &= (byte)(0xF7);
                    }
                    else
                    {
                        cbus_bits |= (0x08);
                    }

                    result = (_connectedDock.FTDIDevice.SetBitMode(cbus_bits, FTD2XX_NET.FTDI.FT_BIT_MODES.FT_BIT_MODE_CBUS_BITBANG) == FTD2XX_NET.FTDI.FT_STATUS.FT_OK);
                }

                return await Task.FromResult(result);
            }
            
            
            return await Task.FromResult(false);
        }



        public async Task<bool> SetEnableDevice(bool ven)
        {
            if (_connectedDock == null) return await Task.FromResult(false);

            if (_connectedDock.FTDIDevice == null) return await Task.FromResult(false);               // TODO: Implement a non windows variant

            if (_connectedDock.FTDIDevice.IsOpen == true)
            {
                bool result = false;
                byte cbus_bits = 0;

                if (_connectedDock.FTDIDevice.GetPinStates(ref cbus_bits) == FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                {
                    result = true;


                    if (ven == false)
                    {
                        cbus_bits &= (byte)(0xFB);
                    }
                    else
                    {
                        cbus_bits |= (0x04);
                    }

                    result = (_connectedDock.FTDIDevice.SetBitMode(cbus_bits, FTD2XX_NET.FTDI.FT_BIT_MODES.FT_BIT_MODE_CBUS_BITBANG) == FTD2XX_NET.FTDI.FT_STATUS.FT_OK);
                }

                return await Task.FromResult(result);
            }


            return await Task.FromResult(false);
        }


        public async Task<bool> SetNReset(bool nrst)
        {
            if (_connectedDock == null) return await Task.FromResult(false);

            if (_connectedDock.FTDIDevice == null) return await Task.FromResult(false);               // TODO: Implement a non windows variant

            if (_connectedDock.FTDIDevice.IsOpen == true)
            {
                bool result = false;
                byte cbus_bits = 0;

                if (_connectedDock.FTDIDevice.GetPinStates(ref cbus_bits) == FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                {
                    result = true;


                    if (nrst == false)
                    {
                        cbus_bits &= (byte)(0xF7);
                    }
                    else
                    {
                        cbus_bits |= (0x08);
                    }

                    result = (_connectedDock.FTDIDevice.SetBitMode(cbus_bits, FTD2XX_NET.FTDI.FT_BIT_MODES.FT_BIT_MODE_CBUS_BITBANG) == FTD2XX_NET.FTDI.FT_STATUS.FT_OK);
                }

                return await Task.FromResult(result);
            }

            return await Task.FromResult(false);
        }



        public async Task<bool> SetBoot0Mode(bool boot0)
        {
            if (_connectedDock == null) return await Task.FromResult(false);

            if (_connectedDock.FTDIDevice == null) return await Task.FromResult(false);               // TODO: Implement a non windows variant

            if (_connectedDock.FTDIDevice.IsOpen == true)
            {
                bool result = false;
                byte cbus_bits = 0;

                if (_connectedDock.FTDIDevice.GetPinStates(ref cbus_bits) == FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                {
                    result = true;


                    if (boot0 == false)
                    {
                        cbus_bits &= (byte)(0xFD);
                    }
                    else
                    {
                        cbus_bits |= (0x02);
                    }

                    result = (_connectedDock.FTDIDevice.SetBitMode(cbus_bits, FTD2XX_NET.FTDI.FT_BIT_MODES.FT_BIT_MODE_CBUS_BITBANG) == FTD2XX_NET.FTDI.FT_STATUS.FT_OK);
                }

                return await Task.FromResult(result);
            }

            return await Task.FromResult(false);
        }



        public async Task<bool> ResetDevice()
        {
            bool res = false;

            res = await SetNReset(true);

            if(res == true)
            {
                await Task.Delay(150);
                res = await SetNReset(false);
            }

            return await Task.FromResult(res);
        }


        public async Task<bool> DockDisconnect()
        {
            bool r = false;

            if (_connectedDock != null)
            {
                if (_connectedDock.UsbDock != null)
                {

                    _connectedDock.UsbDock.ReleaseInterface(0);
                    _connectedDock.UsbDock.Close();
                    _connectedDock.UsbDock.Dispose();
                    _connectedDock = null;
                    GC.Collect();
                    r = true;
                }
                else if(_connectedDock.FTDIDevice != null)
                {
                    if(_connectedDock.FTDIDevice.Close() == FTD2XX_NET.FTDI.FT_STATUS.FT_OK)
                    {
                        _connectedDock = null;
                        GC.Collect();
                        r = true;
                    }
                }                
            }

            return await Task.FromResult(r);
        }

    }




    public class DockConnectionEventArgs : EventArgs
    {
        public string DebugMessage { get; set; }

        public bool IsConnected { get; set; }

        public DockDevice ? Dock { get; set; }


        public DockConnectionEventArgs(bool connection, string debugmsg, DockDevice ? dockDevice) : base()
        {
            DebugMessage = debugmsg;
            IsConnected = connection;
            Dock = dockDevice;
        }
    }
}
