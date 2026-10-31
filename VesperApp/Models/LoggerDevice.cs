using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ASDLibUSBWrapper;
using System.IO.Ports;
using Avalonia.Threading;
using ReactiveUI;
using VesperApp.Services;
using Splat;
using System.Diagnostics.Metrics;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using System.Text;
using Version = System.Version;   // not ASDLibUSBWrapper.Version

namespace VesperApp.Models
{

    public class LoggerDevice : ReactiveObject, IDisposable, IEquatable<LoggerDevice>
    {
        private UsbContext? _usbContext;
        private UsbDevice? _usbDevice;
        // CDC transport: async AsdConsoleClient-backed shim (replaces the legacy
        // three-thread SerialMessage). Same surface + wire framing. See ConsoleTransport.
        private ConsoleTransport? _comport;
        private string _serialnumber;
        private string _name;
        private DeviceTypes? _type;

        private int _interfaceVendor;
        private ASDLibUSBWrapper.ReadEndpointID _readEndpointID;
        private ASDLibUSBWrapper.WriteEndpointID _writeEndpointID;

        private UsbEndpointReader? _usbEndpointReader;
        private UsbEndpointWriter? _usbEndpointWriter;

        private FlashGeometry _flashGeometry;

        public UsbDevice? USBDevice => _usbDevice;

        public bool IsComportDevice => ((_usbDevice == null) && (_comport != null));

        public string ComPort => (_comport != null) ? _comport.PortName : string.Empty;
        public string? SerialNumber { get => _serialnumber; }
        public string? Name { get => _name; }
        public DeviceTypes? DeviceType => _type;

        public string? Nickname { get; set; }

        public string? HwId 
        { 
            get => hwid; 
            set => this.RaiseAndSetIfChanged(ref hwid, value); 
        }
        private string? hwid;

        public string? FwId 
        { 
            get => fwid; 
            set => this.RaiseAndSetIfChanged(ref fwid, value); 
        }
        private string? fwid;

        public string? LocalDateTime 
        { 
            get => localDateTime; 
            set => this.RaiseAndSetIfChanged(ref localDateTime, value);
        }
        private string? localDateTime;

        public double? DiskSize { get; set; }
        public double? DiskOccupancy { get; set; }

        public double ? DownloadProgress { get; set; } 
        public string? DiskStatus
        {
            get => diskStatus;
            set => this.RaiseAndSetIfChanged(ref diskStatus, value);
        }
        private string? diskStatus;

        public string? BatteryChargePercent 
        { 
            get => batteryCharge; 
            set => this.RaiseAndSetIfChanged(ref batteryCharge, value); 
        }
        private string? batteryCharge;



        public LoggerDevice(string port, int baudrate, DeviceTypes type, uint serial, Version? firmware = null)
        {
            this._comport = new ConsoleTransport(port, baudrate);
            _serialnumber = serial.ToString("X");
            _firmwareVersion = firmware;
            fwid = firmware != null ? FirmwareIdText(firmware) : null;
            this._usbDevice = null;
            _type = type;
            HwId = "4.0.0";
            switch (_type)
            {
                case DeviceTypes.Nanotag:
                    _name = "NANOTAG";
                    break;

                case DeviceTypes.Vesper:
                    _name = "VESPER";
                    break;

                case DeviceTypes.Pipistrelle:
                    _name = "PIPISTRELLE";
                    break;
                case DeviceTypes.Kol:
                    _name = "KOL";
                    break;
                default:
                    _name = "Unknown";
                    break;
            }

            this._comport.ErrorEvent += _comport_ErrorEvent;
            this._comport.MessageEvent += _comport_MessageEvent;
            memoryStreamPage = new MemoryStream();
        }

        private void _comport_MessageEvent(object? sender, MessageEventArgs e)
        {
            switch (e.typeOfMessage)
            {
                case MessageTypes.VESPER_GET_VER:
                    if (e.MessageData != null)
                    {
                        /* GET_VER: [0] major, [1] minor, [2..5] UID (LE), [6] device type */
                        Version fw = ParseGetVerFirmware(e.MessageData);
                        uint serial_num = (uint)((uint)e.MessageData[2] + ((uint)e.MessageData[3] << 8) + ((uint)e.MessageData[4] << 16) + ((uint)e.MessageData[5] << 24));

                        this.FwId = FirmwareIdText(fw);
                        this._serialnumber = serial_num.ToString("X");
                        this.FirmwareVersion = fw;
                    }
                    break;

                case MessageTypes.VESPER_GET_RTC:
                    if (e.MessageData != null)
                    {
                        DateTime dt = SerialMessage.BytesToDateTime(e.MessageData);
                    }
                    break;

                case MessageTypes.VESPER_GET_USB_MODE:
                    {
                        KolUsbModeState? state = KolUsbModeState.Parse(e.MessageData);
                        if (state != null) { this.UsbMode = state.Active; this.UsbState = state; }
                    }
                    break;

                case MessageTypes.VESPER_GETDISKSIZE:
                    if (e.MessageData != null && e.MessageData.Length >= 4)
                    {
                        UInt64 size = e.MessageData[0] +
                            (UInt32)(e.MessageData[1] << 8) + (UInt32)(e.MessageData[2] << 16) + (UInt32)(e.MessageData[3] << 24);

                        size *= 512;

                        double ss = size / (1024 * 1024 * 1024);
                        this.DiskSize = ss;
                    }
                    break;


                case MessageTypes.GET_VOLTAGE:
                    if (e.MessageData != null && e.MessageData.Length >= 16)
                    {
                        UInt16 header = (UInt16)((UInt16)e.MessageData[0] + (UInt16)(e.MessageData[1] << 8));

                        if ((header >> 8) == 0x06)
                        {
                            int year = SerialMessage.BCD2BIN(e.MessageData[9]) + 2000;
                            int month = SerialMessage.BCD2BIN(e.MessageData[7]);
                            int day = SerialMessage.BCD2BIN(e.MessageData[8]);

                            DateTime ts = new DateTime(year, month, day,
                            SerialMessage.BCD2BIN(e.MessageData[2]), SerialMessage.BCD2BIN(e.MessageData[3]), SerialMessage.BCD2BIN(e.MessageData[4]));
                        }
                        else
                        {
                            DateTime ts = new DateTime(e.MessageData[9] + 2000, e.MessageData[7], e.MessageData[8],
                            e.MessageData[2], e.MessageData[3], e.MessageData[4]);
                        }

                        UInt16 vltg = (UInt16)((UInt16)e.MessageData[10] + (UInt16)(e.MessageData[11] << 8));
                        UInt16 charge = (UInt16)((UInt16)e.MessageData[12] + (UInt16)(e.MessageData[13] << 8));

                        double vl = 0.0;
                        double cl = 0.0;


                        if ((header >> 8) == 0x06)
                        {
                            vl = (vltg / 4096.0) * 2.8 * 2;
                        }
                        else
                        {
                            if (charge == 0xFFFF)
                            {
                                vl = (vltg / 4096.0) * 3.3 * 2;
                            }
                            else
                            {
                                vl = vltg * 2.2E-3;
                                cl = charge / 512.0;
                            }
                        }

                        this.BatteryChargePercent = cl.ToString();
                    }
                    break;


                case MessageTypes.VESPER_GET_FLAGS:
                    if (e.MessageData != null && e.MessageData.Length >= 16)
                    {
                        byte[] data = e.MessageData;

                        UInt32 state = (UInt32)((UInt32)data[12] + (UInt32)(data[13] << 8) + (UInt32)(data[14] << 16) + (UInt32)(data[15] << 24));

                        UInt32 major = (UInt32)(data[16]);
                        UInt32 minor = (UInt32)(data[17]);
                        byte[] vuid = { data[18], data[19], data[20], data[21] };
                        var fw = new Version((int)major, (int)minor);
                        string fwid = FirmwareIdText(fw);

                        if (this.FwId != fwid) { this.FwId = fwid; }
                        this.FirmwareVersion = fw;

//                        vd.isarmed = (data[24] == 0) ? "Armed" : "Not Armed";
                        byte[] dtarray = { data[25], data[26], data[27], data[28], data[29], data[30], data[31] };
                        DateTime dtt = SerialMessage.BytesToDateTime(dtarray);
                        this.LocalDateTime = dtt.ToString();

                        UInt32 voltage = (UInt32)((UInt32)data[32] + (UInt32)(data[33] << 8));
                        this.BatteryChargePercent = voltage.ToString();
                    }
                    break;
            }
        }

