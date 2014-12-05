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
            // Alternatives are BitConverter.ToSingle(BitConverter.GetBytes(val).Reverse().ToArray(), 0)
            // and BitConverter.ToSingle(BitConverter.GetBytes(Swap(BitConverter.ToInt32(BitConverter.GetBytes(val), 0))), 0)
            return ToSingle(Swap(ToInt32(val)));
        }

        public static double Swap(double val)
        {
            // We *could* use BitConverter.Int64BitsToDouble(Swap(BitConverter.DoubleToInt64Bits(val))), but that throws if
            // system endianness isn't LittleEndian... Unlikely to ever not be the case, but we have a good workaround
            // (and we don't require that assertion)
            return ToDouble(Swap(ToInt64(val)));
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
