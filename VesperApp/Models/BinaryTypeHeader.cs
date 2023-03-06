using AvaloniaEdit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Models
{
    public class BinaryTypeHeader
    {
        private const int MAX_DEVICENAME_LEN = 16;
        public const int FILE_HEADER_LENGTH = 128;
        public const UInt32 FILE_HEADER_PREAMBLE = 0xDEAFDAC0;

        private UInt32 preamble;
        private UInt32 uid;
        private string name;
        UInt16 fwid;
        UInt16 hwid;
        UInt32 sampling_rate;
        UInt32 window_rate;
        UInt32 window_len;
        UInt32 bitmask;
        UInt32 config_data0;
        UInt32 config_data1;
        UInt32 config_data2;
        UInt32 config_data3;
        UInt32 reserved;

        byte[] original_bytes;

        public BinaryTypeHeader() 
        { 
            this.preamble = 0;
            this.uid = 0;
            name = "UNKNOWN";
            fwid = 0;
            hwid = 0;
            sampling_rate = 0;
            window_rate = 0;
            bitmask = 0;
            config_data0 = 0;
            config_data1 = 0;
            config_data2 = 0;
            config_data3 = 0;
            reserved = 0;
            original_bytes = new byte[0];
        }
        public BinaryTypeHeader(byte[] data)
        {
            this.preamble = 0;
            this.uid = 0;
            name = "UNKNOWN";
            fwid = 0;
            hwid = 0;
            sampling_rate = 0;
            window_rate = 0;
            bitmask = 0;
            config_data0 = 0;
            config_data1 = 0;
            config_data2 = 0;
            config_data3 = 0;
            reserved = 0;
            original_bytes= new byte[0];
            if (data != null && data.Length >= FILE_HEADER_LENGTH)
            {
                int i = 0;

                UInt32 pmbl = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24);

                if(pmbl == FILE_HEADER_PREAMBLE)
                {
                    original_bytes = new byte[FILE_HEADER_LENGTH];

                    Array.Copy(data, original_bytes, FILE_HEADER_LENGTH);

                    this.preamble = pmbl;

                    this.uid = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24);
                    this.name = Encoding.ASCII.GetString(data, i, MAX_DEVICENAME_LEN);
                    i += MAX_DEVICENAME_LEN;

                    this.fwid = (UInt16)((UInt16)((UInt16)data[i++]) + ((UInt16)data[i++] << 8));
                    this.hwid = (UInt16)((UInt16)((UInt16)data[i++]) + ((UInt16)data[i++] << 8));
                    this.sampling_rate = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24);
                    this.window_len = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24);
                    this.window_rate = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24); 
                    this.bitmask = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24);
                    this.config_data0 = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24);
                    this.config_data1 = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24);
                    this.config_data2 = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24);
                    this.config_data3 = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24);
                }
            }
        }

        public byte[] ToBytes()
        {
            return this.original_bytes;
        }


        public string DeviceDriverName => this.name;
        public UInt32 UId => uid;
        public UInt32 Configuration0 => this.config_data0;
        public UInt32 Configuration1 => this.config_data1;
        public UInt32 Configuration2 => this.config_data2;
        public UInt32 Configuration3 => this.config_data3;


    }
}