        private void _comport_ErrorEvent(object? sender, Services.ErrorEventArgs e)
        {
        }

        public LoggerDevice(UsbContext c, UsbDevice d)
        {
            this._usbContext = c;
            this._usbDevice = d;
            this._comport = null;
            this._serialnumber = (this._usbDevice.Info.SerialNumber == null) ? "" : this._usbDevice.Info.SerialNumber.TrimEnd(new char[] {' ', '\n', '\r', '\0' }).ToUpper();
            this._name = this._usbDevice.Info.Product;

            if (_name != null)
            {
                string nu = _name.ToUpper();
                string nanotag = "NANOTAG";
                string vesper = "VESPER";
                string pipistrelle = "PIPISTRELLE";
                string kol = "KOL";

                if (nu.Contains(nanotag)) _type = DeviceTypes.Nanotag;
                else if (nu.Contains(vesper)) _type = DeviceTypes.Vesper;
                else if (nu.Contains(pipistrelle)) _type = DeviceTypes.Pipistrelle;
                else if (nu.Contains(kol)) _type = DeviceTypes.Kol;
            }
            else
            {
                _name = "Unknown";
            }

            switch(_type)
            {
                case DeviceTypes.Nanotag:
                    this._interfaceVendor = 2;
                    this._readEndpointID = ReadEndpointID.Ep03;
                    this._writeEndpointID = WriteEndpointID.Ep03;
                    break;

                case DeviceTypes.Vesper:
                    break;

                case DeviceTypes.Pipistrelle:
                    break;
                case DeviceTypes.Kol:
                    break;
            }


            this.BatteryChargePercent = "-";
            this.DiskOccupancy = 0.0;
            this.DiskSize = 0.0;
            this.DownloadProgress = 0.0;
            this.LocalDateTime = "-";
            this.HwId = "-";
            this.FwId = "-";

            memoryStreamPage = new MemoryStream();
        }

        public override bool Equals(object? obj) => this.Equals(obj as LoggerDevice);


        public void Dispose()
        {
            if (_usbDevice != null) _usbDevice.Dispose();
            _usbContext = null;
        }


        private UInt16 freeDiskPercent;
        private UInt32 nandSizePages;
        private UInt32 pageSizeBytes;

        public async Task<bool> Connect()
        {
            bool r = false;

            //if (_co != null) return await Task.FromResult(r);

            if (this.USBDevice != null)
            {
                r = this.USBDevice.TryOpen();

                if (r == true)
                {
                    IUsbDevice wholeUsbDevice = this.USBDevice as IUsbDevice;
                    if (!ReferenceEquals(wholeUsbDevice, null))
                    {
                        // This is a "whole" USB device. Before it can be used, 
                        // the desired configuration and interface must be selected.

                        try
                        {
                            this.USBDevice.SetAutoDetachKernelDriver(true);
                        }
                        catch (UsbException ue)
                        {
                            Console.WriteLine("Set autodetaching kernel driver: " + ue.Message);
                        }


                        this.USBDevice.SetConfiguration(1);

                        if (this.USBDevice.IsKernelDriverActive(2) == true)
                        {
                            try
                            {
                                this.USBDevice.DetachKernelDriver(2);
                            }
                            catch (UsbException ue)
                            {
                                Console.WriteLine("Error detaching kernel driver: " + ue.Message);
                            }
                        }

                        try
                        {
                            r = this.USBDevice.ClaimInterface(this._interfaceVendor);
                            this._usbEndpointReader = this.USBDevice.OpenEndpointReader(_readEndpointID, ((4096 + 64 + 4) * 2), EndpointType.Bulk);
                            this._usbEndpointWriter = this.USBDevice.OpenEndpointWriter(_writeEndpointID, EndpointType.Bulk);
                        }
                        catch (UsbException ue)
                        {
                            Console.WriteLine("Error opening bulk endpoints: " + ue.Message);
                        }

                        if(this._usbEndpointReader == null || this._usbEndpointWriter == null)
                        {
                            r = false;
                        }

                    }
                }
            }

            if (_type == DeviceTypes.Nanotag)
            {
                if (r == true && this.USBDevice != null && this.USBDevice.Info != null)
                {
                    this._serialnumber = this.USBDevice.Info.SerialNumber;
                    this._name = this.USBDevice.Info.Product;

                    byte[] response;
                    int retb = WriteRead(Nanotag.VND_CMD_GET_DISKINFO, new byte[0], out response, 40);

                    if (retb == 0)
                    {
                        nandSizePages = (UInt32)response[0] +
                                                ((UInt32)response[1] << 8) +
                                                ((UInt32)response[2] << 16) +
                                                ((UInt32)response[3] << 24);
                        pageSizeBytes = (UInt32)response[4] +
                                                ((UInt32)response[5] << 8) +
                                                ((UInt32)response[6] << 16) +
                                                ((UInt32)response[7] << 24);

                        freeDiskPercent = (UInt16)((UInt16)response[36] +
                                                ((UInt16)response[37] << 8));

                        DiskSize = ((double)(nandSizePages * pageSizeBytes)) / (double)(1024 * 1024 * 1024); // GB
                        DiskOccupancy = (double)freeDiskPercent;
                        UpdateDiskStatus();
                    }
                }
            }
            else if(_type == DeviceTypes.Vesper && this._comport != null)
            {
                this._comport.Start();
                r = this._comport.IsRunning;
            }
            else if(_type == DeviceTypes.Pipistrelle && this._comport != null)
            {
                this._comport.Start();
                r = this._comport.IsRunning;
            }
            else if (_type == DeviceTypes.Kol && this._comport != null)
            {
                this._comport.Start();
                r = this._comport.IsRunning;
            }

            return await Task.FromResult(r);
        }

        private void UpdateDiskStatus()
        {
            DiskStatus = DownloadProgress?.ToString("N2") + "%" + Environment.NewLine +
                            DiskOccupancy?.ToString("N2") + "%" + Environment.NewLine +
                            DiskSize?.ToString("N2") + "GB";

        }

        public bool IsConnected => (this.USBDevice == null) ? ((this._comport == null) ? false : this._comport.IsRunning)  : this.USBDevice.IsOpen == true;

        public async Task<bool> Disconnect()
        {
            bool r = false;

            if (this.USBDevice != null && this.USBDevice.IsOpen)
            {
                this.USBDevice.ReleaseInterface(this._interfaceVendor);
                this.USBDevice.Close();
                r = true;
            }
            else if(this._comport != null && this._comport.IsRunning)
            {
                this._comport.Stop();
                r = true;
            }

            return await Task.FromResult(r);
        }




        private int WriteRead(byte cmd, byte[] data2send, out byte[] data2recv, int bytes2recv)
        {
            return WriteRead(cmd, 0, 0, 0, data2send, out data2recv, bytes2recv);
        }


        private int WriteRead(byte cmd, byte param1, byte param2, byte param3, byte[] data2send, out byte[] data2recv, int bytes2recv)
            => WriteReadTimed(cmd, param1, param2, param3, data2send, out data2recv, bytes2recv, out _, out _);

        private readonly object _vendorLock = new object();

        /// <summary>One vendor-interface request / reply. <paramref name="t1"/> / <paramref name="t4"/>: Stopwatch
        /// timestamps just before the request is written and just after the reply was read (time sync).</summary>
        private int WriteReadTimed(byte cmd, byte param1, byte param2, byte param3, byte[] data2send, out byte[] data2recv, int bytes2recv, out long t1, out long t4)
        {
            lock (_vendorLock)
                return WriteReadLocked(cmd, param1, param2, param3, data2send, out data2recv, bytes2recv, out t1, out t4);
        }

