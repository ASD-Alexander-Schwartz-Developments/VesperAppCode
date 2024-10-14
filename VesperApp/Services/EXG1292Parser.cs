using Avalonia.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VesperApp.Models;

namespace VesperApp.Services
{
    public class EXG1292Parser : IDisposable
    {
        const byte Header = 0xC0;

        private string Filename;
        private bool IsOpened;

        private List<string>? csv_lines;

        public EXG1292Parser(string filename, byte[] data, DateTime dtStart, uint gain, uint us_sample) 
        { 
            if(data == null) throw new ArgumentNullException("data");

            if (data.Length == 0) throw new ArgumentException("Data Cannot be empty");

            Filename = filename;

            csv_lines = new List<string>();
            csv_lines.Add(EXG1292RecordLine.HeaderText());

            int index = 0;
            DateTime ts = dtStart;
            double __gain = 1.0;

            if(gain <= 0x07)
            {
                __gain = EXGGainOptions.ToMultiplier((byte)gain);
            }
            /// Data from SPI looks like this: 
            /// 1100 RNPN PII0 0000 0000 0000
            while(index < data.Length)
            {
                byte b = (byte)(data[index] & 0xF0);
                if (b == Header && index + 9 < data.Length)
                {
                    index++;
                    EXG1292RecordLine line = new EXG1292RecordLine();
                    ts = ts.Add(TimeSpan.FromMicroseconds(us_sample));
                    line.Header = Header;
                    line.Timestamp = ts;
                    byte loff1 = (byte)(b & 0x0F);
                    byte loff2 = (byte)data[index++];
                    index++;

                    line.LOFFRLD = ((loff1 & (byte)(1 << 3)) != 0);
                    line.LOFF2N = ((loff1 & (byte)(1 << 2)) != 0);
                    line.LOFF2P = ((loff1 & (byte)(1 << 1)) != 0);
                    line.LOFF1N = ((loff1 & (byte)(1 << 0)) != 0);
                    line.LOFF1P = ((loff2 & (byte)(1 << 7)) != 0);

                    Int32 cha1_raw = Utils.FromBytesS24bitMSB(data, index);
                    index += 3;
                    Int32 cha2_raw = Utils.FromBytesS24bitMSB(data, index);
                    index += 3;

                    line.CH1 = (((cha1_raw / (double)((1 << 24) - 1)) / 256.0) * 2420000.0) / (double)__gain;
                    line.CH2 = (((cha2_raw / (double)((1 << 24) - 1)) / 256.0) * 2420000.0) / (double)__gain;

                    csv_lines.Add(line.ToString());
                }
                else
                {
                    index += 9;
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





    internal class EXG1292RecordLine
    {
        public byte Header { get; set; }
        public DateTime Timestamp { get; set; }
        public bool LOFF1P {  get; set; }
        public bool LOFF1N { get; set; }
        public bool LOFF2P { get; set; }
        public bool LOFF2N { get; set; }
        public bool LOFFRLD { get; set; }

        public double CH1 { get; set; }
        public double CH2 { get; set; }

        public static string HeaderText()
        {
            string mysep = Utils.GetSeparator();
            return $"Date{mysep}Time{mysep}CH1[uV]{mysep}CH1-{mysep}CH1+{mysep}CH2[uV]{mysep}CH2-{mysep}CH2+{mysep}RLD OFF";
        }

        public override string ToString()
        {
            if (Header != 0xC0) return "Bad Row";
            string mysep = Utils.GetSeparator() ;

            return Timestamp.ToShortDateString() + mysep +
                Timestamp.ToString("HH:mm:ss.FFF") + mysep +
                CH1.ToString("F4") + mysep +
                LOFF1P.ToString() + mysep +
                LOFF1N.ToString() + mysep +
                CH2.ToString("F4") + mysep +
                LOFF2P.ToString() + mysep +
                LOFF2N.ToString() + mysep + 
                LOFFRLD.ToString();
        }
    }
}
