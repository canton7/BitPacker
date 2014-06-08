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
                new PrimitiveTypeInfo(typeof(bool), sizeof(bool)),
                new PrimitiveTypeInfo(typeof(byte), sizeof(byte)),
                new PrimitiveTypeInfo(typeof(char), sizeof(char)),
                new PrimitiveTypeInfo(typeof(sbyte), sizeof(sbyte)),
                new PrimitiveTypeInfo(typeof(double), sizeof(double)),
                new PrimitiveTypeInfo(typeof(decimal), sizeof(decimal)),
                new PrimitiveTypeInfo(typeof(short), sizeof(short)),
                new PrimitiveTypeInfo(typeof(ushort), sizeof(ushort)),
                new PrimitiveTypeInfo(typeof(int), sizeof(int)),
                new PrimitiveTypeInfo(typeof(uint), sizeof(uint)),
                new PrimitiveTypeInfo(typeof(long), sizeof(long)),
                new PrimitiveTypeInfo(typeof(ulong), sizeof(ulong)),
                new PrimitiveTypeInfo(typeof(float), sizeof(float)),
            };
            Types = primitiveTypes.ToDictionary(x => x.Type, x => x);
        }
    }
}
