using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ASDLibUSBWrapper;
using ReactiveUI;

namespace VesperApp.Models
{
    
    public class LoggerDevice : ReactiveObject, IDisposable, IEquatable<LoggerDevice>
    {
        public const byte VND_CMD_GET_INFO = 0x00;
        public const byte VND_CMD_GET_DISKINFO = 0x01;
        public const byte VND_CMD_GET_LOGNPAGES = 0x02;
        public const byte VND_CMD_GET_LOGCHUNK = 0x03;
        public const byte VND_CMD_GET_DATANPAGES = 0x04;
        public const byte VND_CMD_GET_DATACHUNK = 0x05;
        public const byte VND_CMD_SET_DATETIME = 0x06;
        public const byte VND_CMD_SET_SLEEP = 0x07;

        public const byte VND_CMD_GET_CFGCHUNK_GEN = 0x0A;
        public const byte VND_CMD_GET_CFGCHUNK_SCH = 0x0B;
        public const byte VND_CMD_GET_CFGCHUNK_DEV = 0x0C;
        public const byte VND_CMD_SET_CFGCHUNK_GEN = 0x1A;
        public const byte VND_CMD_SET_CFGCHUNK_SCH = 0x1B;
        public const byte VND_CMD_SET_CFGCHUNK_DEV = 0x1C;

        public const byte VND_CMD_FORMAT_DISK = 0x3F;



        private UsbContext? _usbContext;
        private UsbDevice? _usbDevice;
        private FTD2XX_NET.FTDI? _fTDI;
        private FTD2XX_NET.FTDI.FT_DEVICE_INFO_NODE? _ftdiNODE;
        private string _serialnumber;
        private string _name;
        private DeviceTypes ? _type;

        private int _interfaceVender;
        private ASDLibUSBWrapper.ReadEndpointID _readEndpointID;
        private ASDLibUSBWrapper.WriteEndpointID _writeEndpointID;

        private UsbEndpointReader ? _usbEndpointReader;
        private UsbEndpointWriter ? _usbEndpointWriter;
        private FlashGeometry _flashGeometry;

        public UsbDevice? USBDevice => _usbDevice;
        public FTD2XX_NET.FTDI? FTDIDevice => _fTDI;


        public string? SerialNumber { get => _serialnumber; }
        public string? Name { get => _name; }

        public string? Nickname { get; set; }

        public string? HwId { get; set; }
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

        public string? DiskSize { get; set; }
        public string? DiskOccupancy { get; set; }
        public string? BatteryChargePercent 
        { 
            get => batteryCharge; 
            set => this.RaiseAndSetIfChanged(ref batteryCharge, value); 
        }
        private string? batteryCharge;

        public LoggerDevice(UsbContext c, UsbDevice d)
        {
            _ftdiNODE = null;
            _fTDI = null;
            this._usbContext = c;
            this._usbDevice = d;
            this._serialnumber = (this._usbDevice.Info.SerialNumber == null) ? "" : this._usbDevice.Info.SerialNumber.TrimEnd(new char[] {' ', '\n', '\r', '\0' }).ToUpper();
            this._name = this._usbDevice.Info.Product;

            if (_name != null)
            {
                if (this._name.Contains("Nanotag")) _type = DeviceTypes.Nanotag;
                else if (this._name.Contains("Vesper")) _type = DeviceTypes.Vesper;
                else if (this._name.Contains("Pipistrelle")) _type = DeviceTypes.Pipistrelle;
            }

            this._interfaceVender = 2;
            this._readEndpointID = ReadEndpointID.Ep03;
            this._writeEndpointID = WriteEndpointID.Ep03;

            this.BatteryChargePercent = "-";
            this.DiskOccupancy = "-";
            this.DiskSize = "-";
            this.LocalDateTime = DateTime.Now.ToLongDateString();
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
                            r = this.USBDevice.ClaimInterface(this._interfaceVender);
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

            if(r == true && this.USBDevice != null && this.USBDevice.Info != null)
            {
                this._serialnumber = this.USBDevice.Info.SerialNumber;
                this._name = this.USBDevice.Info.Product;
            }

            return await Task.FromResult(r);
        }



        public bool IsConnected => (this.USBDevice == null) ? false : this.USBDevice.IsOpen;

