using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal static class EndianUtilities
    {
        public static Endianness HostEndianness = BitConverter.IsLittleEndian ? Endianness.LittleEndian : Endianness.BigEndian;        

        public static ushort Swap(ushort val)
        {
            unchecked
            {
                return (ushort)(((val & 0xFF00) >> 8) | ((val & 0x00FF) << 8));
            }
        }

        public static short Swap(short val)
        {
            unchecked
            {
                return (short)Swap((ushort)val);
            }
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
            unchecked
            {
                return (int)Swap((uint)val);
            }
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
            unchecked
            {
                return (long)Swap((ulong)val);
            }
        }

        public static byte[] SwapToBytes(float val)
        {
            return BitConverter.GetBytes(Swap(ToInt32(val)));
        }

        public static float SwapSingleFromBytes(byte[] bytes)
        {
            return ToSingle(Swap(BitConverter.ToInt32(bytes, 0)));
        }

        public static byte[] SwapToBytes(double val)
        {
            return BitConverter.GetBytes(Swap(ToInt64(val)));
        }

        public static double SwapDoubleFromBytes(byte[] bytes)
        {
            return ToDouble(Swap(BitConverter.ToInt64(bytes, 0)));
        }


        public static byte[] SwapToBytes(decimal val)
        {
            int[] ints = Decimal.GetBits(val);
            byte[] bytes = new byte[4 * 4];
            
            // Read the ints right-left, and write each one right-most byte first
            for (int i = 0; i < 4; i++)
            {
                var thisInt = ints[4 - 1 - i];
                bytes[i * 4 + 0] = (byte)(thisInt >> 24);
                bytes[i * 4 + 1] = (byte)(thisInt >> 16);
                bytes[i * 4 + 2] = (byte)(thisInt >> 8);
                bytes[i * 4 + 3] = (byte)thisInt;
            }

            return bytes;
        }

        public static decimal SwapDecimalFromBytes(byte[] bytes)
        {
            int[] ints = new int[4];
            for (int i = 0; i < 4; i++)
            {
                // First byte forms the highest byte of the last int
                ints[4 - 1 - i] = (bytes[i * 4 + 0] << 24) | (bytes[i * 4 + 1] << 16) | (bytes[i * 4 + 2] << 8) | bytes[i * 4 + 3];
            }
            return new Decimal(ints);
        }

        // Thanks to chilvers in ##csharp: https://gist.github.com/chilversc/f4a031f6f7327f2e5ab4
        private static int ToInt32(float value)
        {
            return new IntFloatMap() { Float = value }.Int;
        }

        private static float ToSingle(int value)
        {
            return new IntFloatMap() { Int = value }.Float;
        }

        private static long ToInt64(double value)
        {
            return new LongDoubleMap() { Double = value }.Long;
        }

        private static double ToDouble(long value)
        {
            return new LongDoubleMap() { Long = value }.Double;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct IntFloatMap
        {
            [FieldOffset(0)]
            public int Int;

            [FieldOffset(0)]
            public float Float;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct LongDoubleMap
        {
            [FieldOffset(0)]
            public long Long;

            [FieldOffset(0)]
            public double Double;
        }
    }
}