        private int WriteReadLocked(byte cmd, byte param1, byte param2, byte param3, byte[] data2send, out byte[] data2recv, int bytes2recv, out long t1, out long t4)
        {
            data2recv = new byte[0];
            t1 = 0;
            t4 = 0;

            if (bytes2recv < 0) bytes2recv = 0;

            Console.WriteLine("Write endpoint " + _usbEndpointWriter?.EpNum.ToString() + " Max Size: " + _usbEndpointWriter?.EndpointInfo.MaxPacketSize.ToString());
            Console.WriteLine("Read endpoint " + _usbEndpointReader?.EpNum.ToString() + " Max Size: " + _usbEndpointReader?.EndpointInfo.MaxPacketSize.ToString());

            //Create a buffer with some data in it
            if (data2send == null) data2send = new byte[0];

            var buffer = new byte[4 + data2send.Length];

            int wr = 0, rd = 0;

            buffer[0] = cmd;
            buffer[1] = param1;
            buffer[2] = param2;
            buffer[3] = param3;

            data2send.CopyTo(buffer, 4);
            //Write three bytes
            //Console.WriteLine("Writing");
            t1 = Stopwatch.GetTimestamp();
            LibUsbError ?err = _usbEndpointWriter?.Write(buffer, 500, out wr);

            if(err != null && err != LibUsbError.Success)
            {
                if(err == LibUsbError.Io || err == LibUsbError.NoDevice || err == LibUsbError.NotFound)
                {
                    this.Dispose();
                    return -999;
                }
            }

           // Console.WriteLine("Written " + wr.ToString() + " bytes");

            if (wr < buffer.Length) return -2;

            var readBuffer = new byte[bytes2recv + 4];

            //Read some data
            DateTime dtstart = DateTime.Now;
            err = _usbEndpointReader?.Read(readBuffer, 500, out rd);
            t4 = Stopwatch.GetTimestamp();
            if (err != null && err != LibUsbError.Success)

            {
                if (err == LibUsbError.Io || err == LibUsbError.NoDevice || err == LibUsbError.NotFound)
                {
                    this.Dispose();
                    return -999;
                }
            }

            TimeSpan ts = DateTime.Now - dtstart;

            //Debug.Write(" Done - " + rd.ToString() + "[B]" + ts.TotalMilliseconds.ToString() + "[ms] ");

            if (rd < (4)) return -3;
            if (readBuffer[0] != (cmd | 0x80)) return -4;
            UInt16 bytesinstream = (UInt16)((UInt16)readBuffer[2] + (UInt16)readBuffer[3] << 8);

            if (bytesinstream < bytes2recv)
            {
                if (readBuffer[1] == 0xBD) return -200;
                else return -10;
            }

            //_usbEndpointReader?.Read(readBuffer, 500, out rd);
            //Console.WriteLine("Read " + rd.ToString() + " bytes");
            /*
            foreach (byte b in readBuffer)
            {
                Console.Write(b.ToString("X") + ",");
            }
            Console.WriteLine();*/

            if (rd < (4 + bytes2recv)) return -3;

            if (readBuffer[0] != (cmd | 0x80)) return -4;

            _lastReplyByte1 = readBuffer[1];        // Nanotag 1.39 GET_DATACHUNK: the page's read status
            data2recv = new byte[bytes2recv];
            Array.Copy(readBuffer, 4, data2recv, 0, bytes2recv);

            return 0;
        }



        public async Task<bool> GetInfo()
        {
            bool res = false;

            if (this.USBDevice != null && this._usbEndpointReader != null && this._usbEndpointWriter != null)
            {
                var response = new byte[0];

                int retb = WriteRead(Nanotag.VND_CMD_GET_INFO, new byte[0], out response, 20);

                if (retb == 0)
                {
                    byte fwidmj;
                    byte fwidmd;
                    byte fwidmn;
                    byte fwidbld;

                    fwidmj = response[0];
                    fwidmd = response[1];
                    fwidmn = response[2];
                    fwidbld = response[3];
                    FwId = fwidmj.ToString() + "." + fwidmd.ToString() + "." + fwidmn.ToString() + "." + fwidbld.ToString();
                    if (_type == DeviceTypes.Nanotag) FirmwareVersion = NanotagFirmwareVersion(response);

                    byte hwidmj = response[4];
                    byte hwidmn = response[5];
                    HwId = hwidmj.ToString() + "." + hwidmn.ToString();
                    // spare 2 bytes

                    UInt16 batt_level = (UInt16)((UInt16)response[8] + ((UInt16)response[9] << 8));

                    if (_type != null)
                    {
                        if (_type == DeviceTypes.Nanotag)
                        {
                            double voltage = ((((double)batt_level / 65535.0) * 3.0) * 2.0);
                            double max_volatge = 4.18;
                            double min_voltage = 2.9;
                            double charge = ((voltage - min_voltage) / (max_volatge - min_voltage)) * 100.0;

                            if (charge > 100.0) charge = 100.0;
                            else if (charge < 0.0) charge = 0.0;

                            BatteryChargePercent = charge.ToString("N2") + "%";
                        }
                    }

                    // spare 2 bytes

                    UInt32 ts = (UInt32)((UInt32)response[12] + ((UInt32)response[13] << 8) + ((UInt32)response[14] << 16)  + ((UInt32)response[15] << 24));

                    DateTime dateTime = Nanotag.FromTimestamp(ts, 0);

                    this.LocalDateTime = dateTime.ToString("G");

                    res = true;
                }
            }
            else if(_comport != null && _comport.IsRunning)
            {
                //Debug.WriteLine("Prepare Get Flags request message");
                byte[] buffer = SerialMessage.PROTO_MsgBuild((byte)MessageTypes.VESPER_GET_FLAGS, 0, new byte[0], 0);
                //Debug.WriteLine("Prepare Get Flags request array");
                this._comport.SendToDevice(buffer, 0, buffer.Length);
                //Debug.WriteLine("Sent Get Flags request");
                res = true;
            }

            return await Task.FromResult(res);
        }




        public async Task<bool> FormatDisk()
        {
            bool r = false;

            if (this.USBDevice != null && this._usbEndpointReader != null && this._usbEndpointWriter != null)
            {
                var response = new byte[0];

                int retb = WriteRead(Nanotag.VND_CMD_FORMAT_DISK, new byte[0], out response, 0);

                if (retb == 0) r = true;
            }
            else if (_comport != null && _comport.IsRunning)
            {
               // Debug.WriteLine("Prepare Format request message");
                byte[] buffer = SerialMessage.PROTO_MsgBuild((byte)MessageTypes.VESPER_FORMATDISK, 0, new byte[0], 0);
                this._comport.SendToDevice(buffer, 0, buffer.Length);
                r = true;
            }
            return await Task.FromResult(r);
        }



        public async Task<bool> Sleep(bool isarmed)
        {
            bool r = false;

            if (this.USBDevice != null && this._usbEndpointReader != null && this._usbEndpointWriter != null)
            {
                var response = new byte[0];

                int retb = WriteRead(Nanotag.VND_CMD_SET_SLEEP, ((isarmed == true) ? (byte)1 : (byte)0), 0, 0, new byte[0], out response, 0);

                if (retb == 0) r = true;
            }
            else if (_comport != null && _comport.IsRunning)
            {
                MessageOutEventArgs moea = new MessageOutEventArgs();
                byte[] data = new byte[1];
                data[0] = (byte)((isarmed == true) ? (byte)1 : (byte)0);
                moea.MessageData = SerialMessage.PROTO_MsgBuild((byte)MessageTypes.VESPER_SLEEP, (byte)data.Length, data, 0);
                this._comport.SendMessage(moea);
            }

            return await Task.FromResult(r);
        }



        public async Task<bool> Bootloader()
        {
            bool r = false;

            if (this.USBDevice != null && this._usbEndpointReader != null && this._usbEndpointWriter != null)
            {
                var response = new byte[0];

                int retb = WriteRead(Nanotag.VND_CMD_SET_BOOT, 0, 0, 0, new byte[0], out response, 0);

                if (retb == 0) r = true;
            }

            return await Task.FromResult(r);
        }



