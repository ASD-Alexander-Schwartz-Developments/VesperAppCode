using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace VesperApp.Services
{
    /// <summary>
    /// ProxTit encounter records (<c>prox_log_entry_t</c>, 16 B, little endian), as VesperU5 writes them to X.BIN
    /// (split into PRX/*.XBN) and the Nanotag to PRX pages (0x24); asd-interfaces interfaces/nanotag-addon-pages.md:
    ///   u32 timestampStart (Unix seconds on the tag clock, set by SET_TIME from the logger's local RTC)
    ///   u32 seconds_duration, u8[6] peer BLE address, i8 averaged RSSI (dBm), i8 TX power (whole dBm).
    /// Output: &lt;file&gt;.csv.
    /// </summary>
    public sealed class ProxEncounterParser : IDisposable
    {
        public const int RecordSize = 16;

        private readonly string _filename;
        private List<string>? _lines;

        public int Records { get; private set; }
        public int Skipped { get; private set; }

        public ProxEncounterParser(string filename, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _filename = filename;
            string sep = Utils.GetSeparator();
            _lines = new List<string>
            {
                $"Start{sep}End{sep}Duration [s]{sep}Peer address{sep}RSSI [dBm]{sep}TX power [dBm]"
            };

            for (int i = 0; i + RecordSize <= data.Length; i += RecordSize)
            {
                if (IsBlank(data, i)) { Skipped++; continue; }      // erased / padding
                _lines.Add(Line(data, i, sep));
                Records++;
            }
        }

        private static bool IsBlank(byte[] d, int at)
        {
            bool ff = true, zero = true;
            for (int k = 0; k < RecordSize; k++)
            {
                if (d[at + k] != 0xFF) ff = false;
                if (d[at + k] != 0x00) zero = false;
            }
            return ff || zero;
        }

        /// <summary>BLE address as usually written: most significant byte first (the record holds it LSB first).</summary>
        public static string Address(byte[] d, int at) =>
            string.Join(":", Enumerable.Range(0, 6).Select(k => d[at + 5 - k].ToString("X2", CultureInfo.InvariantCulture)));

        public static string Line(byte[] d, int at, string sep)
        {
            uint start = BitConverter.ToUInt32(d, at);
            uint duration = BitConverter.ToUInt32(d, at + 4);
            DateTime t0 = DateTime.UnixEpoch.AddSeconds(start);
            DateTime t1 = t0.AddSeconds(duration);
            sbyte rssi = unchecked((sbyte)d[at + 14]);
            sbyte tx = unchecked((sbyte)d[at + 15]);
            return string.Join(sep,
                t0.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                t1.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                duration.ToString(CultureInfo.InvariantCulture),
                Address(d, at + 8),
                rssi.ToString(CultureInfo.InvariantCulture),
                tx.ToString(CultureInfo.InvariantCulture));
        }

        public void WriteFile()
        {
            if (_lines == null || _lines.Count < 2) return;
            try { File.WriteAllLines(_filename + ".csv", _lines); }
            catch (IOException) { }
        }

        public void Dispose() { _lines = null; }
    }
}
