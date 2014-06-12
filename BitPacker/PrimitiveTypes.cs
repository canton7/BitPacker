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
                new PrimitiveTypeInfo<bool>(sizeof(bool), false, (x, y) => x.Write(y), x => x.ReadBoolean()),
                new PrimitiveTypeInfo<byte>(sizeof(byte), true, (x, y) => x.Write(y), x => x.ReadByte()),
                new PrimitiveTypeInfo<char>(sizeof(char), false, (x, y) => x.Write(y), x => x.ReadChar()),
                new PrimitiveTypeInfo<sbyte>(sizeof(sbyte), true, (x, y) => x.Write(y), x => x.ReadSByte()),
                new PrimitiveTypeInfo<double>(sizeof(double), false, (x, y) => x.Write(y), x => x.ReadDouble()),
                new PrimitiveTypeInfo<decimal>(sizeof(decimal), false, (x, y) => x.Write(y), x => x.ReadDecimal()),
                new PrimitiveTypeInfo<short>(sizeof(short), true, (x, y) => x.Write(y), x => x.ReadInt16()),
                new PrimitiveTypeInfo<ushort>(sizeof(ushort), true, (x, y) => x.Write(y), x => x.ReadUInt16()),
                new PrimitiveTypeInfo<int>(sizeof(int), true, (x, y) => x.Write(y), x => x.ReadInt32()),
                new PrimitiveTypeInfo<uint>(sizeof(uint), true, (x, y) => x.Write(y), x => x.ReadUInt32()),
                new PrimitiveTypeInfo<long>(sizeof(long), true, (x, y) => x.Write(y), x => x.ReadInt64()),
                new PrimitiveTypeInfo<ulong>(sizeof(ulong), true, (x, y) => x.Write(y), x => x.ReadUInt64()),
                new PrimitiveTypeInfo<float>(sizeof(float), false, (x, y) => x.Write(y), x => x.ReadSingle()),
            };
            Types = primitiveTypes.ToDictionary(x => x.Type, x => x);
        }

        public static bool IsPrimitive(Type type)
        {
            return Types.ContainsKey(type);
        }
    }
}
