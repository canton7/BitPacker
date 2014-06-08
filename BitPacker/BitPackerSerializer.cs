using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public abstract class BitPackerSerializerBase
    {
        protected Action<BinaryWriter, object> serializer;

        public bool HasFixedSize { get; private set; }
        public int MinSize { get; private set; }

        public BitPackerSerializerBase(Type subjectType)
        {
            var writer = Expression.Parameter(typeof(BinaryWriter), "writer");
            var subject = Expression.Parameter(typeof(object), "subject");

            var subjectVar = Expression.Variable(subjectType, "typedSubject");
            var assignment = Expression.Assign(subjectVar, Expression.Convert(subject, subjectType));

            var builder = new BitPackerExpressionBuilder(writer);
            var typeDetails = builder.SerializeCustomType(subjectVar, subjectType);

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            var block = Expression.Block(new[] { subjectVar }, assignment, typeDetails.OperationExpression);
            this.serializer = Expression.Lambda<Action<BinaryWriter, object>>(block, writer, subject).Compile();
        }

        protected void SerializeInternal(BinaryWriter writer, object subject)
        {
            this.serializer(writer, subject);
        }

        protected byte[] SerializeInternal(object subject)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                this.serializer(writer, subject);
                return ms.GetBuffer().Take((int)ms.Position).ToArray();
            }
        }
    }

    public class BitPackerSerializer : BitPackerSerializerBase
    {
        public BitPackerSerializer(Type subjectType)
            : base(subjectType)
        { }

        public void Serialize(BinaryWriter writer, object subject)
        {
            this.SerializeInternal(writer, subject);
        }

        public byte[] Serialize(object subject)
        {
            return this.SerializeInternal(subject);
        }
    }

    public class BitPackerSerializer<T> : BitPackerSerializerBase
    {
        public BitPackerSerializer()
            : base(typeof(T))
        { }

        public void Serialize(BinaryWriter writer, T subject)
        {
            this.SerializeInternal(writer, subject);
        }

        public byte[] Serialize(T subject)
        {
            return this.SerializeInternal(subject);
        }
    }
}
