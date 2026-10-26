using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using VesperApp.Models;

namespace VesperApp.Services
{
    /// <summary>
    /// One Nanotag NAND page as read over the vendor interface (GET_DATACHUNK / GET_LOGCHUNK): 4096 data bytes
    /// followed by the 128-byte spare, which starts with <c>FSGenericMetaData_t</c> (firmware CgFSInterface.h).
    /// Contracts: asd-interfaces interfaces/logger-file.md (log pages 0x2F) and nanotag-addon-pages.md (0x23 / 0x24).
    /// </summary>
    public sealed class NanotagPage
    {
        public const int DataSize = 4096;
        public const int SpareSize = 128;
        public const int FullSize = DataSize + SpareSize;
        public const int PagesPerBlock = 64;
        public const uint Preamble = 0x54445341;            // "ASDT"

        public const byte TypeSnap = 0x21;                  // GPS / ACC snapshots
        public const byte TypeSns = 0x23;                   // TPRH31, ALS3001D add-on records
        public const byte TypePrx = 0x24;                   // ProxTit encounter records
        public const byte TypeLog = 0x2F;                   // LOGGER log area

        public const byte SubTypeTprh31 = 9;                // SENSOR_Typedef: SNS_TPRH
        public const byte SubTypeAls3001d = 7;              // SNS_ALS
        public const byte SubTypeProxtit = 2;               // SNS_BLE

        private readonly byte[] _raw;

        private NanotagPage(byte[] raw) { _raw = raw; }

        public byte PageType => _raw[DataSize + 6];
        public byte PageSubType => _raw[DataSize + 7];
        public ushort BattLevel => U16(10);
        public uint DataLength => U32(12);
        public uint FirmwareVersion => U32(24);

        // metadata union at spare offset 28
        /// <summary>Log page: +1 per page, 1 after a format.</summary>
        public uint LogSeq => U32(28);
        public uint LogFirstTime => U32(32);
        public uint LogLastTime => U32(36);
        public ushort LogMask => U16(40);
        public byte LogFormat => _raw[DataSize + 42];
        public byte LogDeviceType => _raw[DataSize + 43];

        /// <summary>Add-on page: RTC clock word when the page was written (packed, as GPS / ACC pages).</summary>
        public uint SnsTimestamp => U32(28);
        public ushort SnsSubsecond => U16(32);
        public ushort SnsSampleRate => U16(34);
        /// <summary>Page number within the sensor's session, from 0.</summary>
        public uint SnsSeqId => U32(36);
        public uint SnsPeriodMs => U32(40);
        public uint SnsRawData0 => U32(44);

        /// <summary>The page's records: data[0 .. dataLength).</summary>
        public ReadOnlySpan<byte> Records => new ReadOnlySpan<byte>(_raw, 0, (int)Math.Min(DataLength, (uint)DataSize));

        private ushort U16(int at) => BitConverter.ToUInt16(_raw, DataSize + at);
        private uint U32(int at) => BitConverter.ToUInt32(_raw, DataSize + at);

        /// <summary>A written page with the ASD preamble; null for anything else (erased, short, foreign).</summary>
        public static NanotagPage? TryParse(byte[]? page)
        {
            if (page == null || page.Length < FullSize) return null;
            if (BitConverter.ToUInt32(page, DataSize) != Preamble) return null;
            return new NanotagPage(page);
        }

        /// <summary>
        /// Release version of the firmware that wrote the page, from FW_VER (MAJOR.MID.MINOR.BLD; release = MINOR.BLD).
        /// Pages from 1.37 and older carry MINOR twice (0x00010125 = "0.1.1.37"): MID is ignored.
        /// </summary>
        public static Version ReleaseOf(uint fwVer) => new Version((int)((fwVer >> 8) & 0xFF), (int)(fwVer & 0xFF));
    }

    /// <summary>
    /// The LOGGER log area of a Nanotag (logger-file.md, Nanotag section): a ring of pages in the NAND, read with
    /// GET_LOGNPAGES (area start, end exclusive, oldest, newest) and GET_LOGCHUNK.
    /// </summary>
    public static class NanotagLog
    {
        public const uint Empty = 0xFFFFFFFF;

