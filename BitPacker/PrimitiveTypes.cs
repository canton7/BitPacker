using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal static class PrimitiveTypes
    {
        public static IReadOnlyDictionary<Type, PrimitiveTypeInfo> Types;

        static PrimitiveTypes()
        {
            var primitiveTypes = new[]
            {
                new PrimitiveTypeInfo(typeof(bool), sizeof(bool), false),
                new PrimitiveTypeInfo(typeof(byte), sizeof(byte), true),
                new PrimitiveTypeInfo(typeof(char), sizeof(char), false),
                new PrimitiveTypeInfo(typeof(sbyte), sizeof(sbyte), true),
                new PrimitiveTypeInfo(typeof(double), sizeof(double), false),
                new PrimitiveTypeInfo(typeof(decimal), sizeof(decimal), false),
                new PrimitiveTypeInfo(typeof(short), sizeof(short), true),
                new PrimitiveTypeInfo(typeof(ushort), sizeof(ushort), true),
                new PrimitiveTypeInfo(typeof(int), sizeof(int), true),
                new PrimitiveTypeInfo(typeof(uint), sizeof(uint), true),
                new PrimitiveTypeInfo(typeof(long), sizeof(long), true),
                new PrimitiveTypeInfo(typeof(ulong), sizeof(ulong), true),
                new PrimitiveTypeInfo(typeof(float), sizeof(float), false),
            };
            Types = primitiveTypes.ToDictionary(x => x.Type, x => x);
        }

        public static bool IsPrimitive(Type type)
        {
            return Types.ContainsKey(type);
        }
    }
}
