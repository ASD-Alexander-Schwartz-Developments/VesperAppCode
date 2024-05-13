using Avalonia.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    public class TPHParser : IDisposable
    {
        const byte Header = 0x59;
        const int bytes_in_sensor = 4 * 3;          // 4bytes for float x 3 axis

        private string Filename;
        private bool IsOpened;

        private List<string>? csv_lines;

        public TPHParser(string filename, byte[] data, DateTime dtStart, UInt16 subsec_frac, UInt32 ms_sample) 
        { 
            if(data == null) throw new ArgumentNullException("data");

            if (data.Length == 0) throw new ArgumentException("Data Cannot be empty");

            Filename = filename;

            csv_lines = new List<string>();
            csv_lines.Add(TPHRecordLine.HeaderText());

            int index = 0;
            DateTime ts = dtStart;

            while(index < data.Length)
            {
                if (data[index] == Header && index + 18 < data.Length)
                {
                    TPHRecordLine line = new TPHRecordLine();
                    ts = ts.Add(TimeSpan.FromMilliseconds(ms_sample));
                    line.Header = Header;
                    index++;
                    line.Flags = data[index];
                    index++;
                    DateTime dt;
                    index += Utils.ExtractDateTimeSlowSensors(data, index, subsec_frac, out dt);
                    line.Timestamp = dt; 

                    line.Temperature = (double)((UInt16)((UInt16)data[index + 1] + (UInt16)((UInt16)data[index] << 8)));
                    index += 3;
                    line.RelativeHumidity = (double)((UInt16)((UInt16)data[index + 1] + (UInt16)((UInt16)data[index] << 8)));
                    index += 3;

                    line.RelativeHumidity = 100.0 * (line.RelativeHumidity / 65535.0);
                    line.Temperature = -45 + 175 * (line.Temperature / 65535.0);

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

        public async void WriteFile()
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

                await File.AppendAllLinesAsync(fname, csv_lines.ToArray());
                IsOpened = true;
            }
            catch (Exception ex)
            {
                IsOpened = false;
            }
            IsOpened = false;            
        }
    }





    internal class TPHRecordLine
    {
        public byte Header { get; set; }
        public byte Flags { get; set; }
        public DateTime Timestamp { get; set; }
        public double RelativeHumidity { get; set; }
        public double Temperature { get; set; }

        public static string HeaderText()
        {
            return "Time,Temperature [C],Relative Humidity [%]";
        }

        public override string ToString()
        {
            if (Header != 0x59) return "Bad Row";

            string dt = Timestamp.ToShortDateString() + " " + Timestamp.ToString("HH:mm:ss.FFF");

            return dt + "," +
                Temperature.ToString("F2") + "," +
                RelativeHumidity.ToString("F2");
        }
    }
}
