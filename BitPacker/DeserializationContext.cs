using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class DeserializationStepContext
    {
        public ObjectDetails ObjectDetails { get; private set; }
        public Expression Subject { get; private set; }
        public string MemberName { get; private set; }

        public DeserializationStepContext(ObjectDetails objectDetails, Expression subject, string memberName)
        {
            this.ObjectDetails = objectDetails;
            this.Subject = subject;
            this.MemberName = memberName;
        }
    }

    internal class DeserializationContext
    {
        private readonly IImmutableStack<DeserializationStepContext> stack;

        public ObjectDetails ObjectDetails { get; private set; }

        public Expression Subject
        {
            get { return this.stack.Peek().Subject; }
        }

        public DeserializationContext(ObjectDetails objectDetails)
            : this(objectDetails, ImmutableStack<DeserializationStepContext>.Empty)
        { }

        private DeserializationContext(ObjectDetails objectDetails, IImmutableStack<DeserializationStepContext> stack)
        {
            this.ObjectDetails = objectDetails;
            this.stack = stack;
        }

        public DeserializationContext Push(ObjectDetails objectDetails, Expression subject, string memberName)
        {
            return new DeserializationContext(objectDetails, this.stack.Push(new DeserializationStepContext(this.ObjectDetails, subject, memberName)));
        }

        public bool TryFindLengthKey(string key, out PropertyObjectDetails objectDetails, out Expression subject)
        {
            PropertyObjectDetails lengthField;

            foreach (var step in this.stack)
            {
                if (step.ObjectDetails.IsCustomType && step.ObjectDetails.LengthFields.TryGetValue(key, out lengthField))
                {
                    objectDetails = lengthField;
                    subject = step.Subject;
                    return true;
                }
            }

            objectDetails = null;
            subject = null;
            return false;
        }

        public List<string> GetMemberPath()
        {
            return this.stack.Select(x => x.MemberName).Reverse().ToList();
        }
    }
}