        public async Task<bool> SetDateTime(DateTime ldt)
        {
            bool r = false;

            if (this.USBDevice != null && this._usbEndpointReader != null && this._usbEndpointWriter != null)
            {
                var response = new byte[0];

                UInt32 ts = Nanotag.ToTimestamp(ldt);

                byte[] buffer = new byte[4];
                buffer[0] = (byte)(ts & 0xFF);
                buffer[1] = (byte)(ts >> 8);
                buffer[2] = (byte)(ts >> 16);
                buffer[3] = (byte)(ts >> 24);

                int retb = WriteRead(Nanotag.VND_CMD_SET_DATETIME, buffer, out response, 0);

                if (retb == 0) r = true;
            }
            else if(this._comport != null && this._comport.IsRunning == true)
            {
                MessageOutEventArgs moea = new MessageOutEventArgs();
                byte[] data = SerialMessage.DateTimeToBytes(ldt);
                moea.MessageData = SerialMessage.PROTO_MsgBuild((byte)MessageTypes.VESPER_SET_RTC, (byte)data.Length, data, 0);
                this._comport.SendMessage(moea);
            }

            return await Task.FromResult(r);
        }



        /// <summary>
        /// Send a binary command frame over the CDC console and await the matching
        /// response payload, correlating by message type via the comport
        /// MessageEvent. Returns the response payload bytes (header/checksum
        /// stripped), or null on timeout or when this is not a running comport
        /// device. Used by the microphone health check (VESPER_TEST_AUDIO).
        /// </summary>
        public async Task<byte[]?> SendCommandAsync(MessageTypes type, byte[] payload, int timeoutMs = 8000)
        {
            if (this._comport == null || this._comport.IsRunning == false)
                return null;

            payload ??= new byte[0];

            var tcs = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<MessageEventArgs> handler = null!;
            handler = (s, e) =>
            {
                if (e.typeOfMessage == type)
                    tcs.TrySetResult(e.MessageData);
            };

            this._comport.MessageEvent += handler;
            try
            {
                // OutMsgID = 0 matches the rest of this class; the device's checksum
                // uses whatever id we send, so it must be the same we sum here.
                byte[] frame = SerialMessage.PROTO_MsgBuild((byte)type, (byte)payload.Length, payload, 0);
                this._comport.SendToDevice(frame, 0, frame.Length);

                using (var cts = new System.Threading.CancellationTokenSource(timeoutMs))
                using (cts.Token.Register(() => tcs.TrySetResult(null)))
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                this._comport.MessageEvent -= handler;
            }
        }

        #region KOL USB modes / commanded bootloader (CDC console)

        /// <summary>The device is a KOL reached over its CDC console.</summary>
        public bool IsKol => _type == DeviceTypes.Kol && IsComportDevice;

        /// <summary>MAJOR.MINOR from a GET_VER reply (id 15, VesperU5 / KOL): [0] major, [1] minor.</summary>
        internal static Version ParseGetVerFirmware(byte[] reply) => new Version(reply[0], reply[1]);

        /// <summary>Firmware id as shown in the device list: "1.7" for KOL 1.7, "1.22" for Vesper 1.22.</summary>
        internal static string FirmwareIdText(Version firmware) => $"{firmware.Major}.{firmware.Minor}";

        /// <summary>
        /// Nanotag feed / release version from a VND_CMD_GET_INFO reply ([0] MAJOR, [1] MID, [2] MINOR, [3] BLD,
        /// shown as 0.0.1.39): the release is MINOR.BLD ("1.39"), which is what the capability gates compare.
        /// </summary>
        internal static Version NanotagFirmwareVersion(byte[] info) => new Version(info[2], info[3]);

        /// <summary>Application firmware MAJOR.MINOR (GET_VER / GET_RTC; Nanotag: MINOR.BLD from GET_INFO);
        /// null until known.</summary>
        public Version? FirmwareVersion
        {
            get => _firmwareVersion;
            private set
            {
                if (value == _firmwareVersion) return;
                this.RaiseAndSetIfChanged(ref _firmwareVersion, value);
                this.RaisePropertyChanged(nameof(SupportsUsbModes));
            }
        }
        private Version? _firmwareVersion;

        /// <summary>The unit answers the USB mode / DFU / mic parameter commands (62-66) on its CDC
        /// console: any KOL, a Vesper / Pipistrelle with new enough firmware
        /// (<see cref="DeviceCapabilities.SupportsUsbModes"/>).</summary>
        public bool SupportsUsbModes => IsComportDevice && DeviceCapabilities.SupportsUsbModes(_type, _firmwareVersion);

        /// <summary>Last USB operational mode reported by a KOL (null until queried).</summary>
        public KolUsbMode? UsbMode
        {
            get => usbMode;
            set => this.RaiseAndSetIfChanged(ref usbMode, value);
        }
        private KolUsbMode? usbMode;

        /// <summary>Last full VESPER_GET_USB_MODE reply (KOL: with status flags and record
        /// readiness on firmware with the operating states); null until queried.</summary>
        public KolUsbModeState? UsbState
        {
            get => usbState;
            private set => this.RaiseAndSetIfChanged(ref usbState, value);
        }
        private KolUsbModeState? usbState;

        /// <summary>
        /// Send a command whose reply is a bare ACK / NACK frame (not a typed reply)
        /// and await it. True on ACK, false on NACK, timeout, or when this is not a
        /// running comport device.
        /// </summary>
        public async Task<bool> SendAckCommandAsync(MessageTypes type, byte[] payload, int timeoutMs = 3000)
        {
            var (ack, _, _) = await SendAckCommandWithReplyAsync(type, payload, timeoutMs);
            return ack;
        }

        /// <summary>
        /// As <see cref="SendAckCommandAsync"/>, also returning whether the device answered at
        /// all and the payload of a NACK (a KOL record request carries the refusal reason).
        /// </summary>
        public async Task<(bool Ack, bool Answered, byte[]? NackData)> SendAckCommandWithReplyAsync(MessageTypes type, byte[] payload, int timeoutMs = 3000)
        {
            if (this._comport == null || this._comport.IsRunning == false)
                return (false, false, null);

            payload ??= new byte[0];

            var tcs = new TaskCompletionSource<(bool, bool, byte[]?)>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<MessageEventArgs> handler = null!;
            handler = (s, e) =>
            {
                if (e.typeOfMessage == MessageTypes.ACK) tcs.TrySetResult((true, true, null));
                else if (e.typeOfMessage == MessageTypes.NACK) tcs.TrySetResult((false, true, e.MessageData));
            };

            this._comport.MessageEvent += handler;
            try
            {
                byte[] frame = SerialMessage.PROTO_MsgBuild((byte)type, (byte)payload.Length, payload, 0);
                this._comport.SendToDevice(frame, 0, frame.Length);

                using (var cts = new System.Threading.CancellationTokenSource(timeoutMs))
                using (cts.Token.Register(() => tcs.TrySetResult((false, false, null))))
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                this._comport.MessageEvent -= handler;
            }
        }

        /// <summary>Read the unit's active/pending USB mode (VESPER_GET_USB_MODE). On a KOL,
        /// <paramref name="rereadConfig"/> has firmware with the operating states read
        /// config.json from the card again first (it is otherwise the one loaded at boot -
        /// the user may have just copied a new one onto the USB disk); others ignore it.</summary>
        public async Task<KolUsbModeState?> GetUsbModeAsync(int timeoutMs = 3000, bool rereadConfig = false)
        {
            if (!SupportsUsbModes) return null;
            byte[] payload = (rereadConfig && IsKol) ? new byte[] { 0x01 } : new byte[0];
            byte[]? resp = await SendCommandAsync(MessageTypes.VESPER_GET_USB_MODE, payload,
                                                  rereadConfig ? Math.Max(timeoutMs, 5000) : timeoutMs);
            KolUsbModeState? state = KolUsbModeState.Parse(resp);
            if (state != null) { this.UsbMode = state.Active; this.UsbState = state; }
            return state;
        }

