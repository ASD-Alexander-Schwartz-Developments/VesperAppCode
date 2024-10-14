using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    public class Utils
    {
        public static uint FromBytes(byte[] bytes, int startindex)
        {
            uint result = 0;

            result |= (uint)bytes[startindex + 0];
            result |= (uint)bytes[startindex + 1] << 8;
            result |= (uint)bytes[startindex + 2] << 16;
            result |= (uint)bytes[startindex + 3] << 24;

            return result;
        }
        public static Int32 FromBytesS24bitMSB(byte[] bytes, int startindex)
        {
            uint result = 0;

            result |= (uint)bytes[startindex + 2] << 8;
            result |= (uint)bytes[startindex + 1] << 16;
            result |= (uint)bytes[startindex + 0] << 24;

            return (Int32)result;
        }

        public static float FFromBytes(byte[] bytes, int startindex)
        {
            byte[] rev = new byte[4];

            if (BitConverter.IsLittleEndian == false)
            {
                rev[0] = (byte)bytes[startindex + 3];
                rev[1] = (byte)bytes[startindex + 2];
                rev[2] = (byte)bytes[startindex + 1];
                rev[3] = (byte)bytes[startindex + 0];
            }
            else
            {
                rev[0] = (byte)bytes[startindex + 0];
                rev[1] = (byte)bytes[startindex + 1];
                rev[2] = (byte)bytes[startindex + 2];
                rev[3] = (byte)bytes[startindex + 3];
            }

            float r = BitConverter.ToSingle(rev, 0);

            return r;
        }

        public static short SFromBytes(byte[] bytes, int startindex)
        {
            byte[] rev = new byte[2];

            if (BitConverter.IsLittleEndian == false)
            {
                rev[0] = (byte)bytes[startindex + 1];
                rev[1] = (byte)bytes[startindex + 0];
            }
            else
            {
                rev[0] = (byte)bytes[startindex + 0];
                rev[1] = (byte)bytes[startindex + 1];
            }

            short r = (short)((ushort)(rev[1] << 8 | rev[0]));

            return r;
        }



        public static byte BCD2BIN(byte bcdNumber)
        {
            byte digit1 = (byte)(bcdNumber >> 4);
            byte digit2 = (byte)(bcdNumber & 0x0f);

            return (byte)(digit1 * 10 + digit2);
        }

        public static int ExtractDateTimeSlowSensors(byte[] buffer, int offset, UInt16 subsecond_frac, out DateTime dt)
        {
            int i = offset;
            DateTime dateTime = DateTime.MinValue;
            dt = dateTime;

            if (i + 10 < buffer.Length)
            {
                i++;
                int m = BCD2BIN(buffer[i++]);
                int d = BCD2BIN(buffer[i++]);
                int y = BCD2BIN(buffer[i++]);
                int hh = BCD2BIN(buffer[i++]);
                int mm = BCD2BIN(buffer[i++]);
                int ss = BCD2BIN(buffer[i++]);
                i++;
                y += 2000;
                UInt16 subsecond = (UInt16)(((UInt16)buffer[i++]) + ((UInt16)buffer[i++] << 8));

                double milisecs = 1000.0 * ((double)((double)subsecond_frac - (double)subsecond) / (double)((double)subsecond_frac + 1.0));

                if (milisecs < 0)
                {
                    if (ss > 0) ss--;
                    else ss = 59;

                    milisecs *= -1;
                    //milisecs = 1000 + milisecs;
                }

                if(milisecs > 999 || milisecs < 0)
                {
                    Console.WriteLine("dd");
                }

                dateTime = new DateTime(y, m, d, hh, mm, ss, (int)Math.Round(milisecs));
                dt = dateTime;
            }

            return 10;
        }


        public static ArrayList scan(string s, string fmt)
        {
            ArrayList result = new ArrayList();

            int ind = 0; // s upto ind has been consumed

            for (int i = 0; i < fmt.Length; i++)
            {
                char c = fmt[i];
                if (c == '%' && i < fmt.Length - 1)
                {
                    char d = fmt[i + 1];
                    if (d == 's')
                    {
                        string schars = "";
                        for (int j = ind; j < s.Length; j++)
                        {
                            if (Char.IsWhiteSpace(s[j]))
                            { break; }
                            else
                            { schars = schars + s[j]; }
                        }
                        result.Add(schars);
                        ind = ind + schars.Length;
                        i++;
                    }
                    else if (d == 'f')
                    {
                        String fchars = "";
                        for (int j = ind; j < s.Length; j++)
                        {
                            Char x = s[j];
                            if (x == '.' || Char.IsDigit(x))
                            { fchars = fchars + x; }
                            else
                            { break; }
                        }

                        try
                        {
                            double v = double.Parse(fchars);
                            ind = ind + fchars.Length;
                            result.Add(v);
                        }
                        catch (Exception _ex)
                        { Console.WriteLine("!! Error in double format: " + fchars); }
                        i++;
                    }
                    else if (d == 'd')
                    {
                        String inchars = "";
                        for (int j = ind; j < s.Length; j++)
                        {
                            Char x = s[j];
                            if (Char.IsDigit(x))
                            { inchars = inchars + x; }
                            else
                            { break; }
                        }

                        try
                        {
                            int v = int.Parse(inchars);
                            ind = ind + inchars.Length;
                            result.Add(v);
                        }
                        catch (Exception _ex)
                        { Console.WriteLine("!! Error in integer format: " + inchars); }
                        i++;
                    }
                    else if (d == 'u')
                    {
                        String inchars = "";
                        for (int j = ind; j < s.Length; j++)
                        {
                            Char x = s[j];
                            if (Char.IsDigit(x))
                            { inchars = inchars + x; }
                            else
                            { break; }
                        }

                        try
                        {
                            uint v = uint.Parse(inchars);
                            ind = ind + inchars.Length;
                            result.Add(v);
                        }
                        catch (Exception _ex)
                        { Console.WriteLine("!! Error in unsigned integer format: " + inchars); }
                        i++;
                    }
                }
                else if (s[ind] == c)
                { ind++; }
                else
                { return result; }

            }
            return result;
        }


        public static string GetSeparator()
        {
            string sep = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;
            string num_decimal = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string mysep = ",";

            if (sep != num_decimal)
            {
                mysep = sep;
            }
            else
            {
                if (num_decimal == ".")
                {
                    mysep = ",";
                }
                else
                {
                    mysep = ";";
                }
            }

            return mysep;
        }

    }
}
