using Avalonia.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    public class IMU10Parser : IDisposable
    {
        const byte Header = 0x55;
        const byte FlagsAcc = 0x01;
        const byte FlagsGyro = 0x02;
        const byte FlagsMag = 0x04;
        const byte FlagsBar = 0x08;
        const int bytes_in_sensor = 4 * 3;          // 4bytes for float x 3 axis

        /// <summary>
        /// Barometer field size: IMU10 writes 4 bytes (int16 temperature in 0.01 degC, uint16 hPa);
        /// IMU10HT reserves 6 (the same 4 + 2 reserved bytes, by design). Told apart by the
        /// "Sensor:" line of the intermediate's .txt sidecar.
        /// </summary>
        public static int BaroBytesFor(string intermediatePath)
        {
            try
            {
                string meta = intermediatePath + ".txt";
                if (File.Exists(meta))
                    foreach (string line in File.ReadAllLines(meta))
                        if (line.StartsWith("Sensor:", StringComparison.OrdinalIgnoreCase)
                            && line.Substring(7).Trim().Equals("IMU10HT", StringComparison.OrdinalIgnoreCase))
                            return 6;
            }
            catch (IOException) { }
            return 4;
        }

        /// <summary>
        /// The time of one record from its own RTC stamp (minute, second, ms; the hour and date come from the
        /// previous record, starting with the file's start time). It used to be the start time plus n times
        /// 1000 / rate integer ms: at 26 Hz that is 38 ms instead of 38.46 ms, 1.2 % fast (bench 2026-11-10,
        /// 2.25 s ahead after 3 min), and gaps in the data were not seen. Null for an invalid stamp (the
        /// caller keeps the count-based time).
        ///
        /// Firmware up to 1.20 stamps a stale minute / second: the sampling loop read the RTC time without
        /// the date, so the seconds stayed frozen until other code read the date (VesperU5 6deabcf). The
        /// sub-second counter is always live. Customer files (fw 1.20, 60 Hz, 2026-06-04): the second was
        /// refreshed every 0.68 s, a third of the rows carried the previous second (a few the one before),
        /// about 1140 steps of 1 s back per 19.5 min file. A stale stamp is only ever behind, so a stamp up
        /// to <see cref="StaleStampSeconds"/> behind the previous record is moved forward by whole seconds
        /// to follow it; the ms are kept. A stamp further behind is in the next hour.
        /// </summary>
        internal static DateTime? StampTime(ref DateTime last, int minute, int second, int ms)
        {
            if (minute > 59 || second > 59 || second < 0 || ms < 0 || ms > 999) return null;
            var t = new DateTime(last.Year, last.Month, last.Day, last.Hour, 0, 0, last.Kind)
                .AddMinutes(minute).AddSeconds(second).AddMilliseconds(ms);
            if (t > last.AddMinutes(59)) t = t.AddHours(-1);                  // stale stamp of the hour that just ended
            else if (t < last.AddSeconds(-StaleStampSeconds)) t = t.AddHours(1);
            while (t < last.AddMilliseconds(-500)) t = t.AddSeconds(1);       // stale second(s): the counter has wrapped
            last = t;
            return t;
        }

        /// <summary>How far behind the previous record a stamp may be and still be taken as stale seconds.</summary>
        internal const int StaleStampSeconds = 60;

        private string Filename;
        private bool IsOpened;

        private List<string>? csv_lines;

        public IMU10Parser(string filename, byte[] data, DateTime dtStart, UInt16 subsec_frac, UInt32 ms_sample) 
        { 
            if(data == null) throw new ArgumentNullException("data");

            if (data.Length == 0) throw new ArgumentException("Data Cannot be empty");

            Filename = filename;

            csv_lines = new List<string>();
            csv_lines.Add(IMU10RecordLine.HeaderText());

            int index = 0;
            DateTime ts = dtStart;
            DateTime last = dtStart;
            int baro_bytes = BaroBytesFor(filename);

            while(index < data.Length)
            {
                if (data[index] == Header && index + 6 <= data.Length)
                {
                    IMU10RecordLine line = new IMU10RecordLine();
                    ts = ts.Add(TimeSpan.FromMilliseconds(ms_sample));
                    line.Header = Header;
                    index++;
                    line.Flags = data[index];
                    index++;
                    line.Minute = (data[index]);
                    index++;
                    line.Second = (data[index]);
                    index++;
                    UInt16 subsec = (UInt16)((UInt16)data[index] + (UInt16)((UInt16)data[index+1] << 8));

                    if(subsec > subsec_frac)
                    {
                        //Console.WriteLine("333");
                    }

                    int ms = (int)(1000.0 * ((double)((double)subsec_frac - (double)subsec) / (double)((double)subsec_frac + 1.0)));
                    if(ms < 0)
                    {
                        ms += 1000;
                        line.Second--;
                    }
                    DateTime? stamped = StampTime(ref last, (int)line.Minute, unchecked((int)line.Second), ms);
                    line.Timestamp = stamped ?? ts;
                    if (stamped != null)
                    {
                        // the Minute / Second columns follow the Time column (a stale second is corrected in both)
                        line.Minute = (uint)stamped.Value.Minute;
                        line.Second = (uint)stamped.Value.Second;
                    }
                    line.Milisecond = (uint)ms;
                    index += 2;

                    if (((line.Flags & FlagsGyro) == FlagsGyro) && (index + bytes_in_sensor <= data.Length))
                    {
                        line.GY_X = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.GY_Y = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.GY_Z = (double)Utils.FFromBytes(data, index);
                        index += 4;
                    }
                    if ((line.Flags & FlagsAcc) == FlagsAcc && (index + bytes_in_sensor <= data.Length))
                    {
                        line.XL_X = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.XL_Y = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.XL_Z = (double)Utils.FFromBytes(data, index);
                        index += 4;
                    }
                    if (((line.Flags & FlagsMag) == FlagsMag) && (index + bytes_in_sensor <= data.Length))
                    {
                        line.Mag_X = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.Mag_Y = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.Mag_Z = (double)Utils.FFromBytes(data, index);
                        index += 4;
                    }
                    if (((line.Flags & FlagsBar) == FlagsBar) && (index + baro_bytes <= data.Length))
                    {
                        // LPS22HB TEMP_OUT is signed (0.01 degC): below 0 degC it read as ~655 degC before.
                        line.Temperature = BitConverter.ToInt16(data, index) / 100.0;
                        line.Pressure = BitConverter.ToUInt16(data, index + 2);
                        index += baro_bytes;
                    }
                    csv_lines.Add(line.ToString());
                }
                else
                {
                    index++;
                }
            }
        }

        public void Dispose()
        {
            if (csv_lines != null)
            {
                csv_lines.Clear();
                csv_lines = null;
            }
        }

        public void WriteFile()      // synchronous: callers (Auto Decode) count the CSV as written when this returns
        {
            if(csv_lines == null) { return; }
            if(csv_lines.Count < 2) { return; }

            try
            {
                string fname = Filename + ".csv";
                if (File.Exists(fname) == true)
                {
                    File.Delete(fname);
                }

                File.AppendAllLines(fname, csv_lines);
                IsOpened = true;
            }
            catch (Exception ex)
            {
                IsOpened = false;
            }
            IsOpened = false;            
        }
    }





    internal class IMU10RecordLine
    {
        public byte Header { get; set; }
        public byte Flags { get; set; }
        public DateTime Timestamp { get; set; }

        public uint Minute { get; set; }
        public uint Second { get; set; }
        public uint Milisecond { get; set; }

        public double XL_X { get; set; }
        public double XL_Y { get; set; }
        public double XL_Z { get; set; }
        public double GY_X { get; set; }
        public double GY_Y { get; set; }
        public double GY_Z { get; set; }
        public double Mag_X { get; set; }
        public double Mag_Y { get; set; }
        public double Mag_Z { get; set; }

        public double Temperature { get; set; }
        public double Pressure { get; set; }

        public static string HeaderText()
        {
            string mysep = Utils.GetSeparator();

            return $"Time{mysep}Minute{mysep}Second{mysep}Milisecond{mysep}Acc X [mg]{mysep}Acc Y [mg]{mysep}Acc Z [mg]{mysep}Gyro X [mdps]{mysep}Gyro Y [mdps]" +
                $"{mysep}Gyro Z [mdps]{mysep}Mag X [mGauss]{mysep}Mag Y [mGauss]{mysep}Mag Z [mGauss]{mysep}" +
                $"Temperature [C]{mysep}Bar Pressure [hPa]";
        }

        public override string ToString()
        {
            if (Header != 0x55) return "Bad Row";

            string mysep = Utils.GetSeparator();

            string dt = Timestamp.ToShortDateString() + " " + Timestamp.ToString("HH:mm:ss.FFF");
            
            return dt + mysep +
                Minute.ToString() + mysep +
                Second.ToString() + mysep +
                Milisecond.ToString() + mysep +
                XL_X.ToString("F4") + mysep +
                XL_Y.ToString("F4") + mysep +
                XL_Z.ToString("F4") + mysep +
                GY_X.ToString("F4") + mysep +
                GY_Y.ToString("F4") + mysep +
                GY_Z.ToString("F4") + mysep +
                Mag_X.ToString("F4") + mysep +
                Mag_Y.ToString("F4") + mysep +
                Mag_Z.ToString("F4") + mysep +
                Temperature.ToString("F2") + mysep +
                Pressure.ToString("F1");
        }
    }
}
