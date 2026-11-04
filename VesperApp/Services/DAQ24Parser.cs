using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace VesperApp.Services
{
    /// <summary>
    /// VT01-DAQ24 (ADS1115) records from a Vesper / Pipistrelle S.BIN window (.SBN), VesperU5 ads1115.c.
    /// 20-byte record: 0x5A, flags (bit per input converted), RTC date + time + subsecond as the ALS record,
    /// then AIN0..AIN3 as int16 LE codes of 125 µV (±4.096 V range). Contract: asd-interfaces interfaces/daq24.md.
    /// </summary>
    public class DAQ24Parser : IDisposable
    {
        public const byte Header = 0x5A;
        public const int RecordSize = 20;
        public const double VoltsPerCode = 125e-6;

        private readonly string _filename;
        private List<string>? _lines;

        /// <summary>Records decoded (exposed for tests).</summary>
        public List<DAQ24Record> Records { get; } = new();

        public DAQ24Parser(string filename, byte[] data, UInt16 subsecFrac)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _filename = filename;
            _lines = new List<string> { DAQ24Record.HeaderText() };

            int i = 0;
            while (i + RecordSize <= data.Length)
            {
                if (data[i] != Header) { i++; continue; }
                var r = new DAQ24Record { Flags = data[i + 1] };
                Utils.ExtractDateTimeSlowSensors(data, i + 2, subsecFrac, out DateTime dt);
                r.Timestamp = dt;
                for (int ch = 0; ch < 4; ch++)
                {
                    short code = BitConverter.ToInt16(data, i + 12 + 2 * ch);
                    r.Volts[ch] = (r.Flags & (1 << ch)) != 0 ? code * VoltsPerCode : (double?)null;
                }
                Records.Add(r);
                _lines.Add(r.ToString());
                i += RecordSize;
            }
        }

        public void Dispose() { _lines?.Clear(); _lines = null; }

        public void WriteFile()      // synchronous: Auto Decode counts the CSV as written when this returns
        {
            if (_lines == null || _lines.Count < 2) return;
            string fname = _filename + ".csv";
            if (File.Exists(fname)) File.Delete(fname);
            File.AppendAllLines(fname, _lines);
        }
    }

    public class DAQ24Record
    {
        public byte Flags { get; set; }
        public DateTime Timestamp { get; set; }
        public double?[] Volts { get; } = new double?[4];

        public static string HeaderText()
        {
            string s = Utils.GetSeparator();
            return $"Time{s}AIN0 (V){s}AIN1 (V){s}AIN2 (V){s}AIN3 (V)";
        }

        public override string ToString()
        {
            string s = Utils.GetSeparator();
            string dt = Timestamp.ToShortDateString() + " " + Timestamp.ToString("HH:mm:ss.FFF");
            string V(double? v) => v.HasValue ? v.Value.ToString("F5", CultureInfo.CurrentCulture) : "";
            return dt + s + V(Volts[0]) + s + V(Volts[1]) + s + V(Volts[2]) + s + V(Volts[3]);
        }
    }
}
