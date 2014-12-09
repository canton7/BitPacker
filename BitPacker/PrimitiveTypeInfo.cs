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
        bool IsSigned { get; }
        ulong MaxValue { get; }
        long MinValue { get; }
        Expression SerializeExpression(Expression writer, Expression value);
        Expression DeserializeExpression(Expression reader);
    }

    internal class PrimitiveTypeInfo<T> : IPrimitiveTypeInfo where T : struct
    {
        private readonly Type type;
        private readonly int size;
        private readonly bool isIntegral;
        private readonly bool isSigned;
        private readonly ulong maxValue;
        private readonly long minValue;

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

        public bool IsSigned
        {
            get
            {
                if (!this.IsIntegral)
                    throw new InvalidOperationException("Not integral");
                return this.isSigned;
            }
        }

        public ulong MaxValue
        {
            get
            {
                if (!this.IsIntegral)
                    throw new InvalidOperationException("Not integral");
                return this.maxValue;
            }
        }

        public long MinValue
        {
            get
            {
                if (!this.IsIntegral)
                    throw new InvalidOperationException("Not integral");
                return this.minValue;
            }
        }

        public static PrimitiveTypeInfo<T> Integer(int size, bool isSigned, T minValue, T maxValue, Expression<Action<BitfieldBinaryWriter, T>> writer, Expression<Func<BitfieldBinaryReader, T>> reader)
        {
            return new PrimitiveTypeInfo<T>(size, true, isSigned, minValue, maxValue, writer, reader);
        }

        public static PrimitiveTypeInfo<T> NonInteger(int size, Expression<Action<BitfieldBinaryWriter, T>> writer, Expression<Func<BitfieldBinaryReader, T>> reader)
        {
            return new PrimitiveTypeInfo<T>(size, false, false, default(T), default(T), writer, reader);
        }

        private PrimitiveTypeInfo(int size, bool isIntegral, bool isSigned, T minValue, T maxValue, Expression<Action<BitfieldBinaryWriter, T>> writer, Expression<Func<BitfieldBinaryReader, T>> reader)
        {
            this.type = typeof(T);
            this.size = size;
            this.isIntegral = isIntegral;
            this.isSigned = isSigned;
            this.minValue = Convert.ToInt64(minValue);
            this.maxValue = Convert.ToUInt64(maxValue);

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
