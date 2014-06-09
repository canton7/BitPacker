using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal static class EndianUtilities
    {
        public static Endianness HostEndianness = BitConverter.IsLittleEndian ? Endianness.LittleEndian : Endianness.BigEndian;        

        public static ushort Swap(ushort val)
        {
            return (ushort)(((val & 0xFF00) >> 8) | ((val & 0x00FF) << 8));
        }

        public static short Swap(short val)
        {
            return (short)Swap((ushort)val);
        }

        public static uint Swap(uint val)
        {
            // Swap adjacent 16-bit blocks
            val = (val >> 16) | (val << 16);

            // Swap adjacent 8-bit blocks
            val = ((val & 0xFF00FF00) >> 8) | ((val & 0x00FF00FF) << 8);
            return val;
        }

        public static int Swap(int val)
        {
            return (int)Swap((uint)val);
        }

        public static ulong Swap(ulong val)
        {
            // Swap adjacent 32-bit blocks
            val = (val >> 32) | (val << 32);
            // Swap adjacent 16-bit blocks
            val = ((val & 0xFFFF0000FFFF0000) >> 16) | ((val & 0x0000FFFF0000FFFF) << 16);
            // Swap adjacent 8-bit blocks
            val = ((val & 0xFF00FF00FF00FF00) >> 8) | ((val & 0x00FF00FF00FF00FF) << 8);
            return val;
        }

        public static long Swap(long val)
        {
            return (long)Swap((ulong)val);
        }

        public static float Swap(float val)
        {
            // We can't get the raw bytes ourselves without unmanaged code, and I don't want this library to be unmanaged
            // So use BitConverter, even though it's slower
            // TODO: Profile this against  BitConverter.ToSingle(BitConverter.GetBytes(val).Reverse().ToArray(), 0)
            // The current implementation doesn't do an array reversal and allocate, but does have more method calls
            return BitConverter.ToSingle(BitConverter.GetBytes(Swap(BitConverter.ToInt32(BitConverter.GetBytes(val), 0))), 0);
        }

        public static double Swap(double val)
        {
            // We can play a slightly better trick on this one
            return BitConverter.Int64BitsToDouble(Swap(BitConverter.DoubleToInt64Bits(val)));
        }
    }
}
