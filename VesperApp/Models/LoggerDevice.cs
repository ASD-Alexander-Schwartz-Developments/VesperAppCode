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

namespace VesperApp.Models
{

    public class LoggerDevice : ReactiveObject, IDisposable, IEquatable<LoggerDevice>
    {
        private UsbContext? _usbContext;
        private UsbDevice? _usbDevice;
        private SerialMessage? _comport;
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



        public LoggerDevice(string port, int baudrate, DeviceTypes type, uint serial)
        {
            this._comport = new SerialMessage(port, baudrate);
            _serialnumber = serial.ToString("X");
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
                        byte l = e.MessageData[0];
                        byte h = e.MessageData[1];

                        uint serial_num = (uint)((uint)e.MessageData[2] + ((uint)e.MessageData[3] << 8) + ((uint)e.MessageData[4] << 16) + ((uint)e.MessageData[5] << 24));


                        this.FwId = h.ToString() + l.ToString();
                        this._serialnumber = serial_num.ToString("X");
                    }
                    break;

                case MessageTypes.VESPER_GET_RTC:
                    if (e.MessageData != null)
                    {
                        DateTime dt = SerialMessage.BytesToDateTime(e.MessageData);
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
                        string fwid = major.ToString() + "." + minor.ToString();

                        if (this.FwId != fwid) { this.FwId = fwid; }

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
        {
            data2recv = new byte[0];

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

                                    ProcessOnePage(dpageresponse, path);
                                }
                                else if (getpageresult == -200)
                                {
                                    Debug.WriteLine(" - BAD Block");
                                    i += 63;    // the 64th will be inside for loop
                                }
                                await Dispatcher.UIThread.InvokeAsync(() => UpdateDiskStatus());
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
