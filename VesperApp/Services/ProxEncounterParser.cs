using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace VesperApp.Services
{
    /// <summary>
    /// ProxTit encounter records (<c>prox_log_entry_t</c>, 16 B, little endian), as VesperU5 writes them to X.BIN
    /// (split into PRX/*.XBN) and the Nanotag to PRX pages (0x24); asd-interfaces interfaces/nanotag-addon-pages.md:
    ///   u32 timestampStart (Unix seconds on the tag clock, set by SET_TIME from the logger's local RTC)
    ///   u32 seconds_duration, u8[6] peer BLE address, i8 averaged RSSI (dBm), i8 TX power (whole dBm).
    /// Marker records (I2C contract v2, tag firmware 02.02.00+): address {type, FF x5}, RSSI 127.
    /// Output: &lt;file&gt;.csv. The last two columns: the tag that recorded the file (its source record, when the
    /// logger wrote one) and the record kind (encounter, or the marker and its values).
    /// </summary>
    public sealed class ProxEncounterParser : IDisposable
    {
        public const int RecordSize = 16;
        public const sbyte MarkerRssi = 127;

        private readonly string _filename;
        private List<string>? _lines;

        public int Records { get; private set; }
        public int Markers { get; private set; }
        public int Skipped { get; private set; }

        /// <param name="sourceTag">The recording tag's address ("90:35:EA:AE:D2:07"), from the source record; null if unknown.</param>
        public ProxEncounterParser(string filename, byte[] data, string? sourceTag = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _filename = filename;
            string sep = Utils.GetSeparator();
            string tag = sourceTag ?? string.Empty;
            _lines = new List<string>
            {
                $"Start{sep}End{sep}Duration [s]{sep}Peer address{sep}RSSI [dBm]{sep}TX power [dBm]{sep}Tag address{sep}Record"
            };

            for (int i = 0; i + RecordSize <= data.Length; i += RecordSize)
            {
                if (IsBlank(data, i)) { Skipped++; continue; }      // erased / padding
                if (IsMarker(data, i))
                {
                    _lines.Add(MarkerLine(data, i, sep, tag));
                    Markers++;
                    continue;
                }
                _lines.Add(Line(data, i, sep) + sep + tag + sep + "encounter");
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

        /// <summary>Marker: RSSI 127 (no beacon gives it) and address bytes 1..5 = FF; byte 0 is the type.</summary>
        public static bool IsMarker(byte[] d, int at)
        {
            if (unchecked((sbyte)d[at + 14]) != MarkerRssi) return false;
            for (int k = 1; k < 6; k++)
                if (d[at + 8 + k] != 0xFF) return false;
            return true;
        }

        /// <summary>BLE address as usually written: most significant byte first (the record holds it LSB first).</summary>
        public static string Address(byte[] d, int at) =>
            string.Join(":", Enumerable.Range(0, 6).Select(k => d[at + 5 - k].ToString("X2", CultureInfo.InvariantCulture)));

        private static string Time(uint unix) =>
            DateTime.UnixEpoch.AddSeconds(unix).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

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

        /// <summary>
        /// A marker as a row: Start = its time; the peer, RSSI and TX columns are empty; Record names it
        /// (Host/proxtool/docs/ble-protocol.md § Markers). No commas inside the text (any separator is safe).
        /// </summary>
        public static string MarkerLine(byte[] d, int at, string sep, string tag)
        {
            uint t = BitConverter.ToUInt32(d, at);
            uint v = BitConverter.ToUInt32(d, at + 4);
            byte type = d[at + 8];
            sbyte extra = unchecked((sbyte)d[at + 15]);
            string what = type switch
            {
                0x01 => $"BOOT reset cause 0x{v:X} boot {extra}",
                0x02 => $"TIMESET clock was {Time(v)} (step {(long)t - v:+#;-#;0} s)",
                0x03 => "POWEROFF " + (v switch { 1 => "mode off", 2 => "low battery", 3 => "host sleep", _ => $"reason {v}" }),
                0x04 => $"START previous mode {v}" + (extra != 0 ? " clock set" : " clock not set"),
                0x05 => $"SETTINGS {v} changed at start" + (extra == 1 ? " (update from 01.x)" : ""),
                0xFE => "UNREADABLE record (tag flash)",
                _ => $"MARKER 0x{type:X2} value {v}",
            };
            return string.Join(sep, type == 0xFE ? "" : Time(t), "", "", "", "", "", tag, what);
        }

        public void WriteFile()
        {
            if (_lines == null || _lines.Count < 2) return;
            try { File.WriteAllLines(_filename + ".csv", _lines); }
            catch (IOException) { }
        }

        public void Dispose() { _lines = null; }
    }

    /// <summary>
    /// Which tag recorded a ProxTit file: the 20-byte source record (contract v2, ProxTit
    /// Proxtit/docs/proxtit_i2c_contract.md § 9) at offset 64 of a VesperU5 /prox file header and of a Nanotag
    /// PRX page's spare: "PXT1", u8[6] tag address LSB first, u8[3] firmware, u8 variant, u16 contract, u32 boot.
    /// </summary>
    public sealed record ProxSource(string Address, string Firmware, byte Variant, ushort Contract, uint BootCount)
    {
        public const int Offset = 64;
        public const int Length = 20;
        public const uint Magic = 0x31545850;           // "PXT1"

        /// <summary>The record at <paramref name="at"/>, or null when absent (0xFF fill, older loggers).</summary>
        public static ProxSource? Parse(byte[]? buf, int at)
        {
            if (buf == null || at < 0 || at + Length > buf.Length) return null;
            if (BitConverter.ToUInt32(buf, at) != Magic) return null;
            string addr = ProxEncounterParser.Address(buf, at + 4);
            string fw = string.Format(CultureInfo.InvariantCulture, "{0:00}.{1:00}.{2:00}", buf[at + 10], buf[at + 11], buf[at + 12]);
            return new ProxSource(addr, fw, buf[at + 13], BitConverter.ToUInt16(buf, at + 14), BitConverter.ToUInt32(buf, at + 16));
        }

        public string VariantName => Variant switch { 0 => "BLE", 1 => "ProxLink", _ => Variant.ToString(CultureInfo.InvariantCulture) };

        /// <summary>Sidecar (.txt) lines; <see cref="ProxEncounterParser"/> reads SourceTag back.</summary>
        public string MetaLines()
        {
            var sb = new StringBuilder();
            sb.Append("SourceTag:").Append(Address).Append(Environment.NewLine);
            sb.Append("SourceTagFW:").Append(Firmware).Append(Environment.NewLine);
            sb.Append("SourceTagVariant:").Append(VariantName).Append(Environment.NewLine);
            sb.Append("SourceTagBoot:").Append(BootCount.ToString(CultureInfo.InvariantCulture)).Append(Environment.NewLine);
            return sb.ToString();
        }
    }
}
