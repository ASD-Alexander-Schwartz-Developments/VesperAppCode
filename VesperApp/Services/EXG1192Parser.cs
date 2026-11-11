using System;
using System.Collections.Generic;
using System.IO;

namespace VesperApp.Services
{
    /// <summary>
    /// EEG2 / EMG2 / ECG2 boards (TI ADS1192, 2 channels, 16-bit) behind a fixed pre-amplifier.
    /// <para>Record (VesperU5 ads1192.c, 1.22): 9 bytes per sample. The 6-byte ADS1192 frame
    /// (1100 + LOFF_STAT[4:1] | LOFF_STAT[0], GPIO[2:1], 0... | CH1 16-bit MSB first | CH2 16-bit), then 3 filler bytes
    /// (the driver reads the ADS1292 sample size; they repeat the frame's first 3 bytes).</para>
    /// <para>µV at the electrodes = code x 2,420,000 / (32,768 x PGA x pre-amp gain): VREF 2.42 V (CONFIG2 VREF_4V = 0),
    /// PGA from the header's gain code, pre-amp gain from the sensor name (<see cref="ExgGain.Ads1192PreampGain"/>).</para>
    /// </summary>
    public class EXG1192Parser : IDisposable
    {
        public const int RecordSize = 9;
        private const byte Header = 0xC0;

        private readonly string Filename;
        private List<string>? csv_lines;

        /// <summary>Decoded rows (header row excluded).</summary>
        public int Rows => csv_lines == null ? 0 : csv_lines.Count - 1;

        /// <param name="gain">ADS1192 PGA code (<see cref="ExgGain"/>, header Config0 bits 3..5); above 7 = x1.</param>
        /// <param name="preampGain">pre-amp gain of the board (2001 / 999 / 201), 1 when the variant is unknown.</param>
        public EXG1192Parser(string filename, byte[] data, DateTime dtStart, uint gain, double preampGain, double us_sample)
            : this(filename, data, dtStart, gain, preampGain, preampGain, us_sample) { }

        /// <param name="preamp1">pre-amp gain in channel 1's path (x1 when its mux is an internal input such as the test
        /// signal: <see cref="ExgGain.Ads1192ChannelPreampGain"/>).</param>
        /// <param name="preamp2">the same for channel 2.</param>
        public EXG1192Parser(string filename, byte[] data, DateTime dtStart, uint gain, double preamp1, double preamp2, double us_sample)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("Data Cannot be empty");

            Filename = filename;
            csv_lines = new List<string> { EXG1292RecordLine.HeaderText() };

            double pga = gain <= 0x07 ? VesperApp.Models.EXGGainOptions.ToMultiplier((byte)gain) : 1.0;
            double total1 = pga * (preamp1 > 0 ? preamp1 : 1.0);
            double total2 = pga * (preamp2 > 0 ? preamp2 : 1.0);
            long n = 0;
            int index = 0;

            while (index + 6 <= data.Length)
            {
                if ((data[index] & 0xF0) != Header || (data[index + 1] & 0x1F) != 0 || !FillerMatches(data, index))
                {
                    index++;                                    // resync on the next frame start
                    continue;
                }

                byte s0 = data[index], s1 = data[index + 1];
                var line = new EXG1292RecordLine
                {
                    Header = Header,
                    Timestamp = dtStart.AddTicks((long)Math.Round(++n * us_sample * 10.0)),   // from the count: no rounding build-up
                    LOFFRLD = (s0 & 0x08) != 0,
                    LOFF2N = (s0 & 0x04) != 0,
                    LOFF2P = (s0 & 0x02) != 0,
                    LOFF1N = (s0 & 0x01) != 0,
                    LOFF1P = (s1 & 0x80) != 0,
                    CH1 = ExgGain.MicroVolts16((short)((data[index + 2] << 8) | data[index + 3]), 2420000.0, total1),
                    CH2 = ExgGain.MicroVolts16((short)((data[index + 4] << 8) | data[index + 5]), 2420000.0, total2),
                };
                csv_lines.Add(line.ToString());
                index += RecordSize;
            }
        }

        /// <summary>
        /// The 3 filler bytes repeat the frame's first 3, so a filler looks like a frame start (0xC0, status). A record
        /// is taken only where its bytes 6..8 equal bytes 0..2: from a filler start they are CH1 / CH2 bytes of the
        /// next frame and do not match (bench T03, 2026-11-11: 8,883 of 15,152 rows were decoded 6 bytes late). The
        /// last record of a file may be cut: header check only.
        /// </summary>
        private static bool FillerMatches(byte[] d, int i) =>
            i + RecordSize > d.Length || (d[i + 6] == d[i] && d[i + 7] == d[i + 1] && d[i + 8] == d[i + 2]);

        public void Dispose()
        {
            csv_lines?.Clear();
            csv_lines = null;
        }

        public void WriteFile()
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
