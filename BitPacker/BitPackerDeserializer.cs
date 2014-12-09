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
        private Func<BitfieldBinaryReader, object> deserializer;
        private Type subjectType;

        public bool HasFixedSize { get; private set; }
        public int MinSize { get; private set; }

        public BitPackerDeserializer(Type subjectType)
        {
            this.subjectType = subjectType;

            var reader = Expression.Parameter(typeof(BitfieldBinaryReader), "reader");

            var builder = new DeserializerExpressionBuilder(reader, subjectType);
            var typeDetails = builder.Deserialize();

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            this.deserializer = Expression.Lambda<Func<BitfieldBinaryReader, object>>(typeDetails.OperationExpression, reader).Compile();
        }

        public object Deserialize(Stream stream)
        {
            using (var reader = new BitfieldBinaryReader(stream))
            {
                return this.deserializer(reader);
            }
        }
    }

    public class BitPackerDeserializer<T> : IDeserializer<T>
    {
        private static Lazy<BitPackerDeserializer<T>> lazy = new Lazy<BitPackerDeserializer<T>>(() => new BitPackerDeserializer<T>());
        internal static BitPackerDeserializer<T> Instance
        {
            get { return lazy.Value; }
        }

        private Func<BitfieldBinaryReader, T> deserializer;

        public bool HasFixedSize { get; private set; }
        public int MinSize { get; private set; }

        public BitPackerDeserializer()
        {
            var subjectType = typeof(T);

            var reader = Expression.Parameter(typeof(BitfieldBinaryReader), "reader");

            var builder = new DeserializerExpressionBuilder(reader, subjectType);
            var typeDetails = builder.Deserialize();

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            this.deserializer = Expression.Lambda<Func<BitfieldBinaryReader, T>>(typeDetails.OperationExpression, reader).Compile();
        }

        public T Deserialize(Stream stream)
        {
            using (var reader = new BitfieldBinaryReader(stream))
            {
                return this.deserializer(reader);
            }
        }
    }
}
