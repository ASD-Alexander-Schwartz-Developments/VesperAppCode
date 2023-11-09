using Avalonia.Controls.Templates;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Models
{
    public enum BinHeaderType { BIN_TYPE_HEADER = 0, BIN_TYPE_FOOTER = 1, BIN_TYPE_SYNC = 2, BIN_TYPE_INVALID = 0xFF }


    public class BinHeader
    {
        public readonly static UInt32 PreambleHeader = 0xA55AA55A;
        public readonly static UInt32 PreambleFooter = 0xA55A5AA5;
        public readonly static UInt32 PreambleSync = 0xABCDEFEF;
        public readonly static int BIN_HEADER_LENGTH = 16;

        private UInt32 preamble;
        private DateTime datetime;
        private byte[] original_bytes;
        private int start_position;
        private char first_char;

        public BinHeader(byte[] data, int start_position, char first_char)
        {
            original_bytes = new byte[0];
            if (data != null && data.Length >= BIN_HEADER_LENGTH)
            {
                int i = start_position;

                UInt32 pmbl = ((UInt32)data[i++]) + ((UInt32)data[i++] << 8) + ((UInt32)data[i++] << 16) + ((UInt32)data[i++] << 24);

                if (pmbl == PreambleFooter || pmbl == PreambleHeader || pmbl == PreambleSync)
                {
                    original_bytes = new byte[BIN_HEADER_LENGTH];

                    Array.Copy(data, original_bytes, BIN_HEADER_LENGTH);

                    this.preamble = pmbl;

                    datetime = BinHeader.ExtractDateTime(data, i);
                }
            }

            this.start_position = start_position;

            if (this.preamble == PreambleHeader) this.start_position += BIN_HEADER_LENGTH;

            this.first_char = first_char;
        }



        public char FirstLetter => first_char;
        public int StartPosition => start_position;

        public static byte BCD2BIN(byte bcdNumber)
        {
            byte digit1 = (byte)(bcdNumber >> 4);
            byte digit2 = (byte)(bcdNumber & 0x0f);

            return (byte)(digit1 * 10 + digit2);
        }


        public static DateTime ExtractDateTime(byte[] buffer, int offset)
        {
            int i = offset;
            DateTime dateTime= DateTime.MinValue;

            if(i+12 < buffer.Length)
            {
                int hh = BCD2BIN(buffer[i++]);
                int mm = BCD2BIN(buffer[i++]);
                int ss = BCD2BIN(buffer[i++]);
                i++;
                i++;
                int m = BCD2BIN(buffer[i++]);
                int d = BCD2BIN(buffer[i++]);
                int y = BCD2BIN(buffer[i++]);
                y += 2000;
                UInt16 subsecond_frac = (UInt16)(((UInt16)buffer[i++]) + ((UInt16)buffer[i++] << 8));
                UInt16 subsecond = (UInt16)(((UInt16)buffer[i++]) + ((UInt16)buffer[i++] << 8));

                double milisecs = 1000.0 * ((double)((double)subsecond_frac - (double)subsecond) / (double)((double)subsecond_frac + 1.0));

                if(milisecs < 0)
                {
                    if (ss > 0) ss--;
                    else ss = 59;

                    milisecs *= -1;
                    //milisecs = 1000 + milisecs;
                }

                dateTime = new DateTime(y, m, d, hh, mm, ss, (int)Math.Round(milisecs));
            }

            return dateTime;
        }


        public byte[] ToBytes()
        {
            return this.original_bytes;
        }


        public override string ToString()
        {
            string wn = String.Format("{0}{1,2:D2}_{2,2:D2}_{3,2:D2}_{4,2:D2}_{5,2:D2}_{6,2:D2}_{7,3:D3}",
                                    FirstLetter, 
                                    HeaderTimestamp.Year, 
                                    HeaderTimestamp.Month, 
                                    HeaderTimestamp.Day, 
                                    HeaderTimestamp.Hour, 
                                    HeaderTimestamp.Minute, 
                                    HeaderTimestamp.Second,
                                    HeaderTimestamp.Millisecond);

            return wn;
        }


        public bool IsHeaderValid => (preamble == PreambleFooter || preamble == PreambleHeader || preamble == PreambleSync);
        public bool IsHeaderFooter => (preamble == PreambleFooter);
        public bool IsHeaderHeader => (preamble == PreambleHeader);
        public bool IsHeaderSync => (preamble == PreambleSync);

        public DateTime HeaderTimestamp => this.datetime;

        public BinHeaderType HeaderType
        {
            get
            {
                if(IsHeaderFooter)
                {
                    return BinHeaderType.BIN_TYPE_FOOTER;
                } 
                else if(IsHeaderHeader)
                {
                    return BinHeaderType.BIN_TYPE_HEADER;
                }
                else if(IsHeaderSync)
                {
                    return BinHeaderType.BIN_TYPE_SYNC;
                }
                else
                {
                    return BinHeaderType.BIN_TYPE_INVALID;
                }
            }
        }
    }



    public class BinTimestamp
    {
        public BinHeader? StartHeader { get; set; }
        public BinHeader? EndHeader { get; set;}

        public List<BinHeader> SyncTimestamps;

        public string OriginalFileName { get; set; }

        public BinTimestamp(string originalFileName)
        {
            this.StartHeader = null;
            this.EndHeader = null;
            OriginalFileName = originalFileName;
            SyncTimestamps = new List<BinHeader>();
        }


        public void InsertSyncPoint(BinHeader hdr)
        {
            this.SyncTimestamps.Add(hdr);
        }
    }
}
