using Avalonia.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    public class IMU10Parser
    {
        const byte Header = 0x55;
        const byte FlagsAcc = 0x01;
        const byte FlagsGyro = 0x02;
        const byte FlagsMag = 0x04;
        const byte FlagsBar = 0x08;

        public IMU10Parser(byte[] data, DateTime dtStart) 
        { 
            if(data == null) throw new ArgumentNullException("data");

            if (data.Length == 0) throw new ArgumentException("Data Cannot be empty");

            int index = 0;

            while(index < data.Length)
            {
                if (data[index] == Header)
                {
                    IMU10RecordLine line = new IMU10RecordLine();

                    line.Header = Header;
                    index++;
                    line.Flags = data[index];
                    index++;
                    byte min = (byte)(data[index]);
                    index++;
                    byte sec = (byte)(data[index]);
                    index++;
                    UInt16 subsec = (UInt16)((UInt16)data[index] + (UInt16)((UInt16)data[index+1] << 8));
                    index += 2;

                    if((line.Flags & FlagsAcc) == FlagsAcc && (index < data.Length - 6))
                    {
                        
                    }
                    if (((line.Flags & FlagsGyro) == FlagsGyro) && (index < data.Length - 6))
                    {

                    }
                    if (((line.Flags & FlagsMag) == FlagsMag) && (index < data.Length - 6))
                    {

                    }
                    if (((line.Flags & FlagsBar) == FlagsBar) && (index < data.Length-4))
                    {
                        line.Temperature = (double)((UInt16)((UInt16)data[index] + (UInt16)((UInt16)data[index + 1] << 8)) / 100.0);
                        index += 2;
                        line.Pressure = (double)((UInt16)((UInt16)data[index] + (UInt16)((UInt16)data[index + 1] << 8)));
                        index += 2;
                    }
                }
            }
        }

    }



    internal class IMU10RecordLine
    {
        public byte Header { get; set; }
        public byte Flags { get; set; }
        public DateTime Timestamp { get; set; }

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

        public string HeaderText()
        {
            return "Time,Acc X [mg],Acc Y [mg],Acc Z [mg],Gyro X [dps],Gyro Y [dps],Gyro Z [dps],Mag X [mGauss],Mag Y [mGauss],Mag Z [mGauss],Temperature [C],Bar Pressure [hPa]";
        }

        public override string ToString()
        {
            if (Header != 0x55) return "Bad Row";

            string dt = Timestamp.ToShortDateString() + Timestamp.ToString("HH:mm:ss.FFF");

            return dt + "," +
                XL_X.ToString("F4") + "," +
                XL_Y.ToString("F4") + "," +
                XL_Z.ToString("F4") + "," +
                GY_X.ToString("F4") + "," +
                GY_Y.ToString("F4") + "," +
                GY_Z.ToString("F4") + "," +
                Mag_X.ToString("F4") + "," +
                Mag_Y.ToString("F4") + "," +
                Mag_Z.ToString("F4") + "," +
                Temperature.ToString("F2") +
                Pressure.ToString("F1");
        }
    }
}
