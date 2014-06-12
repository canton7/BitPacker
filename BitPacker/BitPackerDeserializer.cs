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
        private Action<BinaryReader, object> deserializer;
        private Type subjectType;

        public bool HasFixedSize { get; private set; }
        public int MinSize { get; private set; }

        public BitPackerDeserializer(Type subjectType)
        {
            this.subjectType = subjectType;

            var reader = Expression.Parameter(typeof(BinaryReader), "reader");
            var subject = Expression.Parameter(typeof(object), "subject");

            var subjectVar = Expression.Variable(subjectType, "typedSubject");
            var assignment = Expression.Assign(subjectVar, Expression.Convert(subject, subjectType));

            var builder = new DeserializerExpressionBuilder(reader, subjectType);
            var typeDetails = builder.Deserialize(subjectVar);

            this.HasFixedSize = typeDetails.HasFixedSize;
            this.MinSize = typeDetails.MinSize;

            var block = Expression.Block(new[] { subjectVar }, assignment, typeDetails.OperationExpression);
            this.deserializer = Expression.Lambda<Action<BinaryReader, object>>(block, reader, subject).Compile();
        }

        private void CheckType(object subject)
        {
            if (!this.subjectType.IsAssignableFrom(subject.GetType()))
                throw new Exception(String.Format("Deserializer for type {0} call with subject of type {1}", this.subjectType, subject.GetType()));
        }

        public void Deserialize(BinaryReader reader, object subject)
        {
            this.CheckType(subject);
            this.deserializer(reader, subject);
        }
    }
}
