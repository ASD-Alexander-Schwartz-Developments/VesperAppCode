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

    }
}
