using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    public class NanoAccParser : IDisposable
    {
        public const byte Header = 0xAC;
        const int bytes_in_sensor = 9;          // 

        private string Filename;
        private bool IsOpened;

        private List<string>? csv_lines;

        public NanoAccParser(string filename, byte[] data, DateTime dtStart, UInt16 subsec_frac, UInt32 ms_sample) 
        { 
            if(data == null) throw new ArgumentNullException("data");

            if (data.Length == 0) throw new ArgumentException("Data Cannot be empty");

            Filename = filename;

            csv_lines = new List<string>();
            csv_lines.Add(NanoAccParserRecordLine.HeaderText());

            int index = 0;
            DateTime ts = dtStart;

            while(index < data.Length)
            {
                if (index + bytes_in_sensor < data.Length)
                {
                    if (data[index++] == Header)
                    {
                        NanoAccParserRecordLine line = new NanoAccParserRecordLine();
                        ts = ts.Add(TimeSpan.FromMilliseconds(ms_sample));
                        line.Timestamp = ts;

                        short lx = Utils.SFromBytes(data, index);
                        index += 2;
                        short ly = Utils.SFromBytes(data, index);
                        index += 2;
                        short lz = Utils.SFromBytes(data, index);
                        index += 2;

                        byte range = data[index++];
                        index += 2;

                        line.Header = Header;

                        double Slope = 4.0 / 65536.0;

                        switch(range)
                        {
                            case 0:
                                Slope = 4.0 / 65536.0;
                                break;
                            case 1:
                                Slope = 8.0 / 65536.0;
                                break;
                            case 2:
                                Slope = 16.0 / 65536.0;
                                break;
                            case 3:
                                Slope = 8.0 / 65536.0;
                                break;
                            default:    
                                break;
                        }
                        Console.WriteLine(Slope.ToString("F2"));
                        line.XL_X = (double)lx * Slope*1000;
                        line.XL_Y = (double)ly * Slope * 1000;
                        line.XL_Z = (double)lz * Slope * 1000;

                        csv_lines.Add(line.ToString());
                    }
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





    internal class NanoAccParserRecordLine
    {
        public byte Header { get; set; }
        public DateTime Timestamp { get; set; }

        public uint Minute { get; set; }
        public uint Second { get; set; }
        public uint Milisecond { get; set; }

        public double XL_X { get; set; }
        public double XL_Y { get; set; }
        public double XL_Z { get; set; }
        public static string HeaderText()
        {
            return "Time,Minute,Second,Milisecond,Acc X [mg],Acc Y [mg],Acc Z [mg]";
        }

        public override string ToString()
        {
            if (Header != 0xAC) return "Bad Row";

            string dt = Timestamp.ToShortDateString() + " " + Timestamp.ToString("HH:mm:ss.FFF");

            return dt + "," +
                Minute.ToString() + "," +
                Second.ToString() + "," +
                Milisecond.ToString() + "," +
                XL_X.ToString("F4") + "," +
                XL_Y.ToString("F4") + "," +
                XL_Z.ToString("F4");
        }
    }
}
