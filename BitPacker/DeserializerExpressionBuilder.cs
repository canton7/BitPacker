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

        public TypeDetails Deserialize()
        {
            var subject = Expression.Parameter(this.objectType, "rootSubject");

            var objectDetails = new ObjectDetails(this.objectType, subject, new BitPackerMemberAttribute());
            objectDetails.Discover();

            // First, we need to make sure it's fully constructed
            var blockMembers = new List<Expression>();

            var objectPath = new ImmutableStack<ObjectDetails>(objectDetails);
            var deserialized = this.DeserializeAndAssignValue(subject, objectPath);
            blockMembers.Add(deserialized.OperationExpression);
            // Set return value
            blockMembers.Add(subject);

            var block = Expression.Block(new[] { subject }, blockMembers.Where(x => x != null));

            return new TypeDetails(deserialized.HasFixedSize, deserialized.MinSize, block);
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

        private TypeDetails DeserializeAndAssignValue(Expression subject, IImmutableStack<ObjectDetails> objectPath)
        {
            var typeDetails = this.DeserializeValue(objectPath);
            return new TypeDetails(typeDetails.HasFixedSize, typeDetails.MinSize, Expression.Assign(subject, typeDetails.OperationExpression));
        }

        private TypeDetails DeserializeValue(IImmutableStack<ObjectDetails> objectPath)
        {
            var objectDetails = objectPath.Peek();
            if (objectDetails.IsEnumerable)
                return this.DeserializeEnumerable(objectPath);

            if (PrimitiveTypes.Types.ContainsKey(objectDetails.Type))
                return this.DeserializePrimitive(objectDetails);

            if (objectDetails.IsEnum)
                return this.DeserializeEnum(objectDetails);

            if (objectDetails.IsCustomType)
                return this.DeserializeCustomType(objectPath);

            throw new Exception(String.Format("Don't know how to deserialize type {0}", objectDetails.Type.Name));
        }

        public TypeDetails DeserializeCustomType(IImmutableStack<ObjectDetails> objectPath)
        {
            var objectDetails = objectPath.Peek();

            // If it's not marked with our attribute, we're not deserializing it
            if (!objectDetails.IsCustomType)
                return null;

            var blockMembers = new List<Expression>();
            var subject = Expression.Variable(objectDetails.Type, objectDetails.Type.Name);

            blockMembers.Add(Expression.Assign(subject, Expression.New(objectDetails.Type)));

            var typeDetails = objectDetails.Properties.Select(property =>
            {
                var specificObjectPath = objectPath.Push(property);
                return this.DeserializeAndAssignValue(property.AccessExpression(subject), specificObjectPath);
            }).ToArray();

            // If they claim to be able to serialize themselves, let them
            if (typeof(IDeserialize).IsAssignableFrom(objectDetails.Type))
            {
                var method = typeof(ISerialize).GetMethod("Deserialize");
                blockMembers.Add(Expression.Call(subject, method, this.reader));
            }
            else
            {
                blockMembers.AddRange(typeDetails.Select(x => x.OperationExpression));
            }

            blockMembers.Add(subject); // Last value in block is the return value
            var result = Expression.Block(new[] { subject }, blockMembers.Where(x => x != null));
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

        public TypeDetails DeserializeEnum(ObjectDetails objectDetails)
        {
            var typeDetails = this.DeserializePrimitive(objectDetails.EnumEquivalentObjectDetails);
            var value = Expression.Convert(typeDetails.OperationExpression, objectDetails.Type);
            return new TypeDetails(typeDetails.HasFixedSize, typeDetails.MinSize, value);
        }

        private TypeDetails DeserializeEnumerable(IImmutableStack<ObjectDetails> objectPath)
        {
            var objectDetails = objectPath.Peek();

            bool hasFixedLength = objectDetails.EnumerableLength > 0;

            var subject = Expression.Parameter(objectDetails.Type, String.Format("enumerableOf{0}", objectDetails.ElementType.Name));
            Expression arrayInit;
            Expression arrayLength;

            // Is it variable legnth?
            if (objectDetails.LengthKey != null)
            {
                PropertyObjectDetails lengthField = null;
                foreach (var obj in objectPath)
                {
                    if (obj.IsCustomType && obj.LengthFields.TryGetValue(objectDetails.LengthKey, out lengthField))
                        break;
                }

                if (lengthField == null)
                    throw new Exception(String.Format("Could not find integer field with Length Key {0}", objectDetails.LengthKey));

                arrayLength = lengthField.Value;
                arrayInit = Expression.Assign(
                    subject,
                    CreateListOrArray(objectDetails, arrayLength)
                );
            }
            else if (hasFixedLength)
            {
                arrayLength = Expression.Constant(objectDetails.EnumerableLength);
                arrayInit = Expression.Assign(
                    subject,
                    CreateListOrArray(objectDetails, arrayLength)
                );
                
            }
            else
            {
                throw new Exception("Unknown length for array");
            }

            var typeDetails = this.DeserializeValue(objectPath.Push(objectDetails.ElementObjectDetails));


            var loopVar = Expression.Variable(typeof(int), "loopVar");
            var forLoop = ExpressionHelpers.For(
                loopVar,
                Expression.Constant(0),
                Expression.LessThan(loopVar, arrayLength),
                Expression.PostIncrementAssign(loopVar),
                objectDetails.ElementObjectDetails.AssignExpression(subject, loopVar, typeDetails.OperationExpression)
            );

            var block = Expression.Block(new[] { subject },
                arrayInit,
                forLoop,
                subject
            );

            return new TypeDetails(hasFixedLength && typeDetails.HasFixedSize, hasFixedLength ? objectDetails.EnumerableLength * typeDetails.MinSize : 0, block);
        }

        private Expression CreateListOrArray(ObjectDetails objectDetails, Expression length)
        {
            if (objectDetails.Type.IsArray)
            {
                return Expression.NewArrayBounds(objectDetails.ElementType, length);
            }
            else
            {
                var listType = typeof(List<>).MakeGenericType(objectDetails.ElementType);
                var ctor = listType.GetConstructor(new[] { typeof(int) });
                return Expression.New(ctor, length);
            }
        }
    }
}
