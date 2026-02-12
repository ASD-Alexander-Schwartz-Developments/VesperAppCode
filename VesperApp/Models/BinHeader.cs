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
    /// <summary>
    /// Types of BIN header blocks recognized by the parser.
    /// </summary>
    public enum BinHeaderType { BIN_TYPE_HEADER = 0, BIN_TYPE_FOOTER = 1, BIN_TYPE_SYNC = 2, BIN_TYPE_INVALID = 0xFF }


    /// <summary>
    /// Represents a parsed binary file header that includes a preamble and timestamp.
    /// Parses headers from raw byte buffers and exposes header metadata and timestamp.
    /// </summary>
    public class BinHeader
    {
        /// <summary>Magic value marking a header preamble.</summary>
        public readonly static UInt32 PreambleHeader = 0xA55AA55A;
        /// <summary>Magic value marking a footer preamble.</summary>
        public readonly static UInt32 PreambleFooter = 0xA55A5AA5;
        /// <summary>Magic value marking a sync preamble.</summary>
        public readonly static UInt32 PreambleSync = 0xABCDEFEF;
        /// <summary>Length of the binary header in bytes.</summary>
        public readonly static int BIN_HEADER_LENGTH = 16;

        private UInt32 preamble;
        private DateTime datetime;
        private byte[] original_bytes;
        private int start_position;
        private char first_char;

        /// <summary>Creates a BinHeader from a raw buffer at the specified start position.</summary>
        /// <param name="data">Buffer containing binary data.</param>
        /// <param name="start_position">Byte offset where header parsing should start.</param>
        /// <param name="first_char">The first character that follows the header (used for naming/identification).</param>
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

        /// <summary>Character identifying record type or first letter used for created filenames.</summary>
        public char FirstLetter => first_char;
        /// <summary>Start byte index of the next payload (after header) inside the original buffer.</summary>
        public int StartPosition => start_position;

        /// <summary>Convert a BCD encoded byte to binary (decimal) value.</summary>
        /// <param name="bcdNumber">BCD encoded byte.</param>
        /// <returns>Decoded byte value (0..99).</returns>
        public static byte BCD2BIN(byte bcdNumber)
        {
            byte digit1 = (byte)(bcdNumber >> 4);
            byte digit2 = (byte)(bcdNumber & 0x0f);

            return (byte)(digit1 * 10 + digit2);
        }


        /// <summary>Extracts a DateTime from the buffer using the BIN header timestamp format.</summary>
        /// <param name="buffer">Source buffer that contains timestamp fields.</param>
        /// <param name="offset">Offset into the buffer where timestamp begins.</param>
        /// <returns>Parsed DateTime or DateTime.MinValue if parse fails.</returns>
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
                dateTime = new DateTime(y, m, d, hh, mm, ss, (int)Math.Round((Math.Abs(milisecs))));

                if (milisecs < 0)
                {
                    dateTime -= TimeSpan.FromSeconds(1.0);
                }
            }

            return dateTime;
        }


        /// <summary>Returns the raw header bytes (or empty array if none).</summary>
        public byte[] ToBytes()
        {
            return this.original_bytes;
        }


        /// <summary>Returns a formatted representation used for filenames.</summary>
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


        /// <summary>Whether the header preamble is recognized as header/footer/sync.</summary>
        public bool IsHeaderValid => (preamble == PreambleFooter || preamble == PreambleHeader || preamble == PreambleSync);
        /// <summary>Whether this header is a footer type.</summary>
        public bool IsHeaderFooter => (preamble == PreambleFooter);
        /// <summary>Whether this header is a header type.</summary>
        public bool IsHeaderHeader => (preamble == PreambleHeader);
        /// <summary>Whether this header is a sync type.</summary>
        public bool IsHeaderSync => (preamble == PreambleSync);

        /// <summary>Timestamp parsed from the header.</summary>
        public DateTime HeaderTimestamp => this.datetime;

        /// <summary>Type of header as a <see cref="BinHeaderType"/>.</summary>
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



    /// <summary>
    /// Holds start/end headers and sync points for a parsed binary file along with the original file name.
    /// </summary>
    public class BinTimestamp
    {
        /// <summary>Start (header) information if present.</summary>
        public BinHeader? StartHeader { get; set; }
        /// <summary>End (footer) information if present.</summary>
        public BinHeader? EndHeader { get; set;}

        /// <summary>List of sync header timestamps found inside the file.</summary>
        public List<BinHeader> SyncTimestamps;

        /// <summary>Original file name from which the timestamps were extracted.</summary>
        public string OriginalFileName { get; set; }

        /// <summary>Create a timestamp holder for the specified file.</summary>
        /// <param name="originalFileName">Name of the original file.</param>
        public BinTimestamp(string originalFileName)
        {
            this.StartHeader = null;
            this.EndHeader = null;
            OriginalFileName = originalFileName;
            SyncTimestamps = new List<BinHeader>();
        }


        /// <summary>Insert a sync header into the internal sync point list.</summary>
        /// <param name="hdr">Header to insert.</param>
        public void InsertSyncPoint(BinHeader hdr)
        {
            this.SyncTimestamps.Add(hdr);
        }
    }
}
