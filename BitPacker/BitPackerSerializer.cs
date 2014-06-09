using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public class BitPackerSerializer
    {
        protected internal Action<BinaryWriter, object> serializer;
        protected internal Type subjectType;

        public bool HasFixedSize { get; private set; }
        public int MinSize { get; private set; }

        public BitPackerSerializer(Type subjectType)
        {
            this.subjectType = subjectType;

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

        private void CheckType(object subject)
        {
            if (!this.subjectType.IsAssignableFrom(subject.GetType()))
                throw new Exception(String.Format("Serializer for type {0} call with subject of type {1}", this.subjectType, subject.GetType()));
        }

        public void Serialize(BinaryWriter writer, object subject)
        {
            this.CheckType(subject);
            this.serializer(writer, subject);
        }

        public byte[] Serialize(object subject)
        {
            this.CheckType(subject);
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                this.serializer(writer, subject);
                return ms.GetBuffer().Take((int)ms.Position).ToArray();
            }
        }
    }

    public class BitPackerSerializer<T>
    {
        public bool HasFixedSize { get; private set; }
        public int MinSize { get; private set; }

        private Action<BinaryWriter, T> serializer;

        public BitPackerSerializer()
	    {
            var subjectType = typeof(T);
            var writer = Expression.Parameter(typeof(BinaryWriter), "writer");
            var subject = Expression.Parameter(subjectType, "subject");

            var builder = new BitPackerExpressionBuilder(writer);
            var typeDetails = builder.SerializeCustomType(subject, subjectType);

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            this.serializer = Expression.Lambda<Action<BinaryWriter, T>>(typeDetails.OperationExpression, writer, subject).Compile();
	    }

        public void Serialize(BinaryWriter writer, T subject)
        {
            this.serializer(writer, subject);
        }

        public byte[] Serialize(T subject)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                this.serializer(writer, subject);
                return ms.GetBuffer().Take((int)ms.Position).ToArray();
            }
        }
    }
}
