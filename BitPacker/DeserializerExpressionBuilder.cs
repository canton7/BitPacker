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
        private Dictionary<string, ObjectDetails> lengthFields;

        public DeserializerExpressionBuilder(Expression reader, Type objectType)
        {
            this.reader = reader;
            this.objectType = objectType;
        }

        public TypeDetails Deserialize(Expression subject)
        {
            var objectDetails = new ObjectDetails(this.objectType, subject, new BitPackerMemberAttribute());
            objectDetails.Discover();

            this.lengthFields = this.FindLengthFields(objectDetails);

            // First, we need to make sure it's fully constructed
            var blockMembers = new List<Expression>();

            //blockMembers.Add(this.EnsureFullyConstructed(objectDetails));

            var deserialized = this.DeserializeCustomType(objectDetails);
            blockMembers.Add(deserialized.OperationExpression);

            return new TypeDetails(deserialized.HasFixedSize, deserialized.MinSize, Expression.Block(blockMembers.Where(x => x != null)));
        }

        //private Expression EnsureFullyConstructed(ObjectDetails objectDetails)
        //{
        //    var properties = objectDetails.RecursiveFlatProperties().Where(x => x.IsCustomType);
        //    var blockMembers = properties.Select(property =>
        //    {
        //        return Expression.IfThen(
        //            Expression.Equal(property.Value, Expression.Constant(null, property.Type)),
        //            Expression.Assign(property.Value, Expression.New(property.Type))
        //        );
        //    });

        //    return blockMembers.Any() ? Expression.Block(blockMembers) : null;
        //}

        private Dictionary<string, ObjectDetails> FindLengthFields(ObjectDetails objectDetails)
        {
            var groups = objectDetails.RecursiveFlatProperties()
                .Where(x => x.LengthKey != null && PrimitiveTypes.IsPrimitive(x.Type) && PrimitiveTypes.Types[x.Type].IsIntegral)
                .GroupBy(x => x.LengthKey).ToArray();

            foreach (var group in groups)
            {
                if (group.Count() > 1)
                    throw new Exception(String.Format("More than one integral field found with Length Key {0}", group.Key));
            }

            return groups.ToDictionary(x => x.Key, x => x.First());
        }

        private TypeDetails DeserializeValue(ObjectDetails objectDetails)
        {
            if (objectDetails.IsEnumerable)
                return this.DeserializeEnumerable(objectDetails);

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
            // If it's not marked with our attribute, we're not deserializing it
            if (!objectDetails.IsCustomType)
                return null;

            var blockMembers = new List<Expression>();

            blockMembers.Add(Expression.IfThen(
                Expression.Equal(objectDetails.Value, Expression.Constant(null, objectDetails.Type)),
                Expression.Assign(objectDetails.Value, Expression.New(objectDetails.Type))
            ));

            var typeDetails = objectDetails.Properties.Select(property => this.DeserializeValue(property)).ToArray();

            // If they claim to be able to serialize themselves, let them
            if (typeof(IDeserialize).IsAssignableFrom(objectDetails.Type))
            {
                var method = typeof(ISerialize).GetMethod("Deserialize");
                blockMembers.Add(Expression.Call(objectDetails.Value, method, this.reader));
            }
            else
            {
                blockMembers.AddRange(typeDetails.Select(x => x.OperationExpression));
            }

            var result = Expression.Block(blockMembers.Where(x => x != null));
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

        private TypeDetails DeserializeEnumerable(ObjectDetails objectDetails)
        {
            // We start everything off as an array, then convert to a list if appropriate
            var arrayVar = Expression.Parameter(objectDetails.ElementType.MakeArrayType(), "array");
            bool hasFixedLength = objectDetails.EnumerableLength > 0;

            Expression arrayInit;
            Expression arrayLength;

            // Is it variable legnth
            if (objectDetails.LengthKey != null)
            {
                ObjectDetails lengthField;
                if (!this.lengthFields.TryGetValue(objectDetails.LengthKey, out lengthField))
                    throw new Exception(String.Format("Could not find integer field with Length Key {0}", objectDetails.LengthKey));

                arrayInit = Expression.Assign(
                    arrayVar,
                    Expression.NewArrayBounds(objectDetails.ElementType, lengthField.Value)
                );
                arrayLength = lengthField.Value;
            }
            else if (hasFixedLength)
            {
                arrayLength = Expression.Constant(objectDetails.EnumerableLength);
                arrayInit = Expression.Assign(
                    arrayVar,
                    Expression.NewArrayBounds(objectDetails.ElementType, arrayLength)
                );
                
            }
            else
            {
                throw new Exception("Unknown length for array");
            }

            //var typeDetails this.DeserializeValue(objectDetails.ElementObjectDetails);

            var loopVar = Expression.Variable(typeof(int), "loopVar");
            var forLoop = ExpressionHelpers.For(
                loopVar,
                Expression.Constant(0),
                Expression.LessThan(loopVar, arrayLength),
                Expression.PostIncrementAssign(loopVar),
                Expression.Assign(
                    Expression.ArrayAccess(arrayVar, loopVar),
                    Expression.Constant(0, objectDetails.ElementType)
                    //typeDetails.OperationExpression
                )
            );


            //return new TypeDetails(hasFixedLength && typeDetails.HasFixedSize, hasFixedLength ? objectDetails.EnumerableLength * typeDetails.MinSize : 0, block);
            return null;
        }
    }
}