        /// <summary>
        /// Ask a KOL to start standalone recording (VESPER_SET_USB_MODE(1)). The firmware
        /// re-reads config.json and checks card, configuration, sensors, clock and free space:
        /// ACK = it leaves USB and records; NACK carries the <see cref="KolRecordReason"/>
        /// (firmware without the operating states NACKs without one).
        /// </summary>
        public async Task<KolRecordResult> StartRecordingAsync(int timeoutMs = 6000)
        {
            if (!SupportsUsbModes) return new KolRecordResult(false, null, false);
            var (ack, answered, nack) = await SendAckCommandWithReplyAsync(
                MessageTypes.VESPER_SET_USB_MODE, new[] { (byte)KolUsbMode.Sampling }, timeoutMs);
            KolRecordReason? reason = (!ack && nack is { Length: >= 1 }) ? KolRecordReasonInfo.From(nack[0]) : null;
            return new KolRecordResult(ack, reason, answered);
        }

        /// <summary>
        /// Request a KOL USB mode (VESPER_SET_USB_MODE). Returns true when the device
        /// ACKed; the transition happens AFTER the ACK - expect this CDC link to drop
        /// (Idle / Live Audio re-enumerate; Sampling detaches for good).
        /// </summary>
        public async Task<bool> SetUsbModeAsync(KolUsbMode mode, int timeoutMs = 3000)
        {
            if (!SupportsUsbModes) return false;
            return await SendAckCommandAsync(MessageTypes.VESPER_SET_USB_MODE, new[] { (byte)mode }, timeoutMs);
        }

        /// <summary>
        /// Command the KOL into the ST ROM DFU bootloader (VESPER_ENTER_BOOTLOADER).
        /// After the ACK the device re-enumerates as 0483:DF11; any reset returns it
        /// to the application. Use <see cref="Services.Stm32DfuFlasher.FlashDfuDeviceAsync"/>
        /// to program it.
        /// </summary>
        public async Task<bool> EnterBootloaderAsync(int timeoutMs = 3000)
        {
            if (!SupportsUsbModes) return false;
            return await SendAckCommandAsync(MessageTypes.VESPER_ENTER_BOOTLOADER, new byte[0], timeoutMs);
        }

        /// <summary>Read the KOL's live microphone parameters and Live Audio status
        /// (VESPER_GET_MIC_PARAMS). Null when the firmware does not support it.</summary>
        public async Task<KolMicStatus?> GetMicParamsAsync(int timeoutMs = 3000)
        {
            if (!SupportsUsbModes) return null;
            byte[]? resp = await SendCommandAsync(MessageTypes.VESPER_GET_MIC_PARAMS, new byte[0], timeoutMs);
            return KolMicStatus.Parse(resp);
        }

        /// <summary>Set the KOL's microphone parameters (VESPER_SET_MIC_PARAMS). Applied at
        /// once to a running Live Audio capture; kept in RAM only (not written to config.json).</summary>
        public async Task<bool> SetMicParamsAsync(KolMicParams p, int timeoutMs = 3000)
        {
            if (!SupportsUsbModes) return false;
            return await SendAckCommandAsync(MessageTypes.VESPER_SET_MIC_PARAMS, p.Pack(), timeoutMs);
        }

        #endregion

        #region RTC time sync + crystal calibration (KOL, Vesper, Pipistrelle; asd-interfaces interfaces/rtc-time-sync.md)

        /// <summary>A console-attached logger (KOL / Vesper / Pipistrelle over its CDC port).</summary>
        public bool IsConsoleLogger => IsComportDevice && _type != DeviceTypes.Nanotag;

        /// <summary>
        /// One VESPER_TIME_SYNC exchange. The host send time is taken right before the frame is
        /// handed to the port, the receive time is the serial read that completed the reply (not
        /// the later event dispatch). Null on timeout or NACK (firmware without the command).
        /// </summary>
        public async Task<RtcExchange?> TimeSyncExchangeAsync(uint seq, int timeoutMs = 1000)
        {
            if (!IsConsoleLogger || this._comport == null || !this._comport.IsRunning) return null;

            var tcs = new TaskCompletionSource<(byte[]? data, long rx)>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<MessageEventArgs> handler = (s, e) =>
            {
                if (e.typeOfMessage == MessageTypes.VESPER_TIME_SYNC) tcs.TrySetResult((e.MessageData, e.RxTimestamp));
                else if (e.typeOfMessage == MessageTypes.NACK) tcs.TrySetResult((null, 0));
            };

            this._comport.MessageEvent += handler;
            try
            {
                byte[] payload = BitConverter.GetBytes(seq);
                byte[] frame = SerialMessage.PROTO_MsgBuild((byte)MessageTypes.VESPER_TIME_SYNC, (byte)payload.Length, payload, 0);
                long t1 = Stopwatch.GetTimestamp();
                this._comport.SendToDevice(frame, 0, frame.Length);

                using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
                using (cts.Token.Register(() => tcs.TrySetResult((null, 0))))
                {
                    var (data, rx) = await tcs.Task.ConfigureAwait(false);
                    if (data == null) return null;
                    RtcExchange? ex = RtcExchange.Parse(data, t1, rx != 0 ? rx : Stopwatch.GetTimestamp());
                    return (ex != null && ex.Seq == seq) ? ex : null;
                }
            }
            finally
            {
                this._comport.MessageEvent -= handler;
            }
        }

        /// <summary>Set the device RTC (whole seconds; the device restarts its sub-second counter)
        /// and wait for the ACK.</summary>
        public async Task<bool> SetRtcAsync(DateTime deviceTime, int timeoutMs = 2000)
        {
            if (!IsConsoleLogger) return false;
            return await SendAckCommandAsync(MessageTypes.VESPER_SET_RTC, SerialMessage.DateTimeToBytes(deviceTime), timeoutMs);
        }

        /// <summary>Shift the device RTC by less than one second (VESPER_RTC_SHIFT, + advances).</summary>
        public async Task<bool> RtcShiftAsync(int microseconds, int timeoutMs = 2000)
        {
            if (!IsConsoleLogger) return false;
            return await SendAckCommandAsync(MessageTypes.VESPER_RTC_SHIFT, BitConverter.GetBytes(microseconds), timeoutMs);
        }

        /// <summary>VESPER_RTC_CAL request (op 0 get, 1 set, 2 clear, 3 measure temperature).</summary>
        public async Task<RtcCalStatus?> RtcCalAsync(byte[] payload, int timeoutMs = 2000)
        {
            if (!IsConsoleLogger) return null;
            return RtcCalStatus.Parse(await SendCommandAsync(MessageTypes.VESPER_RTC_CAL, payload, timeoutMs));
        }

        #endregion



        /// <summary>First Nanotag release with TIME_SYNC / RTC_CAL on the vendor interface.</summary>
        public static readonly Version NanotagRtcCalFirmware = new(1, 37);

        /// <summary>A Nanotag connected over its USB vendor interface.</summary>
        public bool IsNanotagVendor => _type == DeviceTypes.Nanotag && this.USBDevice != null
                                       && this._usbEndpointReader != null && this._usbEndpointWriter != null;

        /// <summary>
        /// Nanotag TIME_SYNC (vendor 0x08): the console message 76 exchange. t1 / t4 are taken around the USB
        /// write and read. Null when the unit does not answer (firmware before 1.37) or the seq does not match.
        /// </summary>
        public Task<RtcExchange?> NanotagTimeSyncAsync(uint seq)
        {
            if (!IsNanotagVendor) return Task.FromResult<RtcExchange?>(null);
            return Task.Run(() =>
            {
                int r = WriteReadTimed(Nanotag.VND_CMD_TIME_SYNC, 0, 0, 0, BitConverter.GetBytes(seq), out byte[] data,
                                       RtcExchange.ReplyLength, out long t1, out long t4);
                if (r != 0) return null;
                RtcExchange? ex = RtcExchange.Parse(data, t1, t4);
                return (ex != null && ex.Seq == seq) ? ex : null;
            });
        }

