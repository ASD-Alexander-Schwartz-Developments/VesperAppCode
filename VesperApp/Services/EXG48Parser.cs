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
    public class EXG48Parser : IDisposable
    {
        const byte Header = 0xC0;

        private string Filename;
        private bool IsOpened;

        private List<string>? csv_lines;

        public EXG48Parser(string filename, byte[] data, DateTime dtStart, uint gain, uint us_sample) 
        { 
            if(data == null) throw new ArgumentNullException("data");

            if (data.Length == 0) throw new ArgumentException("Data Cannot be empty");

            Filename = filename;

            csv_lines = new List<string>();
            csv_lines.Add(EXG48RecordLine.HeaderText());

            int index = 0;
            DateTime ts = dtStart;
            double __gain = 1.0;

            if(gain <= 0x07)
            {
                __gain = EXGGainOptions.ToMultiplier((byte)gain);
            }

            while(index < data.Length)
            {
                if (data[index] == Header && index + 27 < data.Length)
                {
                    EXG48RecordLine line = new EXG48RecordLine();
                    ts = ts.Add(TimeSpan.FromMicroseconds(us_sample));
                    line.Header = Header;
                    index++;
                    line.Timestamp = ts;
                    byte loffp = (byte)data[index++];
                    byte loffn = (byte)data[index++];

                    line.LOFF1P = ((loffp & (byte)(1 << 0)) != 0);
                    line.LOFF2P = ((loffp & (byte)(1 << 1)) != 0);
                    line.LOFF3P = ((loffp & (byte)(1 << 2)) != 0);
                    line.LOFF4P = ((loffp & (byte)(1 << 3)) != 0);
                    line.LOFF5P = ((loffp & (byte)(1 << 4)) != 0);
                    line.LOFF6P = ((loffp & (byte)(1 << 5)) != 0);
                    line.LOFF7P = ((loffp & (byte)(1 << 6)) != 0);
                    line.LOFF8P = ((loffp & (byte)(1 << 7)) != 0);

                    line.LOFF1N = ((loffn & (byte)(1 << 0)) != 0);
                    line.LOFF2N = ((loffn & (byte)(1 << 1)) != 0);
                    line.LOFF3N = ((loffn & (byte)(1 << 2)) != 0);
                    line.LOFF4N = ((loffn & (byte)(1 << 3)) != 0);
                    line.LOFF5N = ((loffn & (byte)(1 << 4)) != 0);
                    line.LOFF6N = ((loffn & (byte)(1 << 5)) != 0);
                    line.LOFF7N = ((loffn & (byte)(1 << 6)) != 0);
                    line.LOFF8N = ((loffn & (byte)(1 << 7)) != 0);

                    Int32 cha1_raw = Utils.FromBytesS24bitMSB(data, index);
                    index += 3;
                    Int32 cha2_raw = Utils.FromBytesS24bitMSB(data, index);
                    index += 3;
                    Int32 cha3_raw = Utils.FromBytesS24bitMSB(data, index);
                    index += 3;
                    Int32 cha4_raw = Utils.FromBytesS24bitMSB(data, index);
                    index += 3;
                    Int32 cha5_raw = Utils.FromBytesS24bitMSB(data, index);
                    index += 3;
                    Int32 cha6_raw = Utils.FromBytesS24bitMSB(data, index);
                    index += 3;
                    Int32 cha7_raw = Utils.FromBytesS24bitMSB(data, index);
                    index += 3;
                    Int32 cha8_raw = Utils.FromBytesS24bitMSB(data, index);
                    index += 3;

                    line.CH1 = (((cha1_raw / ((1 << 24) - 1)) / 256.0) * 2400000) / __gain;
                    line.CH2 = (((cha2_raw / ((1 << 24) - 1)) / 256.0) * 2400000) / __gain;
                    line.CH3 = (((cha3_raw / ((1 << 24) - 1)) / 256.0) * 2400000) / __gain;
                    line.CH4 = (((cha4_raw / ((1 << 24) - 1)) / 256.0) * 2400000) / __gain;
                    line.CH5 = (((cha5_raw / ((1 << 24) - 1)) / 256.0) * 2400000) / __gain;
                    line.CH6 = (((cha6_raw / ((1 << 24) - 1)) / 256.0) * 2400000) / __gain;
                    line.CH7 = (((cha7_raw / ((1 << 24) - 1)) / 256.0) * 2400000) / __gain;
                    line.CH8 = (((cha8_raw / ((1 << 24) - 1)) / 256.0) * 2400000) / __gain;

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





    internal class EXG48RecordLine
    {
        public byte Header { get; set; }
        public DateTime Timestamp { get; set; }
        public bool LOFF1P {  get; set; }
        public bool LOFF1N { get; set; }
        public bool LOFF2P { get; set; }
        public bool LOFF2N { get; set; }
        public bool LOFF3P { get; set; }
        public bool LOFF3N { get; set; }
        public bool LOFF4P { get; set; }
        public bool LOFF4N { get; set; }
        public bool LOFF5P { get; set; }
        public bool LOFF5N { get; set; }
        public bool LOFF6P { get; set; }
        public bool LOFF6N { get; set; }
        public bool LOFF7P { get; set; }
        public bool LOFF7N { get; set; }
        public bool LOFF8P { get; set; }
        public bool LOFF8N { get; set; }

        public double CH1 { get; set; }
        public double CH2 { get; set; }
        public double CH3 { get; set; }
        public double CH4 { get; set; }
        public double CH5 { get; set; }
        public double CH6 { get; set; }
        public double CH7 { get; set; }
        public double CH8 { get; set; }

        public static string HeaderText()
        {
            return "Date,Time,CH1[uV],CH1-,CH1+,CH2[uV],CH2-,CH2+,CH3[uV],CH3-,CH3+," +
                "CH4[uV],CH4-,CH4+,CH5[uV],CH5-,CH5+,CH6[uV],CH6-,CH6+,CH7[uV],CH7-,CH7+,CH8[uV],CH8-,CH8+";
        }

        public override string ToString()
        {
            if (Header != 0xC0) return "Bad Row";

            return Timestamp.ToShortDateString() + "," +
                Timestamp.ToString("HH:mm:ss.FFF") + "," +
                CH1.ToString("F4") + "," +
                LOFF1P.ToString() + "," +
                LOFF1N.ToString() + "," +
                CH2.ToString("F4") + "," +
                LOFF2P.ToString() + "," +
                LOFF2N.ToString() + "," +
                CH3.ToString("F4") + "," +
                LOFF3P.ToString() + "," +
                LOFF3N.ToString() + "," +
                CH4.ToString("F4") + "," +
                LOFF4P.ToString() + "," +
                LOFF4N.ToString() + "," +
                CH5.ToString("F4") + "," +
                LOFF5P.ToString() + "," +
                LOFF5N.ToString() + "," +
                CH6.ToString("F4") + "," +
                LOFF6P.ToString() + "," +
                LOFF6N.ToString() + "," +
                CH7.ToString("F4") + "," +
                LOFF7P.ToString() + "," +
                LOFF7N.ToString() + "," +
                CH8.ToString("F4") + "," +
                LOFF8P.ToString() + "," +
                LOFF8N.ToString() + ",";
        }
    }
}
