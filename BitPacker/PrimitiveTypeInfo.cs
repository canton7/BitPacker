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
    internal interface IPrimitiveTypeInfo
    {
        Type Type { get; }
        int Size { get; }
        bool IsIntegral { get; }
        Expression SerializeExpression(Expression writer, Expression value);
        Expression DeserializeExpression(Expression reader);
    }

    internal class PrimitiveTypeInfo<T> : IPrimitiveTypeInfo
    {
        private readonly Type type;
        private readonly int size;
        private readonly bool isIntegral;

        private MethodInfo serializeMethod;
        private MethodInfo deserializeMethod;

        public Type Type
        {
            get { return this.type; }
        }

        public int Size
        {
            get { return this.size; }
        }

        public bool IsIntegral
        {
            get { return this.isIntegral; }
        }

        public PrimitiveTypeInfo(int size, bool isIntegral, Expression<Action<BinaryWriter, T>> writer, Expression<Func<BinaryReader, T>> reader)
        {
            this.type = typeof(T);
            this.size = size;
            this.isIntegral = isIntegral;

            this.serializeMethod = ((MethodCallExpression)writer.Body).Method;
            this.deserializeMethod = ((MethodCallExpression)reader.Body).Method;
        }

        public Expression SerializeExpression(Expression writer, Expression value)
        {
            return Expression.Call(writer, this.serializeMethod, value);
        }

        public Expression DeserializeExpression(Expression reader)
        {
            return Expression.Call(reader, this.deserializeMethod);
        }
    }
}