        /// <summary>Nanotag RTC_CAL (vendor 0x09): payload [op, args…] as for message 78. Null on NACK / no answer.</summary>
        public Task<RtcCalStatus?> NanotagRtcCalAsync(byte[] payload)
        {
            if (!IsNanotagVendor) return Task.FromResult<RtcCalStatus?>(null);
            return Task.Run(() =>
                WriteRead(Nanotag.VND_CMD_RTC_CAL, payload, out byte[] data, RtcCalStatus.ReplyLength) == 0
                    ? RtcCalStatus.Parse(data) : null);
        }

        public async Task<bool> UploadConfigFile(string json)

        {
            bool r = false;

            if (this.USBDevice != null && this._usbEndpointReader != null && this._usbEndpointWriter != null)
            {
                var response = new byte[0];

                byte[] cfg = Nanotag.ConfigBinaryConverter(json);

                if (cfg != null && cfg.Length >= (Nanotag.CONFIG_CHUNK_SIZE + Nanotag.CONFIG_CHUNK_SIZE + Nanotag.CONFIG_CHUNK_SIZE))
                {
                    byte[] main_cfg = new byte[Nanotag.CONFIG_CHUNK_SIZE];
                    byte[] sch_cfg = new byte[Nanotag.CONFIG_CHUNK_SIZE];
                    byte[] dev_cfg = new byte[Nanotag.CONFIG_CHUNK_SIZE];

                    Array.Copy(cfg, 0, main_cfg, 0, Nanotag.CONFIG_CHUNK_SIZE);
                    Array.Copy(cfg, Nanotag.CONFIG_CHUNK_SIZE, dev_cfg, 0, Nanotag.CONFIG_CHUNK_SIZE);
                    Array.Copy(cfg, Nanotag.CONFIG_CHUNK_SIZE * 2, sch_cfg, 0, Nanotag.CONFIG_CHUNK_SIZE);

                    int retb = WriteRead(Nanotag.VND_CMD_SET_CFGCHUNK_GEN, main_cfg, out response, 0);
                    //Debug.WriteLine("Sent General config result: " + retb.ToString());
                    if (retb == 0)
                    {
                        await Task.Delay(200);
                        retb = WriteRead(Nanotag.VND_CMD_SET_CFGCHUNK_DEV, dev_cfg, out response, 0);
                        //Debug.WriteLine("Sent Device config result: " + retb.ToString());

                        if (retb == 0)
                        {
                            await Task.Delay(200);
                            retb = WriteRead(Nanotag.VND_CMD_SET_CFGCHUNK_SCH, sch_cfg, out response, 0);
                            //Debug.WriteLine("Sent Schedule config result: " + retb.ToString());
                            r = true;
                        }
                    }
                }
            }

            return await Task.FromResult(r);
        }


        private void CountPageStatus(uint page, byte status, List<string> log)
        {
            switch (status)
            {
                case NanotagService.PageOk: return;
                case NanotagService.PageCorrected: DownloadPagesCorrected++; return;     // normal for NAND: not listed
                case NanotagService.PageCorrectedHigh: DownloadPagesCorrected++; break;
                case NanotagService.PageUncorrectable: DownloadPagesUncorrectable++; break;
                default: DownloadPagesReadErrors++; break;
            }
            log.Add($"page {page}: {NanotagService.PageStatusName(status)}");
        }

        public async Task<bool> DownloadPages(string ? path = "")
        {
            bool r = false;

            if(this.USBDevice != null && this._usbEndpointReader != null && this._usbEndpointWriter != null)
            {
                var response = new byte[0];

                int retb = WriteRead(Nanotag.VND_CMD_GET_DATANPAGES, new byte[0], out response, 8);

                if (retb == 0)
                {
                    await Task.Run(async () =>
                    {
                        DownloadProgress = 0;

                        if (path == null || path.Length == 0)
                            path = Directory.GetCurrentDirectory();

                        if (path.EndsWith("\\") == false)
                            path += "\\";

                        bool foldersok = false;
                        string error = string.Empty;
                        string etype = string.Empty;

                        try
                        {
                            Directory.CreateDirectory(path + "GPS");
                            Directory.CreateDirectory(path + "ACC");
                            Directory.CreateDirectory(path + "SNS");
                            foldersok = true;
                        }
                        catch (ArgumentNullException anex)
                        {
                            error = anex.Message;
                            etype = "Bad output folder name";
                        }
                        catch (ArgumentException aex)
                        {
                            error = aex.Message;
                            etype = "Empty output folder name";
                        }
                        catch(PathTooLongException ptlex)
                        {
                            error = ptlex.Message;
                            etype = "Output folder name too long";
                        }
                        catch (DirectoryNotFoundException dnfex)
                        {
                            error = dnfex.Message;
                            etype = "Output folder does not exists";
                        }
                        catch (NotSupportedException nsex)
                        {
                            error = nsex.Message;
                            etype = "Operation in not supported";
                        }
                        catch (IOException ioex)
                        {
                            error = ioex.Message;
                            etype = "I/O Error creating output folders";
                        }
                        catch(UnauthorizedAccessException uaaex)
                        {
                            error = uaaex.Message;
                            etype = "Access denied creating output folders";
                        }


                        if (foldersok == true)
                        {
                            UInt32 first_page = (UInt32)(((UInt32)response[0]) + ((UInt32)response[1] << 8) + ((UInt32)response[2] << 16) + ((UInt32)response[3] << 24));
                            UInt32 last_page = (UInt32)(((UInt32)response[4]) + ((UInt32)response[5] << 8) + ((UInt32)response[6] << 16) + ((UInt32)response[7] << 24));

                            //Debug.WriteLine("Got disk data usage info: First Page = " + first_page.ToString() + ", Last Page = " + last_page.ToString());
                            var addons = new NanotagAddonCollector();       // TPRH31 / ALS3001D / ProxTit pages (firmware 1.36+)
                            var readStatus = new List<string>();            // pages that did not read cleanly (firmware 1.39+)
                            DownloadPagesCorrected = DownloadPagesUncorrectable = DownloadPagesReadErrors = DownloadPagesCrcErrors = 0;

                            for (UInt32 i = first_page; i < last_page; i++, DownloadProgress = (double)((double)i / (double)last_page) * 100.0)
                            {
                                //Console.WriteLine(("\r" + i.ToString()).PadLeft(Console.WindowWidth - Console.CursorLeft - 1));
                                byte[] addr = new byte[4];
                                addr[0] = (byte)(i & 0xFF);
                                addr[1] = (byte)(i >> 8);
                                addr[2] = (byte)(i >> 16);
                                addr[3] = (byte)(i >> 24);
                                var dpageresponse = new byte[0];
                                Debug.Write("Trying to download page = " + i.ToString());
                                int getpageresult = WriteRead(Nanotag.VND_CMD_GET_DATACHUNK, addr, out dpageresponse, (128 + 4096));

                                if (getpageresult == 0)
                                {
                                    Debug.Write(" - OK ");
                                    if (FirmwareVersion != null && FirmwareVersion >= NanotagDiskHealthFirmware)
                                        CountPageStatus(i, _lastReplyByte1, readStatus);
                                    if (!NanotagService.PageCrcOk(dpageresponse))         // firmware 1.39+ data pages
                                    {
                                        DownloadPagesCrcErrors++;
                                        readStatus.Add($"page {i}: data CRC mismatch");
                                    }

                                    NanotagPage? np = NanotagPage.TryParse(dpageresponse);
                                    if (np == null || !addons.Add(np))
                                        ProcessOnePage(dpageresponse, path);
                                }
                                else if (getpageresult == -200)
                                {
                                    Debug.WriteLine(" - BAD Block");
                                    i += 63;    // the 64th will be inside for loop
                                }
                                await Dispatcher.UIThread.InvokeAsync(() => UpdateDiskStatus());
                            }
                            try { addons.WriteAll(path, _serialnumber); }
                            catch (Exception ex) { Debug.WriteLine("Add-on output failed: " + ex.Message); }
                            if (readStatus.Count > 0)
                            {
                                try { File.WriteAllLines(Path.Combine(path, "download-read-status.txt"), readStatus); }
                                catch (Exception ex) { Debug.WriteLine("Read status file failed: " + ex.Message); }
                            }

                            // LOGGER log area (firmware 1.35+): its own ring of pages, read after the data
                            if (FirmwareVersion == null || FirmwareVersion >= NanotagLoggerFirmware)
                            {
                                try { DownloadLogArea(path); }
                                catch (Exception ex) { Debug.WriteLine("Log download failed: " + ex.Message); }
                            }

                            DownloadProgress = 100.0;
                            UpdateDiskStatus();
                            r = true;
                        }
                        else
                        {
                            var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard(
                            new MessageBoxStandardParams
                            {
                                ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok,
                                ContentTitle = "Download Data Failed",
                                ContentHeader = etype,
                                ContentMessage = error,
                                WindowIcon = App.MainWindow?.Icon,
                                Icon = MsBox.Avalonia.Enums.Icon.Error
                            });

                            await messageBoxStandardWindow.ShowWindowDialogAsync(App.MainWindow);

                        }
                    });
                }
                else
                {
                    ///Debug.WriteLine("Failed getting data partition info");
                }
            }


            return await Task.FromResult(r);
        }