        public async Task<bool> Disconnect()
        {
            bool r = false;

            if (this.USBDevice != null && this.USBDevice.IsOpen)
            {
                this.USBDevice.ReleaseInterface(this._interfaceVender);
                this.USBDevice.Close();
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
            _usbEndpointWriter?.Write(buffer, 500, out wr);

           // Console.WriteLine("Written " + wr.ToString() + " bytes");

            if (wr < buffer.Length) return -2;

            var readBuffer = new byte[bytes2recv + 4];

            //Read some data
            DateTime dtstart = DateTime.Now;
            _usbEndpointReader?.Read(readBuffer, 500, out rd);
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

                int retb = WriteRead(VND_CMD_GET_INFO, new byte[0], out response, 20);

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

                    // spare 4 bytes

                    UInt16 batt_level = (UInt16)((UInt16)response[8] + ((UInt16)response[9] << 8));
                    BatteryChargePercent = batt_level + "[%]";

                    // spare 2 bytes

                    UInt32 ts = (UInt32)((UInt32)response[12] + ((UInt32)response[13] << 8) + ((UInt32)response[14] << 16)  + ((UInt32)response[15] << 24));

                    DateTime dateTime = Nanotag.FromTimestamp(ts, 0);

                    this.LocalDateTime = dateTime.ToString("G");

                    res = true;
                }
            }

            return await Task.FromResult(res);
        }




        public async Task<bool> FormatDisk()
        {
            bool r = false;

            if (this.USBDevice != null && this._usbEndpointReader != null && this._usbEndpointWriter != null)
            {
                var response = new byte[0];

                int retb = WriteRead(VND_CMD_FORMAT_DISK, new byte[0], out response, 0);

                if (retb == 0) r = true;
            }

            return await Task.FromResult(r);
        }



        public async Task<bool> Sleep(bool isarmed)
        {
            bool r = false;

            if (this.USBDevice != null && this._usbEndpointReader != null && this._usbEndpointWriter != null)
            {
                var response = new byte[0];

                int retb = WriteRead(VND_CMD_SET_SLEEP, ((isarmed == true) ? (byte)1 : (byte)0), 0, 0, new byte[0], out response, 0);

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

                int retb = WriteRead(VND_CMD_SET_DATETIME, buffer, out response, 0);

                if (retb == 0) r = true;
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
                byte[] main_cfg = new byte[Nanotag.CONFIG_CHUNK_SIZE];
                byte[] sch_cfg = new byte[Nanotag.CONFIG_CHUNK_SIZE];
                byte[] dev_cfg = new byte[Nanotag.CONFIG_CHUNK_SIZE];

                Array.Copy(cfg, 0, main_cfg, 0, Nanotag.CONFIG_CHUNK_SIZE);
                Array.Copy(cfg, Nanotag.CONFIG_CHUNK_SIZE, dev_cfg, 0, Nanotag.CONFIG_CHUNK_SIZE);
                Array.Copy(cfg, Nanotag.CONFIG_CHUNK_SIZE*2, sch_cfg, 0, Nanotag.CONFIG_CHUNK_SIZE);

                int retb = WriteRead(VND_CMD_SET_CFGCHUNK_GEN, main_cfg, out response, 0);
                Debug.WriteLine("Sent General config result: " + retb.ToString());
                if (retb == 0)
                {
                    await Task.Delay(200);
                    retb = WriteRead(VND_CMD_SET_CFGCHUNK_DEV, dev_cfg, out response, 0);
                    Debug.WriteLine("Sent Device config result: " + retb.ToString());

                    if (retb == 0)
                    {
                        await Task.Delay(200);
                        retb = WriteRead(VND_CMD_SET_CFGCHUNK_SCH, sch_cfg, out response, 0);
                        Debug.WriteLine("Sent Schedule config result: " + retb.ToString());
                        r = true;
                    }
                }
            }

            return await Task.FromResult(r);
        }


