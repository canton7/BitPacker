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
                PrimitiveTypeInfo<bool>.NonInteger(sizeof(bool),  (x, y) => x.Write(y), x => x.ReadBoolean()),
                PrimitiveTypeInfo<byte>.Integer(sizeof(byte), false, byte.MinValue, byte.MaxValue, (x, y) => x.Write(y), x => x.ReadByte()),
                PrimitiveTypeInfo<char>.Integer(sizeof(char), false, char.MinValue, char.MaxValue, (x, y) => x.Write(y), x => x.ReadChar()),
                PrimitiveTypeInfo<sbyte>.Integer(sizeof(sbyte), true, sbyte.MinValue, sbyte.MaxValue, (x, y) => x.Write(y), x => x.ReadSByte()),
                PrimitiveTypeInfo<double>.NonInteger(sizeof(double), (x, y) => x.Write(y), x => x.ReadDouble()),
                PrimitiveTypeInfo<decimal>.NonInteger(sizeof(decimal), (x, y) => x.Write(y), x => x.ReadDecimal()),
                PrimitiveTypeInfo<short>.Integer(sizeof(short), true, short.MinValue, short.MaxValue, (x, y) => x.Write(y), x => x.ReadInt16()),
                PrimitiveTypeInfo<ushort>.Integer(sizeof(ushort), false, ushort.MinValue, ushort.MaxValue, (x, y) => x.Write(y), x => x.ReadUInt16()),
                PrimitiveTypeInfo<int>.Integer(sizeof(int), true, int.MinValue, int.MaxValue, (x, y) => x.Write(y), x => x.ReadInt32()),
                PrimitiveTypeInfo<uint>.Integer(sizeof(uint), false, uint.MinValue, uint.MaxValue, (x, y) => x.Write(y), x => x.ReadUInt32()),
                PrimitiveTypeInfo<long>.Integer(sizeof(long), true, long.MinValue, long.MaxValue, (x, y) => x.Write(y), x => x.ReadInt64()),
                PrimitiveTypeInfo<ulong>.Integer(sizeof(ulong), false, ulong.MinValue, ulong.MaxValue, (x, y) => x.Write(y), x => x.ReadUInt64()),
                PrimitiveTypeInfo<float>.NonInteger(sizeof(float), (x, y) => x.Write(y), x => x.ReadSingle()),
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