        /// <summary>First Nanotag release with the LOGGER log area (GET_LOGNPAGES / GET_LOGCHUNK).</summary>
        public static readonly Version NanotagLoggerFirmware = new(1, 35);

        /// <summary>Firmware 1.39: GET_DATACHUNK reports each page's read status (reply byte 1),
        /// GET_DISKHEALTH 0x0D, format state in GET_DISKINFO.</summary>
        public static readonly Version NanotagDiskHealthFirmware = new(1, 39);

        private byte _lastReplyByte1;

        /// <summary>Pages of the last Nanotag download that read with ECC corrected / uncorrectable / a read error
        /// (firmware 1.39+; always 0 before). Shown in the status line and written to download-read-status.txt.</summary>
        public int DownloadPagesCorrected { get; private set; }
        public int DownloadPagesUncorrectable { get; private set; }
        public int DownloadPagesReadErrors { get; private set; }
        public int DownloadPagesCrcErrors { get; private set; }

        /// <summary>GET_DISKHEALTH (Nanotag 1.39+): block counts, ECC counters since boot, write pointer, format state.</summary>
        public async Task<NanotagDiskHealth?> GetDiskHealthAsync()
        {
            if (!IsNanotagVendor) return null;
            return await Task.Run(() =>
            {
                int rc = WriteRead(Nanotag.VND_CMD_GET_DISKHEALTH, new byte[0], out byte[] resp, NanotagDiskHealth.Length);
                return rc == 0 ? NanotagDiskHealth.Parse(resp) : null;
            });
        }

        /// <summary>The three config rows as the tag holds them (GET_CFGCHUNK_GEN / _DEV / _SCH, 256 B each);
        /// null when a read fails.</summary>
        public async Task<(byte[] General, byte[] Devices, byte[] Schedule)?> ReadNanotagConfigAsync()
        {
            if (!IsNanotagVendor) return null;
            return await Task.Run<(byte[], byte[], byte[])?>(() =>
            {
                if (WriteRead(Nanotag.VND_CMD_GET_CFGCHUNK_GEN, new byte[0], out byte[] g, Nanotag.CONFIG_CHUNK_SIZE) != 0) return null;
                if (WriteRead(Nanotag.VND_CMD_GET_CFGCHUNK_DEV, new byte[0], out byte[] d, Nanotag.CONFIG_CHUNK_SIZE) != 0) return null;
                if (WriteRead(Nanotag.VND_CMD_GET_CFGCHUNK_SCH, new byte[0], out byte[] sc, Nanotag.CONFIG_CHUNK_SIZE) != 0) return null;
                return (g, d, sc);
            });
        }

        /// <summary>
        /// Read the Nanotag LOGGER log area (oldest to newest page, wrapping; a bad block is skipped) and write
        /// HEALTH/H….HBN + .csv / .log.csv into <paramref name="path"/>. Returns the number of log pages read, -1
        /// when the unit does not answer GET_LOGNPAGES (firmware before 1.35).
        /// </summary>
        public int DownloadLogArea(string path)
        {
            if (WriteRead(Nanotag.VND_CMD_GET_LOGNPAGES, new byte[0], out byte[] resp, 16) != 0) return -1;
            var range = NanotagLog.ParseRange(resp);
            if (range == null) return -1;
            List<uint> order = NanotagLog.ReadOrder(range.Value.Start, range.Value.End, range.Value.Oldest, range.Value.Newest);

            var pages = new List<NanotagPage>();
            for (int k = 0; k < order.Count; k++)
            {
                uint page = order[k];
                int res = WriteRead(Nanotag.VND_CMD_GET_LOGCHUNK, BitConverter.GetBytes(page), out byte[] data, NanotagPage.FullSize);
                if (res == 0)
                {
                    NanotagPage? np = NanotagPage.TryParse(data);
                    if (np != null && np.PageType == NanotagPage.TypeLog) pages.Add(np);
                }
                else if (res == -200)
                {
                    uint block = page / NanotagPage.PagesPerBlock;          // bad block: skip the rest of it
                    while (k + 1 < order.Count && order[k + 1] / NanotagPage.PagesPerBlock == block) k++;
                }
                else if (res == -999)
                {
                    break;                                                  // unplugged
                }
                DownloadProgress = 100.0 * (k + 1) / Math.Max(1, order.Count);
            }

            byte[] records = NanotagLog.Assemble(pages, out ushort logmask, out uint first, out uint last);
            NanotagLog.Write(path, records, logmask, first, last, _serialnumber);
            return pages.Count;
        }

        private MemoryStream memoryStreamPage;

        private readonly int bytes_in_flash_page = (4096 + 128);
        private readonly UInt32 preamnle_ok = 0x54445341;
        private readonly int reference_year = 2020;
        private readonly byte NAND_FS_SNAP_PAGE_TYPE = 0x21;

