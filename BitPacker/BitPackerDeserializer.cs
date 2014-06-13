using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public class BitPackerDeserializer
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

        private void CheckType(object subject)
        {
            if (!this.subjectType.IsAssignableFrom(subject.GetType()))
                throw new Exception(String.Format("Deserializer for type {0} call with subject of type {1}", this.subjectType, subject.GetType()));
        }

        public object Deserialize(BinaryReader reader)
        {
            return this.deserializer(reader);
        }
    }
}