        /// <summary>GET_LOGNPAGES reply: four u32 LE.</summary>
        public static (uint Start, uint End, uint Oldest, uint Newest)? ParseRange(byte[]? reply)
        {
            if (reply == null || reply.Length < 16) return null;
            return (BitConverter.ToUInt32(reply, 0), BitConverter.ToUInt32(reply, 4),
                    BitConverter.ToUInt32(reply, 8), BitConverter.ToUInt32(reply, 12));
        }

        /// <summary>
        /// Pages to read, oldest to newest inclusive, wrapping from end − 1 to start. Empty when the log is empty
        /// (oldest = newest = 0xFFFFFFFF) or the range is not consistent.
        /// </summary>
        public static List<uint> ReadOrder(uint start, uint end, uint oldest, uint newest)
        {
            var pages = new List<uint>();
            if (oldest == Empty || newest == Empty || end <= start) return pages;
            if (oldest < start || oldest >= end || newest < start || newest >= end) return pages;
            uint p = oldest;
            for (uint n = 0; n < end - start; n++)
            {
                pages.Add(p);
                if (p == newest) break;
                p = (p + 1 >= end) ? start : p + 1;
            }
            return pages;
        }

        /// <summary>First page of the next block (bad block: GET_LOGCHUNK status 0xBD).</summary>
        public static uint NextBlock(uint page) => (page / NanotagPage.PagesPerBlock + 1) * NanotagPage.PagesPerBlock;

        /// <summary>Log pages (preamble + type 0x2F) in seq order, records concatenated.</summary>
        public static byte[] Assemble(IEnumerable<NanotagPage> pages, out ushort logmask, out uint firstTime, out uint lastTime)
        {
            var log = pages.Where(p => p.PageType == NanotagPage.TypeLog).OrderBy(p => p.LogSeq).ToList();
            logmask = log.Count > 0 ? log[^1].LogMask : (ushort)0;
            firstTime = log.Count > 0 ? log[0].LogFirstTime : 0;
            lastTime = log.Count > 0 ? log[^1].LogLastTime : 0;
            var ms = new MemoryStream();
            foreach (NanotagPage p in log) ms.Write(p.Records);
            return ms.ToArray();
        }

        private static string Stamp(uint secsSince2000) =>
            DeviceEpoch.Epoch2000.AddSeconds(secsSince2000).ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture) + "_000";