        private bool ProcessOnePage(byte [] data, string path)
        {
            if (data.Length > 1)
            {
                memoryStreamPage.Write(data, 0, data.Length);

                if (memoryStreamPage.Length >= bytes_in_flash_page)
                {
                    //Debug.WriteLine("parsing page with length of " + memoryStreamPage.Length.ToString());

                    byte[] bytes;
                    bytes = memoryStreamPage.GetBuffer();
                    if(bytes != null && bytes.Length >= bytes_in_flash_page)
                    { 
                        int ij = 4096;

                        UInt32 ODBA = 0, VEDBA = 0;

                        UInt32 f_preamble = 0;
                        f_preamble += bytes[ij++];
                        f_preamble += (UInt32)(bytes[ij++] << 8);
                        f_preamble += (UInt32)(bytes[ij++] << 16);
                        f_preamble += (UInt32)(bytes[ij++] << 24);

                        UInt16 MetadataLength = 0;
                        MetadataLength += bytes[ij++];
                        MetadataLength += (UInt16)(bytes[ij++] << 8);

                        byte page_type = bytes[ij];
                        ij++;
                        byte page_subtype = bytes[ij];
                        ij++;

                        UInt16 page_rev = 0;
                        page_rev += bytes[ij++];
                        page_rev += (UInt16)(bytes[ij++] << 8);

                        UInt16 battLevel = 0;
                        battLevel += bytes[ij++];
                        battLevel += (UInt16)(bytes[ij++] << 8);


                        ij += 16;    /// padding

                        char snapType = (char)bytes[ij];
                        ij++;
                        ij++;


                        UInt32 snapTimestamp = 0;
                        UInt32 snapID = 0, snapIndex = 0, snapPagesInSnap = 0;
                        UInt16 snapSubsecond = 0;

                        snapTimestamp += bytes[ij++];
                        snapTimestamp += (UInt32)(bytes[ij++] << 8);
                        snapTimestamp += (UInt32)(bytes[ij++] << 16);
                        snapTimestamp += (UInt32)(bytes[ij++] << 24);

                        snapSubsecond += bytes[ij++];
                        snapSubsecond += (UInt16)(bytes[ij++] << 8);

                        UInt16 SampleRate = 0;
                        SampleRate += bytes[ij++];
                        SampleRate += (UInt16)(bytes[ij++] << 8);


                        snapID += bytes[ij++];
                        snapID += (UInt32)(bytes[ij++] << 8);
                        snapID += (UInt32)(bytes[ij++] << 16);
                        snapID += (UInt32)(bytes[ij++] << 24);

                        snapPagesInSnap += bytes[ij++];
                        snapPagesInSnap += (UInt32)(bytes[ij++] << 8);
                        snapPagesInSnap += (UInt32)(bytes[ij++] << 16);
                        snapPagesInSnap += (UInt32)(bytes[ij++] << 24);

                        snapIndex += bytes[ij++];
                        snapIndex += (UInt32)(bytes[ij++] << 8);
                        snapIndex += (UInt32)(bytes[ij++] << 16);
                        snapIndex += (UInt32)(bytes[ij++] << 24);

                        if (snapIndex > 0) snapIndex--;

                        ODBA += bytes[ij++];
                        ODBA += (UInt32)(bytes[ij++] << 8);
                        ODBA += (UInt32)(bytes[ij++] << 16);
                        ODBA += (UInt32)(bytes[ij++] << 24);

                        VEDBA += bytes[ij++];
                        VEDBA += (UInt32)(bytes[ij++] << 8);
                        VEDBA += (UInt32)(bytes[ij++] << 16);
                        VEDBA += (UInt32)(bytes[ij++] << 24);

                        Debug.WriteLine("- Its a " + snapType.ToString() + " ");

                        if (f_preamble == preamnle_ok &&
                            page_type == NAND_FS_SNAP_PAGE_TYPE &&
                            snapType == 'G' &&
                            snapIndex < snapPagesInSnap)
                        {
                            //string outfolder = this.textOutputFolder.Text;
                            string outfolder = path + "GPS\\";
                            

                            //                        if (outfolder.EndsWith("\\") == false)
                            //outfolder += "\\";

                            string filename;//= String.Format("{0}\\{1}.bin",
                                            //                    new object[] {
                                            //                    outfolder, "G"+snapID.ToString("D6")});

                            DateTime dt = Nanotag.FromTimestamp(snapTimestamp, snapSubsecond);

                            filename = String.Format("{0}{1}{2,4:D4}_{3,2:D2}_{4,2:D2}_{5,2:D2}_{6,2:D2}_{7,2:D2}_GC0.dat",
                                new object[] {
                                    outfolder, "snap.", dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second});

                            Debug.WriteLine("Get good page index=" + snapIndex.ToString() + " out of " + snapPagesInSnap.ToString() + " to be saved into " + filename);


                            for (int si = 0; si < bytes.Length; si += 4)
                            {
                                byte l0 = bytes[si];
                                byte l1 = bytes[si + 1];
                                byte l2 = bytes[si + 2];
                                byte l3 = bytes[si + 3];

                                UInt32 temp = (UInt32)(((UInt32)(l0) + (UInt32)(l1 << 8) + (UInt32)(l2 << 16) + (UInt32)(l3 << 24)));

                                temp = VesperApp.Services.ByteHelper.SwapWords(temp);

                                bytes[si] = ((byte)(temp & 0xFF));
                                bytes[si + 1] = ((byte)(temp >> 8));
                                bytes[si + 2] = ((byte)(temp >> 16));
                                bytes[si + 3] = ((byte)(temp >> 24));
                            }


                            using (System.IO.FileStream file = new FileStream(filename, FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite))
                            {
                                if (file.Length < (snapPagesInSnap * 4096))
                                    file.SetLength(snapPagesInSnap * 4096);

                                file.Seek(snapIndex * 4096, SeekOrigin.Begin);
                                file.Write(bytes, 0, 4096);
                                file.Flush();
                                file.Close();
                            }

                            string metadata = string.Empty;
                            double o = Math.Sqrt((double)ODBA);
                            double v = Math.Sqrt((double)VEDBA);
                            metadata += "SNAP: " + snapID.ToString() + "[" + snapIndex.ToString() + "/" + snapPagesInSnap.ToString() + "]" + Environment.NewLine;
                            metadata += "ODBA: " + o.ToString() + Environment.NewLine;
                            metadata += "VEDBA: " + v.ToString() + Environment.NewLine;

                            File.WriteAllText(filename + "_" + snapIndex.ToString() + ".txt", metadata);
                        }
                        else if (f_preamble == preamnle_ok &&
                            page_type == NAND_FS_SNAP_PAGE_TYPE &&
                            snapType == 'A')
                        {
                            string outfolder = path + "ACC\\";

                            //                        if (outfolder.EndsWith("\\") == false)
                            //outfolder += "\\";

                            string filename;//= String.Format("{0}\\{1}.bin",
                                            //                    new object[] {
                                            //                    outfolder, "G"+snapID.ToString("D6")});

                            DateTime dt = Nanotag.FromTimestamp(snapTimestamp, snapSubsecond);

                            filename = String.Format("{0}{1}{2,4:D4}_{3,2:D2}_{4,2:D2}_{5,2:D2}_{6,2:D2}_{7,2:D2}_{8,3:D3}.abn",
                                new object[] {
                                    outfolder, "NACC.", dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 
                                    dt.Second, dt.Millisecond});
                            string mname = filename + ".txt";

                            //Debug.WriteLine("Get good page index=" + snapIndex.ToString() + " out of " + snapPagesInSnap.ToString() + " to be saved into " + filename);

                            using (System.IO.FileStream file = new FileStream(filename, FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite))
                            {
                                file.Write(bytes, 0, 4096);
                                file.Flush();
                                file.Close();
                            }

                            if (SampleRate > 0 && SampleRate < 100)
                            {
                                using (System.IO.FileStream file = new FileStream(mname, FileMode.Create, System.IO.FileAccess.ReadWrite))
                                {
                                    byte[] bytesm = Encoding.UTF8.GetBytes(String.Format("{0}", new object[] { ("SampleRate:" + SampleRate.ToString() + Environment.NewLine) }));
                                    file.Write(bytesm, 0, bytesm.Length);
                                    file.Flush();
                                    file.Close();
                                }
                            }
                        }
                        else if (f_preamble == preamnle_ok &&
                            page_type == NAND_FS_SNAP_PAGE_TYPE &&
                            snapType == 'S' &&
                            snapIndex < snapPagesInSnap)
                        {

                        }
                        else
                        {
                            Debug.WriteLine("Unknown Page: " + f_preamble.ToString("X") + " " + page_type.ToString("X") + " " + snapType.ToString() );
                        }
                    }
                    else
                    {
                        //Debug.WriteLine("Page is bad");
                    }

                    memoryStreamPage.Close();
                    memoryStreamPage = new System.IO.MemoryStream();
                }
            }

            return (true);
        }





        public bool Equals(LoggerDevice? ld)
        {
            if (ld is null)
            {
                return false;
            }

            // Optimization for a common success case.
            if (Object.ReferenceEquals(this, ld!))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (this.GetType() != ld.GetType())
            {
                return false;
            }

            if ((this._serialnumber != ld._serialnumber)) return false;

            if(this._comport == null)
            {
                return ((this.USBDevice?.VendorId == ld.USBDevice?.VendorId) && (this.USBDevice?.ProductId == ld.USBDevice?.ProductId));
            }
            else
            {
                return this._comport.PortName == ld?._comport?.PortName;
            }

            // Return true if the fields match.
            // Note that the base class is not invoked because it is
            // System.Object, which defines Equals as reference equality.
            
        }

        public override int GetHashCode() => ((this._serialnumber == null) ? "" : this._serialnumber).GetHashCode();

        public static bool operator == (LoggerDevice? lhs, LoggerDevice? rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                // Only the left side is null.
                return false;
            }
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        public static bool operator != (LoggerDevice? lhs, LoggerDevice? rhs) => !(lhs == rhs);
    }
}
