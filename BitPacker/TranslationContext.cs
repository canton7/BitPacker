using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal struct TranslationStepContext
    {
        public readonly ObjectDetails ObjectDetails;
        public readonly Expression Subject;
        public readonly string MemberName;
        public readonly bool IsAssigned;

        public TranslationStepContext(ObjectDetails objectDetails, Expression subject, string memberName, bool isAssigned)
        {
            this.ObjectDetails = objectDetails;
            this.Subject = subject;
            this.MemberName = memberName;
            this.IsAssigned = isAssigned;
        }
    }

    internal class TranslationContext
    {
        private readonly ImmutableStack<TranslationStepContext> stack;

        public ObjectDetails ObjectDetails
        {
            get { return this.stack.Peek().ObjectDetails; }
        }

        public Expression Subject
        {
            get { return this.stack.Peek().Subject; }
        }

        public string MemberName
        {
            get { return this.stack.Peek().MemberName; }
        }

        public TranslationContext(ObjectDetails objectDetails, Expression subject)
            : this(ImmutableStack<TranslationStepContext>.Init(new TranslationStepContext(objectDetails, subject, null, false)))
        { }

        private TranslationContext(ImmutableStack<TranslationStepContext> stack)
        {
            this.stack = stack;
        }

        public TranslationContext PushAssigned(ObjectDetails objectDetails, Expression subject, string memberName)
        {
            return new TranslationContext(this.stack.Push(new TranslationStepContext(objectDetails, subject, memberName, true)));
        }

        public TranslationContext PushUnassigned(ObjectDetails objectDetails, Expression subject)
        {
            return new TranslationContext(this.stack.Push(new TranslationStepContext(objectDetails, subject, null, false)));
        }

        public PropertyObjectDetailsWithAccess FindLengthKey(string key)
        {
            return this.FindLengthKey(key, "length field", x => x.LengthFields, true);
        }

        public PropertyObjectDetailsWithAccess FindVariableLengthArrayWithLengthKey(string key)
        {
            return this.FindLengthKey(key, "variable-length array", x => x.VariableLengthArrays, false);
        }

        private PropertyObjectDetailsWithAccess FindLengthKey(string key, string debugTerm,
            Func<ObjectDetails, IReadOnlyDictionary<string, PropertyObjectDetails>> propertySelector,
            bool performOrderChecks)
        {
            PropertyObjectDetailsWithAccess memberAccess = null;
            // This is either the length field, or the variable-length array
            PropertyObjectDetails fieldOfInterest;

            int orderMustBeLessThan = this.ObjectDetails.Order;

            foreach (var step in this.stack)
            {
                if (!step.ObjectDetails.IsCustomType || !step.IsAssigned)
                    continue;

                if (propertySelector(step.ObjectDetails).TryGetValue(key, out fieldOfInterest))
                {
                    if (fieldOfInterest.Order >= orderMustBeLessThan && performOrderChecks)
                        throw new Exception(String.Format("Found {0} with length key '{1}', but it appears after the array it's acting as the length for", debugTerm, key));

                    memberAccess = new PropertyObjectDetailsWithAccess(fieldOfInterest, fieldOfInterest.AccessExpression(step.Subject));
                    break;
                }

                var childCandidatesOfThisStep = (from property in step.ObjectDetails.Properties
                                                let order = property.Order
                                                from recursiveProperty in new[] { new PropertyObjectDetailsWithAccess(property, step.Subject) }
                                                    .Concat(property.RecursiveFlatPropertyAccess(step.Subject))
                                                where recursiveProperty.ObjectDetails.IsCustomType
                                                from subFieldOfInterest in propertySelector(recursiveProperty.ObjectDetails)
                                                select new
                                                {
                                                    Order = order,
                                                    Details = new PropertyObjectDetailsWithAccess(subFieldOfInterest.Value, subFieldOfInterest.Value.AccessExpression(recursiveProperty.Value))
                                                }).ToArray();

                if (childCandidatesOfThisStep.Length > 1)
                    throw new Exception(String.Format("Found more than {0} with length key '{1}'", debugTerm, key));

                if (childCandidatesOfThisStep.Length == 1)
                {
                    var candidate = childCandidatesOfThisStep[0];

                    // In order for us to accept this, the object which ultimately holds the length key must be before us
                    if (candidate.Order >= orderMustBeLessThan && performOrderChecks)
                        throw new Exception(String.Format("Found length key '{0}', but it appears after the array it's acting as the length for", key));

                    memberAccess = candidate.Details;
                    break;
                }


                orderMustBeLessThan = step.ObjectDetails.Order;
            }

            if (memberAccess == null)
                throw new InvalidArraySetupException(String.Format("Could not find {0} with Length Key {1}", debugTerm, key));

            return memberAccess;
        }


        public List<string> GetMemberPath()
        {
            return this.stack.Select(x => x.MemberName).Where(x => x != null).Reverse().ToList();
        }

        public Expression FindParentContextOfType(Type type)
        {
            foreach (var step in this.stack)
            {
                if (step.Subject.Type == type)
                    return step.Subject;
            }
            return null;
        }
    }
}
