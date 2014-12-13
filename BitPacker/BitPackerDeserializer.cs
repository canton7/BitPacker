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
            : this(subjectType, null)
        { }

        public BitPackerDeserializer(Type subjectType, Endianness defaultEndianness)
            : this(subjectType, (Endianness?)defaultEndianness)
        { }

        private BitPackerDeserializer(Type subjectType, Endianness? defaultEndianness)
        {
            this.subjectType = subjectType;

            var reader = Expression.Parameter(typeof(BitfieldBinaryReader), "reader");

            var builder = new DeserializerExpressionBuilder(reader, subjectType, defaultEndianness);
            var typeDetails = builder.BuildExpression();

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            this.deserializer = Expression.Lambda<Func<BitfieldBinaryReader, object>>(typeDetails.OperationExpression, reader).Compile();
        }

        public int Deserialize(Stream stream, out object subject)
        {
            var countingStream = new CountingStream(stream);
            using (var reader = new BitfieldBinaryReader(countingStream))
            {
                subject = this.deserializer(reader);
            }
            return countingStream.BytesRead;
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
            : this(null)
        { }

        public BitPackerDeserializer(Endianness defaultEndianness)
            : this((Endianness?)defaultEndianness)
        { }

        private BitPackerDeserializer(Endianness? defaultEndianness)
        {
            var subjectType = typeof(T);

            var reader = Expression.Parameter(typeof(BitfieldBinaryReader), "reader");

            var builder = new DeserializerExpressionBuilder(reader, subjectType, defaultEndianness);
            var typeDetails = builder.BuildExpression();

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            this.deserializer = Expression.Lambda<Func<BitfieldBinaryReader, T>>(typeDetails.OperationExpression, reader).Compile();
        }

        public int Deserialize(Stream stream, out T subject)
        {
            var countingStream = new CountingStream(stream);
            using (var reader = new BitfieldBinaryReader(countingStream))
            {
                subject = this.deserializer(reader);
            }
            return countingStream.BytesRead;
        }
    }
}
