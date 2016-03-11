using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal static class PrimitiveTypes
    {
        public static IReadOnlyDictionary<Type, IPrimitiveTypeInfo> Types;

        static PrimitiveTypes()
        {
            var primitiveTypes = new IPrimitiveTypeInfo[]
            {
                // We never read/write a bool directly
                new NonIntegerPrimitiveTypeInfo<bool>(sizeof(bool), null, null, null, null),
                new IntegerPrimitiveTypeInfo<byte>(sizeof(byte), false, byte.MinValue, byte.MaxValue, (x, y) => x.Write(y), x => x.ReadByte(), null),
                // We always serialize char as ASCII - impossible to do sensibly any other way, due to unknown size of other encodings
                // If they need something more complex, use string, not char
                new IntegerPrimitiveTypeInfo<char>(1, false, char.MinValue, char.MaxValue, (x, y) => x.Write(y), x => x.ReadChar(), null),
                new IntegerPrimitiveTypeInfo<sbyte>(sizeof(sbyte), true, sbyte.MinValue, sbyte.MaxValue, (x, y) => x.Write(y), x => x.ReadSByte(), null),
                new NonIntegerPrimitiveTypeInfo<double>(sizeof(double), (x, y) => x.Write(y), x => x.ReadDouble(), x => EndianUtilities.SwapToBytes(x), x => EndianUtilities.SwapDoubleFromBytes(x)),
                new NonIntegerPrimitiveTypeInfo<decimal>(sizeof(decimal), (x, y) => x.Write(y), x => x.ReadDecimal(), x => EndianUtilities.SwapToBytes(x), x => EndianUtilities.SwapDecimalFromBytes(x)),
                new IntegerPrimitiveTypeInfo<short>(sizeof(short), true, short.MinValue, short.MaxValue, (x, y) => x.Write(y), x => x.ReadInt16(), x => EndianUtilities.Swap(x)),
                new IntegerPrimitiveTypeInfo<ushort>(sizeof(ushort), false, ushort.MinValue, ushort.MaxValue, (x, y) => x.Write(y), x => x.ReadUInt16(), x => EndianUtilities.Swap(x)),
                new IntegerPrimitiveTypeInfo<int>(sizeof(int), true, int.MinValue, int.MaxValue, (x, y) => x.Write(y), x => x.ReadInt32(), x => EndianUtilities.Swap(x)),
                new IntegerPrimitiveTypeInfo<uint>(sizeof(uint), false, uint.MinValue, uint.MaxValue, (x, y) => x.Write(y), x => x.ReadUInt32(), x => EndianUtilities.Swap(x)),
                new IntegerPrimitiveTypeInfo<long>(sizeof(long), true, long.MinValue, long.MaxValue, (x, y) => x.Write(y), x => x.ReadInt64(), x => EndianUtilities.Swap(x)),
                new IntegerPrimitiveTypeInfo<ulong>(sizeof(ulong), false, ulong.MinValue, ulong.MaxValue, (x, y) => x.Write(y), x => x.ReadUInt64(), x => EndianUtilities.Swap(x)),
                new NonIntegerPrimitiveTypeInfo<float>(sizeof(float), (x, y) => x.Write(y), x => x.ReadSingle(), x => EndianUtilities.SwapToBytes(x), x => EndianUtilities.SwapSingleFromBytes(x)),
            };
            Types = primitiveTypes.ToDictionary(x => x.Type, x => x);
        }

        public static bool IsPrimitive(Type type)
        {
            return Types.ContainsKey(type);
        }

        public static bool TryGetValue(Type type, out IPrimitiveTypeInfo primitiveTypeInfo)
        {
            return Types.TryGetValue(type, out primitiveTypeInfo);
        }
    }
}
