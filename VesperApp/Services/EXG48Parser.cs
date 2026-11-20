﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VesperApp.Models;

namespace VesperApp.Services
{
    /// <summary>
    /// EXG48 (ADS1294 / ADS1296 / ADS1298) .EBN to CSV. VesperU5 records 27 bytes per sample on every board: the
    /// 24-bit status word, then CH1..CH8 as 24-bit two's complement, MSB first (ads1298.c EXG1298_SAMPLE_SIZE).
    /// <list type="bullet">
    /// <item>Status word (ADS129x datasheet SBAS459K, 9.4.1.3.3): 1100, LOFF_STATP[7:0], LOFF_STATN[7:0], GPIO[7:4];
    /// bit 7 = channel 8. Before 2026-11-20 the decoder read byte 1 as LOFF_STATP and byte 2 as LOFF_STATN: the flags
    /// were 4 bits off (the CH1 column showed channel 5's negative input), and a record with an electrode off on
    /// channels 5..8 (first byte not exactly 0xC0) was dropped.</item>
    /// <item>Channels: the ADS1294 has 4 and the ADS1296 6; the absent ones (and powered-down ones) read 0 in every
    /// sample. The CSV holds 4, 6 or 8 channels: the trailing pairs that read 0 in the whole file are left out
    /// (backlog V2). The file does not name the part.</item>
    /// <item>Lead-off flags: "True" = electrode off. Blank where the recording's configuration did not enable lead-off
    /// sensing on that input (the datasheet: ignore the status bit then). The enables are in the header's Config1
    /// (channels 1..4) and Config2 (5..8), one byte per channel: bit 3 positive, bit 4 negative (vesper.h
    /// eeg_params).</item>
    /// </list>
    /// At 32 kSPS (high resolution, code 0) the ADS129x outputs 16 bits per channel; this decoder reads 24 (not
    /// handled; asd-interfaces backlog).
    /// </summary>
    public class EXG48Parser : IDisposable
    {
        public const int RecordSize = 27;
        private const byte Header = 0xC0;

        private readonly string Filename;
        private List<string>? csv_lines;

        /// <summary>Channels in the CSV (4, 6 or 8).</summary>
        public int Channels { get; }

        /// <summary>Records decoded (CSV rows).</summary>
        public int Rows => csv_lines == null ? 0 : csv_lines.Count - 1;

        /// <param name="gain">ADS1298 gain code the recording was made with (<see cref="ExgGain"/>, from the header's
        /// Config0); above 7 (<see cref="ExgGain.Unknown"/>) = x1.</param>
        /// <param name="us_sample">Sample period in µs (<see cref="ExgRate.Exg48UsPerSample"/>; fractional at 16 / 32 kSPS).</param>
        /// <param name="channelConfig">The header's Config1 | Config2 &lt;&lt; 32 (per-channel bytes, lead-off enables);
        /// null = unknown: every lead-off flag is written.</param>
        public EXG48Parser(string filename, byte[] data, DateTime dtStart, uint gain, double us_sample, ulong? channelConfig = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("Data Cannot be empty");

            Filename = filename;
            double g = gain <= 0x07 ? EXGGainOptions.ToMultiplier((byte)gain) : 1.0;

            List<int> starts = FrameStarts(data);
            Channels = ChannelCount(data, starts);

            csv_lines = new List<string> { HeaderText(Channels) };
            string sep = Utils.GetSeparator();
            var sb = new StringBuilder();
            long n = 0;                                 // samples so far: times from the start, no rounding build-up
            foreach (int i in starts)
            {
                n++;
                DateTime ts = dtStart.AddTicks((long)Math.Round(n * us_sample * TimeSpan.TicksPerMicrosecond));
                int status = (data[i] << 16) | (data[i + 1] << 8) | data[i + 2];
                int loffP = (status >> 12) & 0xFF, loffN = (status >> 4) & 0xFF;

                sb.Clear();
                sb.Append(ts.ToShortDateString()).Append(sep).Append(ts.ToString("HH:mm:ss.FFF"));
                for (int ch = 0; ch < Channels; ch++)
                {
                    int raw = Utils.FromBytesS24bitMSB(data, i + 3 + 3 * ch);
                    // FromBytesS24bitMSB returns the code << 8; full scale is 2.4 V / gain at 2^23 (item 25)
                    sb.Append(sep).Append(ExgGain.MicroVolts24(raw >> 8, 2400000.0, g).ToString("F4"));
                    sb.Append(sep).Append(Flag(loffP, ch, channelConfig, 3));
                    sb.Append(sep).Append(Flag(loffN, ch, channelConfig, 4));
                }
                csv_lines.Add(sb.ToString());
            }
        }

