using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class PrimitiveTypeInfo
    {
        public readonly Type Type;
        public readonly int Size;

        private MethodInfo serializeMethod;

        public PrimitiveTypeInfo(Type type, int size)
        {
            this.Type = type;
            this.Size = size;

            this.serializeMethod = typeof(BinaryWriter).GetMethod("Write", new[] { type });
        }

        public Expression SerializeExpression(Expression writer, Expression value)
        {
            return Expression.Call(writer, this.serializeMethod, value);
        }
    }
}
