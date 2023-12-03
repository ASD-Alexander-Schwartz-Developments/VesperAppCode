using System;
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

    }
}
