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

            while(index < data.Length)
            {
                if (data[index] == Header && index + 6 < data.Length)
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
                    line.Timestamp = ts;
                    index += 2;

                    if (((line.Flags & FlagsGyro) == FlagsGyro) && (index < data.Length - bytes_in_sensor))
                    {
                        line.GY_X = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.GY_Y = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.GY_Z = (double)Utils.FFromBytes(data, index);
                        index += 4;
                    }
                    if ((line.Flags & FlagsAcc) == FlagsAcc && (index < data.Length - bytes_in_sensor))
                    {
                        line.XL_X = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.XL_Y = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.XL_Z = (double)Utils.FFromBytes(data, index);
                        index += 4;
                    }
                    if (((line.Flags & FlagsMag) == FlagsMag) && (index < data.Length - bytes_in_sensor))
                    {
                        line.Mag_X = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.Mag_Y = (double)Utils.FFromBytes(data, index);
                        index += 4;
                        line.Mag_Z = (double)Utils.FFromBytes(data, index);
                        index += 4;
                    }
                    if (((line.Flags & FlagsBar) == FlagsBar) && (index < data.Length-4))
                    {
                        line.Temperature = (double)((UInt16)((UInt16)data[index] + (UInt16)((UInt16)data[index + 1] << 8)) / 100.0);
                        index += 2;
                        line.Pressure = (double)((UInt16)((UInt16)data[index] + (UInt16)((UInt16)data[index + 1] << 8)));
                        index += 2;
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

            return $"TimeMinute{mysep}Second{mysep}Milisecond{mysep}Acc X [mg]{mysep}Acc Y [mg]{mysep}Acc Z [mg]{mysep}Gyro X [dps]{mysep}Gyro Y [dps]" +
                $"{mysep}Gyro Z [dps]{mysep}Mag X [mGauss]{mysep}Mag Y [mGauss]{mysep}Mag Z [mGauss]{mysep}" +
                $"{mysep}Temperature [C]{mysep}Bar Pressure [hPa]";
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
