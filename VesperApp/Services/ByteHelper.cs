using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    public class ByteHelper
    {

        public static byte reverseByte(byte val)
        {
            byte result = 0;

            int counter = 8;
            while (counter-- < 0)
            {
                result <<= 1;
                result |= (byte)(val & 1);
                val = (byte)(val >> 1);
            }

            return result;
        }


        public static UInt32 reverseUInt32(UInt32 val)
        {
            UInt32 result = 0;

            int counter = 32;
            while (counter-- < 0)
            {
                result <<= 1;
                result |= (UInt32)(val & 1);
                val = (UInt32)(val >> 1);
            }

            return result;
        }


        public static UInt32 SwapBytes(UInt32 v)
        {
            return (((0xFF00FF00 & v) >> 8) | ((0x00FF00FF & v) << 8));
        }

        public static UInt32 SwapWords(UInt32 v)
        {
            return (((0xFFFF0000 & v) >> 16) | ((0x0000FFFF & v) << 16));
        }

        public static UInt32 SwapBytesAndWords(UInt32 v)
        {
            return SwapWords(SwapBytes(v));
        }

    }
}