        public async Task<bool> DownloadPages()
        {
            bool r = false;

            if(this.USBDevice != null && this._usbEndpointReader != null && this._usbEndpointWriter != null)
            {
                var response = new byte[0];

                int retb = WriteRead(VND_CMD_GET_DATANPAGES, new byte[0], out response, 8);

                if (retb == 0)
                {
                    UInt32 first_page = (UInt32)(((UInt32)response[0]) + ((UInt32)response[1] << 8) + ((UInt32)response[2] << 16) + ((UInt32)response[3] << 24));
                    UInt32 last_page = (UInt32)(((UInt32)response[4]) + ((UInt32)response[5] << 8) + ((UInt32)response[6] << 16) + ((UInt32)response[7] << 24));

                    Debug.WriteLine("Got disk data usage info: First Page = " + first_page.ToString() + ", Last Page = " + last_page.ToString());

                    for (UInt32 i = first_page; i < last_page; i++)
                    {
                        //Console.WriteLine(("\r" + i.ToString()).PadLeft(Console.WindowWidth - Console.CursorLeft - 1));
                        byte[] addr = new byte[4];
                        addr[0] = (byte)(i & 0xFF);
                        addr[1] = (byte)(i >> 8);
                        addr[2] = (byte)(i >> 16);
                        addr[3] = (byte)(i >> 24);
                        var dpageresponse = new byte[0];
                        Debug.Write("Trying to download page = " + i.ToString());
                        int getpageresult = WriteRead(VND_CMD_GET_DATACHUNK, addr, out dpageresponse, (128 + 4096));

                        if (getpageresult == 0)
                        {
                            Debug.WriteLine(" - OK");
                            Task.Factory.StartNew( () => { ProcessOnePage(dpageresponse); });
                        }
                        else if(getpageresult == -200)
                        {
                            Debug.WriteLine(" - BAD Block");
                            i += 63;    // the 64th will be inside for loop
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("Failed getting data partition info");
                }
            }


            return await Task.FromResult(r);
        }

/*
        typedef union
        {
   struct __date_time
        {
            unsigned second :6;
            unsigned minute :6;
            unsigned hour :5;
            unsigned day :5;
            unsigned month :4;
            unsigned year :6;
        }
        date_time;
   uint32_t packedTime;
    }
    vesper_datetime_t;
*/




        private MemoryStream memoryStreamPage;
        private readonly int bytes_in_flash_page = (4096 + 128);
        private readonly UInt32 preamnle_ok = 0x54445341;
        private readonly int reference_year = 2020;
        private readonly byte NAND_FS_SNAP_PAGE_TYPE = 0x21;

        private bool ProcessOnePage(byte [] data)
        {
            if (data.Length > 1)
            {
                memoryStreamPage.Write(data, 0, data.Length);

                if (memoryStreamPage.Length >= bytes_in_flash_page)
                {
                    Debug.WriteLine("parsing page with length of " + memoryStreamPage.Length.ToString());

                    byte[] bytes;
                    bytes = memoryStreamPage.GetBuffer();

                    int ij = 4096;

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

                    ij += 2;

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


                    if (f_preamble == preamnle_ok &&
                        page_type == NAND_FS_SNAP_PAGE_TYPE &&
                        snapType == 'G' &&
                        snapIndex < snapPagesInSnap)
                    {
                        //string outfolder = this.textOutputFolder.Text;
                        string outfolder = Directory.GetCurrentDirectory();
                        if (outfolder.EndsWith("\\") == false)
                            outfolder += "\\";

                        string filename;//= String.Format("{0}\\{1}.bin",
                                        //                    new object[] {
                                        //                    outfolder, "G"+snapID.ToString("D6")});

                        DateTime dt = Nanotag.FromTimestamp(snapTimestamp, snapSubsecond);

                        filename = String.Format("{0}{1}{2,4:D4}_{3,2:D2}_{4,2:D2}_{5,2:D2}_{6,2:D2}_{7,2:D2}_GC0.dat",
                            new object[] {
                                    outfolder, "snap.", dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second});

                        Debug.WriteLine("Get good page index=" + snapIndex.ToString() + " out of " + snapPagesInSnap.ToString() + " to be saved into " + filename);

                        using (System.IO.FileStream file = new FileStream(filename, FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite))
                        {
                            if (file.Length < (snapPagesInSnap * 4096))
                                file.SetLength(snapPagesInSnap * 4096);

                            file.Seek(snapIndex * 4096, SeekOrigin.Begin);
                            file.Write(bytes, 0, 4096);
                            file.Flush();
                            file.Close();
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Page is bad");
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
            if (Object.ReferenceEquals(this, ld))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (this.GetType() != ld.GetType())
            {
                return false;
            }

            // Return true if the fields match.
            // Note that the base class is not invoked because it is
            // System.Object, which defines Equals as reference equality.
            return ((this._serialnumber == ld._serialnumber) && (this.USBDevice?.VendorId == ld.USBDevice?.VendorId) && (this.USBDevice?.ProductId == ld.USBDevice?.ProductId));
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