        /// <summary>
        /// Write the log as Vesper's LOGGER output: HEALTH/H&lt;first&gt;-H&lt;last&gt;.HBN (the records) with its .txt
        /// sidecar (Config1 = format 1 | device type 0 &lt;&lt; 8) and the decoded .csv / .log.csv. Returns the .HBN path,
        /// or null when there are no records.
        /// </summary>
        public static string? Write(string folder, byte[] records, ushort logmask, uint firstTime, uint lastTime, string? serial = null)
        {
            if (records.Length == 0) return null;
            string health = Path.Combine(folder, "HEALTH");
            Directory.CreateDirectory(health);
            string hbn = Path.Combine(health, $"H{Stamp(firstTime)}-H{Stamp(lastTime)}.HBN");
            File.WriteAllBytes(hbn, records);
            var meta = new StringBuilder();
            meta.Append("Sensor:LOGGER").Append(Environment.NewLine);
            meta.Append("Device:Nanotag").Append(Environment.NewLine);
            if (!string.IsNullOrEmpty(serial)) meta.Append("DeviceID:").Append(serial).Append(Environment.NewLine);
            meta.Append("Config0:").Append(logmask.ToString("X")).Append(Environment.NewLine);
            meta.Append("Config1:1").Append(Environment.NewLine);
            File.WriteAllText(hbn + ".txt", meta.ToString());
            using (var lp = new LoggerParser(hbn, records, 0x0001))        // format 1, device type 0 (Nanotag)
                lp.WriteFile();
            return hbn;
        }
    }

    /// <summary>
    /// Collects Nanotag add-on pages from a data download (0x23 SNS: TPRH31 / ALS3001D, 0x24 PRX: ProxTit) and
    /// writes them as Vesper's add-on outputs: TPRH/R….RBN, ALS/L….LBN, PRX/X….XBN (the records, concatenated in
    /// page order, one file per sensor session) + .txt sidecar + decoded .csv (nanotag-addon-pages.md).
    /// </summary>
    public sealed class NanotagAddonCollector
    {
        private sealed class Segment
        {
            public byte Type;
            public byte SubType;
            public uint LastSeq;
            public uint FirstStamp;
            public ushort FirstSubsecond;
            public uint LastStamp;
            public uint PeriodMs;
            public uint FirmwareVersion;
            public readonly MemoryStream Data = new();
        }

        private readonly List<Segment> _segments = new();
        private readonly Dictionary<(byte, byte), Segment> _open = new();

        public int Pages { get; private set; }

        /// <summary>Take the page if it is an add-on page; false otherwise.</summary>
        public bool Add(NanotagPage p)
        {
            if (p.PageType != NanotagPage.TypeSns && p.PageType != NanotagPage.TypePrx) return false;
            var key = (p.PageType, p.PageSubType);
            if (!_open.TryGetValue(key, out Segment? s) || p.SnsSeqId == 0 || p.SnsSeqId <= s.LastSeq)
            {
                s = new Segment
                {
                    Type = p.PageType, SubType = p.PageSubType,
                    FirstStamp = p.SnsTimestamp, FirstSubsecond = p.SnsSubsecond,
                    PeriodMs = p.SnsPeriodMs != 0 ? p.SnsPeriodMs : p.SnsSampleRate,
                    FirmwareVersion = p.FirmwareVersion,
                };
                _open[key] = s;
                _segments.Add(s);
            }
            s.LastSeq = p.SnsSeqId;
            s.LastStamp = p.SnsTimestamp;
            s.Data.Write(p.Records);
            Pages++;
            return true;
        }

        private static (string Folder, string Letter, string Ext, string Sensor)? Kind(byte type, byte sub) => type switch
        {
            NanotagPage.TypePrx => ("PRX", "X", ".XBN", "PROXTIT"),
            NanotagPage.TypeSns when sub == NanotagPage.SubTypeTprh31 => ("TPRH", "R", ".RBN", "TPRH31"),
            NanotagPage.TypeSns when sub == NanotagPage.SubTypeAls3001d => ("ALS", "L", ".LBN", "ALS3001D"),
            _ => null,
        };

        private static string Stamp(DateTime t) =>
            t.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture) + "_" + t.Millisecond.ToString("000", CultureInfo.InvariantCulture);

        /// <summary>Write every collected session; returns the intermediate files written.</summary>
        public List<string> WriteAll(string folder, string? serial = null)
        {
            var written = new List<string>();
            foreach (Segment s in _segments)
            {
                var kind = Kind(s.Type, s.SubType);
                if (kind == null || s.Data.Length == 0) continue;
                var (sub, letter, ext, sensor) = kind.Value;

                DateTime start = Nanotag.FromTimestamp(s.FirstStamp, s.FirstSubsecond);
                DateTime end = Nanotag.FromTimestamp(s.LastStamp, 0);
                string dir = Path.Combine(folder, sub);
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"{letter}{Stamp(start)}-{letter}{Stamp(end)}{ext}");
                byte[] data = s.Data.ToArray();
                File.WriteAllBytes(path, data);

                var meta = new StringBuilder();
                meta.Append("Sensor:").Append(sensor).Append(Environment.NewLine);
                meta.Append("Device:Nanotag").Append(Environment.NewLine);
                if (!string.IsNullOrEmpty(serial)) meta.Append("DeviceID:").Append(serial).Append(Environment.NewLine);
                meta.Append("FWID:").Append(NanotagPage.ReleaseOf(s.FirmwareVersion)).Append(Environment.NewLine);
                meta.Append("SampleRate:").Append(s.PeriodMs).Append(Environment.NewLine);
                File.WriteAllText(path + ".txt", meta.ToString());

                Decode(path, ext, data, start, s.PeriodMs);
                written.Add(path);
            }
            return written;
        }

        private static void Decode(string path, string ext, byte[] data, DateTime start, uint periodMs)
        {
            try
            {
                switch (ext)
                {
                    case ".RBN":
                        using (var ip = new TPHParser(path, data, start, 1023, periodMs)) ip.WriteFile();
                        break;
                    case ".LBN":
                        using (var ip = new ALSParser(path, data, start, 1023, periodMs)) ip.WriteFile();
                        break;
                    case ".XBN":
                        using (var ip = new ProxEncounterParser(path, data)) ip.WriteFile();
                        break;
                }
            }
            catch (Exception)
            {
                // the intermediate stays; Auto Decode can retry it
            }
        }
    }
}
