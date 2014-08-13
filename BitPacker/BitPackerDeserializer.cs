using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public class BitPackerDeserializer : IDeserializer
    {
        private Func<BinaryReader, object> deserializer;
        private Type subjectType;

        public bool HasFixedSize { get; private set; }
        public int MinSize { get; private set; }

        public BitPackerDeserializer(Type subjectType)
        {
            this.subjectType = subjectType;

            var reader = Expression.Parameter(typeof(BinaryReader), "reader");

            var builder = new DeserializerExpressionBuilder(reader, subjectType);
            var typeDetails = builder.Deserialize();

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            this.deserializer = Expression.Lambda<Func<BinaryReader, object>>(typeDetails.OperationExpression, reader).Compile();
        }

        public object Deserialize(BinaryReader reader)
        {
            return this.deserializer(reader);
        }
    }

    public class BitPackerDeserializer<T> : IDeserializer<T>
    {
        private static Lazy<BitPackerDeserializer<T>> lazy = new Lazy<BitPackerDeserializer<T>>(() => new BitPackerDeserializer<T>());
        internal static BitPackerDeserializer<T> Instance
        {
            get { return lazy.Value; }
        }

        private Func<BinaryReader, T> deserializer;

        public bool HasFixedSize { get; private set; }
        public int MinSize { get; private set; }

        public BitPackerDeserializer()
        {
            var subjectType = typeof(T);

            var reader = Expression.Parameter(typeof(BinaryReader), "reader");

            var builder = new DeserializerExpressionBuilder(reader, subjectType);
            var typeDetails = builder.Deserialize();

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            this.deserializer = Expression.Lambda<Func<BinaryReader, T>>(typeDetails.OperationExpression, reader).Compile();
        }

        public T Deserialize(BinaryReader reader)
        {
            return this.deserializer(reader);
        }
    }
}
