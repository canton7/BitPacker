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
        Expression SwappedSerializeExpression(Expression writer, Expression value);
        Expression DeserializeExpression(Expression reader);
        Expression SwappedDeserializeExpression(Expression reader);
    }

    internal abstract class PrimitiveTypeInfo<T> : IPrimitiveTypeInfo where T : struct
    {
        private readonly Type type;
        private readonly int size;
        private readonly bool isIntegral;
        private readonly bool isSigned;
        private readonly ulong maxValue;
        private readonly long minValue;

        protected readonly MethodInfo serializeMethod;
        protected readonly MethodInfo deserializeMethod;

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

        protected PrimitiveTypeInfo(int size,
            bool isIntegral,
            bool isSigned,
            T minValue,
            T maxValue,
            Expression<Action<BitfieldBinaryWriter, T>> writer,
            Expression<Func<BitfieldBinaryReader, T>> reader)
        {
            this.type = typeof(T);
            this.size = size;
            this.isIntegral = isIntegral;
            this.isSigned = isSigned;
            this.minValue = Convert.ToInt64(minValue);
            this.maxValue = Convert.ToUInt64(maxValue);

            if (writer != null)
                this.serializeMethod = ((MethodCallExpression)writer.Body).Method;

            if (reader != null)
                this.deserializeMethod = ((MethodCallExpression)reader.Body).Method;
        }

        public Expression SerializeExpression(Expression writer, Expression value)
        {
            return Expression.Call(writer, this.serializeMethod, value);
        }

        public abstract Expression SwappedSerializeExpression(Expression writer, Expression value);

        public Expression DeserializeExpression(Expression reader)
        {
            return Expression.Call(reader, this.deserializeMethod);
        }

        public abstract Expression SwappedDeserializeExpression(Expression reader);
    }

    internal class IntegerPrimitiveTypeInfo<T> : PrimitiveTypeInfo<T> where T : struct
    {
        private readonly MethodInfo swapMethod;

        public IntegerPrimitiveTypeInfo(int size,
            bool isSigned,
            T minValue,
            T maxValue,
            Expression<Action<BitfieldBinaryWriter, T>> writer,
            Expression<Func<BitfieldBinaryReader, T>> reader,
            Expression<Func<T, T>> swapper)
            : base(size, true, isSigned, minValue, maxValue, writer, reader)
        {
            if (swapper != null)
                this.swapMethod = ((MethodCallExpression)swapper.Body).Method;
        }

        public override Expression SwappedSerializeExpression(Expression writer, Expression value)
        {
            if (this.swapMethod == null)
                return this.SerializeExpression(writer, value);
            return Expression.Call(writer, this.serializeMethod, Expression.Call(this.swapMethod, value));
        }

        public override Expression SwappedDeserializeExpression(Expression reader)
        {
            if (this.swapMethod == null)
                return this.DeserializeExpression(reader);
            return Expression.Call(this.swapMethod, Expression.Call(reader, this.deserializeMethod));
        }
    }

    internal class NonIntegerPrimitiveTypeInfo<T> : PrimitiveTypeInfo<T> where T : struct
    {
        private static readonly MethodInfo writeBytesMethod = typeof(BitfieldBinaryWriter).GetMethod("Write", new[] { typeof(byte[]) });
        private static readonly MethodInfo readBytesMethod = typeof(BitfieldBinaryReader).GetMethod("ReadBytes", new[] { typeof(int) });

        private readonly MethodInfo writeSwapperMethod;
        private readonly MethodInfo readSwapperMethod;

        public NonIntegerPrimitiveTypeInfo(int size,
            Expression<Action<BitfieldBinaryWriter, T>> writer,
            Expression<Func<BitfieldBinaryReader, T>> reader,
            Expression<Func<T, byte[]>> writeSwapper,
            Expression<Func<byte[], T>> readSwapper)
            : base(size, false, false, default(T), default(T), writer, reader)
        {
            if (writeSwapper != null)
                this.writeSwapperMethod = ((MethodCallExpression)writeSwapper.Body).Method;

            if (readSwapper != null)
                this.readSwapperMethod = ((MethodCallExpression)readSwapper.Body).Method;
        }

        public override Expression SwappedSerializeExpression(Expression writer, Expression value)
        {
            return Expression.Call(writer, writeBytesMethod, Expression.Call(this.writeSwapperMethod, value));
        }

        public override Expression SwappedDeserializeExpression(Expression reader)
        {
            return Expression.Call(this.readSwapperMethod, Expression.Call(reader, readBytesMethod, Expression.Constant(this.Size)));
        }
    }
}
