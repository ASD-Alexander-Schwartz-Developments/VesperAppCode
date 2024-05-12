using Avalonia.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    public class ALSParser : IDisposable
    {
        const byte Header = 0x5A;
        const int bytes_in_sensor = 4 * 3;          // 4bytes for float x 3 axis

        private string Filename;
        private bool IsOpened;

        private List<string>? csv_lines;

        public ALSParser(string filename, byte[] data, DateTime dtStart, UInt16 subsec_frac, UInt32 ms_sample) 
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
                    ALSRecordLine line = new ALSRecordLine();
                    ts = ts.Add(TimeSpan.FromMilliseconds(ms_sample));
                    line.Header = Header;
                    index++;
                    line.Flags = data[index];
                    index++;
                    DateTime dt;
                    index += Utils.ExtractDateTimeSlowSensors(data, index, subsec_frac, out dt);
                    line.Timestamp = dt; 

                    UInt16 R = ((UInt16)((UInt16)data[index + 1] + (UInt16)((UInt16)data[index] << 8)));
                    UInt16 E = (UInt16)(R >> 12);
                    R &= (1 << 12) - 1;
                    line.Lux = (double)(0.01 * Math.Pow(2.0, E)) * R;
                    index += 2;

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





    internal class ALSRecordLine
    {
        public byte Header { get; set; }
        public byte Flags { get; set; }
        public DateTime Timestamp { get; set; }
        public double Lux { get; set; }

        public static string HeaderText()
        {
            return "Time,Lux";
        }

        public override string ToString()
        {
            if (Header != 0x5A) return "Bad Row";

            string dt = Timestamp.ToShortDateString() + " " + Timestamp.ToString("HH:mm:ss.FFF");

            return dt + "," +
                Lux.ToString("F2");
        }
    }
}
