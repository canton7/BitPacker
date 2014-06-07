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
    public class BitPackerSerializer
    {
        private Action<BinaryWriter, object> serializer;

        public BitPackerSerializer(Type subjectType)
        {
            var writer = Expression.Parameter(typeof(BinaryWriter), "writer");
            var subject = Expression.Parameter(typeof(object), "subject");

            var subjectVar = Expression.Variable(subjectType, "typedSubject");
            var assignment = Expression.Assign(subjectVar, Expression.Convert(subject, subjectType));

            var builder = new BitPackerExpressionBuilder(writer);
            var expression = builder.SerializeCustomType(subjectVar, subjectType);

            var block = Expression.Block(new[] { subjectVar }, assignment, expression);
            this.serializer = Expression.Lambda<Action<BinaryWriter, object>>(block, writer, subject).Compile();
        }

        public void Serialize(BinaryWriter writer, object subject)
        {
            this.serializer(writer, subject);
        }
    }
}