        private static string Flag(int bits, int ch, ulong? cfg, int enableBit)
        {
            if (cfg.HasValue && ((cfg.Value >> (8 * ch + enableBit)) & 1) == 0) return "";
            return ((bits >> ch) & 1) != 0 ? "True" : "False";
        }

        private static bool IsFrameStart(byte[] d, int i) => i + RecordSize <= d.Length && (d[i] & 0xF0) == Header;

        /// <summary>Record starts, 27 bytes apart. A record counts where it starts with 1100 and so does the next one (or
        /// the data ends): a 0xC_ byte inside channel data is common, so one alone does not set the step. Where that
        /// fails, the next such pair is searched byte by byte.</summary>
        public static List<int> FrameStarts(byte[] d)
        {
            var list = new List<int>();
            bool Ok(int i) => IsFrameStart(d, i) && (i + 2 * RecordSize > d.Length || IsFrameStart(d, i + RecordSize));
            int i = 0;
            while (i + RecordSize <= d.Length)
            {
                if (Ok(i)) { list.Add(i); i += RecordSize; }
                else i++;
            }
            return list;
        }

        /// <summary>8 when channel 7 or 8 reads non-zero in any record, else 6 when channel 5 or 6 does, else 4.</summary>
        public static int ChannelCount(byte[] d, IReadOnlyList<int> starts)
        {
            bool any56 = false;
            foreach (int i in starts)
            {
                for (int k = i + 3 + 3 * 6; k < i + RecordSize; k++) if (d[k] != 0) return 8;
                if (!any56) for (int k = i + 3 + 3 * 4; k < i + 3 + 3 * 6; k++) if (d[k] != 0) { any56 = true; break; }
            }
            return any56 ? 6 : 4;
        }

        public static string HeaderText(int channels)
        {
            string sep = Utils.GetSeparator();
            var sb = new StringBuilder("Date" + sep + "Time");
            for (int ch = 1; ch <= channels; ch++)
                sb.Append(sep).Append($"CH{ch}[uV]").Append(sep).Append($"CH{ch}+ off").Append(sep).Append($"CH{ch}- off");
            return sb.ToString();
        }

        /// <summary>Config1 | Config2 &lt;&lt; 32 from an intermediate's .txt sidecar (hex), or null when either is missing.</summary>
        public static ulong? ChannelConfigFromSidecar(string sidecarPath)
        {
            try
            {
                if (!File.Exists(sidecarPath)) return null;
                uint? c1 = null, c2 = null;
                foreach (string raw in File.ReadAllLines(sidecarPath))
                {
                    int colon = raw.IndexOf(':');
                    if (colon < 0) continue;
                    string key = raw.Substring(0, colon).Trim();
                    if (!uint.TryParse(raw.Substring(colon + 1).Trim(), System.Globalization.NumberStyles.HexNumber,
                                       System.Globalization.CultureInfo.InvariantCulture, out uint v)) continue;
                    if (key.Equals("Config1", StringComparison.OrdinalIgnoreCase)) c1 ??= v;
                    else if (key.Equals("Config2", StringComparison.OrdinalIgnoreCase)) c2 ??= v;
                }
                return c1.HasValue && c2.HasValue ? c1.Value | ((ulong)c2.Value << 32) : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        public void Dispose()
        {
            csv_lines?.Clear();
            csv_lines = null;
        }

        public void WriteFile()      // synchronous: callers (Auto Decode) count the CSV as written when this returns
        {
            if (csv_lines == null || csv_lines.Count < 2) return;
            try
            {
                string fname = Filename + ".csv";
                if (File.Exists(fname)) File.Delete(fname);
                File.AppendAllLines(fname, csv_lines);
            }
            catch (Exception) { }
        }
    }
}
