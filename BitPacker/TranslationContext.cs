using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class TranslationStepContext
    {
        public ObjectDetails ObjectDetails { get; private set; }
        public Expression Subject { get; private set; }
        public string MemberName { get; private set; }

        public TranslationStepContext(ObjectDetails objectDetails, Expression subject, string memberName)
        {
            this.ObjectDetails = objectDetails;
            this.Subject = subject;
            this.MemberName = memberName;
        }
    }

    internal class TranslationContext
    {
        private readonly IImmutableStack<TranslationStepContext> stack;

        public ObjectDetails ObjectDetails { get; private set; }

        public Expression Subject
        {
            get { return this.stack.Peek().Subject; }
        }

        public TranslationContext(ObjectDetails objectDetails)
            : this(objectDetails, ImmutableStack<TranslationStepContext>.Empty)
        { }

        public TranslationContext(ObjectDetails objectDetails, Expression subject)
            : this(objectDetails, new ImmutableStack<TranslationStepContext>(new TranslationStepContext(objectDetails, subject, "root")))
        { }

        private TranslationContext(ObjectDetails objectDetails, IImmutableStack<TranslationStepContext> stack)
        {
            this.ObjectDetails = objectDetails;
            this.stack = stack;
        }

        public TranslationContext Push(ObjectDetails objectDetails, Expression subject, string memberName)
        {
            return new TranslationContext(objectDetails, this.stack.Push(new TranslationStepContext(this.ObjectDetails, subject, memberName)));
        }

        public bool TryFindLengthKey(string key, out PropertyObjectDetails objectDetails, out Expression subject)
        {
            PropertyObjectDetails lengthField;
            int orderMustBeLessThan = this.ObjectDetails.Order;

            foreach (var step in this.stack)
            {
                if (step.ObjectDetails.IsCustomType && step.ObjectDetails.LengthFields.TryGetValue(key, out lengthField))
                {
                    if (lengthField.Order >= orderMustBeLessThan)
                        throw new Exception(String.Format("Found length key '{0}', but it appears after the array it's acting as the length for", key));

                    objectDetails = lengthField;
                    subject = step.Subject;
                    return true;
                }

                orderMustBeLessThan = step.ObjectDetails.Order;
            }

            objectDetails = null;
            subject = null;
            return false;
        }

        public List<string> GetMemberPath()
        {
            return this.stack.Select(x => x.MemberName).Where(x => x != null).Reverse().ToList();
        }
    }
}
