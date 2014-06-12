using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    internal class DeserializerExpressionBuilder
    {
        private readonly Expression reader;
        private readonly Type objectType;

        public DeserializerExpressionBuilder(Expression reader, Type objectType)
        {
            this.reader = reader;
            this.objectType = objectType;
        }

        public TypeDetails Deserialize(Expression subject)
        {
            var objectDetails = new ObjectDetails(this.objectType, subject, new BitPackerMemberAttribute());
            objectDetails.Discover();

            // First, we need to make sure it's fully constructed
            var blockMembers = new List<Expression>();

            blockMembers.Add(this.EnsureFullyConstructed(objectDetails));

            var deserialized = this.DeserializeCustomType(objectDetails);
            blockMembers.Add(deserialized.OperationExpression);

            return new TypeDetails(deserialized.HasFixedSize, deserialized.MinSize, Expression.Block(blockMembers.Where(x => x != null)));
        }

        private Expression EnsureFullyConstructed(ObjectDetails objectDetails)
        {
            var properties = objectDetails.RecursiveFlatProperties().Where(x => x.IsCustomType);
            var blockMembers = properties.Select(property =>
            {
                return Expression.IfThen(
                    Expression.Equal(property.Value, Expression.Constant(null, property.Type)),
                    Expression.Assign(property.Value, Expression.New(property.Type))
                );
            });

            return blockMembers.Any() ? Expression.Block(blockMembers) : null;
        }

        private TypeDetails DeserializeValue(ObjectDetails objectDetails)
        {
            //if (objectDetails.IsEnumerable)
            //    return this.DeserializeEnumerable(objectDetails);

            if (PrimitiveTypes.Types.ContainsKey(objectDetails.Type))
                return this.DeserializeAndAssignPrimitive(objectDetails);

            if (objectDetails.IsEnum)
                return this.DeserializeAndAssignEnum(objectDetails);

            if (objectDetails.IsCustomType)
                return this.DeserializeCustomType(objectDetails);

            throw new Exception(String.Format("Don't know how to deserialize type {0}", objectDetails.Type.Name));
        }

        public TypeDetails DeserializeCustomType(ObjectDetails objectDetails)
        {
            // If it's not marked with our attribute, we're not serializing it
            if (!objectDetails.IsCustomType)
                return null;

            Expression result;
            var typeDetails = objectDetails.Properties.Select(property => this.DeserializeValue(property)).ToArray();

            // If they claim to be able to serialize themselves, let them
            if (typeof(IDeserialize).IsAssignableFrom(objectDetails.Type))
            {
                var method = typeof(ISerialize).GetMethod("Deserialize");
                result = Expression.Call(objectDetails.Value, method, this.reader);
            }
            else
            {
                var blockMembers = typeDetails.Select(x => x.OperationExpression);

                result = Expression.Block(blockMembers.Where(x => x != null));
            }

            return new TypeDetails(typeDetails.All(x => x.HasFixedSize), typeDetails.Sum(x => x.MinSize), result);
        }

        private TypeDetails DeserializePrimitive(ObjectDetails objectDetails)
        {
            // Even through EndiannessUtilities has now Swap(byte) overload, we get an AmbiguousMatchException
            // when we try and find such a method (maybe the byte is being coerced into an int or something?).
            // Therefore, handle this..

            var info = PrimitiveTypes.Types[objectDetails.Type];
            var value = info.DeserializeExpression(this.reader);

            if (objectDetails.Endianness != EndianUtilities.HostEndianness && info.Size > 1)
            {
                // If EndianUtilities has a Swap method for this type, then we can convert it
                var swapMethod = typeof(EndianUtilities).GetMethod("Swap", new[] { objectDetails.Type });
                if (swapMethod != null)
                    value = Expression.Call(swapMethod, value);
            }

            return new TypeDetails(true, info.Size, value);
        }

        private TypeDetails DeserializeAndAssignPrimitive(ObjectDetails objectDetails)
        {
            var typeDetails = this.DeserializePrimitive(objectDetails);
            return new TypeDetails(typeDetails.HasFixedSize, typeDetails.MinSize, Expression.Assign(objectDetails.Value, typeDetails.OperationExpression));
        }

        public TypeDetails DeserializeAndAssignEnum(ObjectDetails objectDetails)
        {
            var typeDetails = this.DeserializePrimitive(objectDetails.EnumEquivalentObjectDetails);
            var expression = Expression.Assign(objectDetails.Value, Expression.Convert(typeDetails.OperationExpression, objectDetails.Type));
            return new TypeDetails(typeDetails.HasFixedSize, typeDetails.MinSize, expression);
        }
    }
}
